#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 路径烘焙器：把场景里的 <see cref="CartRouteSource"/>（子物体折点）写进
/// <see cref="CartRouteSO"/> —— **场景是作者源，资产是产物**，方向永远只有这一个。
///
/// <para>
/// 手动运行：<c>Tools ▸ Cart ▸ Bake Route (选中对象)</c>（选中路径对象或它的任意子物体）。
/// 批处理运行：<c>-executeMethod CartRouteBaker.BakeFromCommandLine</c> ——
/// 它会扫描所有引用了 <see cref="CartRouteSource"/> 的场景，逐个打开、烘焙、按需保存。
/// </para>
///
/// <para>
/// <b>为什么要有 CLI 版：</b>烘焙是"生成物"的一步，漏跑的表现是运行时装作旧路径 ——
/// 静默且难查。有了 CLI 版就能在改完场景后跑一次（和 <c>EntityIdCatalog</c>、
/// <c>Setup Addressables</c> 是同一种套路），失败返回非 0。
/// </para>
///
/// <para>
/// ⚠️ 场景对象的字段用**直接赋值**（<c>source.Route = route</c>），不要走 <c>SerializedObject</c>：
/// 实测在场景里的组件上，对象引用写不进去（CLAUDE.md 记过这条）。
/// </para>
/// </summary>
public static class CartRouteBaker
{
    private const string RouteDir = "Assets/Game/SO/CartRoute";
    private const string TableDir = "Assets/Game/SO/SpawnTable";
    private const string SceneDir = "Assets/Scenes";

    [MenuItem("Tools/Cart/Bake Route (选中对象)")]
    public static void BakeSelectedFromMenu()
    {
        CartRouteSource source = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<CartRouteSource>()
            : null;

        if (source == null)
        {
            Debug.LogError("[CartRouteBaker] 请先选中一个带 CartRouteSource 的对象（或它的子物体）。");
            return;
        }

        bool ok = Bake(source, out string message);

        AssetDatabase.SaveAssets();
        if (!ok)
        {
            Debug.LogError($"[CartRouteBaker] {message}");
            return;
        }

        // 场景里可能刚被写入了 Route 引用（新建资产那条路径），要让场景可保存
        EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
        Debug.Log($"[CartRouteBaker] {message}");
    }

    /// <summary>
    /// 烘焙一个路径源。返回 false 时 <paramref name="message"/> 说明原因
    /// （结构性问题一律拒绝烘焙：宁可没有资产，也不要一份**看起来**能用的错资产）。
    /// </summary>
    public static bool Bake(CartRouteSource source, out string message)
    {
        message = null;

        if (source == null)
        {
            message = "没有 CartRouteSource";
            return false;
        }

        if (!TryBuildRoute(source, out Vector2[] points, out CartRouteNode[] nodes, out string problem))
        {
            message = problem;
            return false;
        }

        CartRouteSO route = source.Route;
        bool created = false;

        if (route == null)
        {
            // 自动建资产：路径本来就是一关一份，让作者先去 CreateAssetMenu 里翻一遍是多余的摩擦
            Directory.CreateDirectory(RouteDir);

            string sceneName = source.gameObject.scene.name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = source.gameObject.name;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{RouteDir}/CartRoute_{sceneName}.asset");
            route = ScriptableObject.CreateInstance<CartRouteSO>();
            AssetDatabase.CreateAsset(route, path);

            source.Route = route;             // 场景对象：直接赋值
            EditorUtility.SetDirty(source);
            created = true;
        }

        route.Points = points;
        route.Nodes = nodes;
        route.SourceScene = source.gameObject.scene.path;
        EditorUtility.SetDirty(route);
        AssetDatabase.SaveAssets();

        // 用**运行时的那份几何**复核一遍：烘焙器与运行时看到的问题必须是同一套说法
        var probe = new CartPath(route);
        if (probe.ValidationIssue != null)
        {
            message = $"烘焙后的路径不可用：{probe.ValidationIssue}";
            return false;
        }

        message = $"{(created ? "已创建并烘焙" : "已烘焙")} {AssetDatabase.GetAssetPath(route)}：" +
                  $"{points.Length} 个折点、{nodes.Length} 个节点、总长 {probe.Length:F1}";

        if (probe.WarningIssue != null)
            message += $"（注意：{probe.WarningIssue}）";

        // 生成表的骨架跟着路径一起烘焙：段/节点与折点一一对应，作者因此不用手填弧长数字
        BakeSpawnTable(source, route, ref message);

        return true;
    }

