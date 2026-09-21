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

    /// <summary>兼容旧属性，建议迁移到 LocalPlayer</summary>
    public PlayerController player => _localPlayer;

    protected override void OnSingletonAwake()
    {
        ServiceLocator.Register<IPlayerManager>(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ServiceLocator.Unregister<IPlayerManager>();
    }

    public void Register(PlayerController player)
    {
        if (player == null) return;
        if (!_allPlayers.Contains(player))
            _allPlayers.Add(player);
        if (_localPlayer == null)
            _localPlayer = player;
    }

    public void Unregister(PlayerController player)
    {
        _allPlayers.Remove(player);
        if (_localPlayer == player)
            _localPlayer = _allPlayers.Count > 0 ? _allPlayers[0] : null;
    }

    /// <summary>
    /// 查找玩家对象（兼容旧代码，内部调用 Register）
    /// </summary>
    public void FindPlayer(PlayerController playerController = null)
    {
        var player = playerController ?? FindFirstObjectByType<PlayerController>();
        Register(player);
    }

    public void MissPlayer()
    {
        if (_localPlayer != null)
            Unregister(_localPlayer);
    }
}
