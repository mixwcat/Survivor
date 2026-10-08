#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 把塔的攻击**武器化**：为每种塔攻击生成独立的武器 prefab + <see cref="WeaponEntitySO"/> +
/// <see cref="TowerWeaponDataSO"/>，并把塔 prefab 改成"挂武器 + 用控制器切换"（幂等）。
///
/// <para>
/// <b>为什么用工具而不是手搭：</b>每把武器要接 4 处引用（攻击方式 SO、投射物 AssetReference、
/// data、EntitySO），漏一处就是"塔不打人"或"数值全默认"；而且数值必须从塔原来的
/// <c>TowerDataSO</c> **逐项搬过来**（炮弹读的是 <c>ShellDamage</c>、治疗读的是 <c>HealAmount</c>），
/// 搬错只会"打出来伤害不对"。表里把这些对应关系写死。
/// </para>
///
/// <para>
/// <b>本工具做四件事：</b>
/// ① 建 <c>TowerWeaponDataSO</c>（数值从塔 data 复制）；② 建武器 prefab（<c>BaseWeapon</c> +
/// <c>AttackDriver</c> + 索敌圈 + <c>TowerWeaponRangeSync</c>，**不接 <c>_attack</c>**）；
/// ③ 建 <c>WeaponEntitySO</c>（含 <c>attack</c> —— 行为写在这里，装配时注入 driver）；
/// ④ 塔 prefab 上挂 <c>TowerWeaponController</c>（填武器列表）并**移除塔身上的 AttackDriver**。
/// </para>
///
/// <para>
/// <b>塔身上不再有索敌圈</b>（2026-10 迁移）：<c>SearchRange</c> 已删除，<c>BaseTower</c> 也不再持有
/// <c>_detectionCollider</c>。索敌完全随武器走 —— 每把武器自带检测圈（layer <c>TowerDetector</c>），
/// 半径来自**武器自己的** <c>StatType.AttackRange</c>（由 <c>TowerWeaponRangeSync</c> 写入）。
/// 本工具因此**不再创建/维护塔侧的检测圈**。
/// </para>
///
/// <para>手动：Tools ▸ Setup Tower Weapons　批处理：<c>-executeMethod TowerWeaponSetup.SetupFromCommandLine</c></para>
/// </summary>
public static class TowerWeaponSetup
{
    private const string PrefabFolder = "Assets/Game/Prefabs/Weapon";
    private const string EntitySoFolder = "Assets/Game/SO/EntitySO";
    private const string DataFolder = "Assets/Game/SO/BaseDataSO";
    private const string AttackFolder = "Assets/Game/SO/Attack";

    /// <summary>塔的索敌圈层（与武器自带检测圈同层：只与敌人本体配对）。</summary>
    private const string DetectorLayerName = "TowerDetector";

    private readonly struct WeaponSpec
    {
        public readonly string Name;            // Weapon_TetoBullet
        public readonly string Id;              // weapon_teto_bullet
        public readonly string DisplayName;
        public readonly string AttackAsset;     // Attack_TetoBullet
        public readonly string ProjectileGuid;  // 投射物 prefab 的 guid（无投射物则空）

        public WeaponSpec(string name, string id, string displayName, string attackAsset, string projectileGuid)
        {
            Name = name;
            Id = id;
            DisplayName = displayName;
            AttackAsset = attackAsset;
            ProjectileGuid = projectileGuid;
        }
    }

    private static readonly (string TowerPrefab, string TowerData, WeaponSpec[] Weapons)[] Plan =
    {
        ("Assets/Game/Prefabs/Tower/Tower_Teto.prefab", "Assets/Game/SO/BaseDataSO/Tower_Teto.asset", new[]
        {
            // 投射物 guid = Common/TetoBullet.prefab（两个槽共用同一发子弹）
            new WeaponSpec("Weapon_TetoBullet", "weapon_teto_bullet", "Teto 速射弹",
                           "Attack_TetoBullet", "9abb3b1217f70ef47b8e6fb917d48958"),
            // 炮弹用**自己的**投射物 prefab（挂 TetoShellController，缩放 3 烘在 Transform 上）——
            // 2026-10 之前它与速射弹共用一份，靠 ProjectileAttackSO.Scale 放大
            new WeaponSpec("Weapon_TetoShell", "weapon_teto_shell", "Teto 溅射炮弹",
                           "Attack_TetoShell", "9423265b79f74bbc84d340cb3ebf82d0"),
        }),
        ("Assets/Game/Prefabs/Tower/Tower_Rin.prefab", "Assets/Game/SO/BaseDataSO/Tower_Rin.asset", new[]
        {
            new WeaponSpec("Weapon_RinAoE", "weapon_rin_aoe", "Rin 范围攻击", "Attack_RinAoE", null),
        }),
        ("Assets/Game/Prefabs/Tower/Tower_Luo.prefab", "Assets/Game/SO/BaseDataSO/Tower_Luo.asset", new[]
        {
            new WeaponSpec("Weapon_LuoHeal", "weapon_luo_heal", "Luo 治疗", "Attack_LuoHeal", null),
        }),
    };

