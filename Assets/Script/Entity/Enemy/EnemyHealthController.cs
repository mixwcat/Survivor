using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 敌人血量控制器
/// 波次增强已移至 EnemyController.EnhanceWithWave()
///
/// <para>
/// <b>死亡只结算一次</b>：判据是 <see cref="BaseHealthController.IsDead"/>。
/// 同一帧被多颗子弹打中、或者接触伤害与投射物同时到账，都会走多次 <c>TakeDamage</c>；
/// 没有这道闸门时击杀点会发多份、经验掉多份、击杀数多记，
/// 而 <c>Destroy</c>/回池要到帧末才生效，中间这段时间实例仍然可被命中（Boss 尤其明显）。
/// </para>
///
/// <para>
/// <b>接触伤害按"归一化后的目标"去重</b>：一个目标（推车、塔）往往有多个子碰撞体，
/// 逐个记下来会让它被当成多个接触对象，每次周期结算就多受一份伤害；
/// 而退出其中一个碰撞体时又只移除一条记录 —— 伤害翻倍且不对称。
/// 这里只保存**碰撞体**列表，结算时再归一化成目标并去重：
/// 目标被销毁时不会留下"以已销毁对象为键"的残留记录。
/// </para>
/// </summary>
public class EnemyHealthController : BaseHealthController
{
    /// <summary>当前与本触发器重叠的碰撞体（未归一化）。销毁后会变成伪 null，由周期结算清理。</summary>
    private readonly List<Collider2D> _contactColliders = new List<Collider2D>();

    /// <summary>周期结算用的去重目标列表（复用，不分配）。</summary>
    private readonly List<BaseHealthController> _contactTargets = new List<BaseHealthController>();

    private EnemyController _enemy;

    /// <summary>本副本是否该结算伤害/死亡。联机时只有服务端那一份（见 <see cref="NetworkAuthority"/>）。</summary>
    private NetworkAuthority _authority;
    private bool _authorityReady;

    /// <summary>
    /// 懒解析的权威判据。
    /// 不用「在 Start 里初始化」：<c>TakeDamage</c> 可能在 <c>Start</c> 之前就到
    /// （生成当帧就被命中），那时读到默认值会把服务端自己挡掉。
    /// </summary>
    private bool IsAuthority
    {
        get
        {
            if (!_authorityReady)
            {
                _authority = new NetworkAuthority(gameObject);
                _authorityReady = true;
            }

            return _authority.IsAuthority;
        }
    }

    /// <summary>
    /// 本实例死亡。**一次性订阅**：订阅方（如节点门槛的判定）收到后必须立即退订 ——
    /// 敌人是池化对象，事件不会随回池自动清空，留着会让下一次复用误触发。
    /// </summary>
    public event System.Action Died;

    /// <summary>
    /// 死亡后是否归还对象池。
    /// Boss 设为 false：它一局只出现一次，走池会让"死亡"与实例生命周期脱钩
    /// （回池不销毁，引用仍然有效，很难判断这一只是不是已经死了）。
    /// </summary>
    [Header("生命周期")]
    public bool UsePool = true;

    protected override void Start()
    {
        base.Start();
        _enemy = GetComponent<EnemyController>();
        // 增强已在 EnemyController.OnGetFromPool 中完成，这里只初始化血量
        CurrentHealth = MaxHealth;
        ArmPeriodicDamage();
    }

    /// <summary>
    /// 池化复用时重置：满血 + **复位死亡标记** + 清空接触列表 + 重新挂上周期伤害。
    /// <c>Start</c> 只跑第一次，而 <c>OnDisable</c> 会 <c>CancelInvoke</c>，所以必须在这里重新计时。
    /// </summary>
    public override void ResetHealth()
    {
        base.ResetHealth();
        _contactColliders.Clear();
        _contactTargets.Clear();
        ArmPeriodicDamage();
    }

    private void ArmPeriodicDamage()
    {
        CancelInvoke(nameof(HurtColliders));
        InvokeRepeating(nameof(HurtColliders), 0f, 1f);
    }

