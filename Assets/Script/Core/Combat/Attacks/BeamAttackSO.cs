using UnityEngine;

/// <summary>
/// 光束攻击（Laser）—— 瞬时直线伤害，没有飞行时间。
///
/// <para>
/// <b>为什么不复用 <see cref="ProjectileAttackSO"/>：</b>投射物是"生成一个会飞的东西，
/// 它命中时结算"，而光束是"这一瞬间，这条线上的敌人就受伤了"。
/// 硬用投射物做激光只有两条路，都错：把速度调到极高（仍有一帧延迟、仍会被墙挡、
/// 仍可能追不上移动目标），或者把溅射半径调大（那变成球形爆炸，不是线）。
/// </para>
///
/// <para>
/// <b>判定用 <c>CircleCast</c> 而不是 <c>Raycast</c>：</b>Raycast 是零宽度的线，
/// 对圆形碰撞体的敌人只有正好穿过圆心才算命中 —— "看起来打中了却没伤害"正是这么来的。
/// 宽度参数让视觉与判定一致。
/// </para>
///
/// <para>
/// <b>它不生成任何东西</b>：伤害在这一帧就结算完了，没有实体需要回收。
/// 视觉表现（光束特效）留给载体订阅 <c>AttackDriver.OnPerformed</c> 播放 ——
/// SO 不持有资源引用（见基类红线二）。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "BeamAttack", menuName = "Game/Attack/Beam")]
public class BeamAttackSO : AttackMethodSO
{
    [Header("光束")]
    [Tooltip("射程（世界单位）")]
    public float Range = 12f;

    [Tooltip("判定宽度（半径）。太小会退化成零宽度射线 —— 只有正对圆心才命中")]
    [Range(0.05f, 2f)]
    public float Width = 0.35f;

    [Tooltip("是否穿透：命中后继续向后打")]
    public bool Pierce = true;

    [Tooltip("穿透时的最大命中数（防止一发打穿整张地图）")]
    public int MaxHits = 8;

    [Header("读取的数值")]
    public StatType DamageStat = StatType.Damage;

    [Tooltip("攻击速率（次/秒，越大越快）")]
    public StatType SpeedStat = StatType.AttackSpeed;

    /// <summary>
    /// 命中查询的复用缓冲。
    ///
    /// <para>
    /// 它是**临时**的（每次执行都被覆盖），不承载任何跨调用的状态 —— 放静态而不是实例字段，
    /// 是因为 SO 的实例字段会被误认为"可以在 Inspector 里配的运行时状态"（见基类红线一）。
    /// 用 NonAlloc 是必须的：逐帧路径上每次攻击分配一个数组，在怪多的时候很可观。
    /// </para>
    /// </summary>
    private static readonly RaycastHit2D[] HitBuffer = new RaycastHit2D[32];

    /// <summary>
    /// 本次已经结算过的目标（按**实体**去重）。
    ///
    /// <para>
    /// <b>为什么必须去重：</b>一个敌人身上有多个碰撞体（根节点的实体碰撞体 + 子节点的
    /// <c>ColliderTrigger</c>），而 <c>GetComponentInParent&lt;BaseHealthController&gt;()</c>
    /// 会把它们解析成**同一个**血量组件。不去重时一发激光会扣两次血，而且
    /// <c>MaxHits</c> 也被重复消耗 —— 实际伤害与穿透数取决于 prefab 上有几个碰撞体，
    /// 而不是策划配的数据。
    /// </para>
    /// <para>
    /// 与 <c>BulletController</c> 的溅射同理（那边靠 <c>useTriggers = false</c> 只命中实体碰撞体）。
    /// 激光**不能**照抄那个做法：敌人的实体碰撞体可能比 trigger 小，关掉 trigger 会漏掉本该命中的目标。
    /// </para>
    /// </summary>
    private static readonly BaseHealthController[] HitTargets = new BaseHealthController[32];

    /// <summary>光束是打向指定方向的，需要瞄准输入（UI 据此决定是否显示攻击摇杆）。</summary>
    public override bool RequiresAimInput => true;

    public override StatType[] RequiredStats => new[] { DamageStat, SpeedStat };

    public override float GetInterval(EntityBehaviour self)
    {
        float speed = self.GetStat(SpeedStat);
        return speed > 0f ? 1f / speed : float.PositiveInfinity;
    }

    public override bool Execute(in AttackContext ctx)
    {
        if (ctx.Self == null) return false;

        // 没有有效朝向就空转：零向量会让 CircleCast 朝着世界的 +X 打，
        // 表现是"明明没瞄准却打出去了"
        if (ctx.Direction.sqrMagnitude < 0.0001f) return false;

        Vector2 origin = ctx.Origin != null ? (Vector2)ctx.Origin.position : (Vector2)ctx.Self.transform.position;
        Vector2 direction = ctx.Direction.normalized;
        int damage = (int)ctx.Self.GetStat(DamageStat);

        // 只查敌人本体层：ContactFilter2D 默认构造出的 layerMask 是 0，会过滤掉**所有**层，
        // 查询会**静默**返回 0 个结果（CLAUDE.md 记过这一条）—— 必须显式设置
        var filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = 1 << LayerMask.NameToLayer("EnemyBody");
        filter.useTriggers = true;

        int count = Physics2D.CircleCast(origin, Width, direction, filter, HitBuffer, Range);
        if (count <= 0) return false;

        int hits = 0;
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = HitBuffer[i].collider;
            if (collider == null) continue;

            // 目标解析走统一入口（本体碰撞体上的 Hurtbox 标记）：每个实体只结算一次，
            // 与碰撞体是不是触发体无关；"该不该打"由 TargetTag 决定
            BaseHealthController health = DamageTargetResolver.ResolveAlive(collider);
            if (health == null || !health.CompareTag(TargetTag)) continue;

            // 同一实体的多个碰撞体只结算一次（否则双 Collider 的敌人会吃两份伤害）
            if (AlreadyHit(health, hits)) continue;

            // 去重缓冲与命中上限共用容量：装满了就停手，不要越界
            if (hits >= HitTargets.Length) break;

            // 归属用 Host（武器打死敌人算宿主的），数值用 Self
            health.TakeDamage(new DamageInfo(damage, 0f, ctx.Host, DamageSource.Projectile));
            HitTargets[hits] = health;

            hits++;
            if (!Pierce || hits >= MaxHits) break;
        }

        // 一个都没打到就返回 false：driver 据此不发 OnPerformed，
        // 避免对着空气播光束（与其它攻击方式的契约一致）
        return hits > 0;
    }

    /// <summary>本次光束是否已经打过这个目标（命中数最多 MaxHits，线性扫描足够）。</summary>
    private static bool AlreadyHit(BaseHealthController target, int hitCount)
    {
        int limit = hitCount < HitTargets.Length ? hitCount : HitTargets.Length;

        for (int i = 0; i < limit; i++)
        {
            if (HitTargets[i] == target) return true;
        }

        return false;
    }
}
