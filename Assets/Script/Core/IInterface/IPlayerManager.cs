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

    /// <summary>注册玩家实例</summary>
    void Register(PlayerController player);

    /// <summary>注销玩家实例</summary>
    void Unregister(PlayerController player);
}
