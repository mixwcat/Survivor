#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 补齐武器**自己的**升级项（幂等）。
///
/// <para>
/// 武器是独立实体：升级写的是**武器自己**的 StatModel（<c>WeaponDataSO</c> 灌进去的那一份），
/// 与玩家/塔的数值互不影响。Gun 与 Spin 早就有 3 项，Cannon 与 Laser 一直是空的 ——
/// 面板上表现为"这两把武器没有可升级的东西"，而没有任何报错。
/// </para>
///
/// <para>
/// <b>为什么用工具：</b>升级项是"SO 引用 SO"，手工拖 5 个资产容易漏；
/// 而且 <c>TargetStat</c> 必须与武器实际读取的 <c>StatType</c> 对齐
/// （Cannon 的爆炸范围读的是 <c>ShellExplosionRadius</c>，不是某个通用 Radius）——
/// 写错只会"升了没效果"。表里把对应关系写死，改攻击方式时一起看。
/// </para>
///
/// <para>手动：Tools ▸ Setup Weapon Upgrades　批处理：<c>-executeMethod WeaponUpgradeSetup.SetupFromCommandLine</c></para>
/// </summary>
public static class WeaponUpgradeSetup
{
    private const string UpgradeRoot = "Assets/Game/SO/LevUpSO/PlayerLevelUp";
    private const string IconSourcePath = "Assets/Game/SO/LevUpSO/PlayerLevelUp/GunWeapon/ShootGunDamage.asset";

    /// <summary>一项升级的配置：资产名 / 文案 / 目标属性 / 数值 / 修改方式。</summary>
    private readonly struct Entry
    {
        public readonly string AssetName;
        public readonly string Text;
        public readonly StatType Stat;
        public readonly float Value;
        public readonly EModifierType ModifierType;

        public Entry(string assetName, string text, StatType stat, float value, EModifierType modifierType)
        {
            AssetName = assetName;
            Text = text;
            Stat = stat;
            Value = value;
            ModifierType = modifierType;
        }
    }

    /// <summary>武器 EntitySO → 升级项目录 + 要补的项（已存在同名资产则只做接线）。</summary>
    private static readonly (string EntitySoPath, string Folder, Entry[] Entries)[] Plan =
    {
        ("Assets/Game/SO/EntitySO/Weapon_Cannon.asset", "CannonWeapon", new[]
        {
            new Entry("CannonDamage", "提高炮弹伤害", StatType.Damage, 8f, EModifierType.Add),
            new Entry("CannonAttackSpeed", "提高开火频率", StatType.AttackSpeed, 0.25f, EModifierType.Multiply),
            new Entry("CannonSplashRadius", "扩大爆炸范围", StatType.ShellExplosionRadius, 0.5f, EModifierType.Add),
        }),
        ("Assets/Game/SO/EntitySO/Weapon_Laser.asset", "LaserWeapon", new[]
        {
            new Entry("LaserDamage", "提高激光伤害", StatType.Damage, 3f, EModifierType.Add),
            new Entry("LaserAttackSpeed", "提高照射频率", StatType.AttackSpeed, 0.25f, EModifierType.Multiply),
        }),
    };

    [MenuItem("Tools/Setup Weapon Upgrades")]
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
            Debug.LogError($"[WeaponUpgradeSetup] 异常: {e}");
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
        Sprite icon = AssetDatabase.LoadAssetAtPath<LevelUpSO>(IconSourcePath)?.levelUpSprite;

        for (int i = 0; i < Plan.Length; i++)
        {
            if (!ApplyWeapon(Plan[i].EntitySoPath, Plan[i].Folder, Plan[i].Entries, icon)) return false;
        }

        AssetDatabase.SaveAssets();
        return true;
    }

    private static bool ApplyWeapon(string entitySoPath, string folder, Entry[] entries, Sprite icon)
    {
        string folderPath = $"{UpgradeRoot}/{folder}";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(UpgradeRoot, folder);
            Debug.Log($"[WeaponUpgradeSetup] 新建目录 {folderPath}");
        }

        var entitySo = AssetDatabase.LoadAssetAtPath<ScriptableObject>(entitySoPath);
        if (entitySo == null)
        {
            Debug.LogError($"[WeaponUpgradeSetup] 打不开 {entitySoPath}");
            return false;
        }

        var serialized = new SerializedObject(entitySo);
        SerializedProperty upgrades = serialized.FindProperty("upgrades");
        if (upgrades == null || !upgrades.isArray)
        {
            Debug.LogError($"[WeaponUpgradeSetup] {entitySoPath} 上找不到 upgrades 数组");
            return false;
        }

        int added = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            LevelUpSO asset = EnsureAsset(folderPath, entries[i], icon);
            if (asset == null) return false;

            if (Contains(upgrades, asset)) continue;

            int index = upgrades.arraySize;
            upgrades.InsertArrayElementAtIndex(index);
            upgrades.GetArrayElementAtIndex(index).objectReferenceValue = asset;
            added++;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[WeaponUpgradeSetup] {System.IO.Path.GetFileName(entitySoPath)}：" +
                  $"补了 {added} 项，共 {upgrades.arraySize} 项");
        return true;
    }

    private static LevelUpSO EnsureAsset(string folderPath, in Entry entry, Sprite icon)
    {
        string path = $"{folderPath}/{entry.AssetName}.asset";

        LevelUpSO existing = AssetDatabase.LoadAssetAtPath<LevelUpSO>(path);
        if (existing != null) return existing;

        var so = ScriptableObject.CreateInstance<LevelUpSO>();
        so.levelUpText = entry.Text;
        so.cost = 1;
        so.levelUpSprite = icon;
        so.statModifiers = new List<StatModifierData>
        {
            new StatModifierData
            {
                TargetStat = entry.Stat,
                Value = entry.Value,
                ModifierType = entry.ModifierType,
            },
        };

        AssetDatabase.CreateAsset(so, path);
        Debug.Log($"[WeaponUpgradeSetup] 新建升级项 {entry.AssetName}（{entry.Stat} {entry.Value}）");
        return so;
    }

    private static bool Contains(SerializedProperty array, Object target)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == target) return true;
        }

        return false;
    }
}
#endif
