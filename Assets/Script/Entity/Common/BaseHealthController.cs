using UnityEngine;

/// <summary>
/// 基础血量控制器 — 所有实体共用
/// maxHealth 从 EntityBehaviour.StatModel 读取，不再持有独立字段
/// currentHealth 为运行时状态，由伤害/治疗修改
/// </summary>
public class BaseHealthController : MonoBehaviour
{
    protected EntityBehaviour _entity;

    /// <summary>当前血量（运行时状态）</summary>
    public float CurrentHealth { get; protected set; }

    /// <summary>最大血量（从 StatModel 实时读取）</summary>
    public float MaxHealth => _entity != null ? _entity.GetStat(StatType.BaseMaxHealth) : 100f;

    /// <summary>攻击力（从 StatModel 实时读取，敌人使用）</summary>
    public float Damage => _entity != null ? _entity.GetStat(StatType.BaseDamage) : 0f;

    protected virtual void Awake()
    {
        _entity = GetComponent<EntityBehaviour>();
        CurrentHealth = MaxHealth;

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
    /// 受到伤害
    /// </summary>
    public virtual void TakeDamage(float damage)
    {
        CurrentHealth -= damage;
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 满血恢复
    /// </summary>
    public void FullHeal()
    {
        CurrentHealth = MaxHealth;
    }

    /// <summary>
    /// 治疗
    /// </summary>
    public virtual void Heal(float amount)
    {
        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }
        DamageNumManager.Instance.SpawnDamageNum(transform.position, amount, DamageNumType.green);
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
        if (type == StatType.BaseMaxHealth)
        {
            // 最大血量增加时，确保当前血量不超过最大值
            if (CurrentHealth > MaxHealth)
                CurrentHealth = MaxHealth;
        }
    }
}
