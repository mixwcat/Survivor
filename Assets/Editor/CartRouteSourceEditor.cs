#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 折点增删的共用实现（<see cref="CartRouteSourceEditor"/> 与 <see cref="CartWaypointEditor"/> 都用它）。
///
/// <para>
/// <b>为什么所有 Selection 改动都要 <c>EditorApplication.delayCall</c> 延后一帧：</b>
/// 这些方法是从按钮回调里调用的，也就是**正处在 Inspector 的绘制过程中**。
/// 此时同步改 <c>Selection</c>，Inspector 窗口会在绘制中途重建，新建的 inspector
/// 拿到 null target，于是报出（2026-10 实际踩过）：
/// </para>
/// <code>
/// SerializedObjectNotCreatableException: Object at index 0 is null
///   UnityEditor.Editor.CreateSerializedObject ()
///   UnityEditor.GameObjectInspector.OnEnable ()
/// NullReferenceException
///   UnityEditor.GameObjectInspector.OnDisable ()
/// </code>
/// <para>
/// 同理，**销毁当前选中的对象**之后必须把选中项挪走，否则 Inspector 会一直抱着一个已销毁的对象；
/// 而"销毁正在被绘制的那个对象"本身也要延后（否则它自己就是那个 null target）。
/// 这类报错只影响编辑器界面，不影响游戏运行，但会刷满控制台、把真正的问题淹掉。
/// </para>
/// </summary>
public static class CartRouteEditorTools
{
    /// <summary>在末尾追加一个点（沿最后一段方向延伸同样长度）。</summary>
    public static void Append(CartRouteSource source)
    {
        if (source == null) return;

        List<CartWaypoint> points = source.CollectWaypoints();
        Vector3 position = points.Count == 0
            ? source.transform.position
            : ExtendFrom(points, points.Count - 1);

        Create(source, position, -1, points.Count);
    }

    /// <summary>在指定折点之后插入一个点（放在它与下一个点的正中间）。</summary>
    public static void InsertAfter(CartRouteSource source, CartWaypoint point)
    {
        if (source == null || point == null) return;

        List<CartWaypoint> points = source.CollectWaypoints();
        int index = points.IndexOf(point);
        if (index < 0) return;

        Vector3 position = index + 1 < points.Count
            ? (point.transform.position + points[index + 1].transform.position) * 0.5f
            : ExtendFrom(points, index);

        Create(source, position, point.transform.GetSiblingIndex() + 1, points.Count);
    }

    /// <summary>删除一个折点（至少保留 2 个）。销毁与选中都延后到 GUI 之后，理由见类注释。</summary>
    public static void Delete(CartRouteSource source, CartWaypoint point)
    {
        if (source == null || point == null) return;

        if (source.CollectWaypoints().Count <= 2)
        {
            Debug.LogWarning("[CartRouteEditorTools] 路径至少需要 2 个折点，已忽略本次删除。", source);
            return;
        }

        GameObject pointObject = point.gameObject;
        GameObject sourceObject = source.gameObject;

        EditorApplication.delayCall += () =>
        {
            // 延后执行期间用户可能已经改过选中项 / 关掉场景
            if (pointObject == null) return;

            Undo.DestroyObjectImmediate(pointObject);

            if (sourceObject == null) return;

            EditorSceneManager.MarkSceneDirty(sourceObject.scene);
            Selection.activeGameObject = sourceObject;
        };
    }

    /// <summary>延迟一帧再改选中项。**不要在 OnInspectorGUI 里直接写 <c>Selection</c>**（见类注释）。</summary>
    public static void SelectDeferred(GameObject target)
    {
        if (target == null) return;

        EditorApplication.delayCall += () =>
        {
            if (target != null) Selection.activeGameObject = target;
        };
    }

    /// <summary>沿最后一段的方向再延伸同样长度 —— 加点时不用手动摆，接着画就行。</summary>
    private static Vector3 ExtendFrom(List<CartWaypoint> points, int index)
    {
        Vector3 last = points[index].transform.position;
        if (index == 0) return last + Vector3.right;

        Vector3 direction = last - points[index - 1].transform.position;
        return last + (direction.sqrMagnitude > 0f ? direction : Vector3.right);
    }

    private static void Create(CartRouteSource source, Vector3 position, int siblingIndex, int existingCount)
    {
        var go = new GameObject($"P{existingCount}");
        Undo.RegisterCreatedObjectUndo(go, "添加路径折点");

        Undo.SetTransformParent(go.transform, source.transform, "添加路径折点");
        go.transform.position = position;      // 世界坐标：路径点存的就是世界坐标
        go.AddComponent<CartWaypoint>();

        if (siblingIndex >= 0) go.transform.SetSiblingIndex(siblingIndex);

        EditorSceneManager.MarkSceneDirty(source.gameObject.scene);

        // 选中新点（方便接着拖），但**必须延后**：见类注释
        SelectDeferred(go);
    }
}

/// <summary>
/// <see cref="CartRouteSource"/> 的编辑器界面：**场景里改路径，这里管烘焙与整体状态**。
///
/// <para>
/// 拖点用的是 Unity 原生的移动工具（点是子物体，不需要自写 handle）。
/// 单个点的插入 / 删除在**折点自己的 Inspector**（<see cref="CartWaypointEditor"/>）里 ——
/// 选中一个点时 Unity 显示的是它的界面，把按钮放在这里才够得着。
/// </para>
/// </summary>
[CustomEditor(typeof(CartRouteSource))]
public class CartRouteSourceEditor : Editor
{
    private static GUIStyle _labelStyle;

