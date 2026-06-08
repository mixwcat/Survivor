using UnityEngine;

/// <summary>
/// Rin 塔 — 范围攻击
/// 所有数值属性从 StatModel 读取
/// </summary>
public class Rin : BaseTower
{
    private float _fireTimer;
    private Animator _anim;

    protected override void Start()
    {
        base.Start();
        _anim = GetComponent<Animator>();
    }

    protected override void Update()
    {
        base.Update();
        _fireTimer += Time.deltaTime;
        float interval = GetStat(StatType.TowerAttackInterval);
        if (_fireTimer >= interval)
        {
            Attack();
            _fireTimer = 0f;
        }
    }

    private void Attack()
    {
        if (enemyInRange.Count == 0) return;

        _anim.SetTrigger("Attack");
        BKMusic.Instance.PlaySound(ResourceEnum.RinAttack);
        DrawCircle();

        float damage = GetStat(StatType.BaseDamage);
        for (int i = enemyInRange.Count - 1; i >= 0; i--)
        {
            if (enemyInRange[i] != null)
            {
                enemyInRange[i].GetComponent<EnemyHealthController>()?.TakeDamage(damage);
            }
            else
            {
                enemyInRange.RemoveAt(i);
            }
        }
    }
}
