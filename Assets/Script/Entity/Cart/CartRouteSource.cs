using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景里的**路径来源**：子物体是折点（<see cref="CartWaypoint"/>），自己持有烘焙产物，
/// 并在运行时把它注入给推车。
///
/// <para>
/// <b>为什么路径不直接挂在推车下面：</b>路径是**关卡数据**，不是车的一部分 ——
/// 挂在车下会让"车一动，整条路径跟着动"，而且换关卡要复制推车 prefab。
/// 更根本的是：prefab 上不该出现"每关不同"的配置（见 CLAUDE.md 的
/// 「按局内选择变化的配置由装配方注入」），所以推车上的 <c>Route</c> 字段**留空**，
/// 由本组件在 <see cref="Start"/> 注入 —— 与武器配置由 <c>WeaponAssembler</c> 注入是同一条规矩。
/// </para>
///
/// <para>
/// <b>编辑流程：</b>选中本物体 → 拖动子物体改路径（Unity 原生移动工具，无需自写 handle）→
/// 点 <c>烘焙到资产</c>（或在 Inspector 里看一致性状态）。加/删/排序点在
/// <c>Assets/Editor/CartRouteSourceEditor.cs</c> 的按钮里。
/// </para>
///
/// <para>
/// <b>顺序 = 子物体顺序</b>，且点必须是**直接**子物体（嵌套会让顺序变成深度优先遍历顺序，
/// 那对作者是不可见的）。这一条由 <see cref="TryDescribeMismatch"/> 与烘焙器检查。
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class CartRouteSource : MonoBehaviour
{
    [Tooltip("烘焙产物（路径资产）。运行时由本组件注入给推车；为空则推车不动")]
    public CartRouteSO Route;

    [Tooltip("本关的推车。字段是 public：场景配置组件的引用本就该在 Inspector 里配，" +
             "而且脚本化接线时直接赋值比走 SerializedObject 可靠")]
    public CartController Cart;

    [Tooltip("判定「场景与资产是否一致」时允许的坐标误差（世界单位）")]
    [SerializeField] private float _positionTolerance = 0.01f;

    /// <summary>子物体里的折点（按层级顺序）。**不缓存**：编辑期随时会增删，缓存就会变陈旧。</summary>
    public List<CartWaypoint> CollectWaypoints()
    {
        var points = new List<CartWaypoint>();
        GetComponentsInChildren(true, points);
        return points;
    }

    /// <summary>
    /// 由折点列表推出节点表（只有 <see cref="CartNodeKind.None"/> 以外的点才算节点）。
    /// 烘焙器与一致性校验**共用这一份**，否则"写进去的"和"比对的"可能不是同一个规则。
    /// </summary>
    public static List<CartRouteNode> BuildNodes(List<CartWaypoint> points)
    {
        var nodes = new List<CartRouteNode>();

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null || points[i].Node == CartNodeKind.None) continue;

            nodes.Add(new CartRouteNode { PointIndex = i, Node = points[i].Node });
        }

        return nodes;
    }

    /// <summary>
    /// 场景路径与烘焙资产是否一致。不一致时 <paramref name="mismatch"/> 给出**第一条**差异
    /// （足够定位问题，不必一次列全）。
    ///
    /// <para>
    /// 它存在的原因很具体：资产是产物、场景是作者源，**"改了场景忘了烘焙"是静默的** ——
    /// 运行时车会照着旧路径开，而画面上（gizmos）显示的是新路径。
    /// 这里把它变成一句能读懂的话，由编辑器在 Inspector 与进入 Play 时各查一次。
    /// </para>
    /// </summary>
    public bool TryDescribeMismatch(out string mismatch)
    {
        mismatch = null;

        List<CartWaypoint> points = CollectWaypoints();

        // 嵌套的折点会让顺序变成"深度优先"，作者在层级里看不出差别 —— 先报这个
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].transform.parent != transform)
            {
                mismatch = $"折点「{points[i].name}」不是直接子物体（顺序会按深度优先算，与层级所见不同）";
                return true;
            }
        }

        if (Route == null)
        {
            mismatch = "还没有烘焙资产（Route 为空）";
            return true;
        }

        if (points.Count != Route.Points.Length)
        {
            mismatch = $"点数不一致：场景 {points.Count} / 资产 {Route.Points.Length}";
            return true;
        }

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 scenePoint = points[i].transform.position;
            if (Vector2.Distance(scenePoint, Route.Points[i]) <= _positionTolerance) continue;

            mismatch = $"折点 {i} 位置不一致：场景 {scenePoint} / 资产 {Route.Points[i]}";
            return true;
        }

        List<CartRouteNode> nodes = BuildNodes(points);
        if (nodes.Count != Route.Nodes.Length)
        {
            mismatch = $"节点数不一致：场景 {nodes.Count} / 资产 {Route.Nodes.Length}";
            return true;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].PointIndex == Route.Nodes[i].PointIndex && nodes[i].Node == Route.Nodes[i].Node)
                continue;

            mismatch = $"节点 {i} 不一致：场景（折点 {nodes[i].PointIndex} / {nodes[i].Node}）" +
                       $" / 资产（折点 {Route.Nodes[i].PointIndex} / {Route.Nodes[i].Node}）";
            return true;
        }

        return false;
    }

    /// <summary>
    /// 把路径注入推车。<c>Start</c> 晚于所有 <c>Awake</c>，而推车只在 <c>Update</c> 里用路径，
    /// 所以这里不存在初始化顺序问题（推车那边也是懒检查，不赌顺序）。
    /// </summary>
    private void Start()
    {
        if (Cart == null)
        {
            Debug.LogError($"[{nameof(CartRouteSource)}]「{name}」没有接线推车（Cart 为空），" +
                           "路径不会被使用 —— 推车不会移动。", this);
            return;
        }

        if (Route == null)
        {
            Debug.LogError($"[{nameof(CartRouteSource)}]「{name}」没有烘焙资产（Route 为空）。" +
                           "请在 Inspector 里点「烘焙到资产」。", this);
            return;
        }

        Cart.SetRoute(Route);
    }

    /// <summary>
    /// 在 Scene 里画出路径。编辑器专用（Gizmos 不进包），所以这里不做"零分配"的讲究 ——
    /// 反过来，**不做缓存**是为了永远显示当前层级里的真实情况。
    /// </summary>
    private void OnDrawGizmos()
    {
        List<CartWaypoint> points = CollectWaypoints();
        if (points.Count == 0) return;

        // 折线
        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i] == null || points[i - 1] == null) continue;

            Gizmos.DrawLine(points[i - 1].transform.position, points[i].transform.position);
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null) continue;

            Vector3 position = points[i].transform.position;
            bool isNode = points[i].Node != CartNodeKind.None;

            // 节点（充能点/终点）画大一圈并用另一种颜色：路径上"哪里会停车"必须一眼可见
            Gizmos.color = isNode ? new Color(1f, 0.75f, 0.2f, 1f) : new Color(0.25f, 0.85f, 1f, 1f);
            Gizmos.DrawWireSphere(position, isNode ? 0.6f : 0.3f);
        }
    }
}
