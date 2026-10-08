using System;
using System.Threading.Tasks;

/// <summary>
/// UI 面板服务。
/// UI 是纯本地表现层，联机模式下每个客户端独立，不跨端同步。
/// 统一管理面板的加载、生命周期、显示层级与 ESC 分发。
///
/// 实现约定（与 IAssetService / IAudioService 一致）：
/// - 由组合根 <see cref="GameBootstrap"/> 创建并注册，业务代码通过 <c>UIService.Service</c> 访问；
/// - 面板资源按 <c>UI/&lt;面板类名&gt;</c> 地址**首次显示时**加载并缓存，无需注册、也不做全量预热。
/// </summary>
public interface IUIService
{
    /// <summary>
    /// 初始化服务（由组合根调用，幂等且并发安全）：
    /// 创建常驻 Canvas、接入全局输入，不预加载任何面板。
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 显示面板：按需加载 prefab，依次执行 <paramref name="configure"/> → <see cref="BasePanel.Init"/> → 淡入。
    /// 面板已显示时复用实例、重跑 <paramref name="configure"/> 并置于最上层。
    /// 加载失败返回 null（不会抛异常）。
    /// </summary>
    /// <param name="configure">显示前注入数据（先于 Init 执行，因此 Init 内可安全读取这些数据）</param>
    Task<T> ShowPanelAsync<T>(Action<T> configure = null) where T : BasePanel;

    /// <summary>隐藏并销毁面板（<paramref name="isFade"/> 为 false 时立即销毁）</summary>
    void HidePanel<T>(bool isFade = true) where T : BasePanel;

    /// <summary>获取当前已显示的面板，未显示返回 null</summary>
    T GetPanel<T>() where T : BasePanel;

    /// <summary>面板当前是否已显示</summary>
    bool IsPanelOpen<T>() where T : BasePanel;

    /// <summary>当 ESC 未被任何面板消费时触发（用于打开暂停菜单等全局行为）</summary>
    event Action OnEscapeUnhandled;

    /// <summary>
    /// 处理一次 ESC：从显示栈顶向下找第一个「可处理 ESC」的面板并调用其 EscLogic；
    /// 若无面板处理，则触发 <see cref="OnEscapeUnhandled"/>。
    /// </summary>
    void HandleEscape();
}
