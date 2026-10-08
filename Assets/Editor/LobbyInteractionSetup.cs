#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 交互系统接线工具（幂等，可重复运行）。
///
/// <para>
/// 做四件事：
/// <list type="number">
/// <item>建世界空间提示 prefab <c>UI/InteractionPrompt</c>（Canvas + TMP + InteractionPromptView + 点击用 Collider2D）；</item>
/// <item>把三个塔 prefab 的 <c>PlayerDetectRange</c> 接上 <see cref="InteractionSensor"/> 与提示视图；</item>
/// <item>Lobby 场景：武器台接传感器与新提示、新建塔台与切换角色台、补一个 <c>EventSystem</c>；</item>
/// <item>建 <c>UI/TowerBenchPanel</c> prefab 并登记 Addressables。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>为什么需要它：</b>这些接线全是"漏了就静默不工作"的东西（传感器没挂 = 靠近没反应，
/// 提示没接 = 什么都不显示，EventSystem 没有 = 面板按钮点不动）。写成工具跑一次，
/// 比在 Inspector 里逐个拖引用可靠，也能在批处理里复现。
/// </para>
///
/// <para>
/// 手动运行：Tools ▸ Setup Lobby Interaction
/// 批处理运行：<c>-executeMethod LobbyInteractionSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class LobbyInteractionSetup
{
    private const string PromptPrefabPath = "Assets/Game/Prefabs/UI/InteractionPrompt.prefab";
    private const string WeaponBenchPanelPath = "Assets/Game/Prefabs/UI/WeaponBenchPanel.prefab";
    private const string TowerBenchPanelPath = "Assets/Game/Prefabs/UI/TowerBenchPanel.prefab";
    private const string LobbyScenePath = "Assets/Scenes/Lobby.unity";

    private static readonly string[] TowerPrefabPaths =
    {
        "Assets/Game/Prefabs/Tower/Tower_Rin.prefab",
        "Assets/Game/Prefabs/Tower/Tower_Luo.prefab",
        "Assets/Game/Prefabs/Tower/Tower_Teto.prefab",
    };

    /// <summary>武器台内容：枪手的三把武器（与 Character_Gunner.allowedWeaponIds 一致）。</summary>
    private static readonly string[] WeaponAssetPaths =
    {
        "Assets/Game/SO/EntitySO/Weapon_Gun.asset",
        "Assets/Game/SO/EntitySO/Weapon_Cannon.asset",
        "Assets/Game/SO/EntitySO/Weapon_Laser.asset",
    };

    /// <summary>塔台内容：三座塔。</summary>
    private static readonly string[] TowerAssetPaths =
    {
        "Assets/Game/SO/EntitySO/Tower_Rin.asset",
        "Assets/Game/SO/EntitySO/Tower_Luo.asset",
        "Assets/Game/SO/EntitySO/Tower_Teto.asset",
    };

    /// <summary>切换角色台内容：可选角色（与 LobbyDirector / PlayerSpawner 同一份）。</summary>
    private static readonly string[] CharacterAssetPaths =
    {
        "Assets/Game/SO/CharacterSO/Character_Gunner.asset",
        "Assets/Game/SO/CharacterSO/Character_Engineer.asset",
    };

    [MenuItem("Tools/Setup Lobby Interaction")]
    public static void SetupFromMenu()
    {
        bool ok = Run();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[LobbyInteractionSetup] CLI_OK (menu)" : "[LobbyInteractionSetup] CLI_FAIL (menu)");
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
            Debug.LogError($"[LobbyInteractionSetup] 异常: {e}");
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
        bool ok = true;

        ok &= EnsurePromptPrefab();
        ok &= EnsureTowerBenchPanelPrefab();
        ok &= WireTowerPrefabs();
        ok &= WireLobbyScene();
        ok &= RegisterAddressables();

        return ok;
    }

    #region 提示 prefab

    /// <summary>
    /// 建 <c>UI/InteractionPrompt</c>：世界空间 Canvas + TMP 文案 + <see cref="InteractionPromptView"/> + 点击碰撞体。
    ///
    /// <para>
    /// 结构照抄塔 prefab 里已有的世界空间提示（<c>CanvasWithEntity</c>：1920×1080、scale 0.01），
    /// 字号也取塔上那份的值 —— 不新造一套视觉标准。
    /// </para>
    /// </summary>
    private static bool EnsurePromptPrefab()
    {
        if (File.Exists(PromptPrefabPath))
        {
            Debug.Log($"[LobbyInteractionSetup] 提示 prefab 已存在，跳过：{PromptPrefabPath}");
            return true;
        }

        TMP_FontAsset font = FindTowerPromptFont();

        var root = new GameObject("InteractionPrompt",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(BoxCollider2D), typeof(InteractionPromptView));

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        rect.localScale = Vector3.one * 0.01f;

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        // 点击/触摸用：尺寸按**世界单位 ÷ 缩放**给（localScale 0.01 → 180×70 局部 = 1.8×0.7 世界）
        var box = root.GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(180f, 70f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = 45.23f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = "E";

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(600f, 90f);
        textRect.anchoredPosition = new Vector2(0f, 120f);

        var view = root.GetComponent<InteractionPromptView>();
        view.Label = tmp;

        PrefabUtility.SaveAsPrefabAsset(root, PromptPrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[LobbyInteractionSetup] 已创建提示 prefab：{PromptPrefabPath}（字体：{(font != null ? font.name : "默认")}）");
        return true;
    }

    /// <summary>从塔 prefab 的提示文本上取字体资源：新建的 TMP 组件不会自动带字体，没有字体就不渲染。</summary>
    private static TMP_FontAsset FindTowerPromptFont()
    {
        foreach (string path in TowerPrefabPaths)
        {
            if (!File.Exists(path)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform label = FindDeep(root.transform, "Text (TMP)");
                if (label == null) continue;

                var tmp = label.GetComponent<TextMeshProUGUI>();
                if (tmp != null && tmp.font != null) return tmp.font;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.LogWarning("[LobbyInteractionSetup] 没找到可复用的 TMP 字体，提示文本可能不渲染。");
        return null;
    }

    #endregion

    #region 塔台面板 prefab

    /// <summary>
    /// 从 <c>WeaponBenchPanel.prefab</c> 复制出 <c>TowerBenchPanel.prefab</c> 并换脚本。
    /// 两个面板的槽位字段名一一对应，所以引用可以逐个搬过去，零手工重拖。
    /// </summary>
    private static bool EnsureTowerBenchPanelPrefab()
    {
        if (File.Exists(TowerBenchPanelPath))
        {
            Debug.Log($"[LobbyInteractionSetup] 塔台面板 prefab 已存在，跳过：{TowerBenchPanelPath}");
            return true;
        }

        if (!File.Exists(WeaponBenchPanelPath))
        {
            Debug.LogError($"[LobbyInteractionSetup] 缺少模板 prefab：{WeaponBenchPanelPath}");
            return false;
        }

        if (!AssetDatabase.CopyAsset(WeaponBenchPanelPath, TowerBenchPanelPath))
        {
            Debug.LogError($"[LobbyInteractionSetup] 复制面板 prefab 失败：{TowerBenchPanelPath}");
            return false;
        }

        AssetDatabase.Refresh();

        GameObject root = PrefabUtility.LoadPrefabContents(TowerBenchPanelPath);
        try
        {
            var source = root.GetComponent<WeaponBenchPanel>();
            var panel = root.AddComponent<TowerBenchPanel>();

            if (source != null)
            {
                panel.button1 = source.button1;
                panel.button2 = source.button2;
                panel.button3 = source.button3;

                panel.imgIcon1 = source.imgIcon1;
                panel.imgIcon2 = source.imgIcon2;
                panel.imgIcon3 = source.imgIcon3;

                panel.txtDescription1 = source.txtDescription1;
                panel.txtDescription2 = source.txtDescription2;
                panel.txtDescription3 = source.txtDescription3;

                panel.txtConsumption1 = source.txtConsumption1;
                panel.txtConsumption2 = source.txtConsumption2;
                panel.txtConsumption3 = source.txtConsumption3;

                panel.btnClose = source.btnClose;

                // 旧脚本必须移除：留着会变成"两个面板抢同一个 GameObject"，
                // 而 UIService 按类名取组件，取到哪一个取决于顺序
                Object.DestroyImmediate(source, true);
            }
            else
            {
                Debug.LogError($"[LobbyInteractionSetup] 模板 prefab 上没有 WeaponBenchPanel，槽位引用需要手工接：{TowerBenchPanelPath}");
            }

            PrefabUtility.SaveAsPrefabAsset(root, TowerBenchPanelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[LobbyInteractionSetup] 已创建塔台面板 prefab：{TowerBenchPanelPath}");
        return true;
    }

    #endregion

    #region 塔 prefab

    /// <summary>
    /// 给三个塔 prefab 的 <c>PlayerDetectRange</c> 挂上传感器与提示视图，并把提示物接上。
    ///
    /// <para>
    /// 提示物沿用塔上**已有**的世界空间提示（<c>CanvasWithEntity</c> 下的 "Text (TMP)"）与箭头 "Money"，
    /// 所以外观不变，只是控制权从 <c>DetectPlayer</c> 的 <c>#if</c> 分支搬到了
    /// <see cref="InteractionPromptView"/>。
    /// </para>
    /// </summary>
    private static bool WireTowerPrefabs()
    {
        bool ok = true;

        foreach (string path in TowerPrefabPaths)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[LobbyInteractionSetup] 缺少塔 prefab：{path}");
                ok = false;
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform detect = FindDeep(root.transform, "PlayerDetectRange");
                if (detect == null)
                {
                    Debug.LogError($"[LobbyInteractionSetup] {path} 里找不到 PlayerDetectRange，跳过。");
                    ok = false;
                    continue;
                }

                var interactable = detect.GetComponent<TowerInteractable>();
                if (interactable == null)
                {
                    Debug.LogError($"[LobbyInteractionSetup] {path} 的 PlayerDetectRange 上没有 TowerInteractable" +
                                   "（脚本改名后 prefab 引用应保持，检查 GUID 是否被改过）。");
                    ok = false;
                    continue;
                }

                var sensor = detect.GetComponent<InteractionSensor>();
                if (sensor == null) sensor = detect.gameObject.AddComponent<InteractionSensor>();
                sensor.TargetBehaviour = interactable;

                var view = detect.GetComponent<InteractionPromptView>();
                if (view == null) view = detect.gameObject.AddComponent<InteractionPromptView>();

                Transform label = FindDeep(root.transform, "Text (TMP)");
                Transform arrow = FindDeep(detect, "Money");

                // VisualRoot 必须显式指定：留空会退化成"关掉自己"，
                // 而本组件与传感器同物体 —— 那样会把交互一起关掉
                view.VisualRoot = label != null ? label.gameObject : null;
                view.ExtraVisual = arrow != null ? arrow.gameObject : null;
                view.Label = label != null ? label.GetComponent<TextMeshProUGUI>() : null;

                if (label == null)
                {
                    Debug.LogWarning($"[LobbyInteractionSetup] {path} 找不到 'Text (TMP)'，提示文案不会显示。");
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[LobbyInteractionSetup] 已接线塔 prefab：{path}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return ok;
    }

    #endregion

    #region Lobby 场景

    private static bool WireLobbyScene()
    {
        if (!File.Exists(LobbyScenePath))
        {
            Debug.LogError($"[LobbyInteractionSetup] 缺少场景：{LobbyScenePath}");
            return false;
        }

        Scene lobby = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        bool ok = true;

        GameObject promptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PromptPrefabPath);
        if (promptPrefab == null)
        {
            Debug.LogError($"[LobbyInteractionSetup] 提示 prefab 不存在，场景接线中止：{PromptPrefabPath}");
            return false;
        }

        // ① 武器台：接传感器 + 提示，并把内容换成枪手的三把武器
        GameObject weaponBench = FindInScene(lobby, "WeaponBench");
        if (weaponBench == null)
        {
            Debug.LogError("[LobbyInteractionSetup] Lobby 场景里找不到 WeaponBench。");
            ok = false;
        }
        else
        {
            var bench = weaponBench.GetComponent<WeaponBenchInteractable>();
            if (bench == null)
            {
                Debug.LogError("[LobbyInteractionSetup] WeaponBench 上没有 WeaponBenchInteractable" +
                               "（脚本改名后场景引用应保持，检查 GUID 是否被改过）。");
                ok = false;
            }
            else
            {
                bench.Weapons = LoadAll<WeaponEntitySO>(WeaponAssetPaths);
                EnsureSensor(weaponBench, bench);
                EnsurePromptChild(weaponBench);
            }
        }

        // ② 塔台：新建（外观沿用武器台，位置右移）
        ok &= CreateBench(lobby, weaponBench, "TowerBench", new Vector3(4f, 0f, 0f),
            root =>
            {
                var interactable = root.AddComponent<TowerBenchInteractable>();
                interactable.Towers = LoadAll<TowerEntitySO>(TowerAssetPaths);
                EnsureSensor(root, interactable);
                EnsurePromptChild(root);
            });

        // ③ 切换角色台：新建（位置左移）
        ok &= CreateBench(lobby, weaponBench, "CharacterSwitchBench", new Vector3(-4f, 0f, 0f),
            root =>
            {
                var interactable = root.AddComponent<CharacterSwitchInteractable>();
                interactable.Characters = LoadAll<CharacterDefinitionSO>(CharacterAssetPaths);
                EnsureSensor(root, interactable);
                EnsurePromptChild(root);
            });

        // ④ EventSystem：Lobby 场景原本没有 —— 没有它 uGUI 按钮**一个都点不动**
        // （选角面板、武器台面板的按钮全部失效，且不报错）
        ok &= EnsureEventSystem(lobby);

        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        return ok;
    }

    /// <summary>新建一个台：复制模板的外观（Sprite + Collider）与位置偏移，再挂上领域组件。</summary>
    private static bool CreateBench(Scene scene, GameObject template, string name, Vector3 offset,
                                    System.Action<GameObject> configure)
    {
        if (FindInScene(scene, name) != null)
        {
            Debug.Log($"[LobbyInteractionSetup] {name} 已存在，跳过创建。");
            return true;
        }

        if (template == null)
        {
            Debug.LogError($"[LobbyInteractionSetup] 缺少模板物体（WeaponBench），无法创建 {name}。");
            return false;
        }

        var go = new GameObject(name);
        go.transform.SetParent(null);
        go.transform.position = template.transform.position + offset;
        go.transform.rotation = template.transform.rotation;
        go.transform.localScale = template.transform.localScale;

        // 外观照抄模板：新台先用同一张图，美术替换时只改 Sprite 即可
        var templateRenderer = template.GetComponent<SpriteRenderer>();
        if (templateRenderer != null)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = templateRenderer.sprite;
            renderer.color = templateRenderer.color;
            renderer.sortingLayerID = templateRenderer.sortingLayerID;
            renderer.sortingOrder = templateRenderer.sortingOrder;
            renderer.sharedMaterial = templateRenderer.sharedMaterial;
        }

        var templateCollider = template.GetComponent<BoxCollider2D>();
        var collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        if (templateCollider != null)
        {
            collider.size = templateCollider.size;
            collider.offset = templateCollider.offset;
        }

        configure?.Invoke(go);
        SceneManager.MoveGameObjectToScene(go, scene);

        Debug.Log($"[LobbyInteractionSetup] 已创建 {name} @ {go.transform.position}");
        return true;
    }

    /// <summary>挂传感器并指定目标（同物体只有一个 IInteractable 时也能自动解析，显式指定更抗改动）。</summary>
    private static void EnsureSensor(GameObject go, MonoBehaviour target)
    {
        var sensor = go.GetComponent<InteractionSensor>();
        if (sensor == null) sensor = go.AddComponent<InteractionSensor>();

        sensor.TargetBehaviour = target;

        var collider = go.GetComponent<Collider2D>();
        if (collider != null) collider.isTrigger = true;
    }

    /// <summary>把世界空间提示 prefab 实例挂到交互物下面（已存在则跳过）。</summary>
    private static void EnsurePromptChild(GameObject owner)
    {
        if (owner.GetComponentInChildren<InteractionPromptView>(true) != null) return;

        GameObject promptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PromptPrefabPath);
        if (promptPrefab == null)
        {
            Debug.LogError($"[LobbyInteractionSetup] 提示 prefab 不存在：{PromptPrefabPath}");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(promptPrefab, owner.transform);
        instance.transform.localPosition = new Vector3(0f, 1.2f, 0f);
    }

    /// <summary>
    /// 保证场景里有一个 EventSystem（Lobby 场景原本没有 —— 没有它 uGUI 按钮**一个都点不动**：
    /// 选角面板、武器台面板、塔台面板的按钮全部失效，而且不报任何错）。
    ///
    /// <para>
    /// 输入模块**不需要手工配动作**：<c>InputSystemUIInputModule.OnEnable</c> 在
    /// <c>HasNoActions()</c> 时会自动 <c>AssignDefaultActions()</c>（包内 DefaultInputActions）。
    /// 所以这里只挂组件，不写 SerializedObject 对象引用（实测在场景组件上不落盘）。
    /// </para>
    /// </summary>
    private static bool EnsureEventSystem(Scene lobby)
    {
        foreach (GameObject root in lobby.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<EventSystem>(true) != null)
            {
                Debug.Log("[LobbyInteractionSetup] Lobby 场景已有 EventSystem，跳过。");
                return true;
            }
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();

        SceneManager.MoveGameObjectToScene(go, lobby);
        Debug.Log("[LobbyInteractionSetup] 已为 Lobby 场景补上 EventSystem" +
                  "（原缺失 → UI 按钮点不动；输入动作由模块 OnEnable 自动分配）。");
        return true;
    }

    #endregion

    #region Addressables

    private static bool RegisterAddressables()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        if (settings == null)
        {
            Debug.LogWarning("[LobbyInteractionSetup] 没有 AddressableAssetSettings，跳过地址登记" +
                             "（可运行 Tools ▸ Setup Addressables）。");
            return true;
        }

        AddressableAssetGroup group = settings.FindGroup("UI");
        if (group == null)
        {
            Debug.LogWarning("[LobbyInteractionSetup] 找不到 UI 组，跳过地址登记。");
            return true;
        }

        return AddEntry(settings, group, TowerBenchPanelPath, "UI/TowerBenchPanel");
    }

    private static bool AddEntry(AddressableAssetSettings settings, AddressableAssetGroup group,
                                 string assetPath, string address)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"[LobbyInteractionSetup] 找不到资产 GUID：{assetPath}");
            return false;
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        if (entry == null) return false;

        entry.SetAddress(address, false);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);

        Debug.Log($"[LobbyInteractionSetup] 地址已登记：{address}");
        return true;
    }

    #endregion

    #region 工具方法

    private static List<T> LoadAll<T>(string[] paths) where T : Object
    {
        var list = new List<T>();

        foreach (string path in paths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogError($"[LobbyInteractionSetup] 加载失败：{path}");
                continue;
            }

            list.Add(asset);
        }

        return list;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDeep(root.transform, name);
            if (found != null) return found.gameObject;
        }

        return null;
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

    #endregion
}
#endif
