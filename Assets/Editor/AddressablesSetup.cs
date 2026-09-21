#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 幂等的 Addressables 自动配置脚本。
///
/// 作用：
/// - 创建/复用 AddressableAssetSettings 与 UI / Common / Weapon / Music 四个分组；
/// - 扫描 Assets/Prefabs/{UI,Common,Weapon,Music}，按「组名/文件名」写入地址。
///
/// 地址规则必须与运行时 AssetKeys.cs 保持一致。
/// 手动运行：菜单 Tools ▸ Setup Addressables
/// 批处理运行：-executeMethod AddressablesSetup.SetupFromCommandLine
/// </summary>
public static class AddressablesSetup
{
    private const string UiDir = "Assets/Prefabs/UI";
    private const string CommonDir = "Assets/Prefabs/Common";
    private const string WeaponDir = "Assets/Prefabs/Weapon";
    private const string MusicDir = "Assets/Prefabs/Music";

    private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".ogg", ".aif", ".aiff" };

    [MenuItem("Tools/Setup Addressables")]
    public static void SetupFromMenu()
    {
        bool ok = Setup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[AddressablesSetup] CLI_OK (menu)" : "[AddressablesSetup] CLI_FAIL (menu)");
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
            Debug.LogError($"[AddressablesSetup] 异常: {e}");
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
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("[AddressablesSetup] 无法获取或创建 AddressableAssetSettings");
            return false;
        }

        // Editor Play 使用「Use Asset Database (fastest)」（index 0），
        // 免去每次改资源都要 Build Addressables；正式打包仍由 Player Build 流程处理。
        settings.ActivePlayModeDataBuilderIndex = 0;

        AddressableAssetGroup ui = EnsureGroup(settings, "UI");
        AddressableAssetGroup common = EnsureGroup(settings, "Common");
        AddressableAssetGroup weapon = EnsureGroup(settings, "Weapon");
        AddressableAssetGroup music = EnsureGroup(settings, "Music");

        int count = 0;
        count += AddPrefabs(settings, ui, UiDir, "UI");
        count += AddPrefabs(settings, common, CommonDir, "Common");
        count += AddPrefabs(settings, weapon, WeaponDir, "Weapon");
        count += AddAudio(settings, music, MusicDir, "Music");

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        EditorUtility.SetDirty(settings);
        Debug.Log($"[AddressablesSetup] 已配置 {count} 个地址。");
        return true;
    }

    private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name)
    {
        AddressableAssetGroup group = settings.FindGroup(name);
        if (group != null) return group;

        var schemas = new List<AddressableAssetGroupSchema>();
        if (settings.DefaultGroup != null)
            schemas.AddRange(settings.DefaultGroup.Schemas);

        return settings.CreateGroup(name, false, false, false, schemas);
    }

    private static int AddPrefabs(AddressableAssetSettings settings, AddressableAssetGroup group, string dir, string prefix)
    {
        if (!Directory.Exists(dir)) return 0;

        int added = 0;
        foreach (string file in Directory.GetFiles(dir, "*.prefab"))
        {
            string path = file.Replace("\\", "/");
            string name = Path.GetFileNameWithoutExtension(path);
            if (AddEntry(settings, group, path, $"{prefix}/{name}")) added++;
        }
        return added;
    }

    private static int AddAudio(AddressableAssetSettings settings, AddressableAssetGroup group, string dir, string prefix)
    {
        if (!Directory.Exists(dir)) return 0;

        int added = 0;
        foreach (string file in Directory.GetFiles(dir))
        {
            string path = file.Replace("\\", "/");
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (System.Array.IndexOf(AudioExtensions, ext) < 0) continue;

            string name = Path.GetFileNameWithoutExtension(path);
            if (AddEntry(settings, group, path, $"{prefix}/{name}")) added++;
        }
        return added;
    }

    private static bool AddEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string address)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[AddressablesSetup] 找不到 GUID: {assetPath}");
            return false;
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        if (entry == null) return false;

        entry.SetAddress(address, false);
        return true;
    }
}
#endif
