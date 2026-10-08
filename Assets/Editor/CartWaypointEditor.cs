#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="CartWaypoint"/> 的编辑器界面 —— **插入 / 删除折点的按钮在这里**。
///
/// <para>
/// <b>为什么不在 <see cref="CartRouteSource"/> 的 Inspector 上：</b>那些按钮需要"当前选中的是哪个点"，
/// 而选中一个点时 Unity 显示的是**它自己的** Inspector —— 放在路径对象上根本够不着
/// （第一版就是这么写的，按钮永远是灰的）。按钮跟着点走，选中即操作。
/// </para>
///
/// <para>
/// 附带的好处：折点从此不再用默认的 <c>GameObjectInspector</c>，也就不会在
/// "选中的对象刚被销毁"时冒出 <c>SerializedObjectNotCreatableException</c>（见
/// <see cref="CartRouteEditorTools"/> 的说明）。
/// </para>
/// </summary>
[CustomEditor(typeof(CartWaypoint))]
public class CartWaypointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var waypoint = target as CartWaypoint;

        // 刚点过「删除这个点」时 target 是伪 null：继续画就是那个 SerializedObjectNotCreatableException
        if (waypoint == null) return;

        DrawDefaultInspector();
        EditorGUILayout.Space();

        CartRouteSource source = ResolveSource(waypoint);

        if (source == null)
        {
            EditorGUILayout.HelpBox("这个折点的父物体上没有 CartRouteSource —— 它不会被烘焙进任何路径。",
                                    MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"属于「{source.name}」", EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("在之后插入一个点"))
                CartRouteEditorTools.InsertAfter(source, waypoint);

            if (GUILayout.Button("删除这个点"))
                CartRouteEditorTools.Delete(source, waypoint);
        }

        if (GUILayout.Button("选中所属路径对象"))
            CartRouteEditorTools.SelectDeferred(source.gameObject);
    }

    /// <summary>所属路径源 = 父物体上的组件（折点必须是直接子物体，见 <see cref="CartWaypoint"/>）。</summary>
    private static CartRouteSource ResolveSource(CartWaypoint waypoint)
    {
        Transform parent = waypoint.transform.parent;
        return parent != null ? parent.GetComponent<CartRouteSource>() : null;
    }
}
#endif
