#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 强制重新导入 InputSystem_Actions.inputactions，触发包装类 InputSystem_Actions.cs 重新生成。
/// 手动运行：Tools ▸ Reimport Input Actions
/// 批处理运行：-executeMethod InputActionsSetup.ReimportFromCommandLine
/// </summary>
public static class InputActionsSetup
{
    private const string AssetPath = "Assets/Script/InputSystem/InputSystem_Actions.inputactions";

    [MenuItem("Tools/Reimport Input Actions")]
    public static void ReimportFromMenu()
    {
        bool ok = Reimport();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[InputActionsSetup] CLI_OK" : "[InputActionsSetup] CLI_FAIL");
    }

    public static void ReimportFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = Reimport();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InputActionsSetup] 异常: {e}");
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

    private static bool Reimport()
    {
        if (!File.Exists(AssetPath))
        {
            Debug.LogWarning($"[InputActionsSetup] 找不到 {AssetPath}");
            return false;
        }

        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        Debug.Log($"[InputActionsSetup] 已重新导入 {AssetPath}");
        return true;
    }
}
#endif
