#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 给 Teto 装上"改装：溅射炮弹"升级项（幂等）。
///
/// <para>
/// <b>它做两件事：</b>① 需要时创建 <c>TetoSwapShell.asset</c>；
/// ② 把它挂进 Teto 的 <c>TowerEntitySO.upgrades</c>。
/// </para>
///
/// <para>
/// <b>为什么用工具而不是手拖：</b>升级项是"SO 引用 SO"，漏挂的表现是
/// "面板里少一项"，没有任何报错；而且它必须与 <c>Tower_Teto.prefab</c> 上的
/// 攻击槽下标（<c>_extraAttacks[0]</c> = 溅射炮弹 = 槽 1）保持一致 ——
/// 这个对应关系写在本文件的注释里，改 prefab 槽位顺序时要一起看。
/// </para>
///
/// <para>
/// 手动：Tools ▸ Setup Teto Weapon Swap　批处理：<c>-executeMethod TowerWeaponSwapSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class TowerWeaponSwapSetup
{
    private const string TetoEntitySoPath = "Assets/Game/SO/EntitySO/Tower_Teto.asset";
    private const string TetoPrefabPath = "Assets/Game/Prefabs/Tower/Tower_Teto.prefab";
    private const string SwapAssetPath = "Assets/Game/SO/LevUpSO/TowerLevelUP/Teto/TetoSwapShell.asset";

    /// <summary>图标从同类升级项复制，避免手写 sprite 的 GUID（图集子资源的 GUID 不好引用）。</summary>
    private const string IconSourcePath = "Assets/Game/SO/LevUpSO/TowerLevelUP/Teto/TetoRasieDamage.asset";

    [MenuItem("Tools/Setup Teto Weapon Swap")]
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
            Debug.LogError($"[TowerWeaponSwapSetup] 异常: {e}");
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
        TowerWeaponSwapUpgradeSO swap = EnsureSwapAsset();
        if (swap == null) return false;

        if (!EnsureInTetoUpgrades(swap)) return false;

        return VerifySlotExists(swap);
    }

    /// <summary>
    /// 校验"升级项写的下标"在塔 prefab 上真的存在。
    ///
    /// <para>
    /// 这是最容易悄悄漂移的一处对应关系：升级项只写了一个下标，而武器列表在
    /// <c>TowerWeaponController._weapons</c> 上。调整武器顺序 / 删掉一把之后，
    /// 升级项会变成"点了没反应"（运行时只有一条警告，面板上完全看不出来）。
    /// </para>
    /// </summary>
    private static bool VerifySlotExists(TowerWeaponSwapUpgradeSO swap)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TetoPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[TowerWeaponSwapSetup] 打不开 {TetoPrefabPath}");
            return false;
        }

        TowerWeaponController controller = prefab.GetComponent<TowerWeaponController>();
        if (controller == null)
        {
            Debug.LogError($"[TowerWeaponSwapSetup] {TetoPrefabPath} 上没有 TowerWeaponController —— " +
                           "先跑 Tools ▸ Setup Tower Weapons");
            return false;
        }

        // prefab 资产上走 SerializedObject 可靠
        var serialized = new SerializedObject(controller);
        SerializedProperty weapons = serialized.FindProperty("_weapons");
        int count = weapons != null && weapons.isArray ? weapons.arraySize : 0;

        if (swap.targetSlot < 0 || swap.targetSlot >= count)
        {
            Debug.LogError($"[TowerWeaponSwapSetup] 下标对不上：改装项指向第 {swap.targetSlot} 把武器，" +
                           $"但 {System.IO.Path.GetFileName(TetoPrefabPath)} 只挂了 {count} 把" +
                           "（0 = 第一把）—— 面板上会显示成「点了没反应」。");
            return false;
        }

        Debug.Log($"[TowerWeaponSwapSetup] 下标校验通过：改装项指向第 {swap.targetSlot} 把武器，" +
                  $"塔上共 {count} 把");
        return true;
    }

    private static TowerWeaponSwapUpgradeSO EnsureSwapAsset()
    {
        TowerWeaponSwapUpgradeSO swap = AssetDatabase.LoadAssetAtPath<TowerWeaponSwapUpgradeSO>(SwapAssetPath);
        if (swap != null) return swap;

        swap = ScriptableObject.CreateInstance<TowerWeaponSwapUpgradeSO>();
        swap.levelUpText = "改装：溅射炮弹";
        swap.cost = 1;
        swap.targetSlot = 1;          // Tower_Teto.prefab 的 _extraAttacks[0]

        var iconSource = AssetDatabase.LoadAssetAtPath<LevelUpSO>(IconSourcePath);
        if (iconSource != null) swap.levelUpSprite = iconSource.levelUpSprite;

        AssetDatabase.CreateAsset(swap, SwapAssetPath);
        Debug.Log($"[TowerWeaponSwapSetup] 新建升级项 {SwapAssetPath}（槽 {swap.targetSlot}）");
        return swap;
    }

    private static bool EnsureInTetoUpgrades(TowerWeaponSwapUpgradeSO swap)
    {
        var entitySo = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TetoEntitySoPath);
        if (entitySo == null)
        {
            Debug.LogError($"[TowerWeaponSwapSetup] 打不开 {TetoEntitySoPath}");
            return false;
        }

        // SO 是**资产**：这里走 SerializedObject 是可靠的
        //（CLAUDE.md 的告警只针对**场景里的组件**）
        var serialized = new SerializedObject(entitySo);
        SerializedProperty upgrades = serialized.FindProperty("upgrades");
        if (upgrades == null || !upgrades.isArray)
        {
            Debug.LogError($"[TowerWeaponSwapSetup] {TetoEntitySoPath} 上找不到 upgrades 数组");
            return false;
        }

        for (int i = 0; i < upgrades.arraySize; i++)
        {
            if (upgrades.GetArrayElementAtIndex(i).objectReferenceValue == swap)
            {
                Debug.Log("[TowerWeaponSwapSetup] Teto 的 upgrades 里已有改装项，无需改动");
                return true;
            }
        }

        int index = upgrades.arraySize;
        upgrades.InsertArrayElementAtIndex(index);
        upgrades.GetArrayElementAtIndex(index).objectReferenceValue = swap;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        Debug.Log($"[TowerWeaponSwapSetup] 已把改装项加入 Teto 的 upgrades（第 {index + 1} 项，" +
                  "面板按配置顺序列出）");
        return true;
    }
}
#endif
