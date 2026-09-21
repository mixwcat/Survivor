using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetService : MonoBehaviour, IAssetService
{
    private bool _isInitialized;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        if (Addressables.ResourceLocators.Count() > 0)
        {
            _isInitialized = true;
            return;
        }

        await Addressables.InitializeAsync().Task;
        _isInitialized = true;
    }

    public AsyncOperationHandle<T> LoadAssetAsync<T>(object key) where T : Object
    {
        return Addressables.LoadAssetAsync<T>(key);
    }

    public async Task<T> LoadPrefabAsync<T>(object key, Transform parent = null) where T : Component
    {
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(key);
        await handle.Task;

        GameObject instance = Object.Instantiate(handle.Result, parent);
        return instance.GetComponent<T>();
    }

    public async Task<GameObject> InstantiateAsync(object key, Transform parent = null)
    {
        return await Addressables.InstantiateAsync(key, parent).Task;
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
