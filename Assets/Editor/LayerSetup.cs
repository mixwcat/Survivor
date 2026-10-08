#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 物理层方案的实施工具（见仓库根目录 <c>LAYER_PLAN.md</c>）。
///
/// <para>
/// <b>三步，必须按顺序：</b>
/// <list type="number">
/// <item><see cref="SetupLayerNames"/>：写层名 —— 行为零变化；</item>
/// <item><see cref="SetupColliderLayers"/>：按组件语义给碰撞体分配层 —— **行为从此开始变化**；</item>
/// <item><see cref="ApplyCollisionMatrix"/>：按方案设置矩阵 —— 必须在第 2 步之后，
/// 否则会把还留在 <c>Default</c> 上的子弹/经验球/交互区一起隔离掉。</item>
/// </list>
/// 一键跑三步用 <see cref="SetupAllFromCommandLine"/>。
/// </para>
///
/// <para>
/// <b>为什么 Default(0) 那一行/列不动：</b>UI、场景道具、放置幽灵等仍留在 Default；
/// 对 Default 做隔离只会在改层过程中制造"东西突然不碰撞"的假故障。
/// 方案里真正要禁掉的配对（如 <c>EnemyDetector × WeaponHitbox</c>）两层都是新层，不受影响。
/// </para>
/// </summary>
public static class LayerSetup
{
    private const string LobbyScene = "Assets/Scenes/Lobby.unity";
    private const string LevelScene = "Assets/Scenes/Level0.unity";

    /// <summary>方案里的层名（层号 → 名称）。7/8 沿用现有编号，只是把名字改得表意。</summary>
    private static readonly (int Index, string Name)[] Layers =
    {
        (7, "PlayerBody"),
        (8, "TowerBody"),
        (9, "EnemyBody"),
        (10, "CartBody"),
        (11, "WeaponHitbox"),
        (12, "EnemyDetector"),
        (13, "TowerDetector"),
        (14, "InteractZone"),
        (15, "Pickup"),
        (16, "Boundary"),
    };

    /// <summary>
    /// 方案要求**开启**的层配对（双向）。没列出的配对一律关闭（Default 除外，见类型注释）。
    ///
    /// <para>
    /// 两类配对：<b>触发关系</b>（本体 ↔ 检测体/攻击判定体）与<b>物理阻挡</b>
    /// （本体 ↔ 本体，避免穿模）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <c>EnemyBody × EnemyBody</c> **刻意不列**（= 关闭）：怪可以互相重叠。
    /// 这是玩法取舍（包围感更强、物理开销骤降），别当成遗漏补上。
    /// </para>
    /// </summary>
    private static readonly (string A, string B)[] EnabledPairs =
    {
        // ── 触发关系：谁会被谁打到 / 谁触发谁 ──
        ("PlayerBody", "EnemyDetector"),
        ("PlayerBody", "InteractZone"),
        ("PlayerBody", "Pickup"),

        ("TowerBody", "EnemyDetector"),
        ("TowerBody", "TowerDetector"),      // 治疗友军

        ("CartBody", "EnemyDetector"),

        ("EnemyBody", "WeaponHitbox"),
        ("EnemyBody", "TowerDetector"),
        ("EnemyBody", "Boundary"),

        ("TowerDetector", "PlayerBody"),     // 塔的玩家检测圈

        // ── 物理阻挡：本体之间不穿模（怪 × 怪刻意关闭，见类型注释）──
        ("PlayerBody", "EnemyBody"),
        ("PlayerBody", "TowerBody"),
        ("PlayerBody", "CartBody"),
        ("EnemyBody", "TowerBody"),
        ("EnemyBody", "CartBody"),
        ("TowerBody", "CartBody"),
    };

    // ── ① 层名（行为零变化） ──

    [MenuItem("Tools/Physics Layers/Setup Layer Names")]
    public static void SetupLayerNamesFromMenu()
    {
        SetupLayerNames();
        AssetDatabase.Refresh();
    }

    public static void SetupLayerNamesFromCommandLine()
    {
        RunCli(SetupLayerNames);
    }

