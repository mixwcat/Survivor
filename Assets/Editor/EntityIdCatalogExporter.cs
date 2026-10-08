#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 导出「带稳定 id 的资产」清单为 CSV（<c>Assets/EntityIdCatalog.csv</c>）。
///
/// <para>
/// <b>它导出什么：</b>所有 <see cref="BaseEntitySO"/> 子类资产（玩家 / 敌人 / 塔 / 武器 / 推车 / 角色），
/// 一行一个：类型、id、显示名、资产名与路径、数值 SO、prefab GUID、升级项数量。
/// 扫描口径与 <see cref="EntitySOValidator"/> 完全一致（都是 <c>t:BaseEntitySO</c>），
/// 所以"体检通过"和"清单齐全"说的是同一批资产。
/// </para>
///
/// <para>
/// <b>为什么是导出而不是维护一份手写表：</b>手写表一定会与资产漂移（新增一把武器忘了登记），
/// 而漂移的表现是"策划照着表配了个不存在的 id"。这里每次都是从资产现读，天然不会分叉。
/// </para>
///
/// <para>
/// 手动运行：Tools ▸ Export Entity Id Catalog
/// 批处理运行：<c>-executeMethod EntityIdCatalogExporter.ExportFromCommandLine</c>
/// </para>
/// </summary>
public static class EntityIdCatalogExporter
{
    /// <summary>对外显示的路径（日志里用，便于人去找）。</summary>
    private const string OutputRelativePath = "Assets/EntityIdCatalog.csv";

    /// <summary>
    /// 实际写入路径：用 <c>Application.dataPath</c> 拼绝对路径，**不依赖当前工作目录** ——
    /// 批处理模式下进程的工作目录不保证就是工程根，靠相对路径会写到别处去。
    /// </summary>
    private static string OutputPath => Path.Combine(Application.dataPath, "EntityIdCatalog.csv");

    private const string Header =
        "Class,Id,DisplayName,AssetName,AssetPath,DataRef,PrefabGuid,UpgradeCount";

    [MenuItem("Tools/Export Entity Id Catalog")]
    public static void ExportFromMenu()
    {
        Export();
        AssetDatabase.Refresh();
    }

    public static void ExportFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = Export();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EntityIdCatalogExporter] 异常: {e}");
        }
        finally
        {
            AssetDatabase.Refresh();
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool Export()
    {
        var assets = new List<BaseEntitySO>();

        foreach (string guid in AssetDatabase.FindAssets("t:BaseEntitySO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<BaseEntitySO>(path);
            if (so != null) assets.Add(so);
        }

        // 排序让 diff 稳定：同类型按 id 排
        assets.Sort((a, b) =>
        {
            int byClass = string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
            return byClass != 0 ? byClass : string.CompareOrdinal(a.id, b.id);
        });

        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');

        int written = 0;
        int skipped = 0;

        for (int i = 0; i < assets.Count; i++)
        {
            BaseEntitySO so = assets[i];

            // 只导出"含有 id"的资产；没有 id 的单独计数并在日志里报出来，
            // 免得它们从清单里静默消失（那种资产在存档/联机里根本没法引用）
            if (string.IsNullOrWhiteSpace(so.id))
            {
                skipped++;
                continue;
            }

            string path = AssetDatabase.GetAssetPath(so);
            sb.Append(Escape(so.GetType().Name)).Append(',')
              .Append(Escape(so.id)).Append(',')
              .Append(Escape(DisplayName(so))).Append(',')
              .Append(Escape(Path.GetFileNameWithoutExtension(path))).Append(',')
              .Append(Escape(path)).Append(',')
              .Append(Escape(DataRefName(so))).Append(',')
              .Append(Escape(PrefabGuid(so))).Append(',')
              .Append(UpgradeCount(so)).Append('\n');

            written++;
        }

        // UTF-8 **带 BOM**：不带的话中文显示名在 Excel 里是乱码（Unity 自身读 TextAsset 两者都行）
        File.WriteAllText(OutputPath, sb.ToString(), new UTF8Encoding(true));

        Debug.Log($"[EntityIdCatalogExporter] 已导出 {written} 条到 {OutputRelativePath}" +
                  $"（跳过 {skipped} 个没有 id 的资产；共扫描 {assets.Count} 个 BaseEntitySO）");
        return true;
    }

    /// <summary>显示名：各子类字段名统一叫 <c>displayName</c>（玩家/敌人没有这个字段，返回空）。</summary>
    private static string DisplayName(BaseEntitySO so)
    {
        switch (so)
        {
            case TowerEntitySO tower: return tower.displayName;
            case WeaponEntitySO weapon: return weapon.displayName;
            case CartEntitySO cart: return cart.displayName;
            case CharacterDefinitionSO character: return character.displayName;
            default: return string.Empty;
        }
    }

    /// <summary>数值 SO 的资产名（DataSO 文件名，与 id 不同：它是"数值在哪份资产里"）。</summary>
    private static string DataRefName(BaseEntitySO so)
    {
        if (so.dataRef == null) return string.Empty;

        string path = AssetDatabase.GetAssetPath(so.dataRef);
        return string.IsNullOrEmpty(path) ? so.dataRef.name : Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// prefab 的 GUID。两类引用形态不同：塔/武器/推车/角色用 <see cref="AssetReferenceGameObject"/>，
    /// 敌人用直接引用（<c>GameObject</c>）—— 后者是历史写法，这里一并解析出来，别让它在清单里缺列。
    /// </summary>
    private static string PrefabGuid(BaseEntitySO so)
    {
        switch (so)
        {
            case TowerEntitySO tower: return AssetGuid(tower.prefab);
            case WeaponEntitySO weapon: return AssetGuid(weapon.prefab);
            case CartEntitySO cart: return AssetGuid(cart.prefab);
            case CharacterDefinitionSO character: return AssetGuid(character.prefab);
            case EnemyEntitySO enemy: return DirectGuid(enemy.prefab);
            default: return string.Empty;
        }
    }

    private static string AssetGuid(AssetReferenceGameObject reference)
    {
        return reference != null && reference.RuntimeKeyIsValid() ? reference.AssetGUID : string.Empty;
    }

    private static string DirectGuid(GameObject prefab)
    {
        if (prefab == null) return string.Empty;

        string path = AssetDatabase.GetAssetPath(prefab);
        return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
    }

    private static int UpgradeCount(BaseEntitySO so)
    {
        return so.upgrades != null ? so.upgrades.Count : 0;
    }

    /// <summary>CSV 转义：含逗号/引号/换行的字段整体加引号，内部引号翻倍。</summary>
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        bool needsQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 ||
                          value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;

        return needsQuote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
#endif
