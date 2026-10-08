#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 幂等的**武器内容生成器** —— 生成 Cannon / Laser 的 DataSO、攻击资产、prefab 与 EntitySO，
/// 并把它们接进「枪手可携带武器」与「玩家候选武器」两处配置。
///
/// <para>
/// <b>为什么用脚本生成：</b>这些资产的字段是确定的（数值 + 攻击方式 + 引用），
/// 手拖既不可复现也无法 review；而漏接一处（例如 EntitySO 没进候选列表）的表现是
/// "买得到但装不上"，不报任何错。生成器反复跑结果一致，且每次都会核对接线。
/// </para>
///
/// <para>
/// 手动运行：Tools ▸ Setup Cart Weapons
/// 批处理运行：<c>-executeMethod CartWeaponSetup.SetupFromCommandLine</c>
/// </para>
///
/// <para>
/// ⚠️ 生成后需要再跑一次 <c>Tools ▸ Setup Addressables</c>：武器 prefab 必须在
/// Weapon 组里，否则 <c>AssetReferenceGameObject</c> 加载失败。
/// </para>
/// </summary>
public static class CartWeaponSetup
{
    private const string DataDir = "Assets/Game/SO/BaseDataSO";
    private const string AttackDir = "Assets/Game/SO/Attack";
    private const string EntityDir = "Assets/Game/SO/EntitySO";
    private const string WeaponPrefabDir = "Assets/Game/Prefabs/Weapon";

    private const string BulletPrefabPath = "Assets/Game/Prefabs/Weapon/Bullet.prefab";

    /// <summary>
    /// 炮弹用的投射物 prefab（挂 <see cref="ShellController"/>，缩放烘在 Transform 上）。
    /// 与 <see cref="BulletPrefabPath"/> 分开是 2026-10 的改动：单体与溅射各有一份外观，
    /// 表现（爆炸音效/粒子）写在炮弹控制器里，缩放不再由 <c>ProjectileAttackSO</c> 传。
    /// </summary>
    private const string ShellPrefabPath = "Assets/Game/Prefabs/Weapon/Bullet_Shell.prefab";
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Common/Player.prefab";
    private const string GunnerAssetPath = "Assets/Game/SO/CharacterSO/Character_Gunner.asset";

    /// <summary>占位美术：暂时复用枪的图标，等美术给专门资源后换掉即可。</summary>
    private const string PlaceholderSpriteGuid = "5c4bd644de8b3fe4dba344d129f2dbfc";

    [MenuItem("Tools/Setup Cart Weapons")]
    public static void SetupFromMenu()
    {
        bool ok = Setup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[CartWeaponSetup] CLI_OK (menu)" : "[CartWeaponSetup] CLI_FAIL (menu)");
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
            Debug.LogError($"[CartWeaponSetup] 异常: {e}");
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
        Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(
            AssetDatabase.GUIDToAssetPath(PlaceholderSpriteGuid));

        if (placeholder == null)
            Debug.LogWarning("[CartWeaponSetup] 找不到占位图标，生成的武器不会有图标（不影响功能）。");

        bool ok = true;

        ok &= BuildCannon(placeholder);
        ok &= BuildLaser(placeholder);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ok &= RegisterInGunnerLoadout("weapon_cannon", "weapon_laser");
        ok &= RegisterInPlayerCandidates();

        return ok;
    }

    // ── Cannon ──

