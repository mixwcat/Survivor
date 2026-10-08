using UnityEngine;

/// <summary>
/// 玩家血量控制器
/// 无敌时间从 StatModel 读取
/// 满血恢复由 LevelUpSO.fullHeal 数据驱动，不再需要事件订阅
///
/// <para>
/// <b>死亡不在这里判胜负</b>：<see cref="BaseHealthController.Die"/> 销毁玩家对象，
/// <c>PlayerController.OnDisable</c> 随即把它从 <c>PlayerManager</c> 注销，
/// <see cref="StageDirector"/> 读到「一个玩家都不剩」才结束本局。
/// 原先这里还额外调 <c>GameLevelManager.NotifyPlayerDied</c> 直接结束游戏 ——
/// 那是第二条结束入口：单人时会同时弹出死亡面板与结算面板，
/// 多人时第一个人阵亡就结束全队（与「全员阵亡才算失败」冲突）。
/// </para>
/// </summary>
public class PlayerHealthController : BaseHealthController
{
    [Header("无敌")]
    private bool _isUnbeatable = false;

    /// <summary>
    /// 玩家的血量由服务端权威并同步给各端（见 <c>NetworkPlayerState</c> 的同名 SyncVar）。
    /// 敌人接触伤害从 P3 起就只在服务端结算 —— 不打开这一条的话，
    /// **客户端副本的血量永远不动：HUD 血条一直是满的、也永远不会死**。
    /// </summary>
    protected override bool IsHealthSynced => true;

    protected override void Start()
    {
        // 血量初始化归基类（它知道客户端副本不该自己写 MaxHealth）
        base.Start();
    }

    /// <summary>
    /// 受到伤害（无敌时间从 StatModel 读取）。
    /// 玩家目前不吃击退，<see cref="DamageInfo.HitForce"/> 仅为保持重写签名一致。
    ///
    /// <para>
    /// <b>这里不再推任何 UI</b>：血量变化由 <see cref="BaseHealthController.HealthChanged"/>
    /// 广播，左上角的 HUD 血条自己订阅。头顶那条世界空间血条已经去掉 ——
    /// 血量属于"玩家一直要看的全局状态"，挂在角色头顶会随移动乱跑，也会被角色自己挡住。
    /// </para>
    /// </summary>
    protected override void ApplyDamage(in DamageInfo info)
    {
        if (_isUnbeatable) return;

        // ⚠️ 必须调 base.ApplyDamage，**不能**调 base.TakeDamage ——
        // 后者是"唯一入口"，它在服务端会再走一遍 ApplyDamage ⇒ 无限递归 ⇒ 栈溢出。
        // TakeDamage 是给**外部调用者**用的（子弹/接触伤害），重写里的"先跑基类逻辑"是 ApplyDamage
        base.ApplyDamage(in info);
        DamageNumService.Service?.SpawnDamageNum(transform.position, info.Amount, DamageNumType.Red);
        AudioService.Service?.PlaySfx(ResourceEnum.PlayerGetHurt);

        float unbeatableTime = _entity.GetStat(StatType.PlayerUnbeatableTime);
        _isUnbeatable = true;
        Invoke(nameof(ResetUnbeatableState), unbeatableTime);
    }

    private void ResetUnbeatableState()
    {
        _isUnbeatable = false;
    }
}
