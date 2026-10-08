using System;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 联机组合根 —— 在 <c>[GameBootstrap]</c> 上创建并持有 <see cref="SurvivorNetworkManager"/>。
///
/// <para>
/// <b>为什么由组合根持有：</b>项目里所有「跨场景常驻的全局设施」都由 <see cref="GameBootstrap"/>
/// 创建、注册、初始化，联机的 <c>NetworkManager</c> 属于同一类。放在场景里各摆一个会在切场景时
/// 被销毁（连接随之断开），而重复实例又会互相销毁（与 <c>ManagerSingleton</c> 的「重复实例」是同一类事故）。
/// </para>
///
/// <para>
/// <b>为什么用 prefab 而不是代码配置：</b><c>spawnPrefabs</c> 这类字段需要**序列化的 prefab 引用**，
/// 代码里没有来源。做成 <c>Net/NetworkManager</c> prefab 后：配置在 Inspector 里可视化、
/// 生命周期仍归组合根、也不必新增 additive / 持久场景（见 <c>Docs/MirrorPlan.md</c> §1.3）。
/// </para>
///
/// <para>
/// <b>它不暴露 <c>Instance</c> 给业务代码用。</b>业务代码要问的是「现在是不是联机」这类问题，
/// 走下面几个静态只读属性；直接拿 <c>NetworkBootstrap.Instance</c> 会绕过服务定位器约定。
/// </para>
///
/// <para>
/// <b>失败是可降级的：</b>prefab 加载失败只意味着「不能联机」，单机流程照常 ——
/// 所以组合根不会因为它的失败而让 <c>GameBootstrap.Ready</c> fault（与 <c>IAudioService</c> 同一档）。
/// </para>
/// </summary>
public class NetworkBootstrap : MonoBehaviour
{
    /// <summary>组合根持有的实例（未初始化时为 null）。</summary>
    internal static NetworkBootstrap Instance { get; private set; }

    /// <summary>当前生效的 NetworkManager（未初始化时为 null）。</summary>
    public static SurvivorNetworkManager Manager =>
        SurvivorNetworkManager.singleton as SurvivorNetworkManager;

    /// <summary>本进程是否已经启动网络（服务端或客户端任一侧）。</summary>
    public static bool IsActive => NetworkServer.active || NetworkClient.active;

    /// <summary>联机是否可用（prefab 已就绪）。false 时 <see cref="StartHost"/> / <see cref="StartClient"/> 会拒绝。</summary>
    public static bool IsReady => Manager != null;

    /// <summary>初始化失败的原因（成功时为 null）。</summary>
    public string FailureReason { get; private set; }

    private AsyncOperationHandle<GameObject> _prefabHandle;
    private Task _initTask;

    /// <summary>
    /// 初始化（幂等 + 并发安全，失败可重试）。
    /// 只加载并实例化 prefab；**不**自动建房或连接 —— 那是玩家在房间里做的选择。
    /// </summary>
    public Task InitializeAsync() => _initTask ??= InitializeInternalAsync();

    private async Task InitializeInternalAsync()
    {
        if (Manager != null) return;

        IAssetService assets = AssetService.Service;
        if (assets == null)
        {
            FailureReason = "IAssetService 未注册";
            _initTask = null;          // 允许重试
            return;
        }

        // 句柄持有到进程结束：spawnPrefabs 引用的 prefab 可能只被这个 prefab 引用，
        // 实例化后立刻 Release 会让它们随 bundle 卸载，之后 NetworkServer.Spawn 直接失败。
        _prefabHandle = assets.LoadAssetAsync<GameObject>(AssetKeys.NetworkManager);
        GameObject prefab = await _prefabHandle.Task;

        if (this == null)
        {
            if (_prefabHandle.IsValid()) assets.Release(_prefabHandle);
            return;
        }

        if (prefab == null)
        {
            FailureReason = $"联机 prefab 加载失败（key={AssetKeys.NetworkManager}）。" +
                            WeaponAssembler.DescribeLoadFailure(_prefabHandle);
            if (_prefabHandle.IsValid()) assets.Release(_prefabHandle);
            Debug.LogError($"[NetworkBootstrap] {FailureReason}");
            _initTask = null;
            return;
        }

        if (prefab.GetComponent<SurvivorNetworkManager>() == null)
        {
            FailureReason = $"联机 prefab 上没有 {nameof(SurvivorNetworkManager)}";
            if (_prefabHandle.IsValid()) assets.Release(_prefabHandle);
            Debug.LogError($"[NetworkBootstrap] {FailureReason}（{AssetKeys.NetworkManager}）");
            _initTask = null;
            return;
        }

        // 实例化即触发 NetworkManager.Awake：
        // - InitializeSingleton() 会 SetParent(null) + DontDestroyOnLoad（prefab 上 dontDestroyOnLoad = true）
        // - transport 由同物体上的 KcpTransport 经 TryGetComponent 自动接线（NetworkManager.cs:729-734）
        //   ⇒ prefab 上**必须**把 Transport 与 NetworkManager 放在同一个 GameObject 上
        Instantiate(prefab);

        if (Manager == null)
        {
            FailureReason = "NetworkManager 实例化后仍未成为 singleton";
            Debug.LogError($"[NetworkBootstrap] {FailureReason}");
            _initTask = null;
            return;
        }

        Debug.Log($"[NetworkBootstrap] 联机已就绪（transport={Manager.transport?.GetType().Name}，" +
                  $"offlineScene={Manager.offlineScene}，spawnPrefabs={Manager.spawnPrefabs.Count}）");
    }

