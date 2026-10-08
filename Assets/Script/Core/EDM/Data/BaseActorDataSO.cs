using UnityEngine;

/// <summary>
/// 「有血量、会被伤害结算」的实体数据基类：玩家 / 敌人 / 塔。
///
/// 武器不属于这一支（它没有血量），走 <see cref="WeaponDataSO"/>。
/// 判据不是「它是不是个活物」，而是「<c>BaseHealthController</c> 会不会挂在它身上」——
/// 敌人的接触伤害走 <c>target.GetComponent&lt;BaseHealthController&gt;()?.TakeDamage(Damage)</c>
/// （不传 <c>hitForce</c> → 默认 0 → 不击退），
/// 所以挂血量的实体就必须有 MaxHealth。
/// </summary>
public abstract class BaseActorDataSO : BaseEntityDataSO
{
    [Header("共用属性")]
    public float MaxHealth = 100f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.MaxHealth, MaxHealth);
    }
}
