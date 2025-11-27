using UnityEngine;

public class LuoHealthController : BaseHealthController
{
    private TowerHealthPanel towerHealthPanel;
    [Header("事件")]
    public LevelUpSO luoRecoverHealthLevelUpSO;

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
    /// 订阅恢复血量事件
    /// </summary>
    public void RecoverHealth()
    {
        maxHealth += 10;
        currentHealth += 30;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // 更新血量面板
        towerHealthPanel.UpdateHealthUI();
    }


    void OnEnable()
    {
        luoRecoverHealthLevelUpSO.onLevelUp += RecoverHealth;
    }
    void OnDisable()
    {
        luoRecoverHealthLevelUpSO.onLevelUp -= RecoverHealth;
    }
}
