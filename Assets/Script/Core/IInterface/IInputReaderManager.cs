/// <summary>
/// PC 输入底层适配器的持有者。
///
/// 单独抽接口是为了让 <see cref="InputHandleFactory"/> 能通过 ServiceLocator 拿到它——
/// 否则工厂只能访问具体类型，就会被迫保留一条绕过服务定位器的后门
/// （<c>ManagerSingleton&lt;T&gt;.Instance</c> 已因此移除）。
/// </summary>
public interface IInputReaderManager
{
    /// <summary>桥接新版 Input System 的 ScriptableObject；未配置时由实现自建，不会为 null。</summary>
    InputReader InputReader { get; }
}
