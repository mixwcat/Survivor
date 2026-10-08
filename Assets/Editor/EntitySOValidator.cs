#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EntitySO 资产体检：id 缺失 / 重复 / 数值引用缺失。
///
/// 这是旧 `EntityCatalogSO.OnValidate`（枚举查重）的替代品——
/// 迁移到「直接引用 + 稳定 id」之后，唯一还需要全库校验的就是 id 唯一性。
/// 它只**读**资产、不复制任何数据，所以不会像注册表那样与真实引用分叉。
///
/// 手动：Tools ▸ Validate Entity SOs
/// 批处理：-executeMethod EntitySOValidator.ValidateFromCommandLine
/// </summary>
public static class EntitySOValidator
{
    private sealed class Entry
    {
        public BaseEntitySO Asset;
        public string Path;
    }

    /// <summary>id 允许的形态：小写字母开头，后跟小写字母 / 数字 / 下划线。</summary>
    private static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    [MenuItem("Tools/Validate Entity SOs")]
    public static void ValidateFromMenu()
    {
        Run(exitOnFinish: false);
    }

    public static void ValidateFromCommandLine()
    {
        Run(exitOnFinish: true);
    }

    private static void Run(bool exitOnFinish)
    {
        var problems = new List<string>();
        var byId = new Dictionary<string, Entry>();
        var all = new List<Entry>();

        foreach (string guid in AssetDatabase.FindAssets("t:BaseEntitySO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BaseEntitySO so = AssetDatabase.LoadAssetAtPath<BaseEntitySO>(path);
            if (so == null) continue;

            all.Add(new Entry { Asset = so, Path = path });
        }

        foreach (Entry entry in all)
        {
            BaseEntitySO so = entry.Asset;

            if (string.IsNullOrWhiteSpace(so.id))
            {
                problems.Add($"id 为空: {entry.Path}");
                continue;
            }

            if (byId.TryGetValue(so.id, out Entry existing))
            {
                problems.Add($"id 重复: \"{so.id}\" → {existing.Path} 与 {entry.Path}");
                continue;
            }

            byId[so.id] = entry;
        }

        foreach (Entry entry in all)
        {
            ValidateIdFormat(entry, problems);
        }

        foreach (Entry entry in all)
        {
            if (entry.Asset.dataRef == null)
                problems.Add($"dataRef 为空: {entry.Path}");
        }

        ValidateCharacterConfigs(all, problems);

        ValidateLevelUps(problems);

        int weaponCount = ValidateWeapons(all, problems);

        bool ok = problems.Count == 0;
        if (ok)
        {
            // 把"查了几把武器"写进成功文案：否则一条静默的空循环（比如类型判断写错）
            // 与"全部通过"在日志上长得一模一样
            Debug.Log($"[EntitySOValidator] 通过：{all.Count} 个 EntitySO" +
                      $"（含 {weaponCount} 把武器的数值/资源体检），id 唯一且完整。");
        }
        else
        {
            Debug.LogWarning($"[EntitySOValidator] 发现 {problems.Count} 个问题：\n{string.Join("\n", problems)}");
        }

        Debug.Log(ok ? "[EntitySOValidator] CLI_OK" : "[EntitySOValidator] CLI_FAIL");

        if (exitOnFinish && Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    /// <summary>
    /// id 的**格式**与**前缀**校验。
    ///
    /// <para>
    /// id 是跨边界（存档 / 联机同步 / 跨系统引用）的稳定标识，所以格式必须机器可校验：
    /// 全小写、下划线分隔、不含空格与大写（大小写不敏感的系统会把 <c>Tower_Teto</c> 与
    /// <c>tower_teto</c> 当成两个 id）。
    /// </para>
    /// <para>
    /// 前缀与实体类型绑定，是为了防「新加敌人时把 id 写成 <c>tower_xxx</c>」这类混用 ——
    /// 唯一性校验发现不了它，但存档与网络消息里会留下一个类型错误的标识。
    /// </para>
    /// </summary>
    private static void ValidateIdFormat(Entry entry, List<string> problems)
    {
        string id = entry.Asset.id;
        if (string.IsNullOrEmpty(id)) return;   // 空 id 已在别处报过

        if (!IdPattern.IsMatch(id))
        {
            problems.Add($"id 格式不合法（要求 ^[a-z][a-z0-9_]*$，全小写 + 下划线）: \"{id}\" @ {entry.Path}");
        }

        string prefix = ExpectedIdPrefix(entry.Asset);
        if (prefix != null && !id.StartsWith(prefix, StringComparison.Ordinal))
        {
            problems.Add($"id 前缀与实体类型不符（{entry.Asset.GetType().Name} 应以 \"{prefix}\" 开头）: " +
                         $"\"{id}\" @ {entry.Path}");
        }
    }

    /// <summary>各实体类型期望的 id 前缀；未分类的类型返回 null（不校验前缀）。</summary>
    private static string ExpectedIdPrefix(BaseEntitySO so)
    {
        if (so is PlayerEntitySO) return "player";
        if (so is EnemyEntitySO) return "enemy_";
        if (so is TowerEntitySO) return "tower_";
        if (so is WeaponEntitySO) return "weapon_";
        return null;
    }

    /// <summary>
    /// 角色体检：<see cref="CharacterDefinitionSO.playerConfig"/> 必须存在，且与角色自己的
    /// <c>dataRef</c> 指向**同一份** DataSO。
    ///
    /// <para>
    /// <b>为什么查"同一份"：</b>角色的数值有两个入口 —— <c>dataRef</c>（角色 SO 上声明"我的数值资产"）
    /// 与 <c>playerConfig.dataRef</c>（真正注入给玩家、决定实际数值的那份）。
    /// 两者指向不同资产时，面板/清单上看到的数值与实际生效的数值会分叉，
    /// 而症状只是"改了没反应"。
    /// </para>
    /// </summary>
    private static void ValidateCharacterConfigs(List<Entry> all, List<string> problems)
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Asset is not CharacterDefinitionSO character) continue;

            string path = all[i].Path;

            if (character.playerConfig == null)
            {
                problems.Add($"角色未配置 playerConfig（生成玩家时没有数值来源，玩家会以全 1 出场）: {path}");
                continue;
            }

            if (character.playerConfig.dataRef == null)
            {
                problems.Add($"角色的 playerConfig「{character.playerConfig.name}」没有 dataRef: {path}");
                continue;
            }

            if (character.dataRef != character.playerConfig.dataRef)
            {
                problems.Add($"角色的 dataRef 与 playerConfig.dataRef 指向不同资产" +
                             $"（{Name(character.dataRef)} vs {Name(character.playerConfig.dataRef)}）: {path}");
            }
        }
    }

