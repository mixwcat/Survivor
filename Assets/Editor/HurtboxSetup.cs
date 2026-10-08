#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 给所有可受伤实体补上 <see cref="Hurtbox"/> 本体标记。
///
/// <para>
/// <b>为什么由工具批量加：</b>标记必须挂在**本体碰撞体所在的那个 GameObject** 上
/// （通常就是挂 <see cref="BaseHealthController"/> 的根节点）。手工逐个拖容易漏，
/// 而漏掉的表现是"这个实体打不掉血" —— 只在战斗里才暴露，且不报任何错。
/// </para>
///
/// <para>
/// 幂等：已有标记的跳过；本工具同时供 <c>SceneWiringAudit</c> 做回归检查
/// （新增实体 prefab 忘了加标记会在体检里被拦住）。
/// 手动：Tools ▸ Setup Hurtboxes　批处理：<c>-executeMethod HurtboxSetup.SetupFromCommandLine</c>
/// </para>
/// </summary>
public static class HurtboxSetup
{
    [MenuItem("Tools/Setup Hurtboxes")]
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
            Debug.LogError($"[HurtboxSetup] 异常: {e}");
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
        int scanned = 0;
        int added = 0;

        foreach (string path in FindDamageablePrefabs())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                scanned++;
                bool changed = false;

                foreach (BaseHealthController controller in root.GetComponentsInChildren<BaseHealthController>(true))
                {
                    if (controller.GetComponent<Hurtbox>() != null) continue;

                    controller.gameObject.AddComponent<Hurtbox>();
                    changed = true;
                    added++;

                    Debug.Log($"[HurtboxSetup] {System.IO.Path.GetFileName(path)}: " +
                              $"给「{controller.gameObject.name}」加了 Hurtbox");

                    // 标记要跟本体碰撞体在同一个对象上：不同物体时说明结构特殊，
                    // 得人工确认（否则这个实体根本打不掉血）
                    if (controller.GetComponent<Collider2D>() == null)
                    {
                        Debug.LogWarning($"[HurtboxSetup] 「{controller.gameObject.name}」身上没有 Collider2D，" +
                                         "标记加在这里不会生效 —— 请把标记挂到本体碰撞体所在的对象上。");
                    }
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[HurtboxSetup] 完成：扫描 {scanned} 个 prefab，新增 {added} 个 Hurtbox");
        return true;
    }

    /// <summary>
    /// 回归检查：每个可受伤 prefab 的**每个** <see cref="BaseHealthController"/> 都必须带标记。
    /// 供 <c>SceneWiringAudit</c> 调用（新增实体忘了标记 → 体检失败，而不是进战斗才发现打不掉血）。
    /// </summary>
    public static void VerifyAll(List<string> problems)
    {
        foreach (string path in FindDamageablePrefabs())
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                foreach (BaseHealthController controller in root.GetComponentsInChildren<BaseHealthController>(true))
                {
                    if (controller.GetComponent<Hurtbox>() != null) continue;

                    problems.Add($"可受伤实体缺 Hurtbox 标记（会导致打不掉血）: {path} → {controller.gameObject.name}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    /// <summary>所有含血量控制器的 prefab（玩家 / 敌人 / 塔 / 推车 / 未来的可受伤单位）。</summary>
    private static IEnumerable<string> FindDamageablePrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            yield return path;
        }
    }
}
#endif
