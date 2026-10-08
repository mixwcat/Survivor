using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-130)]
public class PlayerManager : ManagerSingleton<PlayerManager>, IPlayerManager
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IPlayerManager Service =>
        ServiceLocator.TryGet<IPlayerManager>(out var pm) ? pm : null;

    private PlayerController _localPlayer;
    private readonly List<PlayerController> _allPlayers = new();

    public PlayerController LocalPlayer => _localPlayer;
    public IReadOnlyList<PlayerController> AllPlayers => _allPlayers;

    /// <summary>本地玩家变化（生成 / 销毁 / 切换）；参数可能为 null。</summary>
    public event System.Action<PlayerController> LocalPlayerChanged;

    protected override void OnSingletonAwake()
    {
        ServiceLocator.Register<IPlayerManager>(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // 只有"当前注册的就是我"才注销：重复实例被销毁时无条件注销会清掉主实例的服务入口
        ServiceLocator.UnregisterIfSelf<IPlayerManager>(this);
    }

    public void Register(PlayerController player)
    {
        if (player == null) return;
        if (!_allPlayers.Contains(player))
            _allPlayers.Add(player);

        if (_localPlayer == null)
            SetLocalPlayer(player);
    }

    public void Unregister(PlayerController player)
    {
        _allPlayers.Remove(player);

        if (_localPlayer == player)
            SetLocalPlayer(_allPlayers.Count > 0 ? _allPlayers[0] : null);
    }

    /// <summary>
    /// 单一写入点：只有它发 <see cref="LocalPlayerChanged"/>，
    /// 订阅方（升级面板协调者、HUD 等）据此退订旧玩家、绑定新玩家。
    /// </summary>
    private void SetLocalPlayer(PlayerController player)
    {
        if (_localPlayer == player) return;

        _localPlayer = player;
        LocalPlayerChanged?.Invoke(player);
    }
}
