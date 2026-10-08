using System;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;

/// <summary>
/// 本项目的 <see cref="NetworkManager"/> —— 只在 Mirror 的默认流程上补「生成/清理玩家」两件事。
///
/// <para>
/// <b>为什么生成玩家的钩子是 <c>OnServerReady</c> 而不是 <c>OnServerSceneChanged</c>：</b>
/// 本项目的场景分工是 <c>offlineScene = Lobby</c>、<c>onlineScene</c> 留空
/// （见 <c>Docs/MirrorPlan.md</c> §1.3）。于是"在大厅里建房"**不会切场景** ——
/// <c>IsServerOnlineSceneChangeNeeded()</c> 恒为 false（<c>NetworkManager.cs:280-283</c>），
/// <c>StartHost()</c> 直接走 <c>FinishStartHost()</c>，**<c>OnServerSceneChanged</c> 根本不会触发**。
/// </para>
///
/// <para>
/// <c>OnServerReady(conn)</c> 则是每次都来的：连接建立、以及**每次切场景**
/// （Mirror 在 <c>ServerChangeScene</c> 里 <c>SetAllClientsNotReady</c>，客户端加载完场景会重发 Ready）。
/// 于是它可以统一承担「这个连接现在没有玩家对象 → 给它生成一个」。
/// </para>
///
/// <para>
/// <b>幂等判据是 <c>conn.identity</c>：</b>切场景时旧玩家对象被销毁，但 Mirror **不会**把
/// <c>conn.identity</c> 置空（只有 <c>DestroyPlayerForConnection</c>/<c>RemovePlayerForConnection</c> 会）。
/// 好在销毁后的对象是 Unity 的"伪 null"，<c>conn.identity != null</c> 判为 false，
/// 正好表达"现在没有玩家对象"。<c>AddPlayerForConnection</c> 内部也是同一个判据，所以能接上。
/// </para>
/// </summary>
public class SurvivorNetworkManager : NetworkManager
{
    /// <summary>服务端在一个连接就绪后确保它有玩家对象（失败只记日志，不抛给 Mirror 的消息循环）。</summary>
    private async Task EnsurePlayerAsync(NetworkConnectionToClient conn)
    {
        try
        {
            if (conn == null) return;

            // 已经有玩家对象（含"刚生成过、还没销毁"的情况）就不重复生成
            if (conn.identity != null) return;

            PlayerSpawner spawner = PlayerSpawner.Current;
            if (spawner == null)
            {
                Debug.LogError($"[Net] 当前场景没有 {nameof(PlayerSpawner)}，" +
                               $"无法为连接 {conn.connectionId} 生成玩家。");
                return;
            }

            await spawner.SpawnForConnectionAsync(conn);
        }
        catch (Exception e)
        {
            // async void 之外的异步入口必须自己吞异常：漏出去会变成未观测异常，
            // 而 Mirror 的消息循环不该被一个玩家的生成失败打断
            Debug.LogError($"[Net] 为连接生成玩家时异常：{e}");
        }
    }

    // ── 服务端 ──

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"[Net] 服务端已启动（isHost={NetworkClient.active}，" +
                  $"最大连接数={maxConnections}，端口={(transport is PortTransport p ? p.Port : 0)}）");
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[Net] 连接接入：connId={conn.connectionId} address={conn.address}");
    }

    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);

        // 不 await：这是 Mirror 的同步回调，生成流程要走 Addressables
        _ = EnsurePlayerAsync(conn);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // 先清会话表再让基类销毁玩家对象：基类会 DestroyPlayerForConnection(conn)
        NetworkSessionService.Service?.Forget(conn.connectionId);

        base.OnServerDisconnect(conn);

        Debug.Log($"[Net] 连接断开：connId={conn.connectionId}");
    }

    public override void OnStopServer()
    {
        NetworkSessionService.Service?.ClearAll();
        base.OnStopServer();
        Debug.Log("[Net] 服务端已停止。");
    }

    // ── 客户端 ──

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[Net] 客户端已启动，连接到 {networkAddress}。");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();

        // 基类已经决定要不要切回 offlineScene（Lobby）。这里只留一条可诊断的日志 ——
        // "被踢回大厅"与"自己点的离开"在界面上必须能区分开
        Debug.LogWarning("[Net] 与服务器的连接已断开。");
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        Debug.Log("[Net] 客户端已停止。");
    }
}
