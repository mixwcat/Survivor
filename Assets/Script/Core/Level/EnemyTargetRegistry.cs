using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人目标注册表（场景级 Manager）。
///
/// <para>
/// 用 <see cref="ManagerSingleton{T}"/> 是因为它天然按场景重建 ——
/// 目标列表（玩家 / 塔 / 推车）每局都不一样，跨场景保留只会留下上一局的已销毁对象。
/// </para>
///
/// <para>
/// <b>它不校验"是不是合法目标"</b>：注册方（<c>PlayerController</c> / <c>BaseTower</c> /
/// <c>CartController</c>）自己知道该不该注册。放在这里校验会让"谁能被打"变成两处判断。
/// </para>
/// </summary>
[DefaultExecutionOrder(-110)]
public class EnemyTargetRegistry : ManagerSingleton<EnemyTargetRegistry>, IEnemyTargetRegistry
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IEnemyTargetRegistry Service =>
        ServiceLocator.TryGet<IEnemyTargetRegistry>(out var svc) ? svc : null;

    private readonly List<Transform> _targets = new List<Transform>();

    public IReadOnlyList<Transform> Targets => _targets;

    protected override void OnSingletonAwake()
    {
        ServiceLocator.Register<IEnemyTargetRegistry>(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        ServiceLocator.UnregisterIfSelf<IEnemyTargetRegistry>(this);
        _targets.Clear();
    }

    public void Register(Transform target)
    {
        if (target == null || _targets.Contains(target)) return;

        _targets.Add(target);
    }

    public void Unregister(Transform target)
    {
        if (target == null) return;

        _targets.Remove(target);
    }
}
