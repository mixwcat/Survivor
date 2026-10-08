using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路径的**运行时几何** —— 由 <see cref="CartRouteSO"/> 构造，把"点 + 节点"变成
/// "弧长 → 位置 / 切线"的查询。
///
/// <para>
/// <b>为什么单独一个类而不是把数学写进 <see cref="CartController"/>：</b>
/// 累计弧长是**算出来的**（不落盘，见 <see cref="CartRouteSO"/> 的说明），
/// 需要一个地方把它算一次并缓存住 —— 而 SO 上不能放运行时状态（跨 Play 会话存活）。
/// 这个类就是那个缓存：SO = 数据，CartPath = 由数据导出的只读几何，控制器 = 使用者。
/// </para>
///
/// <para>
/// <b>两种问题分开报：</b>
/// <list type="bullet">
/// <item><see cref="ValidationIssue"/> —— **结构性**问题（点数不足、相邻点重合、节点下标越界），
/// 路径不可用，<see cref="CartController"/> 会报红错并拒绝移动；</item>
/// <item><see cref="WarningIssue"/> —— **设计**问题（没有终点、节点类型重复），
/// 路径能用，但关卡多半跑不完整，只告警。</item>
/// </list>
/// 两者都不在逐帧路径上（构造时算一次）。
/// </para>
/// </summary>
public sealed class CartPath
{
    /// <summary>相邻点距离小于它就算重合（重合段没有切线，会让车"卡"在那里）。</summary>
    private const float MinSegmentLength = 0.001f;

    private readonly Vector2[] _points;
    private readonly float[] _cumulative;
    private readonly CartNodeKind[] _nodeKinds;
    private readonly float[] _nodeDistances;

    /// <param name="route">路径资产。为 null 时构造出的是一个"无效但安全"的空路径。</param>
    public CartPath(CartRouteSO route)
    {
        if (route == null)
        {
            _points = Array.Empty<Vector2>();
            _cumulative = Array.Empty<float>();
            _nodeKinds = Array.Empty<CartNodeKind>();
            _nodeDistances = Array.Empty<float>();
            ValidationIssue = "路径资产为空";
            return;
        }

        // 复制一份：SO 是常驻资产，运行时不该被外部改到（复制只在构造时发生一次）
        Vector2[] source = route.Points ?? Array.Empty<Vector2>();
        _points = (Vector2[])source.Clone();

        _cumulative = new float[_points.Length];
        for (int i = 1; i < _points.Length; i++)
        {
            float segment = Vector2.Distance(_points[i - 1], _points[i]);
            if (segment < MinSegmentLength)
                ValidationIssue ??= $"第 {i - 1} 段与第 {i} 段之间的两个点重合（下标 {i - 1} 与 {i}）";

            _cumulative[i] = _cumulative[i - 1] + segment;
        }

        if (_points.Length < 2)
            ValidationIssue ??= $"至少需要 2 个折点（当前 {_points.Length} 个）";
        else if (Length < MinSegmentLength)
            ValidationIssue ??= "路径总长为 0";

        BuildNodes(route, out CartNodeKind[] kinds, out float[] distances, out string warning);
        _nodeKinds = kinds;
        _nodeDistances = distances;
        WarningIssue = warning;
    }

    /// <summary>结构性问题的描述；null = 路径可用。</summary>
    public string ValidationIssue { get; private set; }

    /// <summary>设计问题的描述；null = 没有发现问题。</summary>
    public string WarningIssue { get; private set; }

    /// <summary>路径总长（沿折线的弧长）。</summary>
    public float Length => _cumulative.Length > 0 ? _cumulative[_cumulative.Length - 1] : 0f;

    /// <summary>折点数量。</summary>
    public int PointCount => _points.Length;

    /// <summary>节点数量。</summary>
    public int NodeCount => _nodeKinds.Length;

    /// <summary>路径是否可用（结构上没问题）。</summary>
    public bool IsValid => ValidationIssue == null && _points.Length >= 2;