    private static bool SetupLayerNames()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[LayerSetup] 读不到 ProjectSettings/TagManager.asset");
            return false;
        }

        var tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            Debug.LogError("[LayerSetup] TagManager 里找不到 layers 数组");
            return false;
        }

        int written = 0;
        for (int i = 0; i < Layers.Length; i++)
        {
            (int index, string name) = Layers[i];
            if (index >= layers.arraySize)
            {
                Debug.LogError($"[LayerSetup] 层号 {index} 超出数组范围（{layers.arraySize}）");
                return false;
            }

            SerializedProperty slot = layers.GetArrayElementAtIndex(index);
            if (slot.stringValue == name) continue;

            slot.stringValue = name;
            written++;
        }

        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        Debug.Log($"[LayerSetup] 层名已写入（改动 {written} 项）");
        return true;
    }

    // ── ② 碰撞体层（行为从此变化） ──

    [MenuItem("Tools/Physics Layers/Setup Collider Layers")]
    public static void SetupColliderLayersFromMenu()
    {
        SetupColliderLayers();
        AssetDatabase.Refresh();
    }

    public static void SetupColliderLayersFromCommandLine()
    {
        RunCli(SetupColliderLayers);
    }

    private static bool SetupColliderLayers()
    {
        if (!LayerNamesReady()) return false;

        int changed = 0;
        var unclassified = new List<string>();

        foreach (string path in FindAllPrefabs())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                bool dirty = false;
                foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
                    dirty |= AssignLayer(collider, root, path, unclassified, ref changed);

                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (string scenePath in new[] { LobbyScene, LevelScene })
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool dirty = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
                    dirty |= AssignLayer(collider, collider.gameObject, scenePath, unclassified, ref changed);
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        Debug.Log($"[LayerSetup] 碰撞体层已分配（改动 {changed} 个对象）");

        // 未分类的碰撞体保持原样并报出来：它们仍留在 Default，
        // 可能是"忘了归类"（漏了就会被方案漏掉），也可能本来就无关（UI/放置幽灵）
        if (unclassified.Count > 0)
        {
            Debug.Log($"[LayerSetup] 未归类（保持 Default，共 {unclassified.Count} 项，请人工确认）：\n  " +
                      string.Join("\n  ", unclassified));
        }

        return true;
    }

    /// <summary>按组件语义决定碰撞体该在哪一层；-1 = 未分类（保持原样）。</summary>
    private static int DecideLayer(Collider2D collider, GameObject hierarchyRoot)
    {
        // ① 本体优先：碰撞体所在对象上就有血量控制器。
        // 必须排在触发区规则**之前** —— 塔本体是"带 InteractionSensor 的子物体"的父物体，
        // 先匹配触发区会把塔本体归成 InteractZone（后果：怪打不到塔）
        string bodyLayer = ExpectedBodyLayer(collider.GetComponent<BaseHealthController>());
        if (bodyLayer != null) return LayerMask.NameToLayer(bodyLayer);

        // ② 投射物：整个层级都是攻击判定体
        if (hierarchyRoot.GetComponentInChildren<BulletController>(true) != null) return LayerMask.NameToLayer("WeaponHitbox");
        if (hierarchyRoot.GetComponentInChildren<SpinWeaponController>(true) != null) return LayerMask.NameToLayer("WeaponHitbox");

        // ③ 可拾取物
        if (hierarchyRoot.GetComponentInChildren<ExpSpriteController>(true) != null) return LayerMask.NameToLayer("Pickup");

        // ④ 交互触发区 / 传送门 / 边界：组件可能挂在**父物体**上
        // （如边界的 BoxCollider2D 在 EnemyBoundary 对象的子物体上），
        // 但**不向子物体找** —— 否则"带触发区子物体的实体本体"会被误判成触发区
        if (FindOnSelfOrParent<InteractionSensor>(collider) != null) return LayerMask.NameToLayer("InteractZone");
        if (FindOnSelfOrParent<PortalController>(collider) != null) return LayerMask.NameToLayer("InteractZone");
        if (FindOnSelfOrParent<EnemyBoundary>(collider) != null) return LayerMask.NameToLayer("Boundary");

        // ⑤ 检测体：挂在可受伤实体**子物体**上的触发体
        if (collider.isTrigger)
        {
            if (collider.GetComponentInParent<EnemyHealthController>() != null) return LayerMask.NameToLayer("EnemyDetector");
            if (collider.GetComponentInParent<BaseTower>() != null) return LayerMask.NameToLayer("TowerDetector");
        }

        return -1;
    }

    private static bool AssignLayer(Collider2D collider, GameObject hierarchyRoot, string owner,
                                    List<string> unclassified, ref int changed)
    {
        int layer = DecideLayer(collider, hierarchyRoot);
        if (layer < 0)
        {
            // 只在"看起来该归类"的对象上报告（有触发器或带实体组件的才值得人工确认）
            unclassified.Add($"{owner} → {collider.gameObject.name} ({collider.GetType().Name})");
            return false;
        }

        if (collider.gameObject.layer == layer) return false;

        collider.gameObject.layer = layer;
        changed++;
        return true;
    }

    /// <summary>血量控制器类型 → 本体层名；未知类型返回 null（不猜）。</summary>
    private static string ExpectedBodyLayer(BaseHealthController health)
    {
        if (health is PlayerHealthController) return "PlayerBody";
        if (health is TowerHealthController) return "TowerBody";
        if (health is EnemyHealthController) return "EnemyBody";
        if (health is CartHealthController) return "CartBody";
        return null;
    }

    /// <summary>
    /// 在自身 / **父级**里找组件（触发区的组件不一定和碰撞体同物体）。
    /// 刻意**不向子物体找**：带触发区子物体的实体本体会被误判成触发区。
    /// </summary>
    private static T FindOnSelfOrParent<T>(Collider2D collider) where T : Component
    {
        return collider.GetComponent<T>() ?? collider.GetComponentInParent<T>();
    }

    // ── ③ 碰撞矩阵（必须在 ② 之后） ──

    [MenuItem("Tools/Physics Layers/Apply Collision Matrix")]
    public static void ApplyMatrixFromMenu()
    {
        ApplyCollisionMatrix();
    }

    public static void ApplyMatrixFromCommandLine()
    {
        RunCli(ApplyCollisionMatrix);
    }

    private static bool ApplyCollisionMatrix()
    {
        if (!LayerNamesReady()) return false;

        var enabled = new HashSet<(int, int)>();
        for (int i = 0; i < EnabledPairs.Length; i++)
        {
            int a = LayerMask.NameToLayer(EnabledPairs[i].A);
            int b = LayerMask.NameToLayer(EnabledPairs[i].B);

            enabled.Add((a, b));
            enabled.Add((b, a));   // 对称
        }

        int adjusted = 0;
        for (int a = 1; a < 32; a++)            // 0 = Default 不动
        {
            for (int b = a; b < 32; b++)
            {
                if (b == 0) continue;

                bool shouldEnable = enabled.Contains((a, b));
                bool currentlyEnabled = !Physics2D.GetIgnoreLayerCollision(a, b);

                if (shouldEnable == currentlyEnabled) continue;

                Physics2D.IgnoreLayerCollision(a, b, !shouldEnable);
                adjusted++;
            }
        }

        Debug.Log($"[LayerSetup] 碰撞矩阵已按方案设置（调整 {adjusted} 对；Default 行/列保持不变；" +
                  "EnemyBody × EnemyBody 按方案关闭）");
        return true;
    }

    // ── 一键三步 ──

    public static void SetupAllFromCommandLine()
    {
        RunCli(() => SetupLayerNames() && SetupColliderLayers() && ApplyCollisionMatrix());
    }

    /// <summary>
    /// 打印方案涉及的各层之间的实际配对（走 <c>Physics2D.GetIgnoreLayerCollision</c>，
    /// 不解析 Physics2DSettings 的十六进制 —— 那个字符串的位序容易读反）。
    /// 用于人工审计矩阵，也是"体检说没问题"时的对照物。
    /// </summary>
    public static void DumpMatrixFromCommandLine()
    {
        RunCli(() =>
        {
            int enabled = 0;
            int total = 0;

            for (int i = 0; i < Layers.Length; i++)
            {
                for (int j = i; j < Layers.Length; j++)
                {
                    int a = Layers[i].Index;
                    int b = Layers[j].Index;

                    total++;
                    if (Physics2D.GetIgnoreLayerCollision(a, b)) continue;

                    enabled++;
                    Debug.Log($"[LayerSetup] 开启：{Layers[i].Name} × {Layers[j].Name}");
                }
            }

            Debug.Log($"[LayerSetup] 方案内配对统计：{enabled}/{total} 开启" +
                      $"（EnemyBody × EnemyBody = {(Physics2D.GetIgnoreLayerCollision(9, 9) ? "关闭" : "开启")}）");
            return true;
        });
    }

    // ── 回归检查（由 SceneWiringAudit 调用） ──

    /// <summary>检查层名与矩阵是否符合方案（防止有人手改后无人发现）。</summary>
    public static void VerifyAll(List<string> problems)
    {
        for (int i = 0; i < Layers.Length; i++)
        {
            (int index, string name) = Layers[i];
            if (LayerMask.LayerToName(index) != name)
            {
                problems.Add($"层名不符（{index} 应为「{name}」，实际「{LayerMask.LayerToName(index)}」）—— " +
                             "跑 Tools ▸ Physics Layers ▸ Setup Layer Names");
            }
        }

        for (int i = 0; i < EnabledPairs.Length; i++)
        {
            int a = LayerMask.NameToLayer(EnabledPairs[i].A);
            int b = LayerMask.NameToLayer(EnabledPairs[i].B);
            if (a < 0 || b < 0) continue;

            if (Physics2D.GetIgnoreLayerCollision(a, b))
            {
                problems.Add($"矩阵缺少方案要求的配对：「{EnabledPairs[i].A}」×「{EnabledPairs[i].B}」" +
                             " —— 跑 Tools ▸ Physics Layers ▸ Apply Collision Matrix");
            }
        }

        // 刻意关闭的配对：被重新打开说明有人手改过
        int enemy = LayerMask.NameToLayer("EnemyBody");
        if (enemy >= 0 && !Physics2D.GetIgnoreLayerCollision(enemy, enemy))
        {
            problems.Add("EnemyBody × EnemyBody 应为关闭（方案：怪可以互相重叠）—— " +
                         "跑 Tools ▸ Physics Layers ▸ Apply Collision Matrix");
        }

        // 可受伤实体的本体碰撞体必须在对应的本体层上：
        // 漏掉的后果是"塔看不见这个新敌人"/"子弹打不到它" —— 只在战斗里暴露
        foreach (string path in FindAllPrefabs())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                foreach (BaseHealthController health in root.GetComponentsInChildren<BaseHealthController>(true))
                {
                    string expected = ExpectedBodyLayer(health);
                    if (expected == null) continue;

                    int expectedIndex = LayerMask.NameToLayer(expected);
                    if (expectedIndex < 0 || health.gameObject.layer == expectedIndex) continue;

                    problems.Add($"本体碰撞体层不符（{path} → {health.gameObject.name} 应为「{expected}」，" +
                                 $"实际「{LayerMask.LayerToName(health.gameObject.layer)}」）—— " +
                                 "跑 Tools ▸ Physics Layers ▸ Setup Collider Layers");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    // ── 内部 ──

    private static bool LayerNamesReady()
    {
        for (int i = 0; i < Layers.Length; i++)
        {
            if (LayerMask.NameToLayer(Layers[i].Name) != Layers[i].Index)
            {
                Debug.LogError($"[LayerSetup] 层名未就绪：「{Layers[i].Name}」应在 {Layers[i].Index} —— " +
                               "请先跑 Setup Layer Names");
                return false;
            }
        }

        return true;
    }

    private static void RunCli(System.Func<bool> action)
    {
        bool ok = false;
        try
        {
            ok = action();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LayerSetup] 异常: {e}");
        }
        finally
        {
            AssetDatabase.Refresh();
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static IEnumerable<string> FindAllPrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path)) yield return path;
        }
    }
}
#endif