    // ── 对外操作（房间 UI 调用；业务代码不直接用）──

    /// <summary>创建房间（服务端 + 本机客户端）。未初始化时返回 false。</summary>
    public static bool StartHost()
    {
        if (Manager == null)
        {
            Debug.LogError("[NetworkBootstrap] 联机未就绪，无法创建房间。");
            return false;
        }

        PrepareForNetworkSpawn();
        Manager.StartHost();
        return true;
    }

    /// <summary>加入房间。<paramref name="address"/> 为 IPv4 或主机名。</summary>
    public static bool StartClient(string address, ushort port)
    {
        if (Manager == null)
        {
            Debug.LogError("[NetworkBootstrap] 联机未就绪，无法加入房间。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            Debug.LogWarning("[NetworkBootstrap] 地址为空，忽略本次加入请求。");
            return false;
        }

        Manager.networkAddress = address.Trim();

        // 端口由 transport 持有。用 PortTransport 接口而不是具体类型 kcp2k.KcpTransport ——
        // 换 transport 时这里不用改（Mirror 自己的 NetworkManagerHUD 也是这么写的）。
        if (Manager.transport is PortTransport portTransport) portTransport.Port = port;

        PrepareForNetworkSpawn();
        Manager.StartClient();
        return true;
    }

    /// <summary>
    /// 进入联机之前的统一准备：**销毁离线玩家**。
    ///
    /// <para>
    /// 大厅允许玩家在**离线状态**下先逛（见 <c>Docs/MirrorPlan.md</c> §1.3），
    /// 那时玩家对象是 <c>PlayerSpawner</c> 自己实例化的普通克隆 ——
    /// 它没有经过 <c>AddPlayerForConnection</c>，Mirror 不认它是"某个连接的玩家对象"。
    /// 同一个对象没法两者兼任，所以建房/加入时把它销毁，改由服务端重新生成一个网络玩家。
    /// </para>
    /// </summary>
    private static void PrepareForNetworkSpawn()
    {
        PlayerSpawner.Current?.DiscardOfflinePlayer();
    }

    /// <summary>离开房间（Host 或 Client 都走这里）。随后 Mirror 会切回 <c>offlineScene</c>。</summary>
    public static void Stop()
    {
        SurvivorNetworkManager manager = Manager;
        if (manager == null) return;

        if (NetworkServer.active && NetworkClient.isConnected) manager.StopHost();
        else if (NetworkClient.isConnected) manager.StopClient();
        else if (NetworkServer.active) manager.StopServer();
    }

    /// <summary>仅供服务端：请求切换网络场景（客户端不要自己切）。</summary>
    public static bool ServerChangeScene(string sceneName)
    {
        SurvivorNetworkManager manager = Manager;
        if (manager == null || !NetworkServer.active)
        {
            Debug.LogWarning($"[NetworkBootstrap] 非服务端不能切换网络场景（{sceneName}）。");
            return false;
        }

        manager.ServerChangeScene(sceneName);
        return true;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // 先停网络再放句柄：反过来的话正在跑的连接会引用已卸载的 prefab 资源
        Stop();

        if (_prefabHandle.IsValid() && AssetService.Service != null)
            AssetService.Service.Release(_prefabHandle);
    }

    /// <summary>
    /// 每个 Play 会话清一次静态引用（关掉 Domain Reload 后静态状态会跨会话存活）。
    /// </summary>
    internal static void ResetStatics()
    {
        Instance = null;

        // Mirror 的 NetworkManager.singleton 不用我们清：它自己有一个
        // [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] ResetStatics()
        // （Core/NetworkManager.cs:777-793），会 StopHost 并把 singleton 置 null。
        // 而且那个属性是 `{ get; internal set; }`，我们在 Assembly-CSharp 里也写不了。
    }
}
