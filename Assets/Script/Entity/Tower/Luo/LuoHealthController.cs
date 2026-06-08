using UnityEngine;

/// <summary>
/// Luo 塔血量控制器
/// MaxHealth 从 EntityBehaviour.StatModel 读取
/// 升级回血由 LevelUpSO.statModifiers + bonusHeal 数据驱动
/// </summary>
public class LuoHealthController : BaseHealthController
{
    private TowerHealthPanel _towerHealthPanel;

    private void Start()
    {
        _towerHealthPanel = GetComponentInChildren<TowerHealthPanel>();
        CurrentHealth = MaxHealth;
        _towerHealthPanel.UpdateHealthUI();
    }

    public override void TakeDamage(float damage)
    {
        CurrentHealth -= damage;
        _towerHealthPanel.UpdateHealthUI();
        if (CurrentHealth <= 0) Die();
        DamageNumManager.Instance.SpawnDamageNum(transform.position, damage, DamageNumType.Red);
    }

    protected override void OnAnyStatChanged(StatType type)
    {
        base.OnAnyStatChanged(type);
        if (type == StatType.BaseMaxHealth)
        {
            _towerHealthPanel.UpdateHealthUI();
        }
    }
}