    /// <summary>
    /// 把路径的结构烘进生成表：**段**（相邻折点之间）+ **节点**（折点）。
    ///
    /// <para>
    /// <b>只重建骨架，绝不覆盖内容</b>：标签与米数按路径重算，而每段/每节点已有的
    /// <c>Rules</c> **按下标原样保留**（插点会让后面的内容整体后移一位，报告里会写清楚）。
    /// 新增的段/节点给**空规则** —— 烘焙器不认识任何具体的敌人，
    /// "这一段刷什么"是作者的决定，不该由工具猜。
    /// </para>
    /// </summary>
    private static void BakeSpawnTable(CartRouteSource source, CartRouteSO route, ref string message)
    {
        EnemySpawner spawner = UnityEngine.Object.FindFirstObjectByType<EnemySpawner>();

        if (spawner == null)
        {
            message += "；场景里没有 EnemySpawner，跳过生成表";
            return;
        }

        EnemySpawnTableSO table = spawner.Table;
        bool created = false;

        if (table == null)
        {
            Directory.CreateDirectory(TableDir);

            string sceneName = source.gameObject.scene.name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = source.gameObject.name;

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{TableDir}/SpawnTable_{sceneName}.asset");
            table = ScriptableObject.CreateInstance<EnemySpawnTableSO>();
            AssetDatabase.CreateAsset(table, assetPath);

            spawner.Table = table;            // 场景对象：直接赋值
            EditorUtility.SetDirty(spawner);
            created = true;
        }

        SpawnSegment[] oldSegments = table.Segments ?? Array.Empty<SpawnSegment>();
        SpawnNode[] oldNodes = table.Nodes ?? Array.Empty<SpawnNode>();

        Vector2[] points = route.Points;
        var segments = new SpawnSegment[Mathf.Max(0, points.Length - 1)];
        var nodes = new SpawnNode[points.Length];

        int keptSegments = 0;
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = new SpawnSegment
            {
                Label = $"P{i} → P{i + 1}（{KindLabel(route, i)} → {KindLabel(route, i + 1)}）",
                StartDistance = Cumulative(route, i),
                EndDistance = Cumulative(route, i + 1),
                Rules = i < oldSegments.Length ? oldSegments[i].Rules : Array.Empty<SpawnRule>(),
            };

            if (i < oldSegments.Length) keptSegments++;
        }

        int keptNodes = 0;
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = new SpawnNode
            {
                Label = $"P{i} · {KindName(KindAt(route, i))}",
                Kind = KindAt(route, i),
                Distance = Cumulative(route, i),
                Rules = i < oldNodes.Length ? oldNodes[i].Rules : Array.Empty<SpawnRule>(),
            };

