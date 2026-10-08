using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// UI 面板服务实现（全局表现层服务）。
///
/// 与 <see cref="AssetService"/> / <see cref="AudioService"/> 遵循同一套规范：
/// - 挂在 [GameBootstrap] 物体上，由组合根 AddComponent → 注册 → InitializeAsync；
/// - 业务代码统一通过 <see cref="Service"/> 访问（未注册返回 null，不做任何回退自建）；
/// - 不暴露具体类型单例（无 Instance），OnDestroy 自我注销并释放资源句柄。
///
/// 面板策略：
/// - **按需加载**：面板在首次 ShowPanelAsync 时按 `UI/&lt;类名&gt;` 地址加载并缓存，之后常驻。
///   不做全量预热——预加载无法感知场景，必然把当前场景用不到的面板也拉进内存，
///   且句柄持有到服务销毁、永不归还。新增面板依然零注册（地址即类名）。
/// - **时序确定**：实例化 → configure → Init → 淡入，避免「ShowPanel 返回时 Init 还没跑」的旧问题；
/// - **显示栈**：Show 置顶，ESC 从栈顶向下分发给第一个可消费的面板。
/// </summary>
public class UIService : MonoBehaviour, IUIService
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IUIService Service =>
        ServiceLocator.TryGet<IUIService>(out var svc) ? svc : null;

    private readonly Dictionary<string, BasePanel> _panels = new Dictionary<string, BasePanel>();
    private readonly Dictionary<string, BasePanel> _closing = new Dictionary<string, BasePanel>();
    private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _prefabHandles = new Dictionary<string, AsyncOperationHandle<GameObject>>();
    private readonly Dictionary<string, Task<GameObject>> _loading = new Dictionary<string, Task<GameObject>>();
    private readonly List<BasePanel> _openStack = new List<BasePanel>();

    private Transform _canvasTrans;
    private AsyncOperationHandle<GameObject> _canvasHandle;
    private IAssetService _assetService;
    private IInputHandle _inputHandle;
    private Task _initTask;

    public event Action OnEscapeUnhandled;

    /// <summary>
    /// 服务是否**真的可用**（画布已就位）。
    ///
    /// <para>
    /// <see cref="InitializeAsync"/> 会把加载异常消化成日志后正常返回，
    /// 所以组合根不能只看"有没有抛异常" —— 那样"没有画布"会被当成"UI 就绪"，
    /// 之后每个 <see cref="ShowPanelAsync{T}"/> 都静默返回 null，界面一片空白且没有红错。
    /// </para>
    /// </summary>
    public bool IsOperational => _canvasTrans != null;


    public Task InitializeAsync()
    {
        // 缓存初始化任务：幂等 + 并发安全，失败也不会永久锁死（任务内部已消化异常）
        return _initTask ??= InitializeInternalAsync();
    }


    private async Task InitializeInternalAsync()
    {
        _assetService = AssetService.Service;
        if (_assetService == null)
        {
            Debug.LogError("[UIService] IAssetService 未注册，UI 服务无法初始化。");
            return;
        }

        GameObject canvasPrefab;
        try
        {
            _canvasHandle = _assetService.LoadAssetAsync<GameObject>(AssetKeys.Canvas);
            canvasPrefab = await _canvasHandle.Task;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIService] Canvas 加载失败：{e.Message}");
            return;
        }

        if (canvasPrefab == null)
        {
            Debug.LogError($"[UIService] Canvas 资源缺失：{AssetKeys.Canvas}");
            return;
        }

        // 句柄保留到服务销毁：过早 Release 会让依赖资源在真机 Player Build 下被卸载
        GameObject canvasObj = Instantiate(canvasPrefab);
        canvasObj.name = "[UICanvas]";
        DontDestroyOnLoad(canvasObj);
        _canvasTrans = canvasObj.transform;

        // ESC 统一分发（Android 无物理返回键时句柄可能为空，跳过即可）
        _inputHandle = InputHandleFactory.GetInput(InputHandleFactory.LocalId);
        if (_inputHandle != null)
            _inputHandle.OnEscape += HandleEscape;
    }


    public async Task<T> ShowPanelAsync<T>(Action<T> configure = null) where T : BasePanel
    {
        await InitializeAsync();

        if (_canvasTrans == null)
        {
            Debug.LogError($"[UIService] 画布未就绪，无法显示 {typeof(T).Name}。");
            return null;
        }

        string panelName = typeof(T).Name;

        // 已显示：复用实例，重跑配置并置顶
        if (_panels.TryGetValue(panelName, out BasePanel exist) && exist != null)
        {
            configure?.Invoke(exist as T);
            BringToFront(exist);
            exist.AcquirePauseIfNeeded();
            return exist as T;
        }
        _panels.Remove(panelName);

        // 同名面板正在淡出销毁：立即结束它，避免新面板被旧的销毁回调误删
        ForceClose(panelName);

        GameObject prefab = await LoadPrefabAsync(panelName);
        if (prefab == null) return null;

        // await 期间可能有另一个调用方已经把同名面板显示出来了：必须复用而不是再建一个，
        // 否则先建的那个会失去字典引用，变成 Canvas 下永不销毁、永不淡出的孤儿面板
        if (_panels.TryGetValue(panelName, out BasePanel raced) && raced != null)
        {
            configure?.Invoke(raced as T);
            BringToFront(raced);
            raced.AcquirePauseIfNeeded();
            return raced as T;
        }
        _panels.Remove(panelName);
        ForceClose(panelName);

        GameObject panelObj = Instantiate(prefab);
        panelObj.name = panelName;
        panelObj.transform.SetParent(_canvasTrans, false);

        T panel = panelObj.GetComponent<T>();
        if (panel == null)
        {
            Debug.LogError($"[UIService] 面板预制体缺少组件 {panelName}：{AssetKeys.Panel(panelName)}");
            Destroy(panelObj);
            return null;
        }

        _panels[panelName] = panel;

        // 先注入数据，再 Init：Init 内部可以安全读取 configure 写入的字段
        configure?.Invoke(panel);
        panel.Initialize();
        panel.ShowMe();
        BringToFront(panel);

        // 暂停走面板自己的令牌：本服务是面板生命周期的唯一所有者，
        // 由它统一申请/释放才能保证"模态面板销毁 → 暂停一定被释放"
        // （不依赖每个子类记得重写 OnDestroy，而 LobbyHudPanel 恰好就漏过 base.OnDestroy）
        panel.AcquirePauseIfNeeded();

        return panel;
    }


    public void HidePanel<T>(bool isFade = true) where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (!_panels.TryGetValue(panelName, out BasePanel panel) || panel == null)
        {
            _panels.Remove(panelName);
            return;
        }

        // 立即出栈 + 出字典，避免淡出期间被再次 Show 或被 ESC 命中
        _panels.Remove(panelName);
        _openStack.Remove(panel);

        if (!isFade)
        {
            DestroyPanel(panelName, panel);
            return;
        }

        _closing[panelName] = panel;
        panel.HideMe(() => DestroyPanel(panelName, panel));
    }


    public T GetPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (_panels.TryGetValue(panelName, out BasePanel panel) && panel != null)
            return panel as T;

        return null;
    }


    public bool IsPanelOpen<T>() where T : BasePanel => GetPanel<T>() != null;


    public void HandleEscape()
    {
        for (int i = _openStack.Count - 1; i >= 0; i--)
        {
            BasePanel panel = _openStack[i];
            if (panel == null)
            {
                _openStack.RemoveAt(i);
                continue;
            }

            if (panel.CanHandleEscape)
            {
                panel.EscLogic();
                return;
            }
        }

        OnEscapeUnhandled?.Invoke();
    }


    private void OnDestroy()
    {
        if (_inputHandle != null)
        {
            _inputHandle.OnEscape -= HandleEscape;
            _inputHandle = null;
            InputHandleFactory.ReleaseInput(InputHandleFactory.LocalId);
        }

        // 释放全部 Addressables 句柄（Canvas + 面板 prefab）
        if (_canvasHandle.IsValid())
            _canvasHandle.Release();

        foreach (AsyncOperationHandle<GameObject> handle in _prefabHandles.Values)
        {
            if (handle.IsValid())
                handle.Release();
        }

        // 归还全部暂停令牌：服务销毁时面板也跟着走，令牌不能留在关卡管理器里
        foreach (BasePanel panel in _panels.Values)
        {
            if (panel != null) panel.ReleasePauseIfHeld();
        }

        _prefabHandles.Clear();
        _prefabCache.Clear();
        _panels.Clear();
        _closing.Clear();
        _openStack.Clear();

        if (ServiceLocator.TryGet<IUIService>(out var svc) && ReferenceEquals(svc, this))
            ServiceLocator.Unregister<IUIService>();
    }


    // ---- 内部实现 ----

    /// <summary>把面板移到显示栈顶并置于最上层渲染。</summary>
    private void BringToFront(BasePanel panel)
    {
        _openStack.Remove(panel);
        _openStack.Add(panel);
        panel.transform.SetAsLastSibling();
    }


    /// <summary>销毁面板，并只在回调属于当前实例时才清理 _closing 记录。</summary>
    private void DestroyPanel(string panelName, BasePanel panel)
    {
        if (_closing.TryGetValue(panelName, out BasePanel closing) && ReferenceEquals(closing, panel))
            _closing.Remove(panelName);

        if (panel != null)
        {
            // 归还暂停令牌：面板销毁是**唯一**能保证走到的收尾点（子类可能漏掉 base.OnDestroy）
            panel.ReleasePauseIfHeld();
            Destroy(panel.gameObject);
        }
    }


    /// <summary>立即结束正在淡出的同名面板（先杀动画，确保其淡出回调不再触发）。</summary>
    private void ForceClose(string panelName)
    {
        if (!_closing.TryGetValue(panelName, out BasePanel dying)) return;

        _closing.Remove(panelName);
        if (dying == null) return;

        dying.ReleasePauseIfHeld();
        dying.KillSequence();
        Destroy(dying.gameObject);
    }


    /// <summary>按需加载面板 prefab，同一面板的并发请求共享一次加载。</summary>
    private Task<GameObject> LoadPrefabAsync(string panelName)
    {
        if (_prefabCache.TryGetValue(panelName, out GameObject cached) && cached != null)
            return Task.FromResult(cached);

        if (_loading.TryGetValue(panelName, out Task<GameObject> inFlight))
            return inFlight;

        Task<GameObject> task = LoadPrefabInternalAsync(panelName);

        // 同步完成时内部 finally 已经清理过，不必登记
        if (!task.IsCompleted)
            _loading[panelName] = task;

        return task;
    }


    private async Task<GameObject> LoadPrefabInternalAsync(string panelName)
    {
        string address = AssetKeys.Panel(panelName);

        // 句柄声明在 try **外面**，并用"是否已转交所有权"决定 finally 里要不要释放：
        // 资源返回 null 或 await 抛异常时句柄没进 _prefabHandles，没人会释放它 ——
        // 每重试一次引用计数就 +1，bundle 永不卸载（只在 Player Build 暴露）
        AsyncOperationHandle<GameObject> handle = default;
        bool ownershipTransferred = false;

        try
        {
            handle = _assetService.LoadAssetAsync<GameObject>(address);
            GameObject prefab = await handle.Task;

            if (prefab == null)
            {
                Debug.LogError($"[UIService] 面板资源缺失：{address}");
                return null;
            }

            _prefabHandles[panelName] = handle;
            _prefabCache[panelName] = prefab;
            ownershipTransferred = true;
            return prefab;
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIService] 面板加载失败 {address}: {e.Message}");
            return null;
        }
        finally
        {
            // 失败分支必须在这里归还：句柄没进 _prefabHandles 就没人会释放它
            if (!ownershipTransferred && handle.IsValid())
                handle.Release();

            // 方法体（含 finally）先于 Task 转为完成态执行，这里无条件清理即可；
            // 同步完成的情况下调用方还没有登记，Remove 为空操作。
            _loading.Remove(panelName);
        }
    }
}
