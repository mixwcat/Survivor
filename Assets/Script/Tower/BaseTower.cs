using UnityEngine;
using System.Collections.Generic;

public class BaseTower : MonoBehaviour
{
    private List<EnemyController> enemiesInRange = new List<EnemyController>();

    /// <summary>
    /// 寻找目标
    /// </summary>
    /// <returns></returns>
    protected Transform FindTarget()
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


    void OnEnable()
    {
        TowerManager.Instance.RegisterTower(this);
    }
    void OnDisable()
    {
        TowerManager.Instance.UnregisterTower(this);
    }
}
