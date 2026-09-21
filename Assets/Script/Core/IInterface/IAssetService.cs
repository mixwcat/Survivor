using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public interface IAssetService
{
    Task InitializeAsync();
    AsyncOperationHandle<T> LoadAssetAsync<T>(object key) where T : Object;
    Task<T> LoadPrefabAsync<T>(object key, Transform parent = null) where T : Component;
    Task<GameObject> InstantiateAsync(object key, Transform parent = null);
    void Release(AsyncOperationHandle handle);
    void Release<T>(AsyncOperationHandle<T> handle);
}
