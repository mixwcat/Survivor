using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressables 资源服务（全局服务）。
///
/// 与 <see cref="AudioService"/> / <see cref="UIService"/> 遵循同一套规范：
/// - 挂在 [GameBootstrap] 物体上，由组合根 AddComponent → 注册 → InitializeAsync；
/// - 业务代码统一通过 <see cref="Service"/> 访问（未注册返回 null）；
/// - 不暴露具体类型单例（无 Instance）。
///
/// 句柄/实例所有权约定：
/// - <see cref="LoadAssetAsync{T}"/> 返回的句柄由**调用方**负责 <see cref="Release(AsyncOperationHandle)"/>；
/// - <see cref="InstantiateAsync"/> 创建的实例由调用方用 <see cref="ReleaseInstance"/> 归还（不能直接 Destroy）。
/// </summary>
public class AssetService : MonoBehaviour, IAssetService
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IAssetService Service =>
        ServiceLocator.TryGet<IAssetService>(out var svc) ? svc : null;

    private Task _initTask;

    /// <summary>初始化 Addressables（幂等 + 并发安全）。</summary>
    public Task InitializeAsync()
    {
        return _initTask ??= InitializeInternalAsync();
    }

    private async Task InitializeInternalAsync()
    {
        if (Addressables.ResourceLocators.Count() > 0)
            return;

        await Addressables.InitializeAsync().Task;
    }

    public AsyncOperationHandle<T> LoadAssetAsync<T>(object key) where T : Object
    {
        return Addressables.LoadAssetAsync<T>(key);
    }

    public async Task<GameObject> InstantiateAsync(object key, Transform parent = null)
    {
        return await Addressables.InstantiateAsync(key, parent).Task;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 直接 <c>Destroy</c> 实例不会递减源 prefab 的引用计数，会导致 bundle 永不卸载；
    /// 未被 Addressables 跟踪的对象会返回 false 且不销毁，由调用方自行 Destroy。
    /// </remarks>
    public bool ReleaseInstance(GameObject instance)
    {
        if (instance == null) return false;

        return Addressables.ReleaseInstance(instance);
    }

    public void Release(AsyncOperationHandle handle)
    {
        Addressables.Release(handle);
    }

    public void Release<T>(AsyncOperationHandle<T> handle)
    {
        Addressables.Release(handle);
    }
}
