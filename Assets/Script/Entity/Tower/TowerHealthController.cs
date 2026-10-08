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

    /// <summary>塔目前不吃击退，<see cref="DamageInfo.HitForce"/> 仅为保持重写签名一致。</summary>
    protected override void ApplyDamage(in DamageInfo info)
    {
        // ⚠️ 必须调 base.ApplyDamage，**不能**调 base.TakeDamage（后者是唯一入口，
        // 在服务端会再走一遍 ApplyDamage ⇒ 无限递归 ⇒ 栈溢出）
        base.ApplyDamage(in info);
        _towerHealthPanel?.UpdateHealthUI();
        DamageNumService.Service?.SpawnDamageNum(transform.position, info.Amount, DamageNumType.Red);
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
