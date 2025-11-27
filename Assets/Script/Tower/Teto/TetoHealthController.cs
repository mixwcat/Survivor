using UnityEngine;

public class TetoHealthController : BaseHealthController
{
    private TowerHealthPanel towerHealthPanel;
    [Header("事件")]
    public LevelUpSO tetoRecoverHealthLevelUpSO;

    private void Start()
    {
        currentHealth = maxHealth;
        towerHealthPanel = GetComponentInChildren<TowerHealthPanel>();
        // 初始化血量面板
        towerHealthPanel.UpdateHealthUI();
    }


    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage"></param>
    public override void TakeDamage(float damage)
    {
        currentHealth -= damage;

        // 更新血量面板
        towerHealthPanel.UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }

        // 生成伤害数字
        DamageNumManager.Instance.SpawnDamageNum(transform.position, damage, DamageNumType.Red);
    }


    /// <summary>
    /// 治疗
    /// </summary>
    /// <param name="amount"></param>
    public override void Heal(float amount)
    {
        base.Heal(amount);
        towerHealthPanel.UpdateHealthUI();
    }


    /// <summary>
    /// 订阅恢复生命值事件
    /// </summary>
    public void RecoverHealth()
    {
        maxHealth += 30;
        currentHealth += 60;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // 更新血量面板
        towerHealthPanel.UpdateHealthUI();
    }


    void OnEnable()
    {
        tetoRecoverHealthLevelUpSO.onLevelUp += RecoverHealth;
    }
    void OnDisable()
    {
        tetoRecoverHealthLevelUpSO.onLevelUp -= RecoverHealth;
    }
}
