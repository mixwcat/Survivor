using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 基础血量控制器 — 所有实体共用
/// maxHealth 从 EntityBehaviour.StatModel 读取，不再持有独立字段
/// currentHealth 为运行时状态，由伤害/治疗修改
/// </summary>
public class BaseHealthController : MonoBehaviour
{
    protected EntityBehaviour _entity;

    /// <summary>
    /// 血量变化（当前值, 上限）。UI 订阅它刷新显示，**不要轮询** ——
    /// 轮询会让"掉血了但血条没动"这类问题只能靠调刷新频率来掩盖。
    ///
    /// <para>
    /// 伤害 / 治疗 / 满血恢复 / 上限变化（升级加了 MaxHealth）都会触发；
    /// 订阅方拿到的是**事件里的值**，不必再回读控制器。
    /// </para>
    /// </summary>
    public event Action<float, float> HealthChanged;

    /// <summary>通知订阅者血量变化。所有改动血量的路径都必须调用它。</summary>
    protected void RaiseHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>当前血量（运行时状态）</summary>
    public float CurrentHealth { get; protected set; }

    /// <summary>
    /// 是否已经死亡。
    ///
    /// <para>
    /// <b>它是"死亡只结算一次"的唯一判据。</b>没有它的时候，同一个敌人在同一帧里被
    /// 两颗子弹打中就会走两次 <c>Die()</c>：击杀点发两份、经验掉两份、击杀数 +2，
    /// 而且 <c>Destroy</c>/回池要到帧末才生效，中间这段时间实例仍然可被命中。
    /// Boss 尤其明显 —— 它不走池，帧末销毁前能被打很多次。
    /// </para>
    ///
    /// <para>
    /// <b>池化复用时必须复位</b>：<see cref="ResetHealth"/> 负责（由
    /// <c>EnemyController.OnGetFromPool</c> 调用）。忘了复位的话，从池里取出的敌人
    /// 一出生就是"已死亡"，打它不掉血 —— 而且完全没有报错。
    /// </para>
    /// </summary>
    public bool IsDead { get; protected set; }

    /// <summary>
    /// 最后一次受到的伤害。
    ///
    /// <para>
    /// <b>为什么必须留一份：</b><see cref="Die"/> 不带参数，而伤害是一次次传来的 ——
    /// 不记下来就无从知道"是谁打死的"，击杀归属（升级点、统计、联机上报）全部无从谈起。
    /// </para>
    /// </summary>
    protected DamageInfo LastDamage { get; private set; }

    /// <summary>记录本次伤害（子类重写 <see cref="TakeDamage"/> 时必须调用）。</summary>
    protected void RecordDamage(in DamageInfo info)
    {
        LastDamage = info;
    }

    /// <summary>最大血量（从 StatModel 实时读取）</summary>
    public float MaxHealth => _entity != null ? _entity.GetStat(StatType.MaxHealth) : 100f;

    /// <summary>攻击力（从 StatModel 实时读取，敌人使用）</summary>
    public float Damage => _entity != null ? _entity.GetStat(StatType.Damage) : 0f;

    protected virtual void Start()
    {
        _entity = GetComponent<EntityBehaviour>();

        // 客户端副本的血量来自服务端同步（SyncVar 的 hook 在 Awake 之后、Start 之前就跑过了）。
        // 这里再写一次 MaxHealth 会把刚同步下来的值抹掉 —— 表现是"客户端血量永远是满的、
        // 队友打掉的血看不见"，而且完全静默。只有声明了 IsHealthSynced 的实体受影响
        if (!IsClientHealthReplica) CurrentHealth = MaxHealth;

        // 初始化也要广播一次：订阅方（玩家血条 / 车顶耐久百分比）在 OnEnable 里读到的
        // CurrentHealth 还是 0 —— Start 晚于所有 OnEnable，不补这一发就会一直显示 0%，
        // 直到第一次掉血才"突然正常"
        RaiseHealthChanged();

        if (_entity?.StatModel != null)
        {
            _entity.StatModel.OnStatChanged += OnAnyStatChanged;
        }
    }

    protected virtual void OnDestroy()
    {
        if (_entity?.StatModel != null)
        {
            _entity.StatModel.OnStatChanged -= OnAnyStatChanged;
        }
    }