            if (i < oldNodes.Length) keptNodes++;
        }

        table.Segments = segments;
        table.Nodes = nodes;
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        message += $"；{(created ? "已创建并烘焙" : "已更新")}生成表 {AssetDatabase.GetAssetPath(table)}：" +
                   $"段 {segments.Length}（保留内容 {keptSegments}）、节点 {nodes.Length}（保留内容 {keptNodes}）";

        int added = (segments.Length - keptSegments) + (nodes.Length - keptNodes);
        if (added > 0)
            message += $"；新增 {added} 项**规则为空**，需要你填刷怪方式";
    }

    /// <summary>折点 <paramref name="index"/> 的累计弧长（米）。与 <see cref="CartPath"/> 的算法一致（折线长度）。</summary>
    private static float Cumulative(CartRouteSO route, int index)
    {
        float distance = 0f;

        for (int i = 1; i <= index && i < route.Points.Length; i++)
            distance += Vector2.Distance(route.Points[i - 1], route.Points[i]);

        return distance;
    }

    private static CartNodeKind KindAt(CartRouteSO route, int pointIndex)
    {
        CartRouteNode[] nodes = route.Nodes ?? Array.Empty<CartRouteNode>();

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].PointIndex == pointIndex) return nodes[i].Node;
        }

        return CartNodeKind.None;
    }

    private static string KindLabel(CartRouteSO route, int pointIndex) => KindName(KindAt(route, pointIndex));

    private static string KindName(CartNodeKind kind)
    {
        switch (kind)
        {
            case CartNodeKind.Charge1: return "充能点 1";
            case CartNodeKind.Charge2: return "充能点 2";
            case CartNodeKind.Destination: return "终点";
            default: return "折点";
        }
    }

    /// <summary>
    /// 从场景收集折点与节点并做**结构性**检查（点数、直接子物体、相邻点重合）。
    /// 设计层面的问题（没有终点等）不在这里拦 —— 关卡可能还没设计完，
    /// 那些由 <see cref="CartPath.WarningIssue"/> 在烘焙后作为提示给出。
    /// </summary>
    public static bool TryBuildRoute(CartRouteSource source, out Vector2[] points,
                                     out CartRouteNode[] nodes, out string problem)
    {
        points = Array.Empty<Vector2>();
        nodes = Array.Empty<CartRouteNode>();
        problem = null;

        List<CartWaypoint> waypoints = source.CollectWaypoints();

        if (waypoints.Count < 2)
        {
            problem = $"至少需要 2 个折点（当前 {waypoints.Count} 个）。" +
                      "折点是 CartRouteSource 的子物体，每个上面挂 CartWaypoint。";
            return false;
        }

        points = new Vector2[waypoints.Count];

        for (int i = 0; i < waypoints.Count; i++)
        {
            CartWaypoint waypoint = waypoints[i];

            if (waypoint.transform.parent != source.transform)
            {
                problem = $"折点「{waypoint.name}」不是 CartRouteSource 的**直接**子物体 —— " +
                          "顺序会按深度优先遍历算，与层级里看到的不一致。请把它拖到路径对象下面。";
                return false;
            }

            points[i] = waypoint.transform.position;
        }

        for (int i = 1; i < points.Length; i++)
        {
            if (Vector2.Distance(points[i - 1], points[i]) >= 0.001f) continue;

            problem = $"折点 {i - 1} 与折点 {i} 重合（「{waypoints[i - 1].name}」/「{waypoints[i].name}」）——" +
                      "重合段没有切线，车会卡在那里。";
            return false;
        }

        nodes = CartRouteSource.BuildNodes(waypoints).ToArray();
        return true;
    }

    /// <summary>
    /// 批处理入口：扫描所有引用 <see cref="CartRouteSource"/> 的场景 → 逐个烘焙 → 需要时保存场景。
    /// 任一场景烘焙失败即整体失败（返回非 0），避免"一半新一半旧"。
    /// </summary>
    public static void BakeFromCommandLine()
    {
        bool ok = false;
        int baked = 0;

        try
        {
            ok = BakeAllScenes(out baked);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CartRouteBaker] 异常：{e}");
            ok = false;
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CartRouteBaker] 共烘焙 {baked} 个路径源");
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool BakeAllScenes(out int baked)
    {
        baked = 0;
        bool ok = true;

        string scriptGuid = FindScriptGuid();
        if (scriptGuid == null)
        {
            Debug.LogError("[CartRouteBaker] 找不到 CartRouteSource 的脚本 GUID，无法定位场景。");
            return false;
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { SceneDir });

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);

            // 用文本判断"这个场景里有没有路径源"：比打开场景再找便宜得多，
            // 而且避免了为无关场景付打开/保存的代价（保存是有副作用的）
            if (!File.ReadAllText(scenePath).Contains(scriptGuid)) continue;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            CartRouteSource[] sources = UnityEngine.Object.FindObjectsByType<CartRouteSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int s = 0; s < sources.Length; s++)
            {
                if (!Bake(sources[s], out string message))
                {
                    Debug.LogError($"[CartRouteBaker] {scenePath}：「{sources[s].name}」{message}");
                    ok = false;
                    continue;
                }

                Debug.Log($"[CartRouteBaker] {scenePath}：「{sources[s].name}」{message}");
                baked++;
            }

            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
        }

        return ok && baked > 0;
    }

    /// <summary>取 <see cref="CartRouteSource"/> 的脚本 GUID（用于在场景文本里定位它）。</summary>
    private static string FindScriptGuid()
    {
        string[] guids = AssetDatabase.FindAssets($"{nameof(CartRouteSource)} t:MonoScript");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == nameof(CartRouteSource)) return guids[i];
        }

        return null;
    }
}
#endif
