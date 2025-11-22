using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Teto : BaseTower
{
    [Header("属性")]
    public float fireInterval;
    public int damage;
    public int hitForce;
    [Header("事件")]
    public LevelUpSO fireIntervalLevelUpSO;
    public LevelUpSO damageLevelUpSO;
    public LevelUpSO hitForceLevelUpSO;


    void Start()
    {
        StartCoroutine(GenerateBullet());
    }


    /// <summary>
    /// 用于订阅的方法
    /// </summary>
    private void IncreaseFireInterval()
    {
        fireInterval *= 0.8f;
    }
    private void IncreaseDamage()
    {
        damage += 2;
    }
    private void IncreaseHitForce()
    {
        hitForce += 15;
    }


    /// <summary>
    /// 发射子弹
    /// </summary>
    /// <returns></returns>
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

            // 等待一段时间
            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void OnEnable()
    {
        fireIntervalLevelUpSO.onLevelUp += IncreaseFireInterval;
        damageLevelUpSO.onLevelUp += IncreaseDamage;
        hitForceLevelUpSO.onLevelUp += IncreaseHitForce;
    }

    private void OnDisable()
    {
        fireIntervalLevelUpSO.onLevelUp -= IncreaseFireInterval;
        damageLevelUpSO.onLevelUp -= IncreaseDamage;
        hitForceLevelUpSO.onLevelUp -= IncreaseHitForce;
    }
}
