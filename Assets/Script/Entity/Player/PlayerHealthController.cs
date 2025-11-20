using UnityEngine;

public class PlayerHealthController : BaseHealthController
{
    private bool isUnbeatable = false;
    public float unbeatableTime = 0.5f;


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

        // 触发血量变化事件
        EventCenter.Trigger(PlayerEnum.OnHealthChanged, null);

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


    protected override void Die()
    {
        base.Die();
        EventCenter.Trigger(PlayerEnum.OnPlayerDead, null);
    }
}
