#if UNITY_EDITOR
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一次性接线工具：传送门状态文本、推车耐久百分比、相机跟随、HUD 角色入口按钮文案。
///
/// <list type="number">
/// <item><b>传送门状态文本</b>：Lobby 的传送门上方挂世界空间文本（倒计时 / 等待所有玩家 / 缺什么）；</item>
/// <item><b>推车耐久百分比</b>：Level0 的推车上方挂世界空间文本（"85%"）；</item>
/// <item><b>相机</b>：Level0 的相机由 Cinemachine 驱动（Main Camera 上有 Brain）→
/// 给 <c>CinemachineCamera</c> 挂 <see cref="CinemachinePlayerFollow"/>、移除会与它打架的
/// <see cref="CameraController"/>；Lobby 没有 Cinemachine → 反过来给它挂 <see cref="CameraController"/>；</item>
/// <item><b>HUD 入口文案</b>：<c>btnWeaponShop</c> = 武器升级、<c>btnTowerShop</c> = 建造防御塔。</item>
/// </list>
///
/// <para>
/// 幂等：已存在的对象/组件只补字段，不重建（重建会让场景里已有的手工调整丢失）。
/// 手动：Tools ▸ Setup Status UI　批处理：<c>-executeMethod StatusUiSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class StatusUiSetup
{
    private const string LobbyScene = "Assets/Scenes/Lobby.unity";
    private const string LevelScene = "Assets/Scenes/Level0.unity";
    private const string PortalPrefabPath = "Assets/Game/Prefabs/UI/PortalStatusText.prefab";
    private const string CartPrefabPath = "Assets/Game/Prefabs/UI/CartHealthPercent.prefab";
    private const string GamePanelPath = "Assets/Game/Prefabs/UI/GamePanel.prefab";

    [MenuItem("Tools/Setup Status UI")]
    public static void SetupFromMenu()
    {
        Run();
        AssetDatabase.Refresh();
    }

    public static void SetupFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = Run();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StatusUiSetup] 异常: {e}");
        }
        finally
        {
            AssetDatabase.Refresh();
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool Run()
    {
        GameObject portalPrefab = EnsureTextPrefab<PortalStatusView>(PortalPrefabPath, "PortalStatusText", "等待所有玩家");
        GameObject cartPrefab = EnsureTextPrefab<CartHealthPercentView>(CartPrefabPath, "CartHealthPercent", "100%");
        if (portalPrefab == null || cartPrefab == null) return false;

        SetupLobby(portalPrefab);
        SetupLevel(cartPrefab);
        FixRoleEntryLabels();
        AssetDatabase.SaveAssets();

        Debug.Log("[StatusUiSetup] 完成：传送门状态文本 / 推车耐久百分比 / 相机跟随 / HUD 入口文案");
        return true;
    }

    // ── ① 世界空间文本 prefab ──

    /// <summary>
    /// 生成（或修复）一个"世界空间文本"prefab：Canvas(WorldSpace) + 一个居中 TMP 文本 + 视图组件。
    /// 缩放与排序跟 <c>InteractionPrompt.prefab</c> 保持一致（scale 0.01 / sortingOrder 100），
    /// 这样同一场景里的世界空间 UI 不会互相遮挡得莫名其妙。
    /// </summary>
    private static GameObject EnsureTextPrefab<T>(string path, string name, string defaultText) where T : Component
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);

        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localScale = Vector3.one * 0.01f;

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(root.transform, false);
        label.text = defaultText;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 120f;
        label.color = Color.white;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = new Vector2(1800f, 400f);

        var view = root.AddComponent<T>();
        var serialized = new SerializedObject(view);
        SerializedProperty labelField = serialized.FindProperty("_label");
        if (labelField == null)
        {
            Debug.LogError($"[StatusUiSetup] {typeof(T).Name} 上没有 _label 字段，文本无法接线。");
            Object.DestroyImmediate(root);
            return null;
        }

        labelField.objectReferenceValue = label;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log($"[StatusUiSetup] 已生成 {path}");
        return prefab;
    }

    // ── ② 场景接线 ──

    private static void SetupLobby(GameObject portalPrefab)
    {
        Scene lobby = EditorSceneManager.OpenScene(LobbyScene, OpenSceneMode.Single);

        var portal = Object.FindFirstObjectByType<PortalController>();
        if (portal == null)
        {
            Debug.LogError("[StatusUiSetup] Lobby 里没有 PortalController。");
        }
        else
        {
            EnsureChild(portalPrefab, portal.transform, new Vector3(0f, 2f, 0f));
        }

        // Lobby 没有 Cinemachine：直接给相机挂 CameraController（它自己懒查找本地玩家）
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera != null && camera.GetComponent<CameraController>() == null)
        {
            camera.gameObject.AddComponent<CameraController>();
            Debug.Log($"[StatusUiSetup] Lobby 相机「{camera.name}」已挂 CameraController");
        }

        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);
    }

    private static void SetupLevel(GameObject cartPrefab)
    {
        Scene level = EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Single);

        var cart = Object.FindFirstObjectByType<CartController>();
        if (cart == null)
        {
            Debug.LogError("[StatusUiSetup] Level0 里没有 CartController。");
        }
        else
        {
            EnsureChild(cartPrefab, cart.transform, new Vector3(0f, 1.5f, 0f));
        }

        Camera camera = Object.FindFirstObjectByType<Camera>();

        // Main Camera 上有 CinemachineBrain：它会每帧接管相机 transform，
        // 于是 CameraController 写的值被覆盖 —— 两个驱动者必须只留一个
        if (camera != null)
        {
            var conflict = camera.GetComponent<CameraController>();
            if (conflict != null)
            {
                Object.DestroyImmediate(conflict, true);
                Debug.Log($"[StatusUiSetup] 已移除 Level0 相机上与 Cinemachine 打架的 CameraController");
            }
        }

        // CinemachineCamera 的 Follow 是空的（玩家运行时才生成，Inspector 里没法预接）
        var cinemachine = Object.FindFirstObjectByType<CinemachineCamera>();
        if (cinemachine == null)
        {
            Debug.LogError("[StatusUiSetup] Level0 里没有 CinemachineCamera，相机不会跟随玩家。");
        }
        else if (cinemachine.GetComponent<CinemachinePlayerFollow>() == null)
        {
            cinemachine.gameObject.AddComponent<CinemachinePlayerFollow>();
            Debug.Log("[StatusUiSetup] CinemachineCamera 已挂 CinemachinePlayerFollow");
        }

        EditorSceneManager.MarkSceneDirty(level);
        EditorSceneManager.SaveScene(level);
    }

    /// <summary>把世界空间文本 prefab 实例挂到目标下（已存在则只摆位置，不重建）。</summary>
    private static void EnsureChild(GameObject prefab, Transform parent, Vector3 localPosition)
    {
        if (prefab == null) return;

        string name = prefab.name;
        Transform existing = FindDeep(parent, name);
        if (existing != null)
        {
            existing.localPosition = localPosition;
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = localPosition;
        Debug.Log($"[StatusUiSetup] 已把 {name} 挂到 {parent.name} 下");
    }

    // ── ③ HUD 入口文案 ──

    /// <summary>
    /// 两个入口按钮的文案必须与它们打开的面板一致：旧实现复用一个按钮按能力分支，
    /// 于是工程师看到"通用升级"点开却是建塔面板。
    /// </summary>
    private static void FixRoleEntryLabels()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GamePanelPath);
        if (root == null)
        {
            Debug.LogError($"[StatusUiSetup] 打不开 {GamePanelPath}");
            return;
        }

        try
        {
            SetButtonLabel(root, "btnWeaponShop", "武器升级");
            SetButtonLabel(root, "btnTowerShop", "建造防御塔");
            // 字段名是遗留的 btnTowerLevelUP（原塔升级入口，已删除），现在它的语义是"通用升级"
            SetButtonLabel(root, "btnTowerLevelUP", "通用升级");
            PrefabUtility.SaveAsPrefabAsset(root, GamePanelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetButtonLabel(GameObject root, string buttonName, string text)
    {
        Transform button = FindDeep(root.transform, buttonName);
        if (button == null)
        {
            Debug.LogError($"[StatusUiSetup] GamePanel 里找不到按钮「{buttonName}」。");
            return;
        }

        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
        {
            Debug.LogError($"[StatusUiSetup] 按钮「{buttonName}」下没有 TMP 文本，文案改不了。");
            return;
        }

        label.text = text;
    }

    /// <summary>按名字递归找子物体（按钮/文本层级不是平的，用 transform.Find 会漏）。</summary>
    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }
}
#endif
