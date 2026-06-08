using System.Collections;
using UnityEngine;

/// <summary>
/// Teto 塔 — 远程射击
/// 所有数值属性从 StatModel 读取
/// </summary>
public class Teto : BaseTower
{
    protected override void Start()
    {
        base.Start();
        StartCoroutine(GenerateBullet());
    }

    /// <summary>
    /// 发射子弹
    /// </summary>
    IEnumerator GenerateBullet()
    {
        while (true)
        {
            Vector3 direction = Vector3.zero;
            Transform target = FindTarget();
            if (target != null)
            {
                direction = (target.position - transform.position).normalized;
            }
            else
            {
                yield return new WaitForSeconds(GetStat(StatType.TowerAttackInterval));
                continue;
            }

            DrawCircle();

            int damage = (int)GetStat(StatType.BaseDamage);
            int hitForce = (int)GetStat(StatType.TowerHitForce);
            float interval = GetStat(StatType.TowerAttackInterval);

            Instantiate(Resources.Load<GameObject>("Tower/TetoBullet"), transform.position, Quaternion.identity)
                .GetComponents<TetoBulletController>()[0]
                .Init(damage, hitForce, direction);

            yield return new WaitForSeconds(interval);
        }
    }
}
