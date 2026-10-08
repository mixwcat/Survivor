#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SO 资产字段体检：找出「YAML 里有、C# 类里没有」的**孤儿字段**。
///
/// <para>
/// <b>为什么需要它：</b>Unity 反序列化时**静默忽略**不认识的键（不报错、不告警），
/// 而它又**保留**这些键 —— 于是历史上删掉/改名的字段会一直留在资产里，
/// 造出一批「在 Inspector 里根本看不到、改了也不会有任何反应」的死数据。
/// 更麻烦的是它们会误导人：读到 <c>EntityName: Teto</c> 会以为改它有用。
/// </para>
///
/// <para>
/// 判据是「类 + 基类链上的序列化字段」（用反射取，含 <c>[SerializeField]</c> 私有字段），
/// 对比资产文本里**顶格两个空格**的键；<c>m_</c> 开头的 Unity 内置键不算。
/// </para>
///
/// <para>
/// 它只**读**资产，不修改任何东西。手动：Tools ▸ Audit SO Fields；
/// 批处理：<c>-executeMethod SoFieldAudit.AuditFromCommandLine</c>（发现问题时退出码 1）。
/// </para>
/// </summary>
public static class SoFieldAudit
{
    /// <summary>顶层字段行：两个空格缩进 + 键名 + 冒号。</summary>
    private static readonly Regex TopLevelKey = new Regex(@"^  ([A-Za-z_][A-Za-z0-9_]*):", RegexOptions.Compiled);

    [MenuItem("Tools/Audit SO Fields")]
    public static void AuditFromMenu()
    {
        Run(exitOnFinish: false);
    }

    public static void AuditFromCommandLine()
    {
        Run(exitOnFinish: true);
    }

    private static void Run(bool exitOnFinish)
    {
        var problems = new List<string>();
        int scanned = 0;
        int withOrphans = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) continue;

            // 只扫**项目自己**的 SO 类型。第三方与 Unity 内置资产有大量嵌套的可序列化结构
            // （URP 的 VolumeProfile、TMP 字体资产…），它们的子字段缩进恰好也是两格，
            // 会被当成「顶层孤儿字段」误报。项目代码无 namespace 且在 Assembly-CSharp(*)。
            string assemblyName = asset.GetType().Assembly.GetName().Name;
            if (assemblyName != "Assembly-CSharp" && assemblyName != "Assembly-CSharp-Editor") continue;

            scanned++;

            HashSet<string> known = GetSerializedFieldNames(asset.GetType());
            var orphans = new List<string>();

            foreach (string line in File.ReadAllLines(path))
            {
                Match match = TopLevelKey.Match(line);
                if (!match.Success) continue;

                string key = match.Groups[1].Value;
                if (key.StartsWith("m_", StringComparison.Ordinal)) continue;   // Unity 内置键
                if (known.Contains(key)) continue;

                orphans.Add(key);
            }

            if (orphans.Count == 0) continue;

            withOrphans++;
            problems.Add($"{path}（{asset.GetType().Name}）：{string.Join("、", orphans)}");
        }

        if (problems.Count == 0)
        {
            Debug.Log($"[SoFieldAudit] 通过：{scanned} 个 SO 资产，没有孤儿字段。");
        }
        else
        {
            Debug.LogWarning($"[SoFieldAudit] {scanned} 个 SO 资产中，{withOrphans} 个含孤儿字段" +
                             $"（在 Inspector 里看不到、改了不会有任何效果，属于历史残留）：\n" +
                             string.Join("\n", problems));
        }

        Debug.Log(problems.Count == 0 ? "[SoFieldAudit] CLI_OK" : "[SoFieldAudit] CLI_FAIL");

        if (exitOnFinish && Application.isBatchMode)
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
    }

    /// <summary>
    /// 类及其基类链上所有会被 Unity 序列化的字段名
    /// （<c>public</c> 实例字段，或标了 <c>[SerializeField]</c> 的私有字段；排除 <c>static</c>/<c>readonly</c>/<c>[NonSerialized]</c>）。
    /// </summary>
    private static HashSet<string> GetSerializedFieldNames(Type type)
    {
        var names = new HashSet<string>();

        for (Type t = type;
             t != null && t != typeof(ScriptableObject) && t != typeof(UnityEngine.Object) && t != typeof(object);
             t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic
                                                    | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;
                if (field.IsDefined(typeof(NonSerializedAttribute), false)) continue;

                if (field.IsPublic || field.IsDefined(typeof(SerializeField), false))
                    names.Add(field.Name);
            }
        }

        return names;
    }
}
#endif
