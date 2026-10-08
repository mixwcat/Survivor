#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血量 HUD 接线工具（幂等，可重复运行）。
///
/// <para>
/// 做两件事：
/// <list type="number">
/// <item>把玩家 prefab 上那条**头顶世界空间血条**的子树复制到 <c>GamePanel.prefab</c> 的左上角，
/// 并接到 <see cref="GamePanel.healthPanel"/> 上（美术沿用原有白底 + 红条 + 文本，不重画）；</item>
/// <item>从玩家 prefab 上移除头顶血条（血量改由 HUD 显示）。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>为什么用工具而不是手拖：</b>血条内部的 Slider / Fill / Background / 文本引用是一个整体，
/// 手工复制容易只复制到一半（表现为"血条在，但不跟血量动"），而且没法在批处理里复现。
/// </para>
///
/// <para>
/// 手动运行：Tools ▸ Setup Player Health HUD
/// 批处理运行：<c>-executeMethod PlayerHealthHudSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class PlayerHealthHudSetup
{
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Common/Player.prefab";
    private const string GamePanelPrefabPath = "Assets/Game/Prefabs/UI/GamePanel.prefab";

    /// <summary>玩家 prefab 上那条头顶血条的对象名。</summary>
    private const string SourceWidgetName = "HealthPanel";

    /// <summary>HUD 里这条血条的对象名。</summary>
    private const string HudWidgetName = "HealthHud";

    /// <summary>血条在左上角的位置（像素，锚在左上角）。经验条横跨整屏且占着 y≈-24..-65，所以从 -70 开始。</summary>
    private static readonly Vector2 HudWidgetPosition = new Vector2(24f, -70f);

    /// <summary>血条容器尺寸。</summary>
    private static readonly Vector2 HudWidgetSize = new Vector2(240f, 64f);

    /// <summary>时间/帧率让位后所在的 x（原为 192，会与血条重叠）。</summary>
    private const float TopLeftTextX = 430f;

    [MenuItem("Tools/Setup Player Health HUD")]
    public static void SetupFromMenu()
    {
        bool ok = Run();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[PlayerHealthHudSetup] CLI_OK (menu)" : "[PlayerHealthHudSetup] CLI_FAIL (menu)");
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
            Debug.LogError($"[PlayerHealthHudSetup] 异常: {e}");
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool Run()
    {
        bool ok = MoveWidgetToHud();
        ok &= RemoveOverheadBar();
        return ok;
    }

    /// <summary>把头顶血条的子树复制进 GamePanel 的左上角，并接上面板字段（已接好时只重排布局）。</summary>
    private static bool MoveWidgetToHud()
    {
        GameObject panelRoot = PrefabUtility.LoadPrefabContents(GamePanelPrefabPath);
        try
        {
            var panel = panelRoot.GetComponent<GamePanel>();
            if (panel == null)
            {
                Debug.LogError($"[PlayerHealthHudSetup] {GamePanelPrefabPath} 上没有 GamePanel 组件。");
                return false;
            }

            GameObject widget;

            if (panel.healthPanel != null)
            {
                // 已接好：仍然重排一次布局（本工具可重复运行，改布局常量后重跑即可）
                widget = panel.healthPanel.gameObject;
            }
            else
            {
                GameObject source = ExtractSourceWidget(out GameObject playerRoot);
                if (source == null)
                {
                    if (playerRoot != null) PrefabUtility.UnloadPrefabContents(playerRoot);
                    return false;
                }

                // 复制整棵子树：Slider / Fill / Background / 文本以及 HealthPanel 上的引用一起过来，
                // 只复制 GameObject 的话组件字段会指向原来的对象
                widget = (GameObject)Object.Instantiate(source, panelRoot.transform);
                widget.name = HudWidgetName;

                PrefabUtility.UnloadPrefabContents(playerRoot);

                var component = widget.GetComponent<HealthPanel>();
                if (component == null)
                {
                    Debug.LogError($"[PlayerHealthHudSetup] 复制出来的 {HudWidgetName} 上没有 HealthPanel 组件。");
                    Object.DestroyImmediate(widget);
                    return false;
                }

                panel.healthPanel = component;
            }

            LayoutTopLeft(widget);
            ShiftTopLeftTexts(panel);

            PrefabUtility.SaveAsPrefabAsset(panelRoot, GamePanelPrefabPath);
            Debug.Log($"[PlayerHealthHudSetup] 血条已在 {GamePanelPrefabPath} 左上角就位" +
                      $"（{HudWidgetName} @ {HudWidgetPosition}，时间/帧率右移到 x={TopLeftTextX}）。");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(panelRoot);
        }
    }

    /// <summary>从玩家 prefab 取出头顶血条对象（调用方负责 UnloadPrefabContents 传出的 root）。</summary>
    private static GameObject ExtractSourceWidget(out GameObject playerRoot)
    {
        playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        Transform found = FindDeep(playerRoot.transform, SourceWidgetName);
        if (found != null) return found.gameObject;

        Debug.LogWarning($"[PlayerHealthHudSetup] {PlayerPrefabPath} 上找不到 {SourceWidgetName}，" +
                         "无法复制血条到 HUD（是否已经迁移过？）。");
        return null;
    }

    /// <summary>
    /// HUD 布局：容器钉在左上角，文本在上、血条在下。
    ///
    /// <para>
    /// 原布局是**世界空间**的：容器拉伸整个 1920×1080 画布，Slider 与文本靠"居中 + y=81"定位到头顶。
    /// 直接搬进 HUD 会让血条跑到屏幕正中，所以这里把锚点、位置、尺寸全部重设一遍。
    /// </para>
    /// </summary>
    private static void LayoutTopLeft(GameObject widget)
    {
        var rect = widget.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = HudWidgetPosition;
            rect.sizeDelta = HudWidgetSize;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        Slider slider = widget.GetComponentInChildren<Slider>(true);
        if (slider != null)
        {
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, -14f);
            sliderRect.sizeDelta = new Vector2(220f, 20f);
        }

        TextMeshProUGUI text = widget.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, 16f);
            textRect.sizeDelta = new Vector2(220f, 32f);

            // 世界空间里字号是 16（画布缩放 0.01 时够看），HUD 上偏小；左对齐更符合角落读数
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Left;
        }
    }

    /// <summary>
    /// 时间与帧率原本占着左上角（x≈192），血条要放在那里 —— 把它们右移一列。
    ///
    /// <para>
    /// 不挪的话两者会叠在一起，而且**不会有任何报错**，只能靠眼睛在运行时发现。
    /// 只往右挪（已经更靠右的不动），这样重复运行工具不会把手工调整过的位置拽回来。
    /// </para>
    /// </summary>
    private static void ShiftTopLeftTexts(GamePanel panel)
    {
        ShiftRight(panel.txtTime);
        ShiftRight(panel.txtFPS);
    }

    private static void ShiftRight(TMPro.TextMeshProUGUI text)
    {
        if (text == null) return;

        RectTransform rect = text.rectTransform;
        if (rect == null || rect.anchoredPosition.x >= TopLeftTextX) return;

        rect.anchoredPosition = new Vector2(TopLeftTextX, rect.anchoredPosition.y);
    }

    /// <summary>移除玩家 prefab 上的头顶血条。</summary>
    private static bool RemoveOverheadBar()
    {
        GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform found = FindDeep(playerRoot.transform, SourceWidgetName);
            if (found == null)
            {
                Debug.Log("[PlayerHealthHudSetup] 玩家 prefab 上没有头顶血条，跳过。");
                return true;
            }

            Object.DestroyImmediate(found.gameObject);

            PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            Debug.Log($"[PlayerHealthHudSetup] 已移除玩家 prefab 上的头顶血条（{SourceWidgetName}）。");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(playerRoot);
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }
}
#endif
