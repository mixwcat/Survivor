using UnityEngine;
using System.Collections.Generic;
using Mirror;

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

    /// <summary>
    /// 登记一个玩家。
    ///
    /// <para>
    /// <b>"谁是本地玩家"在联机下不能按"第一个注册的"来判：</b>
    /// 网络对象的注册时机由 <c>NetworkPlayerState</c> 掌握，而远程玩家的对象可能**先于**
    /// 本地玩家生成（服务端按连接顺序 spawn）。旧规则会让本地 HUD、相机、升级面板
    /// 全部绑到别人身上，而且不报错。
    /// </para>
    ///
    /// <para>
    /// 判据因此是 <see cref="NetworkIdentity.isLocalPlayer"/>：有它（联机对象）就按它判；
    /// 没有（单机生成的玩家）才退回"第一个注册的就是本地玩家"。
    /// </para>
    /// </summary>
    public void Register(PlayerController player)
    {
        if (player == null) return;
        if (!_allPlayers.Contains(player))
            _allPlayers.Add(player);

        if (player.TryGetComponent(out NetworkIdentity identity))
        {
            // 联机对象：只有 isLocalPlayer 的那个才是"我的角色"
            if (identity.isLocalPlayer) SetLocalPlayer(player);
            return;
        }

        // 单机：没有网络身份，第一个注册的即本地玩家
        if (_localPlayer == null)
            SetLocalPlayer(player);
    }

    public void Unregister(PlayerController player)
    {
        _allPlayers.Remove(player);

        if (_localPlayer == player)
            SetLocalPlayer(FindAnyLocalPlayer());
    }

    /// <summary>
    /// 本地玩家被销毁后，从剩下的玩家中再找一个 <c>isLocalPlayer</c> 的（通常没有，返回 null）。
    /// 切场景时旧对象先销毁、新对象后生成，中间这一小段 <see cref="LocalPlayer"/> 为 null 是正常的。
    /// </summary>
    private PlayerController FindAnyLocalPlayer()
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            PlayerController candidate = _allPlayers[i];
            if (candidate != null && candidate.TryGetComponent(out NetworkIdentity identity) && identity.isLocalPlayer)
                return candidate;
        }

        return null;
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
