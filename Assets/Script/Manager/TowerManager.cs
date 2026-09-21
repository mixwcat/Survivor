using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-110)]
public class TowerManager : ManagerSingleton<TowerManager>, ITowerManager
{
    [Header("塔列表")]
    public List<BaseTower> towers = new List<BaseTower>();

    public IReadOnlyList<BaseTower> Towers => towers;

    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static ITowerManager Service =>
        ServiceLocator.TryGet<ITowerManager>(out var svc) ? svc : null;

    protected override void OnSingletonAwake()
    {
        ServiceLocator.Register<ITowerManager>(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ServiceLocator.Unregister<ITowerManager>();
    }

    public void RegisterTower(BaseTower tower)
    {
        towers.Add(tower);
    }
    public void UnregisterTower(BaseTower tower)
    {
        towers.Remove(tower);
    }
}
