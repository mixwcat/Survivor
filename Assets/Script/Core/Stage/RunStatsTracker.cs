using UnityEngine;

/// <summary>
/// 本局统计（场景级）—— 目前只记击杀数，后续的伤害统计、掉落统计都挂这里。
///
/// <para>
/// <b>为什么用场景级服务而不是让统计器订阅每个敌人：</b>敌人是**池化**的，
/// 逐个订阅意味着每生成一个都要挂/摘一次委托，而且回池时事件不会自动清空。
/// 由死亡方主动上报（<c>EnemyHealthController.Die()</c> → <see cref="ReportDeath"/>）
/// 只有一条路径，也不依赖实例生命周期。
/// </para>
///
/// <para>
/// <b>它不判断归属</b>：归属已经在 <c>EnemyDeathInfo.Killer</c> 里解析过了，
/// 统计器只累加总数。按玩家拆分击杀是后续的事（联机结算需要）。
/// </para>
/// </summary>
[DefaultExecutionOrder(-110)]
public class RunStatsTracker : ManagerSingleton<RunStatsTracker>
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static RunStatsTracker Service =>
        ServiceLocator.TryGet<RunStatsTracker>(out var svc) ? svc : null;

    private int _totalKills;

    /// <summary>本局全队击杀总数。</summary>
    public int TotalKills => _totalKills;

    protected override void OnSingletonAwake()
    {
        ServiceLocator.Register(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ServiceLocator.UnregisterIfSelf<RunStatsTracker>(this);
    }

    /// <summary>上报一次敌人死亡。由 <c>EnemyHealthController</c> 调用。</summary>
    public void ReportDeath(in EnemyDeathInfo info)
    {
        _totalKills++;
    }
}