    /// <summary>
    /// 受到伤害。**伤害的唯一入口**（非虚 —— 子类请 override <see cref="ApplyDamage"/>）。
    ///
    /// <para>
    /// <see cref="DamageInfo.HitForce"/> 是**攻击方给出的击退力度**（武器数值，如
    /// <see cref="StatType.HitPushForce"/>）；只提供力度，方向与时长由受击方自己决定 ——
    /// 击退是「受击结果」，属于受击方的内部状态。默认 0 表示不击退。
    /// </para>
    /// <para>
    /// <b>为什么要带攻击者：</b>击杀归属（经验给谁）、伤害统计、仇恨，以及联机下的反作弊校验
    /// 都需要它。
    /// </para>
    /// </summary>
    /// <summary>
    /// 伤害的**唯一入口**（非虚 —— 子类请 override <see cref="ApplyDamage"/>）。
    ///
    /// <para>
    /// <b>它不是虚方法，是为了联机：</b>联机时客户端的副本**不结算**伤害，
    /// 而是把这次命中上报给服务端（见 <see cref="DamageRouter"/>）。
    /// 把这层路由放在唯一入口上，就不必去改每一个攻击方式与投射物的调用点 ——
    /// 那些调用点（子弹、环绕物、范围伤害、光束、接触伤害）有六处，
    /// 挨个改既容易漏，也会让"谁能造成伤害"散落在六个地方。
    /// </para>
    ///
    /// <para>
    /// <b>为什么"客户端就上报"是安全的判据：</b>服务端**永远不会**在客户端的副本上调用
    /// <c>TakeDamage</c> —— 它只在自己的那份上结算，客户端的血量靠状态同步更新。
    /// 所以客户端上出现的每一次 <c>TakeDamage</c> 都必然是本地产生的。
    /// </para>
    /// </summary>
    public void TakeDamage(in DamageInfo info)
    {
        if (DamageRouter.ShouldForwardToServer(gameObject))
        {
            DamageRouter.ForwardToServer(gameObject, info);
            return;
        }

        ApplyDamage(info);
    }

    /// <summary>
    /// 在**本端**结算伤害。子类 override 这个方法（而不是 <see cref="TakeDamage"/>）。
    /// </summary>
    protected virtual void ApplyDamage(in DamageInfo info)
    {
        // 死亡是一次性事件：已经死过的实例必须拒绝后续伤害，
        // 否则同一帧的多次命中会重复走 Die()（重复发点、重复掉落、重复统计）
        if (IsDead) return;

        RecordDamage(info);

        CurrentHealth -= info.Amount;
        RaiseHealthChanged();

        if (CurrentHealth > 0) return;

        IsDead = true;
        Die();
    }

    /// <summary>
    /// 血量是否由服务端同步。
    ///
    /// <para>
    /// 只有**联网且有血量显示**的实体才该返回 true —— 目前是玩家。
    /// 敌人不需要：它们没有血条，死亡靠 <c>NetworkServer.Destroy</c> 广播，本身就是同步信号。
    /// </para>
    /// </summary>
    protected virtual bool IsHealthSynced => false;

    /// <summary>本副本是不是"血量由服务端同步的客户端副本"。</summary>
    protected bool IsClientHealthReplica =>
        IsHealthSynced && NetworkBootstrap.IsActive && !NetworkServer.active;

    /// <summary>
    /// 客户端应用服务端同步过来的血量。
    ///
    /// <para>
    /// <b>只改数据 + 发事件，不走 <c>TakeDamage</c>/<c>Heal</c></b> ——
    /// 那两个是"结算"，在客户端跑就等于两端各结算一次（还会连带触发伤害数字、无敌帧之类）。
    /// </para>
    /// </summary>
    public void ApplyNetworkHealth(float current)
    {
        if (float.IsNaN(current) || current < 0f) return;   // 未初始化（SyncVar 默认值）时忽略

        // 容差：同步是离散的、两端浮点路径也不同，逐位相等是奢望。
        // 不设容差会让 HealthChanged 每次同步都发一次，而订阅方里有 TMP 文本
        //（每帧赋值会触发整套字形网格重建，见 CLAUDE.md 性能红线）
        if (Mathf.Abs(current - CurrentHealth) < 0.01f) return;

        CurrentHealth = current;
        RaiseHealthChanged();
    }

    /// <summary>
    /// 满血恢复
    /// </summary>
    public void FullHeal()
    {
        CurrentHealth = MaxHealth;
        RaiseHealthChanged();
    }

    /// <summary>
    /// 治疗
    /// </summary>
    public virtual void Heal(float amount)
    {
        // 死亡后不再接受治疗：否则一个"已死但尚未销毁"的实例会被拉回血线以上，
        // 而 Die() 的结算早已发生过 —— 表现成"打不死的敌人"
        if (IsDead) return;

        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }
        RaiseHealthChanged();
        DamageNumService.Service?.SpawnDamageNum(transform.position, amount, DamageNumType.green);
    }

    /// <summary>
    /// 把血量重置为当前上限。池化复用必须调用：
    /// <c>Start</c> 对池化对象只执行第一次，复用时不会自动回到满血。
    /// <b>同时复位 <see cref="IsDead"/></b> —— 不复位的话复用出来的敌人一出生就是死的。
    /// </summary>
    public virtual void ResetHealth()
    {
        // 池化对象首次取出时 Start 可能还没跑，这里兜一次组件查找
        if (_entity == null) _entity = GetComponent<EntityBehaviour>();

        IsDead = false;
        CurrentHealth = MaxHealth;
        RaiseHealthChanged();
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    /// <summary>
    /// 当实体的 StatModel 中任意数值变化时触发
    /// 子类可 override 处理特定数值变更（如 MaxHealth 变化时同步调整血量）
    /// </summary>
    protected virtual void OnAnyStatChanged(StatType type)
    {
        if (type == StatType.MaxHealth)
        {
            // 最大血量增加时，确保当前血量不超过最大值
            if (CurrentHealth > MaxHealth)
                CurrentHealth = MaxHealth;

            // 上限变了血条比例就变了（升级加血上限时，UI 必须跟着动）
            RaiseHealthChanged();
        }
    }
}
