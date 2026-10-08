#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 幂等的 Addressables 自动配置脚本。
///
/// 作用：
/// - 创建/复用 AddressableAssetSettings 与 UI / Common / Weapon / Tower / Cart / Music 六个分组；
/// - 扫描 Assets/Game/Prefabs/{UI,Common,Weapon,Tower,Cart,Music}，按「组名/文件名」写入地址。
///
/// 地址规则必须与运行时 AssetKeys.cs 保持一致。
/// 手动运行：菜单 Tools ▸ Setup Addressables
/// 批处理运行：-executeMethod AddressablesSetup.SetupFromCommandLine
/// </summary>
public static class AddressablesSetup
{
    // ⚠️ 必须与实际资源目录一致。这组常量曾经是 "Assets/Prefabs/*"，而 prefab 早已搬到
    // Assets/Game/Prefabs/*：重跑本脚本时 Directory.Exists 全为 false，于是打印
    // 「已配置 0 个地址」并以退出码 0 结束 —— 跑过了但什么都没做，新增 prefab 不会被纳入 Addressables。
    // 改这里的路径时请连带确认 AssetKeys.cs 的地址规则。
    private const string UiDir = "Assets/Game/Prefabs/UI";
    private const string CommonDir = "Assets/Game/Prefabs/Common";
    private const string WeaponDir = "Assets/Game/Prefabs/Weapon";
    private const string TowerDir = "Assets/Game/Prefabs/Tower";
    private const string CartDir = "Assets/Game/Prefabs/Cart";
    private const string MusicDir = "Assets/Game/Prefabs/Music";

    /// <summary>联机 prefab（NetworkManager）。地址 <c>Net/&lt;文件名&gt;</c> —— 见 <c>AssetKeys.NetworkManager</c>。</summary>
    private const string NetDir = "Assets/Game/Prefabs/Net";

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

    /// <summary>
    /// 批处理构建 Addressables 内容（验收项：确认条目能真正打包出来）。
    ///
    /// <para>
    /// 它验证的是"地址建好了但打不出来"这类问题：条目漏进组、AssetReference 指向未打包的资产、
    /// 依赖里混入编辑器资源 —— 这些在编辑器 Play（Asset Database 模式）下**全部正常**，
    /// 只有真机包才会暴露。产物落在 <c>Library/com.unity.addressables</c>（不进版本库）。
    /// </para>
    /// 批处理运行：<c>-executeMethod AddressablesSetup.BuildContentFromCommandLine</c>
    /// </summary>
    public static void BuildContentFromCommandLine()
    {
        bool ok = false;
        try
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                Debug.LogError("[AddressablesSetup] 找不到 AddressableAssetSettings，无法构建内容");
            }
            else
            {
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

                // Addressables 2.x 的结果类型是 AddressablesPlayerBuildResult（不是 AsyncOperationHandle）：
                // 失败信息在 Error 里，空字符串表示成功
                ok = result != null && string.IsNullOrEmpty(result.Error);
                if (!ok)
                    Debug.LogError($"[AddressablesSetup] 内容构建失败：{result?.Error}");
                else
                    Debug.Log("[AddressablesSetup] 内容构建成功");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AddressablesSetup] 内容构建异常: {e}");
        }
        finally
        {
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

        // 先检查目录：缺失时 AddPrefabs/AddAudio 会静默返回 0，
        // 脚本照样打印「已配置 0 个地址」并成功退出 —— 这种「跑了但没做事」最难发现。
        bool allDirsExist = true;
        foreach (string dir in new[] { UiDir, CommonDir, WeaponDir, TowerDir, CartDir, MusicDir, NetDir })
        {
            if (Directory.Exists(dir)) continue;

            Debug.LogError($"[AddressablesSetup] 目录不存在：{dir} —— 请确认资源是否搬迁过；" +
                           "地址规则必须与运行时 AssetKeys.cs 保持一致。");
            allDirsExist = false;
        }
        if (!allDirsExist) return false;

        AddressableAssetGroup ui = EnsureGroup(settings, "UI");
        AddressableAssetGroup common = EnsureGroup(settings, "Common");
        AddressableAssetGroup weapon = EnsureGroup(settings, "Weapon");
        AddressableAssetGroup tower = EnsureGroup(settings, "Tower");
        AddressableAssetGroup cart = EnsureGroup(settings, "Cart");
        AddressableAssetGroup music = EnsureGroup(settings, "Music");
        AddressableAssetGroup net = EnsureGroup(settings, "Net");

        int count = 0;
        count += AddPrefabs(settings, ui, UiDir, "UI");
        count += AddPrefabs(settings, common, CommonDir, "Common");
        count += AddPrefabs(settings, weapon, WeaponDir, "Weapon");
        count += AddPrefabs(settings, tower, TowerDir, "Tower");
        count += AddPrefabs(settings, cart, CartDir, "Cart");
        count += AddPrefabs(settings, net, NetDir, "Net");
        count += AddAudio(settings, music, MusicDir, "Music");

        // 顺手清掉指向"已经不存在的资产"的条目。
        // 本脚本只增不删，于是删掉一个 prefab 之后组里会留下一条永远解析不到地址的条目 ——
        // 它不会报错，但会让 Analyze 的"缺失地址"告警一直挂着，久而久之没人再看那个告警。
        int pruned = PruneMissingEntries(settings);

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        EditorUtility.SetDirty(settings);
        Debug.Log($"[AddressablesSetup] 已配置 {count} 个地址，清理失效条目 {pruned} 个。");
        return true;
    }

    /// <summary>移除 GUID 已解析不到资产的条目（资产被删除/改名后留下的孤儿）。</summary>
    private static int PruneMissingEntries(AddressableAssetSettings settings)
    {
        var stale = new List<AddressableAssetEntry>();

        // 先收集再删除：RemoveAssetEntry 会改 group.entries，边遍历边删会漏掉相邻条目
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null) continue;
                if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(entry.guid))) continue;

                stale.Add(entry);
            }
        }

        for (int i = 0; i < stale.Count; i++)
        {
            Debug.LogWarning($"[AddressablesSetup] 移除失效条目「{stale[i].address}」" +
                             $"（GUID {stale[i].guid} 已解析不到资产）");
            settings.RemoveAssetEntry(stale[i].guid, false);
        }

        return stale.Count;
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
