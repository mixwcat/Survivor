using UnityEngine;

/// <summary>
/// MonoBehaviour 单例基类。
/// Awake 时建立实例并向 ServiceLocator 注册；<see cref="Instance"/> 仅返回当前缓存实例，
/// 不做场景查找、不打印错误。Manager 通过 [DefaultExecutionOrder]（负值）保证先于业务脚本初始化。
/// </summary>
public abstract class ManagerSingleton<T> : MonoBehaviour where T : ManagerSingleton<T>
{
    private static T _instance;

    /// <summary>当前缓存的单例；Awake 之前或销毁之后为 null。</summary>
    public static T Instance => _instance;

    /// <summary>是否已存在有效实例。</summary>
    public static bool HasInstance => _instance != null;

    /// <summary>
    /// 是否跨场景保留。子类可重写为 true（如 InputReaderManager）。
    /// </summary>
    protected virtual bool PersistAcrossScenes => false;

    /// <summary>
    /// Unity 生命周期入口。子类如需扩展，请重写并调用 base.Awake()，
    /// 或优先使用 OnSingletonAwake() 钩子以避免遗漏单例初始化。
    /// </summary>
    protected virtual void Awake()
    {
        InitializeSingleton();
        OnSingletonAwake();
    }

    /// <summary>
    /// 单例初始化逻辑。处理实例赋值、重复销毁、跨场景保留。
    /// </summary>
    protected virtual void InitializeSingleton()
    {
        if (_instance == null)
        {
            _instance = (T)this;
            if (PersistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 场景中存在重复实例，销毁多余的 GameObject: {gameObject.name}");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 子类可重写的初始化钩子，替代 Awake。
    /// 执行顺序保证在 InitializeSingleton 之后。
    /// </summary>
    protected virtual void OnSingletonAwake() { }

    /// <summary>
    /// 销毁时清理静态实例引用。
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
