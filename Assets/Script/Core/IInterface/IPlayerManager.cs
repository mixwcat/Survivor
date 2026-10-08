using System.Collections.Generic;

/// <summary>
/// 玩家管理器接口。
/// 单机实现：LocalPlayer 即唯一玩家，AllPlayers 只有一个元素。
/// 联机实现：LocalPlayer 为本地玩家，AllPlayers 包含所有客户端同步的玩家。
/// </summary>
public interface IPlayerManager
{
    /// <summary>本地玩家（用于摄像机跟随、本地输入）</summary>
    PlayerController LocalPlayer { get; }

    /// <summary>所有已注册的玩家（单机时只有一个）</summary>
    IReadOnlyList<PlayerController> AllPlayers { get; }

    /// <summary>
    /// 本地玩家变化（生成 / 销毁 / 切换）。参数可能为 null（本地玩家已离场）。
    ///
    /// <para>
    /// <b>订阅方用它退订旧玩家</b>：按玩家持有的状态（升级选项、HUD、升级面板入口）
    /// 都挂在玩家实例上，玩家被销毁后订阅仍会留在委托里 ——
    /// 那是一个"已销毁对象上的回调"，下次事件触发时表现为
    /// <c>MissingReferenceException</c> 或者更糟：静默更新了一个不存在的玩家。
    /// 靠每帧比对 <c>LocalPlayer</c> 也能做，但那把"谁负责退订"变成了隐式的。
    /// </para>
    /// </summary>
    event System.Action<PlayerController> LocalPlayerChanged;

    /// <summary>注册玩家实例</summary>
    void Register(PlayerController player);

    /// <summary>注销玩家实例</summary>
    void Unregister(PlayerController player);
}