    /// <summary>
    /// 武器体检：<c>attack</c> / <c>prefab</c> 必须存在；攻击方式读取的数值必须由 <c>dataRef</c>
    /// 提供；需要资源的攻击方式还必须在 prefab 上接好投射物。
    ///
    /// <para>
    /// <b>这是 <c>AttackDriver.ValidateRequiredStats</c> 的静态版。</b>运行时那条要等到
    /// 装配 + <c>Start</c> 才报红错，而这两类错误（漏一个数值、漏接投射物）都是**纯数据关系** ——
    /// 不需要实例化任何东西就能查出来，没理由让它们活到进游戏那一刻。
    /// 尤其是"漏接投射物"：运行时的表现是**武器永不开火且一条日志都没有**。
    /// </para>
    ///
    /// <para>
    /// 判据与运行时保持一致（都用 <c>FillStatModel</c> 灌一份临时模型再 <c>HasStat</c>），
    /// 不复制一份"哪些 DataSO 提供哪些数值"的清单 —— 那种清单迟早与实现分叉。
    /// </para>
    /// </summary>
    /// <returns>实际体检过的武器数量（用于把"查了几把"写进成功文案）。</returns>
    private static int ValidateWeapons(List<Entry> all, List<string> problems)
    {
        int checkedCount = 0;

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Asset is not WeaponEntitySO weapon) continue;

