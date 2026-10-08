#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 场景 / prefab **接线体检** —— 把"哪些 Inspector 槽位还是空的"变成一条可读的清单。
///
/// <para>
/// <b>为什么需要它：</b>漏接一个引用不会报错，只会在运行时表现成"某个功能悄悄不工作"
/// （塔不攻击、面板打不开、Boss 不出现）。这类问题在 Play 里往往只看到现象、看不到原因，
/// 而逐个 prefab 手点一遍既慢又会漏。这里把「这些组件上的这些槽位必须非空」写成表，
/// 一次跑完并直接列出空槽位。
/// </para>
///
/// <para>
/// 手动运行：Tools ▸ Audit Scene Wiring
/// 批处理运行：<c>-executeMethod SceneWiringAudit.AuditFromCommandLine</c>（有必需项为空时退出码 1）
/// </para>
/// </summary>
public static class SceneWiringAudit
{
    private static int _checked;
    private static readonly List<string> Problems = new List<string>();

    /// <summary>一个场景/预制体要检查的内容。</summary>
    private sealed class Target
    {
        public string Path;
        public bool IsScene;
        public Type[] RequiredComponents = Array.Empty<Type>();
        /// <summary>组件类型名 → 必须非空的序列化字段路径。</summary>
        public (Type Component, string[] Fields)[] Fields = Array.Empty<(Type, string[])>();
    }

