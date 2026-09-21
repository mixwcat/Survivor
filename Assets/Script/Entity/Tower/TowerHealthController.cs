using UnityEngine;

/// <summary>
/// 塔通用血量控制器
/// 统一处理 Teto / Rin / Luo 的血量显示、伤害数字、血量面板刷新
/// </summary>
public class TowerHealthController : BaseHealthController
{
    private TowerHealthPanel _towerHealthPanel;

    protected override void Start()
    {
        base.Start();
        _towerHealthPanel = GetComponentInChildren<TowerHealthPanel>(true);
        CurrentHealth = MaxHealth;
        _towerHealthPanel?.UpdateHealthUI();
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        _towerHealthPanel?.UpdateHealthUI();
        DamageNumManager.Service.SpawnDamageNum(transform.position, damage, DamageNumType.Red);
    }

    public override void Heal(float amount)
    {
        base.Heal(amount);
        _towerHealthPanel?.UpdateHealthUI();
    }

    protected override void OnAnyStatChanged(StatType type)
    {
        base.OnAnyStatChanged(type);
        if (type == StatType.MaxHealth)
            _towerHealthPanel?.UpdateHealthUI();
    }
}
