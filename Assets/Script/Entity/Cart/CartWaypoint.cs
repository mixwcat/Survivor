using UnityEngine;

/// <summary>
/// 路径节点的类型。**枚举而不是散落的 int 常量**：Inspector 里是下拉框，
/// 不会出现"填了 4"这种无从发现的错误。
///
/// <para>
/// 数值即协议：<see cref="CartController.NodeCharge1"/> 等常量由它派生，
/// <c>StageDirector</c> 按这些整数分支 —— **不要重排/复用编号**（与 <c>StatType</c> 同一条纪律）。
/// </para>
/// </summary>
public enum CartNodeKind
{
    /// <summary>普通折点，不报告任何事件。</summary>
    None = 0,

    /// <summary>充能点一（第一个 Boss）。</summary>
    Charge1 = 1,

    /// <summary>充能点二（第二个 Boss）。</summary>
    Charge2 = 2,

    /// <summary>终点哨站（抵达即胜利）。</summary>
    Destination = 3,
}

/// <summary>
/// 路径上的一个折点 —— **场景作者数据**，挂在 <see cref="CartRouteSource"/> 的子物体上。
///
/// <para>
/// <b>为什么是一个个 GameObject 而不是数组：</b>这样"在 Scene 里改路径"就是 Unity 原生的
/// 移动/吸附/多选操作，不需要自写拖拽 handle；顺序 = 子物体顺序，所见即所得。
/// 代价是层级里会多出 N 个空物体，以及"点必须是**直接**子物体"这条约束
/// （嵌套会让顺序变成深度优先遍历顺序）。
/// </para>
///
/// <para>
/// 本组件**只在编辑期有意义**：烘焙时它的世界坐标被写进 <see cref="CartRouteSO"/>，
/// 运行时没有任何代码读它（<see cref="CartRouteSource"/> 读它只是为了校验"场景与资产是否一致"）。
/// </para>
/// </summary>
public class CartWaypoint : MonoBehaviour
{
    [Tooltip("0 = 普通折点；充能点/终点会让推车到点停车并通知 StageDirector")]
    public CartNodeKind Node = CartNodeKind.None;
}