    /// <summary>
    /// 死亡：结算击杀归属（发升级点）+ 掉经验 + 归还对象池（不再直接 Destroy）。
    /// 经验掉落放在这里而不是 <c>OnDisable</c> —— 池化归还也会触发 OnDisable，
    /// 放在那里会让「回收一个还活着的敌人」也掉经验。
    ///
    /// <para>
    /// 它只会被调用一次：<see cref="TakeDamage"/> 在扣血前就挡住了已死亡的实例，
    /// 并在调用本方法之前把 <see cref="BaseHealthController.IsDead"/> 置位。
    /// </para>
    /// </summary>
    protected override void Die()
    {
        EnemyController enemy = _enemy != null ? _enemy : GetComponent<EnemyController>();

        // 掉落数量来自 DataSO：EnemyDataSO.ExpReward → StatType.ExpReward
        // （缺配时 GetStat 返回 1 并只告警一次，与旧版硬编码 1 的行为一致）
        int expReward = enemy != null ? (int)enemy.GetStat(StatType.ExpReward) : 1;

        // 击杀归属：解析不出时 Killer 为 null，只记团队击杀，不错误发点。
        // ⚠️ 这里**不发升级点**：升级点是"升级"的奖励（PlayerProgressionController），
        // 按击杀发放会让一局几百次击杀直接把点数冲爆，也让"升级"失去意义。
        var death = new EnemyDeathInfo(_entity, ResolveKiller(LastDamage.Attacker), transform.position, expReward);

        // 统计上报必须在回收之前：之后实例会被复用，位置与状态都不再是"这一只"的
        RunStatsTracker.Service?.ReportDeath(death);

        ExpSpritePool.Instance.SpawnExpSprite(transform, expReward);

        // 先发死亡事件再回收：订阅方（Boss 战）需要在这个时刻推进阶段，
        // 而回池之后实例会被复用，引用与状态都不再可信
        Died?.Invoke();

        // 接触记录清掉：回池后本实例会被复用，残留的引用属于"上一只"的接触对象
        _contactColliders.Clear();
        _contactTargets.Clear();

        // ── 联机：生命周期交给 Mirror ──
        // NetworkServer.Destroy 会广播销毁，各端自己的副本随之消失 ——
        // 这也是敌人血量**不需要** SyncVar 的原因：死亡本身就是同步信号。
        // ⚠️ 经验球目前只在服务端生成本地实例（客户端看不到、捡不到），
        //    经验归属与经验球网络化是 MirrorPlan 的 P4.7。
        if (NetworkBootstrap.IsActive)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }

