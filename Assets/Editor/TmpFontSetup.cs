#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TMP 字体接线工具（幂等，可重复运行）。
///
/// <para>
/// 解决的是同一个坑的两种形态：**TMP 组件用着默认字体（LiberationSans SDF，没有中文字形）**，
/// 运行时把中文写进去就变成一排 □，并刷出
/// <c>The character with Unicode value \uXXXX was not found in the [LiberationSans SDF] font asset</c>。
/// 中文是**运行时**写进去的（"金币 12"、"点数不足"），所以光看 prefab 里的占位文本（"1"）看不出来。
/// </para>
///
/// <list type="number">
/// <item>把中文字体挂成默认字体的 <b>fallback</b> —— 兜底，任何漏改的 TMP 也能显示中文；</item>
/// <item>把 prefab 里仍用默认字体的 TMP 组件换成中文字体 —— 正解（同一段文字里数字与汉字字体一致）。</item>
/// </list>
///
/// <para>
/// 手动运行：Tools ▸ Setup TMP Chinese Font
/// 批处理运行：<c>-executeMethod TmpFontSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class TmpFontSetup
{
    /// <summary>要扫描的 prefab 根目录（实体 prefab 里也有 TMP，例如伤害数字）。</summary>
    private const string PrefabRoot = "Assets/Game/Prefabs";

    private const string DefaultFontPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private const string ChineseFontPath =
        "Assets/Art/_External/_Udemy Vampire Survival Assets/Fonts/Kenney Fonts/BaDingShiWeiTi Title.asset";

    [MenuItem("Tools/Setup TMP Chinese Font")]
    public static void SetupFromMenu()
    {
        bool ok = Run();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[TmpFontSetup] CLI_OK (menu)" : "[TmpFontSetup] CLI_FAIL (menu)");
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
            Debug.LogError($"[TmpFontSetup] 异常: {e}");
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
        TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
        TMP_FontAsset chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);

        if (defaultFont == null)
        {
            Debug.LogError($"[TmpFontSetup] 找不到默认字体：{DefaultFontPath}");
            return false;
        }

        if (chineseFont == null)
        {
            Debug.LogError($"[TmpFontSetup] 找不到中文字体：{ChineseFontPath}");
            return false;
        }

        EnsureFallback(defaultFont, chineseFont);

        List<string> prefabs = CollectPrefabs(PrefabRoot);
        int changedPrefabs = 0;
        int changedComponents = 0;

        for (int i = 0; i < prefabs.Count; i++)
        {
            int changed = SwapInPrefab(prefabs[i], defaultFont, chineseFont);
            if (changed <= 0) continue;

            changedPrefabs++;
            changedComponents += changed;
        }

        Debug.Log($"[TmpFontSetup] 完成：{prefabs.Count} 个 prefab 中，" +
                  $"{changedPrefabs} 个共 {changedComponents} 处 TMP 从默认字体换成了「{chineseFont.name}」。" +
                  $"（默认字体：{defaultFont.name}）");
        return true;
    }

    /// <summary>把中文字体挂成默认字体的 fallback：漏改的 TMP 也不会显示成 □。</summary>
    private static void EnsureFallback(TMP_FontAsset defaultFont, TMP_FontAsset chineseFont)
    {
        var so = new SerializedObject(defaultFont);
        SerializedProperty table = so.FindProperty("m_FallbackFontAssetTable");

        if (table == null)
        {
            Debug.LogWarning("[TmpFontSetup] 默认字体上没有 m_FallbackFontAssetTable 字段，跳过 fallback 配置。");
            return;
        }

        for (int i = 0; i < table.arraySize; i++)
        {
            if (table.GetArrayElementAtIndex(i).objectReferenceValue == chineseFont)
            {
                Debug.Log($"[TmpFontSetup] {defaultFont.name} 已经挂了 fallback「{chineseFont.name}」，跳过。");
                return;
            }
        }

        table.InsertArrayElementAtIndex(table.arraySize);
        table.GetArrayElementAtIndex(table.arraySize - 1).objectReferenceValue = chineseFont;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(defaultFont);

        Debug.Log($"[TmpFontSetup] 已把「{chineseFont.name}」挂成 {defaultFont.name} 的 fallback。");
    }

    /// <summary>把单个 prefab 里仍用默认字体的 TMP 换成中文字体，返回改了几处。</summary>
    private static int SwapInPrefab(string path, TMP_FontAsset defaultFont, TMP_FontAsset chineseFont)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            int changed = 0;

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || text.font != defaultFont) continue;

                text.font = chineseFont;

                // 显式同步材质：字体换了但材质还指着旧图集的话，文字会渲染成空白/花屏，
                // 而且 prefab 里看不出来（字段都有值）
                if (chineseFont.material != null) text.fontSharedMaterial = chineseFont.material;

                changed++;
            }

            if (changed == 0) return 0;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[TmpFontSetup] {path}：{changed} 处 TMP 换成「{chineseFont.name}」");
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static List<string> CollectPrefabs(string root)
    {
        var result = new List<string>();
        if (!Directory.Exists(root)) return result;

        foreach (string file in Directory.GetFiles(root, "*.prefab", SearchOption.AllDirectories))
            result.Add(file.Replace("\\", "/"));

        return result;
    }
}
#endif
