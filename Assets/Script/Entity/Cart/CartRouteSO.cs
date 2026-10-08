using System;
using UnityEngine;

/// <summary>
/// 推车路径（**烘焙产物**）—— 运行时唯一的路径来源。
///
/// <para>
/// <b>为什么是资产而不是"直接读场景里的点"：</b>场景 YAML 里一条路径埋在几千行中间，
/// 改了什么、路径长什么样，人和工具都读不出来；而这份资产是**独立的小文件**，
/// 一条路径的全部信息一眼看完，diff 也干净。代价是多一步烘焙，
/// 以及"场景是作者源、资产是产物"这条单向纪律 —— 漂移由
/// <see cref="CartRouteSource"/> 的一致性校验兜住（不会静默）。
/// </para>
///
/// <para>
/// <b>这里只存"作者给的东西"（点 + 节点）</b>：累计弧长、总长、每段的切线都是**算出来的**
/// （见 <see cref="CartPath"/>），不落盘 —— 存了就会与点不同步，
/// 而"两份数据谁对"是查不出来的。SO 上也不放任何运行时状态（跨 Play 会话存活，
/// 见 <c>AttackMethodSO</c> 的同一条红线）。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CartRoute", menuName = "Game/Cart/Route")]
public class CartRouteSO : ScriptableObject
{
    [Tooltip("路径折点（世界坐标，按行驶顺序）。至少 2 个点、相邻点不能重合")]
    public Vector2[] Points = Array.Empty<Vector2>();

    [Tooltip("路径上的节点（充能点 / 终点）。PointIndex 指向 Points 的下标")]
    public CartRouteNode[] Nodes = Array.Empty<CartRouteNode>();

    [Tooltip("烘焙来源场景路径。仅供体检与排查用，运行时**不读**")]
    public string SourceScene;
}

/// <summary>路径上的一个节点：哪个折点 + 它是什么（充能点 / 终点）。</summary>
[Serializable]
public struct CartRouteNode
{
    [Tooltip("Points 的下标（0 = 起点）")]
    public int PointIndex;

    [Tooltip("节点类型")]
    public CartNodeKind Node;
}
