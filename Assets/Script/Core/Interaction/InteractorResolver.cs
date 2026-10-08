using UnityEngine;

/// <summary>
/// 「碰撞体 → 交互者」的**唯一**解析规则。
///
/// <para>
/// 旧代码里有两套写法：塔用 <c>CompareTag("Player") + TryGetComponent&lt;PlayerInteraction&gt;</c>
/// （要求碰撞体与组件在**同一个** GameObject 上），大厅用 <c>GetComponentInParent&lt;PlayerController&gt;</c>。
/// 玩家碰撞体一旦挪到子物体，塔的交互会**静默失效**（tag 在根节点、碰撞体在子物体，TryGetComponent 找不到）。
/// </para>
///
/// <para>
/// <see cref="Component.GetComponentInParent{T}()"/> 包含自身，所以这一条规则同时覆盖
/// "组件在根节点 + 碰撞体在根节点"与"组件在根节点 + 碰撞体在子物体"两种情况。
/// </para>
/// </summary>
public static class InteractorResolver
{
    /// <summary>从触发碰撞体解析交互者（找不到返回 false，不报错 —— 场景里绝大多数碰撞体都不是玩家）。</summary>
    public static bool TryResolve(Collider2D other, out IInteractor interactor)
    {
        interactor = null;
        if (other == null) return false;

        interactor = other.GetComponentInParent<IInteractor>();
        return interactor != null;
    }

    /// <summary>
    /// 交互者是否还活着。
    ///
    /// <para>
    /// <b>不能用 <c>?.</c> 或 <c>== null</c> 代替：</b>接口引用上的 <c>== null</c> 是纯 C# 判空，
    /// 而 Unity 销毁对象后组件是"伪 null"（C# 层非 null，访问成员才抛 MissingReferenceException）。
    /// 所有跨帧持有 <see cref="IInteractor"/> / <see cref="IInteractable"/> 的地方都要过这一道。
    /// </para>
    /// </summary>
    public static bool IsAlive(IInteractor interactor)
    {
        if (interactor == null) return false;
        return !(interactor is Component component) || component != null;
    }

    /// <inheritdoc cref="IsAlive(IInteractor)"/>
    public static bool IsAlive(IInteractable target)
    {
        if (target == null) return false;
        return !(target is Component component) || component != null;
    }
}
