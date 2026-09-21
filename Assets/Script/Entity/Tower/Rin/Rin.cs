using UnityEngine;

/// <summary>
/// Rin 塔 — 范围攻击
/// 所有数值属性从 StatModel 读取
/// </summary>
public class Rin : BaseTower
{
    private Animator _anim;

    protected override void Start()
    {
        base.Start();
        _anim = GetComponent<Animator>();
    }

    protected override float GetOperateInterval()
    {
        return GetStat(StatType.AttackInterval);
    }

    protected override void OnOperate()
    {
        if (!HasEnemyInRange) return;

        _anim.SetTrigger("Attack");
        AudioService.Service?.PlaySfx(ResourceEnum.RinAttack);

        float damage = GetStat(StatType.Damage);
        ForEachValidTarget(enemyInRange, e =>
            e.GetComponent<EnemyHealthController>()?.TakeDamage(damage));
    }
}
