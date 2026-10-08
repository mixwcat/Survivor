using System;
using System.Collections.Generic;
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
    /// <summary>
    /// 正在生成玩家的连接（按 <c>connectionId</c>）。
    ///
    /// <para>
    /// <b>为什么需要这道闸：</b><c>OnServerReady</c> 可能在一次生成完成之前再来一次
    /// （客户端的 Ready 与场景加载后的 Ready 挨得很近），而生成要走 Addressables 加载、
    /// 中间有 <c>await</c>。两道请求都会通过 <c>conn.identity == null</c> 的判据，
    /// 于是同一个连接被生成**两个**玩家对象 —— 一个成为 <c>conn.identity</c>，
    /// 另一个变成"没有主人的野玩家"（多出来的人影，且不会自己消失）。
    /// </para>
    /// </summary>
    private readonly HashSet<int> _spawningConnections = new HashSet<int>();

    /// <summary>服务端在一个连接就绪后确保它有玩家对象（失败只记日志，不抛给 Mirror 的消息循环）。</summary>
    private async Task EnsurePlayerAsync(NetworkConnectionToClient conn)
    {
        int connectionId = -1;

        try
        {
            if (conn == null) return;

            // 已经有玩家对象（含"刚生成过、还没销毁"的情况）就不重复生成
            if (conn.identity != null) return;

            connectionId = conn.connectionId;
            if (!_spawningConnections.Add(connectionId)) return;   // 已经有一次生成在飞

            PlayerSpawner spawner = PlayerSpawner.Current;
            if (spawner == null)
            {
                Debug.LogError($"[Net] 当前场景没有 {nameof(PlayerSpawner)}，" +
                               $"无法为连接 {connectionId} 生成玩家。");
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
        finally
        {
            if (connectionId >= 0) _spawningConnections.Remove(connectionId);
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

        // 推车状态的消息处理器在**连接建立时**注册，而不是等场景里的 CartNetworkSync.Start ——
        // 服务端在关卡加载完就开始广播，客户端那时可能还没加载完场景，
        // 晚注册会漏掉开头几条（表现是"进关卡后推车停着不动，过一会儿才追上"）
        CartNetworkSync.RegisterClientHandler();

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