        if (UsePool)
        {
            EnemyPool.Return(enemy);
        }
        else
        {
            // 不走池的（Boss）：直接销毁。它的死亡已经通过 Died 通知出去了
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 从攻击者解析出**归属玩家**。
    ///
    /// <para>
    /// 武器实体是玩家层级下的子节点（<c>Player/Weapons/…</c>），投射物带的是武器的
    /// <c>ctx.Self</c>，所以向上找 <see cref="PlayerController"/> 即可。
    /// </para>
    /// <para>
    /// <b>塔要走账本</b>：塔不是玩家的子节点，向上找永远找不到 <c>PlayerController</c>，
    /// 于是塔的击杀会全部落进"只记团队击杀"——建造者一点升级点都拿不到，
    /// 而玩家只会觉得"我的塔白打了"。归属记在运行时挂上去的
    /// <see cref="TowerLedger"/>（谁建的、花了多少）上。
    /// </para>
    /// <para>
    /// owner 已离场（伪 null）时返回 null，同样只记团队击杀 ——
    /// 绝不退化成"发给本地玩家"。
    /// </para>
    /// </summary>
    private static PlayerController ResolveKiller(EntityBehaviour attacker)
    {
        if (attacker == null) return null;                                  // 含"已销毁"的伪 null
        if (attacker is PlayerController player) return player;

        TowerLedger ledger = attacker.GetComponentInParent<TowerLedger>();
        if (ledger != null) return ledger.Owner;

        return attacker.GetComponentInParent<PlayerController>();
    }

    /// <summary>
    /// 定时对接触到的目标造成伤害。目标在这里才归一化并去重，
    /// 所以"一个目标有多个碰撞体"不会让它每次结算多受一份伤害。
    /// </summary>
    private void HurtColliders()
    {
        if (!IsAuthority) return;

        CollectContactTargets();

        for (int i = 0; i < _contactTargets.Count; i++)
        {
            BaseHealthController target = _contactTargets[i];
            if (target == null || target.IsDead) continue;

            target.TakeDamage(new DamageInfo(Damage, 0f, _entity, DamageSource.Contact));
        }
    }

    /// <summary>
    /// 把重叠碰撞体归一化成**去重后**的目标列表，顺便清掉已销毁的碰撞体记录。
    /// 调用频率是每秒一次（每个敌人），列表规模是"当前压在身上/贴着车的碰撞体数"，很小。
    /// </summary>
    private void CollectContactTargets()
    {
        _contactTargets.Clear();

        for (int i = _contactColliders.Count - 1; i >= 0; i--)
        {
            Collider2D collider = _contactColliders[i];

            // 目标被销毁（塔被拆、推车被移除）时 OnTriggerExit2D 不保证补发，这里兜底清理
            if (collider == null)
            {
                _contactColliders.RemoveAt(i);
                continue;
            }

            BaseHealthController target = DamageTargetResolver.Resolve(collider);
            if (target == null || target.IsDead) continue;

            // 同一个目标的多个子碰撞体只保留一条
            if (_contactTargets.Contains(target)) continue;

            _contactTargets.Add(target);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // "碰到了谁"走统一解析（本体碰撞体上的 Hurtbox 标记）；
        // "该不该打"由物理层保证：接触触发体在 EnemyDetector 层，只与
        // PlayerBody / TowerBody / CartBody 配对 —— 敌人的本体（EnemyBody）在物理上就碰不到它，
        // 所以这里不再需要"排除 Enemy"那条规则（以前靠 tag，怪群贴在一起会互相掉血）
        BaseHealthController target = DamageTargetResolver.Resolve(other);
        if (target == null || target.IsDead) return;

        // 同一个碰撞体重复 enter（碰撞体被禁用又启用时会）不重复入列
        if (_contactColliders.Contains(other)) return;

        // 只有"这个目标此前完全没有接触"时才立刻结算一次：
        // 多碰撞体目标不该因为第二个碰撞体进入而多吃一份伤害
        bool alreadyTouching = HasContactWith(target);

        _contactColliders.Add(other);

        // 立刻结算的那一份也要判权威：客户端副本在接触瞬间同样会收到 trigger，
        // 不挡的话它会在本地扣一次血（两端血量各自演化，不报错）
        if (alreadyTouching || !IsAuthority) return;

        target.TakeDamage(new DamageInfo(Damage, 0f, _entity, DamageSource.Contact));
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // 按碰撞体精确移除：同一个目标的其它碰撞体仍在重叠时，目标继续保持接触
        _contactColliders.Remove(other);
    }

    /// <summary>当前重叠的碰撞体里是否已经有指向该目标的。</summary>
    private bool HasContactWith(BaseHealthController target)
    {
        for (int i = 0; i < _contactColliders.Count; i++)
        {
            Collider2D collider = _contactColliders[i];
            if (collider == null) continue;

            if (DamageTargetResolver.Resolve(collider) == target) return true;
        }

        return false;
    }

    /// <summary>
    /// 受到伤害：扣血 → （存活时）击退 → 结算。
    /// 击退写在这里而不是由每个攻击方自己调 <see cref="EnemyController.HitImpact"/>：
    /// 攻击方只提供「伤害 + 力度」，方向/时长属于受击方的内部状态；顺带也让攻击方
    /// 不必知道 EnemyController 的存在（只需 <see cref="BaseHealthController"/> 契约）。
    /// </summary>
    public override void TakeDamage(in DamageInfo info)
    {
        // 联机时伤害只在服务端结算：客户端副本也扣血的话，两端会各自演化出一套血量
        // （不报错，只是"我这边打死了、队友那边还活着"）
        if (!IsAuthority) return;

        // 已经死过的实例拒绝后续一切：重复 Die() 会让击杀点、经验、统计全部翻倍
        if (IsDead) return;

        // 先记来源：Die() 要靠它解析击杀归属
        RecordDamage(info);

        CurrentHealth -= info.Amount;

        if (CurrentHealth <= 0)
        {
            IsDead = true;
            CancelInvoke(nameof(HurtColliders));
            Die();
            return;
        }

        if (info.HitForce > 0f)
        {
            // _enemy 在 Start 里缓存；池化对象首帧即可能受击，所以这里兜一次查找
            EnemyController enemy = _enemy != null ? _enemy : GetComponent<EnemyController>();
            if (enemy != null) enemy.HitImpact(info.HitForce);
        }

        DamageNumService.Service?.SpawnDamageNum(transform.position, info.Amount);
    }

    void OnDisable()
    {
        // 敌人被销毁/回池时停掉周期伤害，避免残留 Invoke 继续结算
        CancelInvoke(nameof(HurtColliders));
        _contactColliders.Clear();
        _contactTargets.Clear();
    }
}
