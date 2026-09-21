#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 幂等设置实体预制体上的 <see cref="EntityBehaviour.entityType"/>。
///
/// 背景：塔预制体在迁移时漏配，`entityType` 保持默认值 <see cref="EntityType.Player"/>，
/// 导致塔读取到玩家配置（缺少 TowerAttackRange 等）。
/// 手动运行：Tools ▸ Setup Entity Prefabs
/// 批处理运行：-executeMethod EntityPrefabSetup.SetupFromCommandLine
/// </summary>
public static class EntityPrefabSetup
{
    private static readonly (string Path, EntityType Type)[] Targets =
    {
        ("Assets/Prefabs/Tower/Tower_Teto.prefab", EntityType.TowerTeto),
        ("Assets/Prefabs/Tower/Tower_Rin.prefab", EntityType.TowerRin),
        ("Assets/Prefabs/Tower/Tower_Luo.prefab", EntityType.TowerLuo),
    };

    [MenuItem("Tools/Setup Entity Prefabs")]
    public static void SetupFromMenu()
    {
        bool ok = Run();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[EntityPrefabSetup] CLI_OK" : "[EntityPrefabSetup] CLI_FAIL");
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
            Debug.LogError($"[EntityPrefabSetup] 异常: {e}");
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

    private static bool Run()
    {
        bool allOk = true;

        foreach ((string path, EntityType type) in Targets)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[EntityPrefabSetup] 找不到 {path}");
                allOk = false;
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                EntityBehaviour behaviour = root.GetComponentInChildren<EntityBehaviour>(true);
                if (behaviour == null)
                {
                    Debug.LogWarning($"[EntityPrefabSetup] {path} 中没有 EntityBehaviour");
                    allOk = false;
                    continue;
                }

                SerializedObject so = new SerializedObject(behaviour);
                SerializedProperty prop = so.FindProperty("entityType");
                if (prop == null)
                {
                    Debug.LogWarning($"[EntityPrefabSetup] {path} 找不到 entityType 字段");
                    allOk = false;
                    continue;
                }

                if (prop.intValue == (int)type)
                {
                    Debug.Log($"[EntityPrefabSetup] {path} 已是 {type}（幂等跳过）");
                    continue;
                }

                prop.intValue = (int)type;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[EntityPrefabSetup] {path} entityType -> {type}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return allOk;
    }
}
#endif
