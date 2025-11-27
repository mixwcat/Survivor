using UnityEngine;

public class PlayerHealthController : BaseHealthController
{
    [Header("无敌时间")]
    private bool isUnbeatable = false;
    public float unbeatableTime = 0.5f;
    [Header("组件")]
    private HealthPanel healthPanel;
    [Header("事件")]
    public LevelUpSO recoverHealthLevelUpSO;

    void Start()
    {
        currentHealth = maxHealth;
        healthPanel = GetComponentInChildren<HealthPanel>();
    }


    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage"></param>
    public override void TakeDamage(float damage)
    {
        if (isUnbeatable)
        {
            return;
        }

        // 造成伤害
        base.TakeDamage(damage);

        // 生成伤害数字
        DamageNumManager.Instance.SpawnDamageNum(transform.position, damage, DamageNumType.Red);

        // 音效
        BKMusic.Instance.PlaySound(ResourceEnum.PlayerGetHurt);

        // 触发血量变化事件
        healthPanel.UpdateHealthUI();

        // 开始无敌时间计时
        isUnbeatable = true;
        Invoke(nameof(ResetUnbeatableState), unbeatableTime);
    }


    /// <summary>
    /// 重置无敌状态
    /// </summary>
    private void ResetUnbeatableState()
    {
        isUnbeatable = false;
    }

    /// <summary>
    /// 订阅恢复生命值事件
    /// </summary>
    private void RecoverHealth()
    {
        currentHealth = maxHealth;
        healthPanel.UpdateHealthUI();
    }

    private void OnEnable()
    {
        recoverHealthLevelUpSO.onLevelUp += RecoverHealth;
    }

    private void OnDisable()
    {
        recoverHealthLevelUpSO.onLevelUp -= RecoverHealth;
    }

    protected override void Die()
    {
        base.Die();
        EventCenter.Trigger(PlayerEnum.OnPlayerDead, null);
    }
}