    private static readonly Target[] Targets =
    {
        new Target
        {
            Path = "Assets/Scenes/Level0.unity",
            IsScene = true,
            RequiredComponents = new[]
            {
                typeof(GameLevelManager), typeof(PlayerManager),
                typeof(RunStatsTracker), typeof(EnemyTargetRegistry), typeof(DamageNumService),
                typeof(CartController), typeof(StageDirector), typeof(CartRouteSource),
                typeof(RunSettlement), typeof(EnemySpawner), typeof(PlayerSpawner),
            },
            Fields = new[]
            {
                (typeof(StageDirector), new[] { "Cart", "Spawner" }),
                (typeof(CartRouteSource), new[] { "Route", "Cart" }),
                (typeof(RunSettlement), new[] { "Director" }),
                (typeof(EnemySpawner), new[] { "Table" }),
                (typeof(PlayerSpawner), new[] { "PlayerPrefab", "AvailableRoles" }),
            },
        },
        new Target
        {
            Path = "Assets/Scenes/Lobby.unity",
            IsScene = true,
            RequiredComponents = new[]
            {
                typeof(PlayerManager), typeof(PlayerSpawner), typeof(LobbyDirector), typeof(PortalController),
                typeof(WeaponBenchInteractable), typeof(TowerBenchInteractable), typeof(CharacterSwitchInteractable),
            },
            Fields = new[]
            {
                (typeof(LobbyDirector), new[] { "Characters" }),
                (typeof(PlayerSpawner), new[] { "PlayerPrefab", "AvailableRoles" }),
                (typeof(WeaponBenchInteractable), new[] { "Weapons" }),
                (typeof(TowerBenchInteractable), new[] { "Towers" }),
                (typeof(CharacterSwitchInteractable), new[] { "Characters" }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Common/Player.prefab",
            RequiredComponents = new[]
            {
                typeof(PlayerController), typeof(PlayerHealthController), typeof(PlayerWeaponController),
                typeof(PlayerUpgradeController), typeof(PlayerProgressionController),
                typeof(PlayerRoleController), typeof(UpgradePointWallet), typeof(ExperienceLevController),
            },
            Fields = new[]
            {
                // 刻意**不查** entityConfig：玩家 prefab 不再预接配置，
                // 职业数值由角色 SO 的 playerConfig 在生成时注入（prefab 根节点未激活 → 注入 → 激活）
                (typeof(PlayerWeaponController), new[] { "_mount", "_candidates" }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/GamePanel.prefab",
            Fields = new[]
            {
                (typeof(GamePanel), new[]
                {
                    "sldExp", "txtLevel", "txtTime", "btnSetting", "txtLevelPoint",
                    "btnWeaponShop", "btnTowerShop", "btnTowerLevelUp",
                    "btnPlaceTowerConfirm", "btnPlaceTowerCancel",
                    "btnWeaponSlot1", "btnWeaponSlot2",
                    "joystickMove", "joystickWeapon", "txtFPS",
                    "healthPanel",
                }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/TowerLevelUpPanel.prefab",
            Fields = new[]
            {
                (typeof(TowerLevelUpPanel), new[] { "content", "itemTemplate", "scrollRect", "btnClose", "_btnDismantle" }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/LevelUpPanel.prefab",
            Fields = new[]
            {
                (typeof(LevelUpPanel), new[]
                {
                    "btn1", "btn2", "btn3", "img1", "img2", "img3",
                    "txt1", "txt2", "txt3", "txtConsumption1", "txtConsumption2", "txtConsumption3", "btnClose",
                }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/WeaponUpgradePanel.prefab",
            Fields = new[]
            {
                (typeof(WeaponUpgradePanel), new[]
                {
                    "btn1", "btn2", "btn3", "img1", "img2", "img3",
                    "txt1", "txt2", "txt3", "txtConsumption1", "txtConsumption2", "txtConsumption3", "btnClose",
                }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/ChooseTowerPanel.prefab",
            Fields = new[]
            {
                (typeof(ChooseTowerPanel), new[]
                {
                    "towerSO1", "towerSO2", "towerSO3",
                    "button1", "button2", "button3", "btnClose",
                }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/WeaponBenchPanel.prefab",
            Fields = new[]
            {
                (typeof(WeaponBenchPanel), new[]
                {
                    "button1", "button2", "button3", "btnClose",
                }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/TowerBenchPanel.prefab",
            Fields = new[]
            {
                (typeof(TowerBenchPanel), new[]
                {
                    "button1", "button2", "button3", "btnClose",
                }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Weapon/Weapon_Cannon.prefab",
            RequiredComponents = new[] { typeof(GunWeapon), typeof(AttackDriver) },
            Fields = new[] { (typeof(AttackDriver), new[] { "_muzzle" }) },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Weapon/Weapon_Laser.prefab",
            RequiredComponents = new[] { typeof(GunWeapon), typeof(AttackDriver), typeof(BeamVisual) },
            Fields = new[] { (typeof(AttackDriver), new[] { "_muzzle" }) },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Weapon/Weapon_Gun.prefab",
            Fields = new[] { (typeof(AttackDriver), new[] { "_muzzle" }) },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Weapon/Weapon_Spin.prefab",
            Fields = new[] { (typeof(AttackDriver), new[] { "_muzzle" }) },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Tower/Tower_Teto.prefab",
            Fields = new[]
            {
                (typeof(BaseTower), new[] { "entityConfig" }),
                (typeof(InteractionSensor), new[] { "TargetBehaviour" }),
                (typeof(InteractionPromptView), new[] { "VisualRoot", "Label" }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Tower/Tower_Rin.prefab",
            Fields = new[]
            {
                (typeof(BaseTower), new[] { "entityConfig" }),
                (typeof(InteractionSensor), new[] { "TargetBehaviour" }),
                (typeof(InteractionPromptView), new[] { "VisualRoot", "Label" }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Tower/Tower_Luo.prefab",
            Fields = new[]
            {
                (typeof(BaseTower), new[] { "entityConfig" }),
                (typeof(InteractionSensor), new[] { "TargetBehaviour" }),
                (typeof(InteractionPromptView), new[] { "VisualRoot", "Label" }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/Cart/Cart.prefab",
            RequiredComponents = new[]
            {
                typeof(CartController), typeof(CartHealthController),
                typeof(CartRepairInteractable), typeof(Hurtbox),
            },
            Fields = new[]
            {
                (typeof(CartController), new[] { "entityConfig" }),
                (typeof(CartRepairInteractable), new[] { "Cart" }),
            },
        },
        new Target
        {
            Path = "Assets/Game/Prefabs/UI/CartHealthPercent.prefab",
            RequiredComponents = new[] { typeof(CartHealthPercentView), typeof(CartRepairProgressView) },
            Fields = new[]
            {
                (typeof(CartHealthPercentView), new[] { "_label" }),
                (typeof(CartRepairProgressView), new[] { "_fill", "_root" }),
            },
        },
    };

    [MenuItem("Tools/Audit Scene Wiring")]
    public static void AuditFromMenu()
    {
        bool ok = Run();
        Debug.Log(ok ? "[SceneWiringAudit] CLI_OK (menu)" : "[SceneWiringAudit] CLI_FAIL (menu)");
    }

    public static void AuditFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = Run();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneWiringAudit] 异常: {e}");
        }
        finally
        {
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool Run()
    {
        _checked = 0;
        Problems.Clear();

        foreach (Target target in Targets)
            AuditTarget(target);

        // 全局检查（与具体目标无关）：可受伤实体必须带 Hurtbox 本体标记 ——
        // 漏掉的表现是"这个实体打不掉血"，只在战斗里才暴露
        HurtboxSetup.VerifyAll(Problems);

        // 全局检查：物理层名与碰撞矩阵必须符合 LAYER_PLAN.md（有人手改后无人发现 = 静默回归）
        LayerSetup.VerifyAll(Problems);

        if (Problems.Count == 0)
        {
            Debug.Log($"[SceneWiringAudit] 通过：{Targets.Length} 个场景/预制体，{_checked} 个槽位全部已接线。");
            return true;
        }

        Debug.LogError($"[SceneWiringAudit] {Problems.Count} 处未接线（已检查 {_checked} 个槽位）：");
        for (int i = 0; i < Problems.Count; i++)
            Debug.LogError($"  ✗ {Problems[i]}");

        return false;
    }

    private static void AuditTarget(Target target)
    {
        if (target.IsScene)
        {
            if (!System.IO.File.Exists(target.Path))
            {
                Problems.Add($"{target.Path}：场景不存在");
                return;
            }

            EditorSceneManager.OpenScene(target.Path, OpenSceneMode.Single);

            foreach (Type type in target.RequiredComponents)
            {
                if (FindFirst(type) == null)
                    Problems.Add($"{target.Path}：缺少组件 {type.Name}");
            }

            foreach ((Type component, string[] fields) in target.Fields)
            {
                UnityEngine.Object instance = FindFirst(component);
                if (instance == null)
                {
                    Problems.Add($"{target.Path}：找不到组件 {component.Name}");
                    continue;
                }

                CheckFields(instance, target.Path, fields);
            }

            return;
        }

        if (!System.IO.File.Exists(target.Path))
        {
            Problems.Add($"{target.Path}：预制体不存在");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(target.Path);
        try
        {
            foreach (Type type in target.RequiredComponents)
            {
                if (root.GetComponentInChildren(type, true) == null)
                    Problems.Add($"{target.Path}：缺少组件 {type.Name}");
            }

            foreach ((Type component, string[] fields) in target.Fields)
            {
                Component instance = root.GetComponentInChildren(component, true);
                if (instance == null)
                {
                    Problems.Add($"{target.Path}：找不到组件 {component.Name}");
                    continue;
                }

                CheckFields(instance, target.Path, fields);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static UnityEngine.Object FindFirst(Type type)
    {
        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(
            type, FindObjectsInactive.Include, FindObjectsSortMode.None);

        return found.Length > 0 ? found[0] : null;
    }

    private static void CheckFields(UnityEngine.Object target, string path, string[] fields)
    {
        var so = new SerializedObject(target);

        foreach (string field in fields)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                // 字段不存在通常是类改名/删字段后留下的旧 prefab —— 值得单独报，因为它同时意味着
                // 这个 prefab 上有一个 Unity 静默保留的孤儿键
                Problems.Add($"{path}：{target.GetType().Name}.{field} 字段不存在（prefab 可能是旧版本）");
                continue;
            }

            _checked++;

            if (prop.isArray)
            {
                if (prop.arraySize == 0)
                {
                    Problems.Add($"{path}：{target.GetType().Name}.{field} 是空列表");
                    continue;
                }

                for (int i = 0; i < prop.arraySize; i++)
                {
                    SerializedProperty element = prop.GetArrayElementAtIndex(i);
                    if (element.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (element.objectReferenceValue == null)
                        Problems.Add($"{path}：{target.GetType().Name}.{field}[{i}] 为空");
                }

                continue;
            }

            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (prop.objectReferenceValue == null)
                Problems.Add($"{path}：{target.GetType().Name}.{field} 为空");
        }
    }
}
#endif
