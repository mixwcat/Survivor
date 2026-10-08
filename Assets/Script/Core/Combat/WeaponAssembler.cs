using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 武器装配器 —— **玩家与塔共用的唯一装配路径**。
///
/// <para>
/// <b>它把「装配一把武器」这件事固定成一条序列：</b>
/// 加载 prefab（<c>AssetReferenceGameObject</c>）→ 实例化到挂点 →
/// 注入 <see cref="WeaponEntitySO"/>（数值与升级项的唯一来源）→ 注入宿主 →
/// 把数值来源与归属告诉 <see cref="AttackDriver"/> → 先停用（由宿主决定何时激活）。
/// </para>
///
/// <para>
/// <b>为什么必须共用：</b>装配序列有四个失败分支（资产服务缺失 / prefab 未配 / 加载失败 /
/// prefab 上没有 <see cref="BaseWeapon"/>）与一条异步生命周期（await 期间宿主可能已被销毁，
/// 此时句柄必须归还）。以前玩家侧（<c>PlayerWeaponController.EquipAsync</c>）与塔侧
/// （<c>TowerWeaponController.EquipAllAsync</c>）各写一遍，任何一边漏一个分支就是句柄泄漏或伪 null 写入。
/// </para>
///
/// <para>
/// <b>武器 prefab 不再预接 <c>entityConfig</c>：</b>装配方注入才是唯一来源 ——
/// prefab 预接会让"同一份 prefab 换一份数值变体 SO"静默失效（<c>SetEntityConfig</c> 在
/// StatModel 建好之后拒绝注入，只打一条告警）。因此 prefab 上的 <c>entityConfig</c> 应当留空，
/// 由本装配器注入；忘了注入会被 <see cref="AttackDriver"/> 在启动时报红错（见那里的检查）。
/// </para>
/// </summary>
public static class WeaponAssembler
{
    /// <summary>
    /// 装配一把武器。<b>返回 null = 装配失败</b>（已打印原因，且不会留下句柄或半成品实例）。
    ///
    /// <para>
    /// <paramref name="owner"/> 既是宿主（必须实现 <see cref="IWeaponHost"/>），
    /// 也是"这次装配属于谁"的生命周期依据 —— await 期间它被销毁（切场景 / 塔被拆 / 玩家死亡）
    /// 时直接放弃，续体不会往已销毁的对象上写字段。
    /// </para>
    /// </summary>
    public static async Task<WeaponInstance> AssembleAsync(Component owner, WeaponEntitySO config, Transform mount)
    {
        if (owner == null || config == null) return null;

        IWeaponHost host = owner as IWeaponHost;
        if (host == null)
        {
            Debug.LogError($"[{nameof(WeaponAssembler)}] {owner.GetType().Name} 没有实现 {nameof(IWeaponHost)}，" +
                           $"武器「{config.name}」无法装配。", owner);
            return null;
        }

        IAssetService assets = AssetService.Service;
        if (assets == null)
        {
            Debug.LogError($"[{nameof(WeaponAssembler)}] IAssetService 未注册，武器「{config.name}」无法装配。", owner);
            return null;
        }

        if (config.prefab == null || !config.prefab.RuntimeKeyIsValid())
        {
            Debug.LogError($"[{nameof(WeaponAssembler)}] 武器「{config.name}」没有配置 prefab（或它不是 Addressable），" +
                           "无法装配。请在 WeaponEntitySO 上指定武器预制体。", owner);
            return null;
        }

        AsyncOperationHandle<GameObject> handle = assets.LoadAssetAsync<GameObject>(config.prefab);
        GameObject prefab = await handle.Task;

        // await 期间宿主可能已被销毁：句柄已经拿到手，必须在这里归还，
        // 否则引用计数只增不减（编辑器下看不出来，真机包才会暴露）
        if (owner == null)
        {
            ReleaseHandle(handle);
            return null;
        }

        if (prefab == null)
        {
            Debug.LogError($"[{nameof(WeaponAssembler)}] 武器 prefab 加载失败：{config.name}。" +
                           DescribeLoadFailure(handle), owner);
            ReleaseHandle(handle);
            return null;
        }

        Transform parent = mount != null ? mount : owner.transform;
        GameObject instance = Object.Instantiate(prefab, parent);

        BaseWeapon weapon = instance.GetComponent<BaseWeapon>();
        if (weapon == null)
        {
            Debug.LogError($"[{nameof(WeaponAssembler)}] 武器 prefab「{prefab.name}」上没有 BaseWeapon，" +
                           "无法装配。已销毁该实例。", owner);
            Object.Destroy(instance);
            ReleaseHandle(handle);
            return null;
        }

        // 注入顺序不能反：先数值（EntityBehaviour），再宿主与归属（AttackDriver）。
        // SetEntityConfig 必须在 StatModel 建立之前生效 —— prefab 留空 entityConfig 时它成立；
        // 若 prefab 预接了别的 SO，这里会拒绝（那正是"换 SO 静默失效"的现场）。
        weapon.SetEntityConfig(config);

        // 注入被拒 = prefab 上预接了**另一份** EntitySO（SetEntityConfig 在 StatModel 建好后拒绝重建）。
        // 后果是"换了 SO 却跑旧数值"—— 数值错了但武器看起来完全正常，只能靠日志发现。
        // 因此这里**当作装配失败**：销毁半成品 + 归还句柄 + 返回 null，
        // 让「失败不会产生 WeaponInstance」成为真正的不变量（见 WeaponInstance 的注释）。
        // 仍然打红错，因为失败原因（prefab 预接了配置）必须被修掉，而不是靠"没装上"来猜。
        if (!ReferenceEquals(weapon.EntityConfig, config))
        {
            Debug.LogError($"[{nameof(WeaponAssembler)}] 武器 prefab「{prefab.name}」上预接了 EntitySO" +
                           $"「{(weapon.EntityConfig != null ? weapon.EntityConfig.name : "null")}」，" +
                           $"本次装配的「{config.name}」被拒绝注入 —— 该武器不会装配。" +
                           "请清空 prefab 的 entityConfig，由装配方注入（见 WeaponAssembler 的类注释）。", owner);

            Object.Destroy(instance);
            ReleaseHandle(handle);
            return null;
        }

        weapon.SetHost(host);
        weapon.BindAttackDriver();

        // 装配出来先停用：Instantiate 的实例默认激活，由宿主决定何时切入
        // （玩家的第一把由 SetActiveEquippedSlot 激活；塔由 SetActiveWeapon 激活）
        weapon.SetActiveSlot(false);

        return new WeaponInstance(config, weapon, handle);
    }

