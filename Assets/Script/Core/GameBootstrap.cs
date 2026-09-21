using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 组合根（Composition Root）。
/// 在首个场景加载前自动创建，按依赖顺序初始化并注册全局服务。
/// 不依赖任何场景/Inspector 配置，随进程常驻。
///
/// 职责边界：
/// - 只负责「全局服务」的生命周期：IAssetService、UIManager、音频、PC 输入。
/// - 场景内业务 Manager（SOManager/PlayerManager/GameLevelManager 等）仍由各自场景持有并自注册。
/// - 场景级 UI 展示通过 await GameBootstrap.Ready 后再执行，避免与服务初始化竞争。
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    private static TaskCompletionSource<bool> _readyTcs = new TaskCompletionSource<bool>();

    /// <summary>全局服务初始化完成信号；场景脚本在展示 UI 前应 await 它。</summary>
    public static Task Ready => _readyTcs.Task;

    /// <summary>
    /// 每次进入 Play 前重置静态状态，兼容关闭 Domain Reload 的编辑器设置。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _readyTcs = new TaskCompletionSource<bool>();
        ServiceLocator.Clear();
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

    private async void Awake()
    {
        if (ServiceLocator.IsRegistered<IAssetService>())
        {
            _readyTcs.TrySetResult(true);
            return;
        }

        // 同步阶段：确保场景实体 Awake 之前就绪的依赖
        AssetService assetService = gameObject.AddComponent<AssetService>();
        ServiceLocator.Register<IAssetService>(assetService);
        await SafeInit("IAssetService", assetService.InitializeAsync);

        AudioService audioService = gameObject.AddComponent<AudioService>();
        ServiceLocator.Register<IAudioService>(audioService);
        await SafeInit("IAudioService", audioService.InitializeAsync);

        ServiceLocator.Register<IUIService>(UIManager.Service);
        await SafeInit("IUIService", UIManager.Service.InitializeAsync);

        _readyTcs.TrySetResult(true);
    }

    /// <summary>
    /// 独立初始化单个服务：任一服务失败不阻塞其余服务。
    /// </summary>
    private static async Task SafeInit(string name, System.Func<Task> init)
    {
        try
        {
            await init();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameBootstrap] {name} 初始化失败: {e}");
        }
    }
}
