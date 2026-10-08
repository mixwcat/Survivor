using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 资源服务（Addressables 封装）。
///
/// <b>句柄/实例的获取与归还必须成对</b>，否则资源引用计数只增不减、bundle 永不卸载：
/// - <see cref="LoadAssetAsync{T}"/> 返回的句柄由调用方负责 <see cref="Release(AsyncOperationHandle)"/>；
/// - <see cref="InstantiateAsync"/> 创建的实例必须用 <see cref="ReleaseInstance"/> 归还，
///   **不能直接 Destroy**（Destroy 不会递减引用计数）。
/// </summary>
public interface IAssetService
{
    /// <summary>初始化 Addressables（由组合根调用，幂等）</summary>
    Task InitializeAsync();

    /// <summary>加载资源；返回的句柄由调用方持有并负责 Release</summary>
    AsyncOperationHandle<T> LoadAssetAsync<T>(object key) where T : Object;

    /// <summary>按地址实例化 prefab；实例必须用 <see cref="ReleaseInstance"/> 销毁</summary>
    Task<GameObject> InstantiateAsync(object key, Transform parent = null);

    /// <summary>
    /// 归还 <see cref="InstantiateAsync"/> 创建的实例：销毁 GameObject 并递减源 prefab 的引用计数。
    /// 返回 false 表示该对象不是 Addressables 实例（未被跟踪，函数不会销毁它），调用方需自行 Destroy。
    /// </summary>
    bool ReleaseInstance(GameObject instance);

    /// <summary>释放资源句柄</summary>
    void Release(AsyncOperationHandle handle);

    /// <summary>释放资源句柄</summary>
    void Release<T>(AsyncOperationHandle<T> handle);
}