    private static bool BuildCannon(Sprite placeholder)
    {
        const string id = "weapon_cannon";
        const string name = "Weapon_Cannon";

        var data = LoadOrCreate<CannonWeaponDataSO>($"{DataDir}/{name}.asset");
        data.AttackSpeed = 0.6f;
        data.Damage = 26f;
        data.BulletSpeed = 14f;
        data.BulletHitForce = 40f;
        data.SplashRadius = 2.5f;
        EditorUtility.SetDirty(data);

        // 炮弹 = SplashProjectileAttackSO（不是 ProjectileAttackSO + bool 开关）：
        // 「是不是溅射」用类型表达，单体资产上就不会留下永远不会被读的 SplashRadiusStat
        var attack = LoadOrCreate<SplashProjectileAttackSO>($"{AttackDir}/Attack_Cannon.asset");
        attack.DamageStat = StatType.Damage;
        attack.SpeedStat = StatType.BulletSpeed;
        attack.ForceStat = StatType.BulletHitForce;
        attack.SplashRadiusStat = StatType.ShellExplosionRadius;
        // 不再有 Scale 字段：炮弹的放大烘在 ShellPrefabPath 那份 prefab 的 Transform 上
        EditorUtility.SetDirty(attack);

        GameObject prefab = BuildWeaponPrefab(
            name,
            weaponComponentType: typeof(GunWeapon),
            projectilePrefabPath: ShellPrefabPath,
            placeholder: placeholder,
            withBeamVisual: false);

        var entity = LoadOrCreate<WeaponEntitySO>($"{EntityDir}/{name}.asset");
        entity.id = id;
        entity.dataRef = data;
        entity.displayName = "Cannon";
        entity.displaySprite = placeholder;
        entity.unlockPrice = 800;
        entity.prefab = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID($"{WeaponPrefabDir}/{name}.prefab"));
        // 攻击方式写在 EntitySO 上：一把武器 = 一个 SO，装配时注入 driver。
        // prefab 不再反指 SO / 不再接 _attack —— 否则"改了 SO 没生效"无从发现。
        entity.attack = attack;
        EditorUtility.SetDirty(entity);

