using UnityEngine;

/// <summary>
/// 玩家血量控制器
/// 无敌时间从 StatModel 读取
/// 满血恢复由 LevelUpSO.fullHeal 数据驱动，不再需要事件订阅
/// </summary>
public class PlayerHealthController : BaseHealthController
{
    [Header("无敌")]
    private bool _isUnbeatable = false;

    [Header("组件")]
    private HealthPanel _healthPanel;

    protected override void Start()
    {
        base.Start();
        _healthPanel = GetComponentInChildren<HealthPanel>();
        CurrentHealth = MaxHealth;
    }

    /// <summary>
    /// 受到伤害（无敌时间从 StatModel 读取）
    /// </summary>
    public override void TakeDamage(float damage)
    {
        if (_isUnbeatable) return;

        base.TakeDamage(damage);
        DamageNumManager.Service.SpawnDamageNum(transform.position, damage, DamageNumType.Red);
        AudioService.Service?.PlaySfx(ResourceEnum.PlayerGetHurt);
        _healthPanel.UpdateHealthUI();

        float unbeatableTime = _entity.GetStat(StatType.PlayerUnbeatableTime);
        _isUnbeatable = true;
        Invoke(nameof(ResetUnbeatableState), unbeatableTime);
    }

    private void ResetUnbeatableState()
    {
        _isUnbeatable = false;
    }

    protected override void Die()
    {
        base.Die();
        // 通过 IGameLevelManager 上报死亡（权威边界：联机时由网络实现转发给服务器）
        GameLevelManager.Service?.NotifyPlayerDied(_entity as PlayerController);
    }
}
