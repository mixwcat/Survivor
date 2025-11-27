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
    public LevelUpSO tetoFireIntervalLevelUpSO;
    public LevelUpSO tetoDamageLevelUpSO;
    public LevelUpSO tetoHitForceLevelUpSO;
    public LevelUpSO tetoAttackRangeLevelUpSO;


    protected override void Start()
    {
        base.Start();
        StartCoroutine(GenerateBullet());
    }

    protected override void Update()
    {
        base.Update();
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

            // 显示攻击范围
            DrawCircle();

            // 生成子弹
            Instantiate(Resources.Load<GameObject>("Tower/TetoBullet"), transform.position, Quaternion.identity).GetComponents<TetoBulletController>()[0].Init(damage, hitForce, direction);

            // 等待一段时间
            yield return new WaitForSeconds(fireInterval);
        }
    }


    /// <summary>
    /// 订阅升级事件
    /// </summary>
    private void IncreaseFireInterval()
    {
        // 攻击间隔
        fireInterval *= 0.9f;
    }
    private void IncreaseDamage()
    {
        // 伤害
        damage += 2;
    }
    private void IncreaseHitForce()
    {
        // 击退力
        hitForce += 15;
    }
    private void IncreaseAttackRange()
    {
        // 攻击范围
        attackRange += 2f;
        detectionCollider.radius = attackRange;
    }

    private void OnEnable()
    {
        tetoFireIntervalLevelUpSO.onLevelUp += IncreaseFireInterval;
        tetoDamageLevelUpSO.onLevelUp += IncreaseDamage;
        tetoHitForceLevelUpSO.onLevelUp += IncreaseHitForce;
        tetoAttackRangeLevelUpSO.onLevelUp += IncreaseAttackRange;
        TowerManager.Instance.RegisterTower(this);
    }

    private void OnDisable()
    {
        tetoFireIntervalLevelUpSO.onLevelUp -= IncreaseFireInterval;
        tetoDamageLevelUpSO.onLevelUp -= IncreaseDamage;
        tetoHitForceLevelUpSO.onLevelUp -= IncreaseHitForce;
        tetoAttackRangeLevelUpSO.onLevelUp -= IncreaseAttackRange;
        TowerManager.Instance.UnregisterTower(this);
    }
}
