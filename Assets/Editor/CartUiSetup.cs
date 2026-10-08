#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 幂等的**UI 资产生成器** —— 为「塔定向升级列表」与「HUD 武器槽按钮」生成/补齐 prefab。
///
/// <para>
/// <b>为什么用脚本生成而不是手拖 prefab：</b>这两处 UI 的层级与组件是确定的
/// （滚动列表 = ScrollRect + Viewport + Content + 行模板；槽位按钮 = Button + TMP），
/// 手拖既不可复现、也无法在 diff 里 review。生成器可以反复跑，结果一致。
/// </para>
///
/// <para>
/// 手动运行：Tools ▸ Setup Cart UI
/// 批处理运行：<c>-executeMethod CartUiSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class CartUiSetup
{
    private const string TowerPanelPath = "Assets/Game/Prefabs/UI/TowerLevelUpPanel.prefab";
    private const string GamePanelPath = "Assets/Game/Prefabs/UI/GamePanel.prefab";

    [MenuItem("Tools/Setup Cart UI")]
    public static void SetupFromMenu()
    {
        bool ok = Setup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[CartUiSetup] CLI_OK (menu)" : "[CartUiSetup] CLI_FAIL (menu)");
    }

    public static void SetupFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = Setup();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CartUiSetup] 异常: {e}");
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool Setup()
    {
        bool ok;

        // 在一个空场景里搭好层级再存成 prefab：PrefabUtility.SaveAsPrefabAsset 需要场景中的对象
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        try
        {
            ok = BuildTowerLevelUpPanel();
        }
        finally
        {
            // 清掉临时场景里的搭建物，免得它们被后续操作（或意外保存）带进去
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        ok &= PatchGamePanel();
        return ok;
    }

    // ── 塔升级面板 ──

    private static bool BuildTowerLevelUpPanel()
    {
        GameObject root = CreateUi("TowerLevelUpPanel", null);
        Stretch(root.GetComponent<RectTransform>());

        Image background = root.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);

        root.AddComponent<CanvasGroup>();

        TowerLevelUpPanel panel = root.AddComponent<TowerLevelUpPanel>();

        // 标题
        TextMeshProUGUI title = CreateText("Title", root.transform, "防御塔", 46, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
               new Vector2(0f, -70f), new Vector2(800f, 70f));

        // 滚动列表
        GameObject scrollObj = CreateUi("Scroll", root.transform);
        Anchor(scrollObj.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f),
               new Vector2(0f, 0f), new Vector2(-160f, -320f));

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        GameObject viewport = CreateUi("Viewport", scrollObj.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUi("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        // 行模板（默认隐藏；面板按需克隆）
        TowerOptionItem template = BuildItemTemplate(content.transform);
        template.gameObject.SetActive(false);

        // 底部按钮
        Button btnClose = CreateButton("btnClose", root.transform, "关闭", new Vector2(-160f, 90f));
        Button btnDismantle = CreateButton("btnDismantle", root.transform, "拆除", new Vector2(160f, 90f));

        panel.content = contentRect;
        panel.itemTemplate = template;
        panel.scrollRect = scroll;
        panel.btnClose = btnClose;

        // 拆除按钮是 [SerializeField] private：走 SerializedObject 写。
        // ⚠️ prefab **资产**上用 SerializedObject 是可靠的（场景里的对象引用才写不进去，见 CLAUDE.md）
        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("_btnDismantle").objectReferenceValue = btnDismantle;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(TowerPanelPath));
        PrefabUtility.SaveAsPrefabAsset(root, TowerPanelPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[CartUiSetup] 已生成 {TowerPanelPath}");
        return true;
    }

    private static TowerOptionItem BuildItemTemplate(Transform parent)
    {
        GameObject item = CreateUi("ItemTemplate", parent);
        item.AddComponent<LayoutElement>().preferredHeight = 120f;

        Image bg = item.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.12f);

        Button button = item.AddComponent<Button>();
        button.targetGraphic = bg;

        TowerOptionItem option = item.AddComponent<TowerOptionItem>();

        GameObject iconObj = CreateUi("Icon", item.transform);
        Anchor(iconObj.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
               new Vector2(80f, 0f), new Vector2(88f, 88f));
        Image icon = iconObj.AddComponent<Image>();
        icon.preserveAspect = true;

        TextMeshProUGUI title = CreateText("Title", item.transform, "升级项", 32, TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.offsetMin = new Vector2(140f, 12f);
        titleRect.offsetMax = new Vector2(-180f, -12f);

        TextMeshProUGUI cost = CreateText("Cost", item.transform, "1", 32, TextAlignmentOptions.Center);
        Anchor(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
               new Vector2(-100f, 0f), new Vector2(150f, 70f));

        option.Button = button;
        option.Icon = icon;
        option.Title = title;
        option.Cost = cost;

        return option;
    }

    // ── GamePanel：补两个武器槽按钮 ──

    private static bool PatchGamePanel()
    {
        if (!File.Exists(GamePanelPath))
        {
            Debug.LogError($"[CartUiSetup] 找不到 {GamePanelPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(GamePanelPath);
        try
        {
            GamePanel panel = root.GetComponent<GamePanel>();
            if (panel == null)
            {
                Debug.LogError($"[CartUiSetup] {GamePanelPath} 上没有 GamePanel 组件");
                return false;
            }

            SerializedObject so = new SerializedObject(panel);

            Button slot1 = EnsureSlotButton(root.transform, so, "btnWeaponSlot1", "1", new Vector2(70f, 150f));
            Button slot2 = EnsureSlotButton(root.transform, so, "btnWeaponSlot2", "2", new Vector2(70f, 260f));

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GamePanelPath);

            Debug.Log($"[CartUiSetup] {GamePanelPath} 武器槽按钮：{slot1.name} / {slot2.name}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 幂等补齐一个槽位按钮：字段已指向有效对象时直接跳过，否则新建并写回字段。
    /// 位置固定在**左下角**（锚点 (0,0)）—— 与移动端摇杆区分开，避免压住移动摇杆。
    /// </summary>
    private static Button EnsureSlotButton(Transform parent, SerializedObject panelSo,
                                           string fieldName, string label, Vector2 anchoredPos)
    {
        SerializedProperty prop = panelSo.FindProperty(fieldName);
        if (prop != null && prop.objectReferenceValue is Button existing && existing != null)
            return existing;

        Button button = CreateButton(fieldName, parent, label, anchoredPos, anchorBottomLeft: true);

        if (prop == null)
        {
            Debug.LogError($"[CartUiSetup] GamePanel 上找不到字段 {fieldName}，" +
                           "按钮生成了但不会被使用（脚本是否还没编译？）");
            return button;
        }

        prop.objectReferenceValue = button;
        return button;
    }

    // ── 构建工具 ──

    private static GameObject CreateUi(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
                               Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text,
                                              float size, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUi(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        else
            Debug.LogWarning("[CartUiSetup] TMP 默认字体未设置（Window ▸ TextMeshPro ▸ Font Asset Creator），" +
                             "生成的文本可能不显示。");

        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label,
                                       Vector2 anchoredPos, bool anchorBottomLeft = false)
    {
        GameObject go = CreateUi(name, parent);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (anchorBottomLeft)
        {
            // 左下角：与移动摇杆区分开
            Anchor(rect, Vector2.zero, Vector2.zero, anchoredPos, new Vector2(120f, 90f));
        }
        else
        {
            // 底部居中：关闭在左、拆除在右
            Anchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchoredPos, new Vector2(240f, 80f));
        }

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.25f, 0.35f, 0.55f, 0.95f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Text", go.transform, label, 34, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);

        return button;
    }
}
#endif
