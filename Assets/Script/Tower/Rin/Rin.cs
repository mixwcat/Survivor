using UnityEngine;

public class Rin : BaseTower
{
    [Header("属性")]
    [Tooltip("攻击间隔")]
    public float fireInterval;
    [Tooltip("伤害")]
    public int damage;
    private float fireTimer;

    [Header("组件")]
    private Animator anim;

    [Header("事件")]
    public LevelUpSO rinIncreaseDamageSO;
    public LevelUpSO rinFireIntervalSO;
    public LevelUpSO rinAttackRangeSO;

    protected override void Start()
    {
        base.Start();
        anim = GetComponent<Animator>();
    }

    protected override void Update()
    {
        base.Update();
        fireTimer += Time.deltaTime;
        if (fireTimer >= fireInterval)
        {
            Attack();
            fireTimer = 0f;
        }
    }

    private void Attack()
    {
        if (enemyInRange.Count == 0) return;
        // 播放攻击动画
        anim.SetTrigger("Attack");
        // 播放攻击音效
        BKMusic.Instance.PlaySound(ResourceEnum.RinAttack);
        // 显示攻击范围
        DrawCircle();
        // 对范围内所有敌人造成伤害
        for (int i = 0; i < enemyInRange.Count; i++)
        {
            if (enemyInRange[i] != null)
            {
                enemyInRange[i].GetComponent<EnemyHealthController>()?.TakeDamage(damage);
            }
            else
            {
                enemyInRange.RemoveAt(i);
                i--;
            }
        }
    }


    /// <summary>
    /// 订阅升级事件
    /// </summary>
    private void IncreaseDamage()
    {
        // 伤害
        damage += 1;
    }
    private void DecreaseFireInterval()
    {
        // 攻击间隔
        fireInterval *= 0.9f;
    }
    private void IncreaseAttackRange()
    {
        // 攻击范围
        attackRange += 1f;
        detectionCollider.radius = attackRange;
    }

    void OnEnable()
    {
        rinIncreaseDamageSO.onLevelUp += IncreaseDamage;
        rinFireIntervalSO.onLevelUp += DecreaseFireInterval;
        rinAttackRangeSO.onLevelUp += IncreaseAttackRange;
        TowerManager.Instance.RegisterTower(this);
    }

    void OnDisable()
    {
        rinIncreaseDamageSO.onLevelUp -= IncreaseDamage;
        rinFireIntervalSO.onLevelUp -= DecreaseFireInterval;
        rinAttackRangeSO.onLevelUp -= IncreaseAttackRange;
        TowerManager.Instance.UnregisterTower(this);
    }
}