    /// <summary>
    /// 卸下一把武器：销毁实例并归还句柄。
    ///
    /// <para>
    /// <b>顺序是"先发起销毁、再归还句柄"</b>，但<b>不要</b>把它读成"实例先死、句柄后放"：
    /// <see cref="Object.Destroy"/> 是**帧末**延迟销毁，而句柄是当帧立即归还的 ——
    /// 真实顺序恰好相反（实例活到帧末）。这不构成问题，因为实例本来就要在当帧消失；
    /// 之所以仍然先写销毁，是为了让"销毁意图"与实例的归属方（本方法）挨在一起。
    /// 需要"实例真的先消失"时不能用 <c>Destroy</c>（<c>DestroyImmediate</c> 不适用于运行时），
    /// 得改成延迟到销毁回调里再归还句柄。
    /// </para>
    ///
    /// <para>
    /// <b>当前没有调用方</b>：唯一的使用者（<c>PlayerWeaponController.Unequip</c>）已随
    /// 局内换装面板一起删除（见 <see cref="IWeaponManager"/> 的类注释）。
    /// 保留它是因为"卸下"是装配的对称操作，重新引入运行时换装时从这里接；
    /// 实例随宿主销毁的路径（切场景 / 玩家死亡）不需要它，只调
    /// <see cref="ReleaseHandle(WeaponInstance)"/> 即可（层级整体销毁，实例不用单独处理）。
    /// </para>
    /// </summary>
    public static void Disassemble(WeaponInstance instance)
    {
        if (instance == null) return;

        if (instance.Weapon != null) Object.Destroy(instance.Weapon.gameObject);

        ReleaseHandle(instance.PrefabHandle);
    }

    /// <summary>只归还句柄，不销毁实例（用于"实例随宿主一起销毁"的路径）。</summary>
    public static void ReleaseHandle(WeaponInstance instance)
    {
        if (instance == null) return;

        ReleaseHandle(instance.PrefabHandle);
    }

    private static void ReleaseHandle(AsyncOperationHandle<GameObject> handle)
    {
        if (!handle.IsValid()) return;

        AssetService.Service?.Release(handle);
    }

    /// <summary>
    /// 把「加载返回 null」翻译成可行动的原因。
    ///
    /// <para>
    /// <b>最常见的一种是"目标不在任何 Addressables 组里"</b>：<c>AssetReferenceGameObject</c> 与
    /// 地址字符串一样要求目标被标记为 Addressable，否则运行时解析不到 ——
    /// 表现是 handle 不报异常、<c>Result</c> 却是 null（于是武器/投射物静默不工作）。
    /// 不翻译的话日志只有"加载失败"，排查要从"路径对不对"一路试到"组里有没有"。
    /// </para>
    ///
    /// <para>
    /// 实例证据：2026-10 四个塔武器 prefab（<c>Weapon_RinAoE</c> / <c>Weapon_TetoBullet</c> /
    /// <c>Weapon_TetoShell</c> / <c>Weapon_LuoHeal</c>）是在最后一次 <c>Setup Addressables</c>
    /// 之后才建的，一直没进组 —— 塔因此从来没有真正开过火。
    /// </para>
    /// </summary>
    public static string DescribeLoadFailure<T>(AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid() && handle.Status == AsyncOperationStatus.Failed && handle.OperationException != null)
            return $"Addressables 报错：{handle.OperationException.Message}";

        return "Addressables 返回空。常见原因：该资产不在任何 Addressables 组里" +
               "（跑 Tools ▸ Setup Addressables 重新登记），或它的组条目被删掉了。";
    }
}
