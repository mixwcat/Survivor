using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投射物对象池 —— 按 prefab 分池（同一种子弹会被多个塔/玩家武器共用）。
///
/// <para>
/// <b>为什么值得池化：</b>子弹是唯一「高频创建 + 短命」的对象 —— 枪升到最高射速时
/// 每秒 20 发、每发存活 3 秒，而 <c>Instantiate</c> 一个带 SpriteRenderer + Collider2D +
/// Rigidbody2D 的 prefab 在 Android 上是明确的主线程尖峰与 GC 抖动来源。
/// 敌人 / 经验球 / 伤害数字都已池化，唯独投射物此前漏了 —— 而它恰恰是创建频率最高的那一类。
/// </para>
///
/// <para>
/// <b>为什么是静态池：</b>与 <see cref="EnemyPool"/> 同理 —— 池化不是横切关注点，
/// 但投射物是**共享资源**（多个发射者用同一种子弹），按 prefab 分池才能最大化复用。
/// 静态状态跨 Play 会话存活，因此必须由 <see cref="Reset"/> 在启动时清空。
/// </para>
///
/// <para>
/// ⚠️ 联机（Mirror）下投射物要交给 <c>NetworkServer.Spawn/UnSpawn</c> 管生命周期，
/// 与本池是两套管理方式，**不要同时用**：届时由可替换的生成入口决定走哪一条
/// （见审计报告 §7.2「实例化接缝」）。
/// </para>
/// </summary>
public static class ProjectilePool
{
    /// <summary>
    /// 单个 prefab 的池保留上限。
    /// 依据：枪的最高射速 20 发/秒（<see cref="AttackMethodSO.MinInterval"/> = 0.05s）
    /// × 存活 3 秒 ≈ 60 发，取 64 覆盖峰值又不会让池随历史峰值无限增长。
    /// </summary>
    private const int MaxRetainedPerPrefab = 64;

    private static readonly Dictionary<GameObject, ObjectPool<BulletController>> _pools = new();
    private static Transform _root;

    /// <summary>
    /// 重置静态池。由 <see cref="GameBootstrap.ResetStatics"/> 在每次进入 Play 前调用
    /// （编辑器关闭了 Domain Reload，静态字典会跨 Play 会话存活，而其中的 Unity 对象已失效）。
    /// </summary>
    public static void Reset()
    {
        _pools.Clear();
        _root = null;
    }

    /// <summary>
    /// 取出一发投射物（优先复用池中空闲实例）。调用方随后**必须**调
    /// <c>BulletController.Init</c> 灌入本次的伤害/速度/发射者 —— 取出的实例带着上一发的残留状态。
    /// </summary>
    public static BulletController Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        BulletController bullet = GetOrCreatePool(prefab).Get();
        if (bullet == null) return null;

        bullet.transform.SetPositionAndRotation(position, rotation);
        return bullet;
    }

    /// <summary>
    /// 归还投射物（命中或超时）。
    /// 找不到归属池（例如 prefab 引用被换过）时**直接销毁**，不做静默泄漏。
    /// </summary>
    public static void Return(BulletController bullet)
    {
        if (bullet == null) return;

        GameObject source = bullet.SourcePrefab;
        if (source != null && _pools.TryGetValue(source, out ObjectPool<BulletController> pool))
        {
            pool.Release(bullet);
            return;
        }

        Object.Destroy(bullet.gameObject);
    }

    /// <summary>
    /// 池中是否还保留着该 prefab 的空闲实例。
    ///
    /// <para>
    /// <b>用途是句柄归还的顺序判断：</b>池是静态的、实例比 <c>AttackDriver</c> 持有的
    /// Addressables 句柄活得久。最后一个 driver 释放句柄后 bundle 可能被卸载，
    /// 池中实例的 Sprite / 材质会变成空壳，下一次建同种塔复用它们时表现为"子弹看不见"。
    /// 所以释放句柄前要先问一句"池里还有没有它的人"。
    /// </para>
    /// </summary>
    public static bool RetainsInstancesOf(GameObject prefab)
    {
        return prefab != null
               && _pools.TryGetValue(prefab, out ObjectPool<BulletController> pool)
               && pool.IdleCount > 0;
    }

    /// <summary>
    /// 销毁该 prefab 的空闲实例并移除池条目。
    /// 正在飞行中的实例不归池管（它们归还时找不到池会直接销毁），所以这里不会误伤在飞的子弹。
    /// </summary>
    public static void DropPool(GameObject prefab)
    {
        if (prefab == null) return;
        if (!_pools.TryGetValue(prefab, out ObjectPool<BulletController> pool)) return;

        pool.Clear();
        _pools.Remove(prefab);
    }

    private static ObjectPool<BulletController> GetOrCreatePool(GameObject prefab)
    {
        if (_pools.TryGetValue(prefab, out ObjectPool<BulletController> pool))
            return pool;

        pool = new ObjectPool<BulletController>(
            () => CreateInstance(prefab),
            MaxRetainedPerPrefab,
            $"ProjectilePool[{prefab.name}]");

        _pools[prefab] = pool;
        return pool;
    }

    private static BulletController CreateInstance(GameObject prefab)
    {
        // 池根节点随场景销毁；这里判空重建，覆盖「切场景后第一次发射」
        if (_root == null)
            _root = new GameObject("[ProjectilePool]").transform;

        GameObject instance = Object.Instantiate(prefab, _root);

        BulletController bullet = instance.GetComponent<BulletController>();
        if (bullet == null)
        {
            Debug.LogError($"[ProjectilePool] prefab「{prefab.name}」上找不到 BulletController，已销毁该实例。" +
                           "请检查 AttackDriver 上的攻击资源引用。");
            Object.Destroy(instance);
            return null;
        }

        // 记录来源 prefab，归还时据此定位所属池
        bullet.SourcePrefab = prefab;
        return bullet;
    }
}