    /// <summary>
    /// 塔的攻击类升级项 → 迁到哪把武器（塔 EntitySO / 武器 EntitySO / 升级项目录 / 资产名）。
    /// <c>*RecoverHealth</c> 刻意不在表里：它回的是塔自己的血，留在塔上是对的。
    /// </summary>
    private static readonly (string TowerSoPath, string WeaponSoPath, string Folder, string[] Names)[] UpgradeMigration =
    {
        ("Assets/Game/SO/EntitySO/Tower_Teto.asset", "Assets/Game/SO/EntitySO/Weapon_TetoBullet.asset", "Teto",
            new[] { "TetoRasieDamage", "TetoDecreaseInterval", "TetoHitRange", "TetoHitForce" }),
        ("Assets/Game/SO/EntitySO/Tower_Teto.asset", "Assets/Game/SO/EntitySO/Weapon_TetoShell.asset", "Teto",
            new[] { "TetoRasieDamage", "TetoDecreaseInterval", "TetoHitRange", "TetoHitForce" }),
        ("Assets/Game/SO/EntitySO/Tower_Rin.asset", "Assets/Game/SO/EntitySO/Weapon_RinAoE.asset", "Rin",
            new[] { "RinIncreaseDamage", "RinAttackInterval", "RinHitRange" }),
        ("Assets/Game/SO/EntitySO/Tower_Luo.asset", "Assets/Game/SO/EntitySO/Weapon_LuoHeal.asset", "Luo",
            new[] { "LuoHealIncrease", "LuoHealInterval", "LuoHealRange" }),
    };

    [MenuItem("Tools/Setup Tower Weapons")]
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
            Debug.LogError($"[TowerWeaponSetup] 异常: {e}");
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
        if (LayerMask.NameToLayer(DetectorLayerName) < 0)
        {
            Debug.LogError($"[TowerWeaponSetup] 层「{DetectorLayerName}」不存在 —— 先跑 LayerSetup.SetupLayerNames");
            return false;
        }

        for (int i = 0; i < Plan.Length; i++)
        {
            if (!ApplyTower(Plan[i].TowerPrefab, Plan[i].TowerData, Plan[i].Weapons)) return false;
        }

        if (!MigrateAttackUpgrades()) return false;

