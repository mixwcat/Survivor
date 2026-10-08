using System;
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
        CurrentHealth = MaxHealth;

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
    /// 受到伤害。
    ///
    /// <para>
    /// <see cref="DamageInfo.HitForce"/> 是**攻击方给出的击退力度**（武器数值，如
    /// <see cref="StatType.HitPushForce"/>）；只提供力度，方向与时长由受击方自己决定 ——
    /// 击退是「受击结果」，属于受击方的内部状态。默认 0 表示不击退。
    /// </para>
    /// <para>
    /// <b>为什么要带攻击者：</b>击杀归属（经验给谁）、伤害统计、仇恨，以及联机下的反作弊校验
    /// 都需要它。旧签名只有 (伤害, 力度) 两个 float，攻击者在调用点就被丢掉了 ——
    /// 等联机（MirrorPlan M3.4）再补，要改所有攻击方式与投射物。
    /// </para>
    /// </summary>
    public virtual void TakeDamage(in DamageInfo info)
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
