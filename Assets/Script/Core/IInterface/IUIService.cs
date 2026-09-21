using System;
using System.Threading.Tasks;

/// <summary>
/// UI 面板服务。
/// UI 是纯本地表现层，联机模式下每个客户端独立，不跨端同步。
/// 统一管理面板生命周期、显示层级与 ESC 分发。
/// </summary>
public interface IUIService
{
    /// <summary>初始化并预加载面板（由组合根调用，幂等）</summary>
    Task InitializeAsync();

    /// <summary>显示面板（已显示则直接返回），并置于最上层</summary>
    T ShowPanel<T>() where T : BasePanel;

    /// <summary>隐藏并销毁面板</summary>
    void HidePanel<T>(bool isFade = true) where T : BasePanel;

    /// <summary>获取当前已显示的面板，未显示返回 null</summary>
    T GetPanel<T>() where T : BasePanel;

    /// <summary>当 ESC 未被任何面板消费时触发（用于打开暂停菜单等全局行为）</summary>
    event Action OnEscapeUnhandled;

    /// <summary>
    /// 处理一次 ESC：从显示栈顶向下找第一个「可处理 ESC」的面板并调用其 EscLogic；
    /// 若无面板处理，则触发 <see cref="OnEscapeUnhandled"/>。
    /// </summary>
    void HandleEscape();
}
