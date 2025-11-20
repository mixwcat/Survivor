using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Teto : MonoBehaviour
{
    public float fireInterval;
    public int damage;
    public int hitForce;
    private List<EnemyController> enemiesInRange = new List<EnemyController>();


    private void Start()
    {
        StartCoroutine(GenerateBullet());
    }


    /// <summary>
    /// 寻找目标
    /// </summary>
    /// <returns></returns>
    private Transform FindTarget()
    {
        if (enemiesInRange.Count == 0) return null;

        if (enemiesInRange[0] != null)
        {
            Transform target = enemiesInRange[0].transform;
            return target;
        }
        else
        {
            enemiesInRange.RemoveAt(0);
            return FindTarget();
        }
    }


    /// <summary>
    /// 碰撞检测，记录进入范围的敌人
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            enemiesInRange.Add(other.GetComponent<EnemyController>());
        }
    }

    IEnumerator GenerateBullet()
    {
        while (true)
        {
            // 获取Direction
            Vector3 direction = Vector3.zero;
            Transform target = FindTarget();
            if (target != null)
            {
                direction = (target.position - transform.position).normalized;
            }
            else
            {
                yield return new WaitForSeconds(fireInterval);
                continue;
            }

            // 生成子弹
            Instantiate(Resources.Load<GameObject>("Tower/TetoBullet"), transform.position, Quaternion.identity).GetComponents<TetoBulletController>()[0].Init(damage, hitForce, direction);
            Debug.Log("发射子弹");

            // 等待一段时间
            yield return new WaitForSeconds(fireInterval);
        }
    }
}