            checkedCount++;
            string path = all[i].Path;

            if (weapon.attack == null)
            {
                problems.Add($"武器未配置 attack（该武器不会攻击）: {path}");
                continue;   // 没有攻击方式，就无从校验它读哪些数值
            }

            if (weapon.dataRef == null)
            {
                problems.Add($"武器的 dataRef 为空（数值来源缺失）: {path}");
            }
            else
            {
                var model = new EntityStatModel();
                weapon.dataRef.FillStatModel(model);

                StatType[] required = weapon.attack.RequiredStats;
                var missing = new List<string>();
                if (required != null)
                {
                    for (int j = 0; j < required.Length; j++)
                    {
                        if (!model.HasStat(required[j])) missing.Add(required[j].ToString());
                    }
                }

                if (missing.Count > 0)
                {
                    problems.Add($"攻击方式「{weapon.attack.name}」需要 {string.Join("、", missing)}，" +
                                 $"但 {weapon.dataRef.name} 没有提供（运行时这些数值会退化成 1）: {path}");
                }
            }

            if (weapon.prefab == null || !weapon.prefab.RuntimeKeyIsValid())
            {
                problems.Add($"武器未配置 prefab（装配时无法实例化）: {path}");
                continue;
            }

            // 不需要资源的攻击方式（范围伤害 / 治疗 / 光束）到此为止
            if (!weapon.attack.RequiresPrefab) continue;

            string prefabPath = AssetDatabase.GUIDToAssetPath(weapon.prefab.AssetGUID);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            AttackDriver driver = prefab != null ? prefab.GetComponent<AttackDriver>() : null;

            if (driver == null)
            {
                problems.Add($"武器 prefab 上没有 AttackDriver（装配时会报错）: {prefabPath}");
                continue;
            }

            var projectile = driver.ProjectilePrefab;
            if (projectile == null || !projectile.RuntimeKeyIsValid())
            {
                problems.Add($"攻击方式「{weapon.attack.name}」需要投射物/召唤物 prefab，" +
                             $"但 {prefabPath} 上没有接（该武器不会开火且不报错）: {path}");
            }
        }

        return checkedCount;
    }

    private static string Name(UnityEngine.Object asset)
    {
        return asset != null ? asset.name : "null";
    }

    /// <summary>
    /// 升级资产体检：每个 <see cref="LevelUpSO"/> 至少要有效果。
    ///
    /// <para>
    /// <c>statModifiers</c> 为空、<c>fullHeal</c> 与 <c>bonusHeal</c> 又都为 0 的升级，
    /// 玩家能抽到、能买、点数照扣，但**什么都不发生**且没有任何日志 ——
    /// 面板一切正常，是最难排查的一类配置错误。
    /// </para>
    /// <para>
    /// 这条校验是补历史的账：项目里已经有过一批这样的资产（塔的 13 条升级中 12 条为空），
    /// 靠人肉读 YAML 才发现。加进来之后同类问题在 CI 就会被拦住。
    /// </para>
    /// </summary>
    private static void ValidateLevelUps(List<string> problems)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:LevelUpSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelUpSO so = AssetDatabase.LoadAssetAtPath<LevelUpSO>(path);
            if (so == null) continue;

            bool hasStatModifier = so.statModifiers != null && so.statModifiers.Count > 0;
            if (hasStatModifier || so.fullHeal || so.bonusHeal > 0f) continue;

            // 效果不在数值上的升级（如塔的"改装切换武器"）自己声明豁免 ——
            // 不豁免会把"有效果但不写数值"误报成空升级，假阳性多了体检就没人看了
            if (so.HasCustomEffect) continue;

            problems.Add($"升级无任何效果（statModifiers 为空且无回血）: {path}");
        }
    }
}
#endif
