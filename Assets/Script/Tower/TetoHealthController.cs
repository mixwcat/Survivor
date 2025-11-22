using UnityEngine;

public class TetoHealthController : BaseHealthController
{
    private TowerHealthPanel towerHealthPanel;
    [Header("事件")]
    public LevelUpSO recoverHealthLevelUpSO;

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
        DamageNumManager.Instance.SpawnDamageNum(transform.position, damage).SpawmRedNum();
    }


    /// <summary>
    /// 恢复血量并增加最大值
    /// </summary>
    public void RecoverHealth()
    {
        maxHealth += 100;
        currentHealth += 150;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // 更新血量面板
        towerHealthPanel.UpdateHealthUI();
    }


    void OnEnable()
    {
        recoverHealthLevelUpSO.onLevelUp += RecoverHealth;
    }
    void OnDisable()
    {
        recoverHealthLevelUpSO.onLevelUp -= RecoverHealth;
    }
}
