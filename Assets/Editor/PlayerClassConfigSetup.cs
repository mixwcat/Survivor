#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 为每个职业生成专属的 <see cref="PlayerEntitySO"/> / <see cref="PlayerDataSO"/>，
/// 并把角色 SO 指过去 —— 取代原先"所有职业共用一份 <c>PlayerEntity</c>"的做法。
///
/// <para>
/// <b>为什么数值要按职业分开：</b>共用一份时，角色 SO 的 <c>dataRef</c> 与玩家 prefab 的
/// <c>entityConfig</c> 指向同一个资产 —— 改枪手的血量会同时改掉工程师，
/// 而"角色"这一层看起来像是能配数值的（字段就在那儿），实际改了一点效果都没有。
/// </para>
///
/// <para>
/// <b>配套的运行时改动：</b>Player prefab 的 <c>entityConfig</c> 被清空、根节点改为**未激活**，
/// 由 <c>PlayerSpawner</c> 在实例化之后、激活之前注入角色的 <c>playerConfig</c>。
/// 顺序不能反：先激活的话 <c>Awake</c> 已经按"没有配置"失败过一次，
/// 血量上限/移速都拿过 1f 兜底值，之后注入会被 <c>SetEntityConfig</c> 拒绝。
/// </para>
///
/// <para>
/// 手动：Tools ▸ Setup Player Class Configs
/// 批处理：<c>-executeMethod PlayerClassConfigSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class PlayerClassConfigSetup
{
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Common/Player.prefab";
    private const string DataFolder = "Assets/Game/SO/BaseDataSO";
    private const string EntityFolder = "Assets/Game/SO/EntitySO";

    /// <summary>改造前的共用资产：第一次运行时它们是数值与升级池的来源，改造后删除。</summary>
    private const string LegacyEntityPath = EntityFolder + "/PlayerEntity.asset";
    private const string LegacyDataPath = DataFolder + "/Player.asset";

    /// <summary>一个职业的配置：角色 id ↔ 专属 id ↔ 资产名。</summary>
    private sealed class ClassConfig
    {
        public string CharacterId;
        public string EntityId;
        public string AssetName;

        public PlayerDataSO Data;
        public PlayerEntitySO Entity;
    }

    private static readonly ClassConfig[] Classes =
    {
        new ClassConfig { CharacterId = "char_gunner", EntityId = "player_gunner", AssetName = "Player_Gunner" },
        new ClassConfig { CharacterId = "char_engineer", EntityId = "player_engineer", AssetName = "Player_Engineer" },
    };

    [MenuItem("Tools/Setup Player Class Configs")]
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
            Debug.LogError($"[PlayerClassConfigSetup] 异常: {e}");
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
        var legacyData = AssetDatabase.LoadAssetAtPath<PlayerDataSO>(LegacyDataPath);
        var legacyEntity = AssetDatabase.LoadAssetAtPath<PlayerEntitySO>(LegacyEntityPath);

        int created = 0;

        // ── ① 每个职业一份数值 + 一份实体配置 ──
        for (int i = 0; i < Classes.Length; i++)
        {
            ClassConfig config = Classes[i];

            string dataPath = $"{DataFolder}/{config.AssetName}Data.asset";
            config.Data = AssetDatabase.LoadAssetAtPath<PlayerDataSO>(dataPath);
            if (config.Data == null)
            {
                config.Data = ScriptableObject.CreateInstance<PlayerDataSO>();

                // 起点与原来的共用配置一致（不凭空改数值）；要差异化请直接改这两份资产
                if (legacyData != null) CopyStats(legacyData, config.Data);

                AssetDatabase.CreateAsset(config.Data, dataPath);
                created++;
            }

            string entityPath = $"{EntityFolder}/{config.AssetName}.asset";
            config.Entity = AssetDatabase.LoadAssetAtPath<PlayerEntitySO>(entityPath);
            if (config.Entity == null)
            {
                config.Entity = ScriptableObject.CreateInstance<PlayerEntitySO>();
                AssetDatabase.CreateAsset(config.Entity, entityPath);
                created++;
            }

            config.Entity.id = config.EntityId;
            config.Entity.dataRef = config.Data;

            // 升级池也按职业分：只有"空着"时才从旧资产抄一次，避免覆盖掉后来手改的内容
            if ((config.Entity.upgrades == null || config.Entity.upgrades.Count == 0) && legacyEntity != null)
                config.Entity.upgrades = new List<LevelUpSO>(legacyEntity.upgrades);

            EditorUtility.SetDirty(config.Entity);
            EditorUtility.SetDirty(config.Data);
        }

        // ── ② 角色 SO 指向本职业的配置 ──
        int repointed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinitionSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
            if (character == null) continue;

            ClassConfig config = Find(character.id);
            if (config == null)
            {
                Debug.LogWarning($"[PlayerClassConfigSetup] 角色「{character.id}」不在本工具的清单里，跳过: {path}");
                continue;
            }

            character.playerConfig = config.Entity;

            // dataRef 与 playerConfig.dataRef 必须是同一份（EntitySOValidator 会校验一致性）：
            // 面板/清单上看到的是 dataRef，真正生效的是 playerConfig.dataRef，指岔了就是"改了没反应"
            character.dataRef = config.Data;

            // 角色自己的 upgrades 从此是冗余（升级池取 playerConfig.upgrades）→ 清掉，避免两份清单漂移
            character.upgrades.Clear();

            EditorUtility.SetDirty(character);
            repointed++;
        }

        // ── ③ Player prefab：清掉预接的配置 + 根节点改为未激活 ──
        if (!ConfigurePlayerPrefab()) return false;

        // ── ④ 删除共用的旧资产（此时已无人引用）──
        DeleteLegacy(LegacyEntityPath);
        DeleteLegacy(LegacyDataPath);

        AssetDatabase.SaveAssets();

        Debug.Log($"[PlayerClassConfigSetup] 完成：新建 {created} 份资产，" +
                  $"重指 {repointed} 个角色；职业 id = " +
                  string.Join(" / ", System.Array.ConvertAll(Classes, c => c.EntityId)));
        return true;
    }

    private static ClassConfig Find(string characterId)
    {
        for (int i = 0; i < Classes.Length; i++)
        {
            if (Classes[i].CharacterId == characterId) return Classes[i];
        }

        return null;
    }

    private static void CopyStats(PlayerDataSO from, PlayerDataSO to)
    {
        to.MaxHealth = from.MaxHealth;
        to.MoveSpeed = from.MoveSpeed;
        to.PickRange = from.PickRange;
        to.UnbeatableTime = from.UnbeatableTime;
    }

    /// <summary>
    /// Player prefab 的两处改动：
    /// <list type="number">
    /// <item>清空 <c>entityConfig</c> —— 数值由角色的 <c>playerConfig</c> 在生成时注入；</item>
    /// <item>根节点设为**未激活** —— <c>Instantiate</c> 后 <c>Awake</c> 不跑，
    /// 注入才有机会成为 StatModel 的唯一来源。</item>
    /// </list>
    /// </summary>
    private static bool ConfigurePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerClassConfigSetup] 打不开 {PlayerPrefabPath}");
            return false;
        }

        try
        {
            var entity = root.GetComponent<EntityBehaviour>();
            if (entity == null)
            {
                Debug.LogError("[PlayerClassConfigSetup] Player prefab 上没有 EntityBehaviour。");
                return false;
            }

            var serialized = new SerializedObject(entity);
            SerializedProperty config = serialized.FindProperty("entityConfig");
            if (config == null)
            {
                Debug.LogError("[PlayerClassConfigSetup] 在 EntityBehaviour 上找不到 entityConfig 字段。");
                return false;
            }

            config.objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void DeleteLegacy(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) return;

        if (AssetDatabase.DeleteAsset(path)) Debug.Log($"[PlayerClassConfigSetup] 已删除共用资产: {path}");
        else Debug.LogWarning($"[PlayerClassConfigSetup] 删除失败（可能仍被引用）: {path}");
    }
}
#endif