    private static GUIStyle LabelStyle
    {
        get
        {
            if (_labelStyle != null) return _labelStyle;

            _labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.55f, 0.95f, 1f) },
            };

            return _labelStyle;
        }
    }

    public override void OnInspectorGUI()
    {
        // 选中对象刚被销毁时（例如刚点了「删除这个点」）target 会是伪 null：
        // 继续画下去就是那个 SerializedObjectNotCreatableException
        var source = target as CartRouteSource;
        if (source == null) return;

        DrawDefaultInspector();
        EditorGUILayout.Space();

        List<CartWaypoint> points = source.CollectWaypoints();

        EditorGUILayout.LabelField($"折点 {points.Count} 个 · 节点 {CartRouteSource.BuildNodes(points).Count} 个 · " +
                                   $"场景路径总长 {SceneLength(points):F1}", EditorStyles.miniLabel);

        DrawConsistency(source);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("烘焙到资产", GUILayout.Height(24)))
                BakeAndReport(source);

            if (GUILayout.Button("校验", GUILayout.Height(24), GUILayout.Width(60)))
                Verify(source);
        }

        if (GUILayout.Button("在末尾追加一个点"))
            CartRouteEditorTools.Append(source);

        EditorGUILayout.LabelField("（插入 / 删除单个点：选中那个折点，在它自己的 Inspector 里操作）",
                                   EditorStyles.miniLabel);
    }

    private static void BakeAndReport(CartRouteSource source)
    {
        bool ok = CartRouteBaker.Bake(source, out string message);

        if (ok)
        {
            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            Debug.Log($"[CartRouteSourceEditor] {message}");
        }
        else
        {
            Debug.LogError($"[CartRouteSourceEditor] {message}");
        }

        AssetDatabase.SaveAssets();
        GUI.FocusControl(null);
    }

    /// <summary>一致性状态：**这个框是整套编辑流程的安全网**，别删。</summary>
    private static void DrawConsistency(CartRouteSource source)
    {
        if (source.TryDescribeMismatch(out string mismatch))
        {
            EditorGUILayout.HelpBox(
                $"场景与烘焙资产不一致：{mismatch}\n\n" +
                "运行时读的是**资产**（旧路径），Scene 里看到的才是新的 —— 请点「烘焙到资产」。",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("场景与烘焙资产一致 ✅", MessageType.Info);
    }

    /// <summary>校验并**把结论打到 Console**（不只是画个框：排查时日志比 Inspector 好翻）。</summary>
    private static void Verify(CartRouteSource source)
    {
        if (source.TryDescribeMismatch(out string mismatch))
        {
            Debug.LogWarning($"[CartRouteSourceEditor]「{source.name}」场景与资产不一致：{mismatch}", source);
            return;
        }

        if (source.Route == null)
        {
            Debug.LogWarning($"[CartRouteSourceEditor]「{source.name}」没有烘焙资产。", source);
            return;
        }

        var path = new CartPath(source.Route);
        if (path.ValidationIssue != null)
        {
            Debug.LogError($"[CartRouteSourceEditor]「{source.name}」路径不可用：{path.ValidationIssue}", source);
            return;
        }

        if (path.WarningIssue != null)
        {
            Debug.LogWarning($"[CartRouteSourceEditor]「{source.name}」{path.WarningIssue}", source);
            return;
        }

        Debug.Log($"[CartRouteSourceEditor]「{source.name}」路径正常：" +
                  $"{path.PointCount} 个折点、{path.NodeCount} 个节点、总长 {path.Length:F1}", source);
    }

    // ── Scene 里的标注 ──

    private void OnSceneGUI()
    {
        var source = target as CartRouteSource;
        if (source == null) return;

        List<CartWaypoint> points = source.CollectWaypoints();

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null) continue;

            string label = points[i].Node == CartNodeKind.None
                ? $"P{i}"
                : $"P{i} · {NodeLabel(points[i].Node)}";

            Handles.Label(points[i].transform.position + Vector3.up * 0.7f, label, LabelStyle);
        }
    }

    private static string NodeLabel(CartNodeKind kind)
    {
        switch (kind)
        {
            case CartNodeKind.Charge1: return "充能点 1";
            case CartNodeKind.Charge2: return "充能点 2";
            case CartNodeKind.Destination: return "终点";
            default: return "折点";
        }
    }

    private static float SceneLength(List<CartWaypoint> points)
    {
        float length = 0f;

        for (int i = 1; i < points.Count; i++)
        {
            if (points[i] == null || points[i - 1] == null) continue;

            length += Vector3.Distance(points[i - 1].transform.position, points[i].transform.position);
        }

        return length;
    }

    // ── 进 Play 前的兜底检查 ──

    /// <summary>
    /// 进入 Play 之前检查所有路径源：**"改了场景忘了烘焙"最晚必须在这里被发现**。
    /// 运行时车会照着旧路径开，而 Scene 里画的是新路径 —— 这个差异在 Play 里很难归因。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void HookPlayModeCheck()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            CartRouteSource[] sources = Object.FindObjectsByType<CartRouteSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].TryDescribeMismatch(out string mismatch)) continue;

                Debug.LogWarning($"[CartRouteSource]「{sources[i].name}」场景路径与烘焙资产不一致：{mismatch}。" +
                                 "本次 Play 会按**资产**（旧路径）行驶 —— 请点「烘焙到资产」。", sources[i]);
            }
        };
    }
}
#endif
