#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一次性接线工具：推车修理（按住 E 5 秒 → 恢复 10% 耐久）与车顶的修理进度条。
///
/// <list type="number">
/// <item><b>推车 prefab</b>：加 <see cref="CartRepairInteractable"/> 并接好它要修的推车；</item>
/// <item><b>耐久 UI prefab</b>：加进度条（底槽 + 填充图 + <see cref="CartRepairProgressView"/>）；</item>
/// <item><b>Level0 场景</b>：在推车下加修理区（触发体 + <see cref="InteractionSensor"/>）与世界空间提示。</item>
/// </list>
///
/// <para>
/// <b>顺序不能反</b>：先改 prefab 再打开场景 —— 场景实例才会带上新组件。
/// 幂等：已存在的对象/组件只补引用，不重建。
/// 手动：Tools ▸ Setup Cart Repair　批处理：<c>-executeMethod CartRepairSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class CartRepairSetup
{
    private const string LevelScene = "Assets/Scenes/Level0.unity";
    private const string CartPrefabPath = "Assets/Game/Prefabs/Cart/Cart.prefab";
    private const string HealthPrefabPath = "Assets/Game/Prefabs/UI/CartHealthPercent.prefab";
    private const string PromptPrefabPath = "Assets/Game/Prefabs/UI/InteractionPrompt.prefab";

    [MenuItem("Tools/Setup Cart Repair")]
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
            Debug.LogError($"[CartRepairSetup] 异常: {e}");
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
        if (!EnsureRepairComponent()) return false;
        if (!EnsureProgressBar()) return false;

        SetupScene();
        AssetDatabase.SaveAssets();

        Debug.Log("[CartRepairSetup] 完成：推车修理（按住 E 5 秒 +10%）+ 车顶进度条");
        return true;
    }

    /// <summary>① 推车 prefab：加修理交互物并接好推车。</summary>
    private static bool EnsureRepairComponent()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CartPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[CartRepairSetup] 打不开 {CartPrefabPath}");
            return false;
        }

        try
        {
            var cart = root.GetComponent<CartController>();
            if (cart == null)
            {
                Debug.LogError("[CartRepairSetup] Cart prefab 上没有 CartController。");
                return false;
            }

            var repair = root.GetComponent<CartRepairInteractable>();
            if (repair == null) repair = root.AddComponent<CartRepairInteractable>();

            // public 字段直接赋值：prefab 内的引用，两种写法都可靠，直接赋值最少意外
            repair.Cart = cart;

            PrefabUtility.SaveAsPrefabAsset(root, CartPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// ② 耐久 UI prefab：加修理进度条（默认隐藏，修理时才显示）。
    ///
    /// <para>
    /// <b>每次都重新套一遍填充图的设置</b>（不是"存在就跳过"）：进度条靠**锚点**表示进度，
    /// 而锚点方案要求 Image 是 Simple 类型；早期版本用的是 <c>fillAmount</c> + Filled 类型，
    /// 没有 sprite 时会被 Unity 忽略（表现为"一出现就是满的"）—— 自愈式重设能修掉这种残留。
    /// </para>
    /// </summary>
    private static bool EnsureProgressBar()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HealthPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[CartRepairSetup] 打不开 {HealthPrefabPath}");
            return false;
        }

        try
        {
            Transform bar = root.transform.Find("RepairBar");
            if (bar == null)
            {
                var go = new GameObject("RepairBar", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);

                var barRect = go.GetComponent<RectTransform>();
                barRect.sizeDelta = new Vector2(600f, 60f);
                barRect.anchoredPosition = new Vector2(0f, -170f);

                var background = go.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.6f);
                background.raycastTarget = false;

                bar = go.transform;
            }

            Transform fill = bar.Find("Fill");
            if (fill == null)
            {
                var go = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(bar, false);
                fill = go.transform;
            }

            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);   // 初始宽度 0（进度由视图写 anchorMax.x）
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.35f, 0.85f, 0.45f, 1f);
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Simple;         // 锚点方案：不要 Filled（没 sprite 时会被忽略）
            fillImage.fillAmount = 1f;

            var view = root.GetComponent<CartRepairProgressView>();
            if (view == null) view = root.AddComponent<CartRepairProgressView>();

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_fill").objectReferenceValue = fillImage;
            serialized.FindProperty("_root").objectReferenceValue = bar.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            bar.gameObject.SetActive(false);   // 没在修就不该占屏幕（视图在 Awake 里也会兜一次）

            PrefabUtility.SaveAsPrefabAsset(root, HealthPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>③ 场景：推车下加修理区（触发体 + 传感器）与世界空间提示。</summary>
    private static void SetupScene()
    {
        Scene level = EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Single);

        var cart = Object.FindFirstObjectByType<CartController>();
        if (cart == null)
        {
            Debug.LogError("[CartRepairSetup] Level0 里没有 CartController。");
            return;
        }

        var repair = cart.GetComponent<CartRepairInteractable>();
        if (repair == null)
        {
            Debug.LogError("[CartRepairSetup] 推车实例上没有 CartRepairInteractable（prefab 改动没生效？）。");
            return;
        }

        Transform zone = cart.transform.Find("RepairZone");
        if (zone == null)
        {
            var go = new GameObject("RepairZone");
            go.transform.SetParent(cart.transform, false);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            // 修理区大小只影响**玩家的交互半径**：它在 InteractZone 层，而层矩阵里
            // EnemyBody × InteractZone 是关闭的，所以它不会扩大"敌人接触推车"的判定范围
            //（旧注释按"触发体也算接触"写，那在引入层矩阵后已不成立）。
            // 与场景里现存的 4×3 对齐，避免"新建出来的和线上不一样"。
            collider.size = new Vector2(4f, 3f);

            var sensor = go.AddComponent<InteractionSensor>();
            sensor.TargetBehaviour = repair;       // 目标在父物体（推车）上，显式指定

            zone = go.transform;
            Debug.Log("[CartRepairSetup] 已创建 RepairZone（修理触发区）");
        }

        if (zone.GetComponentInChildren<InteractionPromptView>(true) == null)
        {
            GameObject promptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PromptPrefabPath);
            if (promptPrefab == null)
            {
                Debug.LogError($"[CartRepairSetup] 提示 prefab 不存在：{PromptPrefabPath}");
            }
            else
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(promptPrefab, zone);
                instance.transform.localPosition = new Vector3(0f, 1f, 0f);
                Debug.Log("[CartRepairSetup] 已挂世界空间提示");
            }
        }

        EditorSceneManager.MarkSceneDirty(level);
        EditorSceneManager.SaveScene(level);
    }
}
#endif
