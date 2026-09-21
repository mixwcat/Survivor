using System.Collections.Generic;

/// <summary>
/// 防御塔注册表服务（全局共享）。
/// 塔为合作模式下所有玩家共享的资源，不区分为玩家归属。
/// </summary>
public interface ITowerManager
{
    IReadOnlyList<BaseTower> Towers { get; }

    void RegisterTower(BaseTower tower);
    void UnregisterTower(BaseTower tower);
}
