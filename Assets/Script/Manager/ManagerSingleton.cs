using UnityEngine;

/// <summary>
/// MonoBehaviour 单例基类。
/// Awake 时建立实例并向 ServiceLocator 注册；Manager 通过 [DefaultExecutionOrder]（负值）保证先于业务脚本初始化。
///
/// <b>刻意不暴露 <c>Instance</c></b>：业务代码一律走各 Manager 的 <c>Service</c> 静态属性
/// （纯 <c>ServiceLocator.TryGet</c>，未注册返回 null），避免出现绕过服务定位器的第二条访问路径。
/// </summary>
public abstract class ManagerSingleton<T> : MonoBehaviour where T : ManagerSingleton<T>
{
    private static T _instance;

    /// <summary>
    /// 是否跨场景保留。子类可重写为 true（如 InputReaderManager）。
    /// </summary>
    protected virtual bool PersistAcrossScenes => false;

    /// <summary>
    /// Unity 生命周期入口。子类如需扩展，请重写并调用 base.Awake()，
    /// 或优先使用 OnSingletonAwake() 钩子以避免遗漏单例初始化。
    ///
    /// <para>
    /// <b>重复实例不会执行 <see cref="OnSingletonAwake"/>：</b>那一步会向 ServiceLocator 注册，
    /// 让重复实例**覆盖**主实例的服务入口；帧末它被销毁时又把服务注销掉，
    /// 最终主实例还在、服务却空了（输入/玩家/塔/统计突然全部不可用，且不报错）。
    /// </para>
    /// </summary>
    protected virtual void Awake()
    {
        if (!InitializeSingleton()) return;

        OnSingletonAwake();
    }

    /// <summary>
    /// 单例初始化逻辑。处理实例赋值、重复销毁、跨场景保留。
    /// </summary>
    /// <returns>本实例是否是主实例。false = 重复实例（已安排销毁），调用方不应再做任何注册。</returns>
    protected virtual bool InitializeSingleton()
    {
        bool isPrimary = TryClaimInstance((T)this, out bool wasDuplicate);

        if (wasDuplicate)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 场景中存在重复实例，销毁多余的 GameObject: {gameObject.name}");

            // 编辑模式下 Destroy 会被拒绝（并打一条红错），必须用 DestroyImmediate ——
            // 否则"重复实例"在编辑器里根本不会被销毁，校验脚本与编辑器工具都会看到假象
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);

            return false;
        }

        if (PersistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        return isPrimary;
    }

    /// <summary>
    /// 单例归属判定。**纯逻辑、可被 CLI 校验器直接断言** ——
    /// 它的退化症状是"主实例还在、服务入口却空了"，发生在切场景后一帧，在 Play 里极难稳定复现。
    ///
    /// <para>
    /// 判据用 <c>_instance == null</c>（Unity 的伪 null 重载）：已销毁的实例会被当成"没有实例"，
    /// 于是下一个实例能正常接管 —— 这是跨场景重建 Manager 的必要条件。
    /// </para>
    /// </summary>
    /// <param name="candidate">候选实例。</param>
    /// <param name="wasDuplicate">true = 已有别的存活实例；调用方应销毁自己且**不要注册服务**。</param>
    public static bool TryClaimInstance(T candidate, out bool wasDuplicate)
    {
        wasDuplicate = false;

        if (_instance == null)
        {
            _instance = candidate;
            return true;
        }

        if (_instance == candidate) return true;

        wasDuplicate = true;
        return false;
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