        Debug.Log($"[CartWeaponSetup] {name} 已就绪（id={id}，溅射半径 {data.SplashRadius}）");
        return prefab != null;
    }

    // ── Laser ──

    private static bool BuildLaser(Sprite placeholder)
    {
        const string id = "weapon_laser";
        const string name = "Weapon_Laser";

        var data = LoadOrCreate<LaserWeaponDataSO>($"{DataDir}/{name}.asset");
        data.AttackSpeed = 4f;
        data.Damage = 6f;
        EditorUtility.SetDirty(data);

        var attack = LoadOrCreate<BeamAttackSO>($"{AttackDir}/Attack_Laser.asset");
        attack.DamageStat = StatType.Damage;
        attack.SpeedStat = StatType.AttackSpeed;
        attack.Range = 12f;
        attack.Width = 0.35f;
        attack.Pierce = true;
        attack.MaxHits = 8;
        EditorUtility.SetDirty(attack);

        GameObject prefab = BuildWeaponPrefab(
            name,
            weaponComponentType: typeof(GunWeapon),
            projectilePrefabPath: null,
            placeholder: placeholder,
            withBeamVisual: true);

        var entity = LoadOrCreate<WeaponEntitySO>($"{EntityDir}/{name}.asset");
        entity.id = id;
        entity.dataRef = data;
        entity.displayName = "Laser";
        entity.displaySprite = placeholder;
        entity.unlockPrice = 1500;
        entity.prefab = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID($"{WeaponPrefabDir}/{name}.prefab"));
        entity.attack = attack;
        EditorUtility.SetDirty(entity);

        Debug.Log($"[CartWeaponSetup] {name} 已就绪（id={id}，射程 {attack.Range}）");
        return prefab != null;
    }

    // ── prefab 构建 ──

    /// <summary>
    /// 搭一个武器 prefab：根节点 = 武器组件 + <see cref="AttackDriver"/>，
    /// 子节点 = 发射点（带 SpriteRenderer，兼作外观）。
    ///
    /// <para>
    /// 结构照抄 <c>Weapon_Gun.prefab</c>：发射点是**子物体**，因为 <c>AttackDriver._muzzle</c>
    /// 只在这一处配置 —— 武器自己再配一个迟早会漂移，而漂移是静默的。
    /// </para>
    /// </summary>
    private static GameObject BuildWeaponPrefab(string name, System.Type weaponComponentType,
                                                string projectilePrefabPath,
                                                Sprite placeholder, bool withBeamVisual)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject(name);
        // 与 Weapon_Gun.prefab 同层（本体层）：武器是玩家的一部分，
        // 层不同会让碰撞与渲染顺序与既有武器不一致 —— 那种差异只在真机上才看得出来。
        // 注：层名已从 "Player" 改为 "PlayerBody"（物理层方案 LAYER_PLAN.md）
        int playerLayer = LayerMask.NameToLayer("PlayerBody");
        root.layer = playerLayer >= 0 ? playerLayer : 0;

        root.AddComponent(weaponComponentType);
        root.AddComponent<AttackDriver>();
        if (withBeamVisual)
        {
            var line = root.AddComponent<LineRenderer>();
            line.material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            root.AddComponent<BeamVisual>();
        }

        var muzzle = new GameObject("Muzzle");
        muzzle.layer = root.layer;
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0.9f, 0f, 0f);

        SpriteRenderer renderer = muzzle.AddComponent<SpriteRenderer>();
        renderer.sprite = placeholder;

        string path = $"{WeaponPrefabDir}/{name}.prefab";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        // 对象引用必须在**prefab 资产**上写：场景对象上的 SerializedObject 对象引用不落盘
        // （CLAUDE.md 记过这条；AssetReference 的内联 GUID 则两种情况下都能写）
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            AttackDriver driver = contents.GetComponent<AttackDriver>();
            SerializedObject so = new SerializedObject(driver);

            // 刻意**不写** _attack：攻击方式的唯一来源是 WeaponEntitySO.attack（装配时注入）。
            // prefab 上再写一份会让"改 SO 没生效"这类问题无从发现 —— 与 entityConfig 同样的理由。
            SerializedProperty muzzleProp = so.FindProperty("_muzzle");
            muzzleProp.objectReferenceValue = contents.transform.Find("Muzzle");

            SerializedProperty prefabProp = so.FindProperty("_prefab");
            string bulletGuid = string.IsNullOrEmpty(projectilePrefabPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(projectilePrefabPath);
            prefabProp.FindPropertyRelative("m_AssetGUID").stringValue = bulletGuid;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        Debug.Log($"[CartWeaponSetup] 已生成 {path}（攻击方式写在 EntitySO 上，prefab 不接）");
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // ── 接线 ──

    /// <summary>
    /// 把新武器加进枪手的可携带白名单。**幂等**：已在列表里就不重复添加。
    /// 漏了这一步的表现是"武器台里买了却装不上"（RunSession 的角色白名单会拒绝）。
    /// </summary>
    private static bool RegisterInGunnerLoadout(params string[] weaponIds)
    {
        CharacterDefinitionSO gunner = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(GunnerAssetPath);
        if (gunner == null)
        {
            Debug.LogError($"[CartWeaponSetup] 找不到 {GunnerAssetPath}");
            return false;
        }

        if (gunner.allowedWeaponIds == null)
            gunner.allowedWeaponIds = new List<string>();

        for (int i = 0; i < weaponIds.Length; i++)
        {
            if (gunner.allowedWeaponIds.Contains(weaponIds[i])) continue;

            gunner.allowedWeaponIds.Add(weaponIds[i]);
            Debug.Log($"[CartWeaponSetup] 枪手可携带武器 + {weaponIds[i]}");
        }

        EditorUtility.SetDirty(gunner);
        return true;
    }

    /// <summary>
    /// 把新武器加进玩家 prefab 的候选列表。**幂等**：按 EntitySO 引用去重。
    /// 漏了这一步的表现是 <c>PlayerSpawner</c> 报"候选列表里没有武器「weapon_cannon」"。
    /// </summary>
    private static bool RegisterInPlayerCandidates()
    {
        if (!File.Exists(PlayerPrefabPath))
        {
            Debug.LogError($"[CartWeaponSetup] 找不到 {PlayerPrefabPath}");
            return false;
        }

        WeaponEntitySO cannon = AssetDatabase.LoadAssetAtPath<WeaponEntitySO>($"{EntityDir}/Weapon_Cannon.asset");
        WeaponEntitySO laser = AssetDatabase.LoadAssetAtPath<WeaponEntitySO>($"{EntityDir}/Weapon_Laser.asset");

        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerWeaponController controller = contents.GetComponentInChildren<PlayerWeaponController>(true);
            if (controller == null)
            {
                Debug.LogError($"[CartWeaponSetup] {PlayerPrefabPath} 上找不到 PlayerWeaponController");
                return false;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty candidates = so.FindProperty("_candidates");

            AppendIfMissing(candidates, cannon);
            AppendIfMissing(candidates, laser);

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void AppendIfMissing(SerializedProperty list, Object asset)
    {
        if (asset == null || list == null) return;

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset) return;
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
        Debug.Log($"[CartWeaponSetup] 玩家候选武器 + {asset.name}");
    }

    // ── 资产读写 ──

    /// <summary>
    /// 取已有资产或新建一个。**保留 GUID**：每次重建资产会让所有引用它的地方静默断链，
    /// 而断链在 Inspector 上只表现为一个空的槽位。
    /// </summary>
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[CartWeaponSetup] 新建资产 {path}");
        return asset;
    }
}
#endif
