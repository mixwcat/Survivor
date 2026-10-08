using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 组合根（Composition Root）。
/// 在首个场景加载前自动创建，按依赖顺序初始化并注册全局服务。
/// 不依赖任何场景/Inspector 配置，随进程常驻。
///
/// 职责边界：
/// - 只负责「全局服务」的生命周期：IAssetService、IAudioService、IUIService、IPlayerProfileService、IRunSessionService。
/// - 场景内业务 Manager（PlayerManager/GameLevelManager 等）仍由各自场景持有并自注册。
/// - 场景级 UI 展示通过 <see cref="TryWaitReadyAsync"/> 之后再执行，避免与服务初始化竞争。
///
/// <para>
/// <b>服务分两档：</b>
/// <list type="bullet">
/// <item><b>关键服务</b>（Asset / UI / Profile / RunSession）—— 缺任何一个，游戏都没法正常跑。
/// 它们失败时 <see cref="Ready"/> **不会成功完成**（fault），场景入口据此停止推进，
/// 而不是带着一个空档案或没有画布的 UI 继续进入大厅。</item>
/// <item><b>可降级服务</b>（Audio）—— 失败只影响表现（静音），不阻塞核心流程，
/// 但会留一条明确的降级日志。</item>
/// </list>
/// 旧实现里"任一服务失败都不阻塞其余服务"是**全部**服务的策略，于是档案初始化抛异常时
/// <c>Ready</c> 照样成功，大厅继续加载、按默认档运行 —— 玩家看到的是进度凭空消失。
/// </para>
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    private static TaskCompletionSource<bool> _readyTcs = new TaskCompletionSource<bool>();

    /// <summary>
    /// 全局服务初始化完成信号。
    /// 关键服务失败时这个任务会 **fault**；场景脚本请用
    /// <see cref="TryWaitReadyAsync"/> 等待（它把异常转成 false，不会污染 async void 的续体）。
    /// </summary>
    public static Task Ready => _readyTcs.Task;

    /// <summary>关键服务初始化失败的原因（成功时为 null）。可直接显示给玩家。</summary>
    public static string FailureReason { get; private set; }

    /// <summary>
    /// 每次进入 Play 前重置静态状态，兼容关闭 Domain Reload 的编辑器设置。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _readyTcs = new TaskCompletionSource<bool>();
        FailureReason = null;
        ServiceLocator.Clear();

        // 静态缓存同样会跨 Play 会话存活，而它们依赖的 Unity 对象已随上一会话销毁
        InputHandleFactory.ClearCache();
        ExpSpritePool.Reset();
        EnemyPool.Reset();
        ProjectilePool.Reset();
        UpgradeSelector.Reset();

        // 联机组合根自己的静态引用（NetworkManager.singleton 清不掉，理由见那里）
        NetworkBootstrap.ResetStatics();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (ServiceLocator.IsRegistered<IAssetService>()) return;

        GameObject go = new GameObject("[GameBootstrap]");
        DontDestroyOnLoad(go);
        go.AddComponent<GameBootstrap>();
        Debug.Log("[GameBootstrap] 组合根已创建");
    }

    /// <summary>
    /// 等待全局服务就绪。
    /// <b>关键服务失败时返回 false</b>（原因见 <see cref="FailureReason"/>），
    /// 调用方应当停止推进并给出可诊断的提示，而不是继续读一个不可用的服务。
    /// </summary>
    public static async Task<bool> TryWaitReadyAsync()
    {
        try
        {
            await _readyTcs.Task;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameBootstrap] 全局服务未就绪：{e.Message}");
            return false;
        }
    }

    private async void Awake()
    {
        if (ServiceLocator.IsRegistered<IAssetService>())
        {
            _readyTcs.TrySetResult(true);
            return;
        }

        var failed = new List<string>();

        // ── 关键服务 1：资源。其余服务都依赖它 ──
        AssetService assetService = gameObject.AddComponent<AssetService>();
        ServiceLocator.Register<IAssetService>(assetService);
        if (!await SafeInit("IAssetService", assetService.InitializeAsync)) failed.Add("IAssetService");

        // ── 可降级服务：音频。失败只静音，不阻塞流程 ──
        AudioService audioService = gameObject.AddComponent<AudioService>();
        ServiceLocator.Register<IAudioService>(audioService);
        if (!await SafeInit("IAudioService", audioService.InitializeAsync))
        {
            Debug.LogWarning("[GameBootstrap] IAudioService 初始化失败 —— 这是可降级服务，" +
                             "游戏会静音继续运行（所有调用点都已判空）。");
        }

        // ── 关键服务 2：UI ──
        UIService uiService = gameObject.AddComponent<UIService>();
        ServiceLocator.Register<IUIService>(uiService);
        // 光看"没抛异常"不够：UIService 内部会把异常消化成日志（画布加载失败时照样正常返回），
        // 于是"没有画布"会被当成"UI 就绪"，之后每个面板都静默不显示
        if (!await SafeInit("IUIService", uiService.InitializeAsync) || !uiService.IsOperational)
            failed.Add("IUIService");

        // ── 关键服务 3：档案。UI 之后是因为 ApplyAudioSettings 需要 IAudioService ──
        PlayerProfileService profileService = gameObject.AddComponent<PlayerProfileService>();
        ServiceLocator.Register<IPlayerProfileService>(profileService);
        // 档案读失败会回退默认档 —— 那不算失败（玩家还能玩），但 Profile 为 null 就是真失败
        if (!await SafeInit("IPlayerProfileService", profileService.InitializeAsync) ||
            profileService.Profile == null)
            failed.Add("IPlayerProfileService");

        // ── 关键服务 4：出行会话 ──
        RunSessionService sessionService = gameObject.AddComponent<RunSessionService>();
        ServiceLocator.Register<IRunSessionService>(sessionService);
        if (!await SafeInit("IRunSessionService", sessionService.InitializeAsync)) failed.Add("IRunSessionService");

        // ── 可降级服务：联机会话表。纯内存字典，创建即可用，失败也不阻塞单机 ──
        gameObject.AddComponent<NetworkSessionService>();

        // ── 可降级服务：联机组合根（NetworkManager）。──
        // 失败**不算关键失败**：它只意味着"不能联机"，单机流程照常跑 ——
        // 与 IAudioService 同一档，但会留一条明确的降级日志。
        var networkBootstrap = gameObject.AddComponent<NetworkBootstrap>();
        if (!await SafeInit("NetworkBootstrap", networkBootstrap.InitializeAsync))
        {
            Debug.LogWarning("[GameBootstrap] NetworkBootstrap 初始化失败 —— 本局只能单机运行。" +
                             $"原因：{networkBootstrap.FailureReason ?? "未知"}");
        }

        // 面板不在此处预加载：由 ShowPanelAsync 按需加载，避免把当前场景用不到的面板也拉进内存。

        if (failed.Count > 0)
        {
            FailureReason = string.Join("、", failed);
            Debug.LogError($"[GameBootstrap] 关键服务初始化失败：{FailureReason}。" +
                           "全局服务不会进入就绪状态，场景入口应停止推进并提示玩家。");

            // fault 而不是 TrySetResult(true)：后者会让大厅照常加载，
            // 然后在读一个空档案 / 不可用服务时抛 NullReference —— 现场离原因太远
            _readyTcs.TrySetException(new InvalidOperationException($"关键服务初始化失败：{FailureReason}"));
            return;
        }

        _readyTcs.TrySetResult(true);
    }

    /// <summary>
    /// 独立初始化单个服务：抛异常时记录并返回 false，由调用方决定它是否致命。
    /// </summary>
    private static async Task<bool> SafeInit(string name, Func<Task> init)
    {
        try
        {
            await init();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameBootstrap] {name} 初始化失败: {e}");
            return false;
        }
    }
}
