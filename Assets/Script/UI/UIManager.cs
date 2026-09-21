using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// UI 面板服务实现。
/// - 组合根调用一次 InitializeAsync 预加载全部面板（反射收集 BasePanel 子类）。
/// - 维护面板显示栈：Show 时置顶，ESC 从栈顶向下分发给第一个可处理的面板。
/// - 面板不再各自订阅 ESC，避免多面板同时响应。
/// </summary>
public class UIManager : IUIService
{
    private static UIManager instance = new UIManager();
    public static UIManager Instance => instance;

    /// <summary>接口访问入口（组合根注册）</summary>
    public static IUIService Service => instance;

    private readonly Dictionary<string, BasePanel> panelDict = new Dictionary<string, BasePanel>();
    private readonly Dictionary<string, GameObject> panelPrefabCache = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> panelPrefabHandles = new Dictionary<string, AsyncOperationHandle<GameObject>>();
    private readonly List<BasePanel> _openStack = new List<BasePanel>();

    private Transform canvasTrans;
    private IAssetService _assetService;
    private IInputHandle _inputHandle;
    private bool _isInitialized;

    public event Action OnEscapeUnhandled;

    private UIManager()
    {
    }


    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _assetService = ServiceLocator.Get<IAssetService>();

        AsyncOperationHandle<GameObject> canvasHandle = _assetService.LoadAssetAsync<GameObject>(AssetKeys.Canvas);
        await canvasHandle.Task;
        GameObject canvasObj = GameObject.Instantiate(canvasHandle.Result);
        canvasTrans = canvasObj.transform;
        GameObject.DontDestroyOnLoad(canvasObj);
        _assetService.Release(canvasHandle);

        // ESC 统一分发（Android 在菜单场景可能无输入句柄，返回 null 时跳过）
        _inputHandle = InputHandleFactory.GetInput("local");
        if (_inputHandle != null)
            _inputHandle.OnEscape += HandleEscape;

        // 反射收集所有具体 BasePanel 子类并预加载，新增面板零改动
        foreach (Type type in Assembly.GetAssembly(typeof(BasePanel)).GetTypes())
        {
            if (type.IsAbstract || !typeof(BasePanel).IsAssignableFrom(type)) continue;
            await PreloadPanelAsync(type.Name);
        }
    }


    private async Task PreloadPanelAsync(string panelName)
    {
        if (panelPrefabCache.ContainsKey(panelName)) return;

        AsyncOperationHandle<GameObject> handle = _assetService.LoadAssetAsync<GameObject>(AssetKeys.Panel(panelName));
        await handle.Task;

        if (handle.Result == null)
        {
            Debug.LogError($"UIManager: 面板资源缺失 {AssetKeys.Panel(panelName)}");
            return;
        }

        panelPrefabCache[panelName] = handle.Result;
        panelPrefabHandles[panelName] = handle;
    }


    /// <summary>
    /// 完全依靠代码控制面板的显示
    /// </summary>
    public T ShowPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (panelDict.TryGetValue(panelName, out BasePanel existing))
        {
            return existing as T;
        }

        if (!panelPrefabCache.TryGetValue(panelName, out GameObject prefab))
        {
            Debug.LogError($"UIManager: Panel prefab {panelName} not preloaded. Call InitializeAsync first.");
            return null;
        }

        GameObject panelObj = GameObject.Instantiate(prefab);
        panelObj.transform.SetParent(canvasTrans, false);

        T panel = panelObj.GetComponent<T>();
        panelDict[panelName] = panel;
        if (!_openStack.Contains(panel))
            _openStack.Add(panel);

        panel.ShowMe();

        return panel;
    }


    public void HidePanel<T>(bool isFade = true) where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (!panelDict.TryGetValue(panelName, out BasePanel panel))
            return;

        // 立即出栈，避免 ESC 命中正在淡出的面板
        _openStack.Remove(panel);

        if (isFade)
        {
            panel.HideMe(() =>
            {
                if (panel != null)
                    GameObject.Destroy(panel.gameObject);
                panelDict.Remove(panelName);
            });
        }
        else
        {
            GameObject.Destroy(panel.gameObject);
            panelDict.Remove(panelName);
        }
    }


    public T GetPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (panelDict.TryGetValue(panelName, out BasePanel panel))
        {
            return panel as T;
        }
        return null;
    }


    public void HandleEscape()
    {
        for (int i = _openStack.Count - 1; i >= 0; i--)
        {
            BasePanel panel = _openStack[i];
            if (panel == null) continue;

            if (panel.CanHandleEscape)
            {
                panel.EscLogic();
                return;
            }
        }

        OnEscapeUnhandled?.Invoke();
    }
}
