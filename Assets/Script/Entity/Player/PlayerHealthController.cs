using UnityEngine;

public class PlayerHealthController : BaseHealthController
{
    private bool isUnbeatable = false;
    public float unbeatableTime = 0.5f;

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


    private void ResetUnbeatableState()
    {
        isUnbeatable = false;
    }
}
