using UnityEngine;

public class RinHealthController : BaseHealthController
{
    private TowerHealthPanel towerHealthPanel;
    [Header("事件")]
    public LevelUpSO rinRecoverHealthLevelUpSO;

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
        maxHealth += 50;
        currentHealth += 100;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // 更新血量面板
        towerHealthPanel.UpdateHealthUI();
    }


    void OnEnable()
    {
        rinRecoverHealthLevelUpSO.onLevelUp += RecoverHealth;
    }
    void OnDisable()
    {
        rinRecoverHealthLevelUpSO.onLevelUp -= RecoverHealth;
    }
}
