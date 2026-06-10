using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour, IPlayerManager
{
    private static PlayerManager _instance;
    public static PlayerManager Instance => _instance;

    /// <summary>
    /// 兼容旧代码的安全访问：优先走 ServiceLocator，未注册时回退到 Instance。
    /// 所有业务代码应逐步迁移到 ServiceLocator.Get&lt;IPlayerManager&gt;()。
    /// </summary>
    public static IPlayerManager Service =>
        ServiceLocator.TryGet<IPlayerManager>(out var pm) ? pm : _instance;

    private PlayerController _localPlayer;
    private readonly List<PlayerController> _allPlayers = new();

    public PlayerController LocalPlayer => _localPlayer;
    public IReadOnlyList<PlayerController> AllPlayers => _allPlayers;

    /// <summary>兼容旧属性，建议迁移到 LocalPlayer</summary>
    public PlayerController player => _localPlayer;

    void Awake()
    {
        _instance = this;
        ServiceLocator.Register<IPlayerManager>(this);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            ServiceLocator.Unregister<IPlayerManager>();
        }
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
        if (player == null) return;
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