    /// <summary>弧长 <paramref name="distance"/> 处的位置（超出范围会被夹到两端）。</summary>
    public Vector2 Evaluate(float distance)
    {
        if (_points.Length == 0) return Vector2.zero;
        if (_points.Length == 1) return _points[0];

        float s = Mathf.Clamp(distance, 0f, Length);
        int index = SegmentIndexAt(s);

        float segmentLength = _cumulative[index + 1] - _cumulative[index];
        if (segmentLength <= 0f) return _points[index];

        float t = (s - _cumulative[index]) / segmentLength;
        return Vector2.Lerp(_points[index], _points[index + 1], t);
    }

    /// <summary>弧长 <paramref name="distance"/> 处的**单位**切线（车头朝向）。</summary>
    public Vector2 Tangent(float distance)
    {
        if (_points.Length < 2) return Vector2.right;

        int index = SegmentIndexAt(Mathf.Clamp(distance, 0f, Length));
        Vector2 direction = _points[index + 1] - _points[index];

        // 退化段（重合点）没有方向：返回右而不是零向量，避免调用方写一个 NaN 角度
        return direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
    }

    /// <summary>第 <paramref name="index"/> 个节点（**按弧长升序**）的类型。</summary>
    public CartNodeKind NodeKindAt(int index) => _nodeKinds[index];

    /// <summary>第 <paramref name="index"/> 个节点（按弧长升序）所在的弧长。</summary>
    public float NodeDistanceAt(int index) => _nodeDistances[index];

    /// <summary>
    /// 落在弧长 <paramref name="s"/> 所在段的起始折点下标。
    ///
    /// <para>
    /// 线性扫描：点数是个位数到几十，且每帧只调两次 —— 不值得为它引入二分或缓存。
    /// **刻意不缓存"上一次的段"**：<c>SetRoute</c> 会把进度重置，
    /// 缓存就成了第二个状态源（这正是"同一件事两处描述"的老毛病）。
    /// </para>
    /// </summary>
    private int SegmentIndexAt(float s)
    {
        if (_points.Length < 2) return 0;

        for (int i = 0; i < _points.Length - 2; i++)
        {
            if (s < _cumulative[i + 1]) return i;
        }

        return _points.Length - 2;
    }

    /// <summary>
    /// 把资产上的节点表转成"按弧长升序"的平行数组，并做体检。
    /// 非法项**跳过**（而不是让整个路径失效）：跳过的东西会在 <paramref name="warning"/> 里点名。
    ///
    /// <para>
    /// 结果用 out 返回而不是直接写字段：字段是 <c>readonly</c>，只有构造函数能赋值。
    /// </para>
    /// </summary>
    private void BuildNodes(CartRouteSO route, out CartNodeKind[] kinds, out float[] distances,
                            out string warning)
    {
        CartRouteNode[] nodes = route.Nodes ?? Array.Empty<CartRouteNode>();

        var kindList = new List<CartNodeKind>(nodes.Length);
        var distanceList = new List<float>(nodes.Length);
        var seen = new List<CartNodeKind>(nodes.Length);
        warning = null;

        for (int i = 0; i < nodes.Length; i++)
        {
            CartRouteNode node = nodes[i];

            if (node.Node == CartNodeKind.None)
            {
                warning ??= $"节点 {i} 的类型是 None（不该出现在节点表里，已忽略）";
                continue;
            }

            if (node.PointIndex < 0 || node.PointIndex >= _points.Length)
            {
                warning ??= $"节点 {i}（{node.Node}）的 PointIndex = {node.PointIndex} 越界" +
                            $"（共 {_points.Length} 个折点，已忽略）";
                continue;
            }

            // 插入排序（稳定）：按弧长升序，让控制器可以只维护一个"下一个节点"下标
            float distance = _cumulative[node.PointIndex];
            int insert = distanceList.Count;
            while (insert > 0 && distanceList[insert - 1] > distance) insert--;

            kindList.Insert(insert, node.Node);
            distanceList.Insert(insert, distance);

            if (seen.Contains(node.Node))
                warning ??= $"节点类型 {node.Node} 出现了多次（会重复触发同一阶段）";

            seen.Add(node.Node);
        }

        if (kindList.Count == 0)
            warning ??= "路径上没有任何节点：推车会一直开到路径尽头，关卡不会推进";
        else if (!seen.Contains(CartNodeKind.Destination))
            warning ??= "路径上没有终点（Destination）节点：抵达终点这条胜利路径不会发生";

        kinds = kindList.ToArray();
        distances = distanceList.ToArray();
    }
}