        AssetDatabase.SaveAssets();
        return true;
    }

    /// <summary>
    /// 把塔的**攻击类**升级项迁到武器上（幂等）。
    ///
    /// <para>
    /// 攻击数值已经改读武器的 StatModel，所以这些项留在塔上就是「买了没效果」。
    /// <c>*RecoverHealth</c> 不在此列 —— 它回的是**塔自己**的血，留在塔上是对的。
    /// </para>
    ///
    /// <para>
    /// Teto 的两把武器都挂同一批攻击升级：它们是"同一门炮的两种形态"，
    /// 各自买各自的（切换武器后要重新投入）—— 这是"每把武器有自己的升级"的直接结果。
    /// </para>
    /// </summary>
    private static bool MigrateAttackUpgrades()
    {
        for (int i = 0; i < UpgradeMigration.Length; i++)
        {
            (string towerSoPath, string weaponSoPath, string folder, string[] names) = UpgradeMigration[i];

            var towerSo = AssetDatabase.LoadAssetAtPath<ScriptableObject>(towerSoPath);
            var weaponSo = AssetDatabase.LoadAssetAtPath<ScriptableObject>(weaponSoPath);
            if (towerSo == null || weaponSo == null)
            {
                Debug.LogError($"[TowerWeaponSetup] 迁移升级项失败：打不开 {towerSoPath} 或 {weaponSoPath}");
                return false;
            }

            var towerSerialized = new SerializedObject(towerSo);
            var weaponSerialized = new SerializedObject(weaponSo);
            SerializedProperty towerUpgrades = towerSerialized.FindProperty("upgrades");
            SerializedProperty weaponUpgrades = weaponSerialized.FindProperty("upgrades");

            int moved = 0;
            for (int n = 0; n < names.Length; n++)
            {
                string path = $"Assets/Game/SO/LevUpSO/TowerLevelUP/{folder}/{names[n]}.asset";
                var upgrade = AssetDatabase.LoadAssetAtPath<LevelUpSO>(path);
                if (upgrade == null)
                {
                    Debug.LogError($"[TowerWeaponSetup] 找不到升级项 {path}");
                    return false;
                }

                bool removed = RemoveFrom(towerUpgrades, upgrade);
                bool added = AppendTo(weaponUpgrades, upgrade);
                if (removed || added) moved++;
            }

            if (moved == 0) continue;

            towerSerialized.ApplyModifiedPropertiesWithoutUndo();
            weaponSerialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[TowerWeaponSetup] 升级项迁移：{System.IO.Path.GetFileName(towerSoPath)} → " +
                      $"{System.IO.Path.GetFileName(weaponSoPath)}（{moved} 处改动）");
        }

        return true;
    }

    private static bool RemoveFrom(SerializedProperty array, Object target)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue != target) continue;

            array.DeleteArrayElementAtIndex(i);
            return true;
        }

        return false;
    }

    private static bool AppendTo(SerializedProperty array, Object target)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == target) return false;
        }

        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        array.GetArrayElementAtIndex(index).objectReferenceValue = target;
        return true;
    }

    private static bool ApplyTower(string towerPrefabPath, string towerDataPath, WeaponSpec[] specs)
    {
        var towerData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(towerDataPath);
        if (towerData == null)
        {
            Debug.LogError($"[TowerWeaponSetup] 打不开塔数据 {towerDataPath}");
            return false;
        }

        var stats = new SerializedObject(towerData);
        var entitySos = new List<WeaponEntitySO>();

        for (int i = 0; i < specs.Length; i++)
        {
            WeaponEntitySO entitySo = EnsureWeapon(specs[i], stats);
            if (entitySo == null) return false;

            entitySos.Add(entitySo);
        }

        return WireTower(towerPrefabPath, entitySos);
    }

    /// <summary>建（或复用）一把武器的 data / prefab / EntitySO。</summary>
    private static WeaponEntitySO EnsureWeapon(in WeaponSpec spec, SerializedObject towerStats)
    {
        string dataPath = $"{DataFolder}/{spec.Name}.asset";
        string prefabPath = $"{PrefabFolder}/{spec.Name}.prefab";
        string entityPath = $"{EntitySoFolder}/{spec.Name}.asset";

        // ① 数据：数值从塔 data 逐项复制（字段同名同义）
        var data = AssetDatabase.LoadAssetAtPath<TowerWeaponDataSO>(dataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<TowerWeaponDataSO>();
            AssetDatabase.CreateAsset(data, dataPath);
            Debug.Log($"[TowerWeaponSetup] 新建武器数据 {dataPath}");
        }

        var dataSo = new SerializedObject(data);
        CopyFloat(towerStats, dataSo, "Damage", 10f);
        CopyFloat(towerStats, dataSo, "AttackSpeed", 1f);
        CopyFloat(towerStats, dataSo, "HitForce", 0f);
        CopyFloat(towerStats, dataSo, "BulletSpeed", 8f);
        CopyFloat(towerStats, dataSo, "ShellDamage", 0f);
        CopyFloat(towerStats, dataSo, "ShellExplosionRadius", 0f);
        CopyFloat(towerStats, dataSo, "AttackRange", 2f);
        CopyFloat(towerStats, dataSo, "HealAmount", 0f);
        CopyFloat(towerStats, dataSo, "HealSpeed", 1f);
        dataSo.ApplyModifiedPropertiesWithoutUndo();

        // ② prefab：BaseWeapon + AttackDriver + 索敌圈（同物体，触发回调才收得到）+ 范围同步
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            var root = new GameObject(spec.Name);

            root.AddComponent<BaseWeapon>();
            var driver = root.AddComponent<AttackDriver>();

            var detector = root.AddComponent<CircleCollider2D>();
            detector.isTrigger = true;
            detector.radius = ReadFloat(towerStats, "AttackRange", 2f);
            root.layer = LayerMask.NameToLayer(DetectorLayerName);

            root.AddComponent<TowerWeaponRangeSync>();

            var driverSo = new SerializedObject(driver);

            // 刻意**不写** _attack：攻击方式的唯一来源是 WeaponEntitySO.attack（见 ③，装配时注入）。
            // prefab 上再写一份会让"改 SO 没生效"无从发现 —— 与 entityConfig 同样的理由。
            if (!string.IsNullOrEmpty(spec.ProjectileGuid))
                driverSo.FindProperty("_prefab").FindPropertyRelative("m_AssetGUID").stringValue = spec.ProjectileGuid;

            // 塔武器由**服务端**驱动计时（联机时客户端只表现）——
            // 这是装配方的声明，网络层读它来设置 HasAuthority（见 AttackAuthority 的"唯一读取时机"）
            driverSo.FindProperty("_authority").enumValueIndex = (int)AttackAuthority.Server;

            driverSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[TowerWeaponSetup] 新建武器 prefab {prefabPath}");
        }

        // ②' 权威归属：塔武器由服务端驱动计时。既有 prefab 走的是修复而不是创建，
        // 所以这一步必须在创建分支之外（见 EnsureAuthority 的说明）
        EnsureAuthority(prefabPath, AttackAuthority.Server);

        // ③ EntitySO
        var entity = AssetDatabase.LoadAssetAtPath<WeaponEntitySO>(entityPath);
        if (entity == null)
        {
            entity = ScriptableObject.CreateInstance<WeaponEntitySO>();
            AssetDatabase.CreateAsset(entity, entityPath);
            Debug.Log($"[TowerWeaponSetup] 新建武器 EntitySO {entityPath}");
        }

        var entitySo = new SerializedObject(entity);
        entitySo.FindProperty("id").stringValue = spec.Id;
        entitySo.FindProperty("dataRef").objectReferenceValue = data;
        entitySo.FindProperty("displayName").stringValue = spec.DisplayName;
        entitySo.FindProperty("prefab").FindPropertyRelative("m_AssetGUID").stringValue =
            AssetDatabase.AssetPathToGUID(prefabPath);
        // 攻击方式写在 EntitySO 上（唯一来源）：装配时由 WeaponAssembler 注入 AttackDriver
        entitySo.FindProperty("attack").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AttackMethodSO>($"{AttackFolder}/{spec.AttackAsset}.asset");
        entitySo.ApplyModifiedPropertiesWithoutUndo();

        return entity;
    }

    /// <summary>塔 prefab：挂武器控制器（填武器列表）+ 移除塔身上的 AttackDriver。</summary>
    private static bool WireTower(string towerPrefabPath, List<WeaponEntitySO> weapons)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(towerPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[TowerWeaponSetup] 打不开 {towerPrefabPath}");
            return false;
        }

        try
        {
            // 塔身上的驱动是旧模型（攻击槽长在塔上）—— 武器化之后必须移除，
            // 否则它会继续按塔自己的数值开火，与武器各打一份
            AttackDriver oldDriver = root.GetComponent<AttackDriver>();
            if (oldDriver != null)
            {
                Object.DestroyImmediate(oldDriver, true);
                Debug.Log($"[TowerWeaponSetup] {System.IO.Path.GetFileName(towerPrefabPath)}：已移除塔身上的 AttackDriver");
            }

            var controller = root.GetComponent<TowerWeaponController>();
            if (controller == null) controller = root.AddComponent<TowerWeaponController>();

            var so = new SerializedObject(controller);
            SerializedProperty list = so.FindProperty("_weapons");
            list.arraySize = weapons.Count;
            for (int i = 0; i < weapons.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, towerPrefabPath);
            Debug.Log($"[TowerWeaponSetup] {System.IO.Path.GetFileName(towerPrefabPath)}：已挂 {weapons.Count} 把武器");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 把武器 prefab 的 <c>_authority</c> 修成期望值（幂等）。
    ///
    /// <para>
    /// <b>为什么需要"修复"而不只是"新建时写对"：</b>本工具会被反复重跑，而已有的 prefab
    /// 不会走创建分支 —— 只在创建分支里写，会让老 prefab 永远停在旧值上，
    /// 而那个坑只在接联机时才暴露（塔在每个客户端各 tick 一份，敌人血量分叉）。
    /// </para>
    /// </summary>
    private static void EnsureAuthority(string prefabPath, AttackAuthority expected)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (contents == null) return;

        try
        {
            AttackDriver driver = contents.GetComponent<AttackDriver>();
            if (driver == null) return;

            var so = new SerializedObject(driver);
            SerializedProperty authority = so.FindProperty("_authority");
            if (authority == null || authority.enumValueIndex == (int)expected) return;

            authority.enumValueIndex = (int)expected;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            Debug.Log($"[TowerWeaponSetup] {System.IO.Path.GetFileName(prefabPath)}：权威归属修正为 {expected}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void CopyFloat(SerializedObject source, SerializedObject destination, string field, float fallback)
    {
        SerializedProperty target = destination.FindProperty(field);
        if (target == null) return;

        target.floatValue = ReadFloat(source, field, fallback);
    }

    private static float ReadFloat(SerializedObject source, string field, float fallback)
    {
        SerializedProperty property = source.FindProperty(field);
        return property != null ? property.floatValue : fallback;
    }
}
#endif
