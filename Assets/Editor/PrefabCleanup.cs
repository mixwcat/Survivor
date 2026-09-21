#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一次性清理：从 Start.prefab 移除 <see cref="InputReaderManager"/>。
///
/// 原因：Start.prefab 同时挂了 Main 与 InputReaderManager，而 InputReaderManager 跨场景常驻；
/// 再次进入菜单场景时新实例会被 ManagerSingleton 判定为重复并销毁整个 GameObject，
/// 连带 Main 一起消失，导致菜单不再显示。InputReaderManager 现由 InputHandleFactory 按需自建。
/// </summary>
public static class PrefabCleanup
{
    private const string StartPrefabPath = "Assets/Game/Prefabs/Start.prefab";

    [MenuItem("Tools/Cleanup Start Prefab")]
    public static void CleanFromMenu()
    {
        bool ok = Clean();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[PrefabCleanup] CLI_OK" : "[PrefabCleanup] CLI_FAIL");
    }

    public static void CleanFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = Clean();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PrefabCleanup] 异常: {e}");
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

    private static bool Clean()
    {
        if (!File.Exists(StartPrefabPath))
        {
            Debug.LogWarning($"[PrefabCleanup] 找不到 {StartPrefabPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(StartPrefabPath);
        try
        {
            InputReaderManager manager = root.GetComponent<InputReaderManager>();
            if (manager == null)
            {
                Debug.Log("[PrefabCleanup] Start.prefab 已无 InputReaderManager（幂等跳过）");
                return true;
            }

            Object.DestroyImmediate(manager, true);
            PrefabUtility.SaveAsPrefabAsset(root, StartPrefabPath);
            Debug.Log("[PrefabCleanup] 已从 Start.prefab 移除 InputReaderManager");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
