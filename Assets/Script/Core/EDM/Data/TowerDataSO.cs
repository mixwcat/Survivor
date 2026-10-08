using UnityEngine;

/// <summary>
/// 攻击型塔的基础配置数据（Teto / Rin）。
///
/// 治疗塔 Luo 请使用 <see cref="LuoTowerDataSO"/> —— 它和本类**平级**，
/// 不会看到 Damage / HitForce / BulletSpeed 这些与自己无关的字段。
/// </summary>
[CreateAssetMenu(fileName = "TowerData", menuName = "Game/Data/Tower/Attack Tower")]
public class TowerDataSO : BaseTowerDataSO
{
    [Header("攻击塔专属属性")]
    public float Damage = 10f;
    [Tooltip("攻击速率（次/秒）：0.5 = 每 2 秒一发。数值越大越快")]
    public float AttackSpeed = 0.5f;
    public float HitForce = 0f;
    public float BulletSpeed = 8f;

    [Header("炮弹（第二种远程攻击，只有装了对应攻击方式才会读）")]
    [Tooltip("炮弹的命中伤害。与 Damage 分开，才能单独升级")]
    public float ShellDamage = 40f;
    [Tooltip("炮弹命中后的溅射半径")]
    public float ShellExplosionRadius = 3f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.Damage, Damage);
        model.SetBaseValue(StatType.AttackSpeed, AttackSpeed);
        model.SetBaseValue(StatType.TowerHitForce, HitForce);
        model.SetBaseValue(StatType.BulletSpeed, BulletSpeed);
        model.SetBaseValue(StatType.ShellDamage, ShellDamage);
        model.SetBaseValue(StatType.ShellExplosionRadius, ShellExplosionRadius);
    }
}
