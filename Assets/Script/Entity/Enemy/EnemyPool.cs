using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人对象池 —— 按 prefab 分池（三种敌人各自成池，避免取到错误类型）。
///
/// <para>
/// <b>为什么敌人值得池化：</b>场景 <c>maxEnemies = 100</c> + 无尽波最短 0.05s 生成间隔，
/// 峰值约 20 次/秒 <c>Instantiate</c> 与击杀 <c>Destroy</c>，
/// 而每个敌人 prefab 带 Rigidbody2D + 2 个 Collider2D + Animator + 若干脚本，
/// 反复重建在 Android 上是明确的主线程尖峰与 GC 抖动来源。
/// </para>
///
/// <para>
/// 与 <see cref="ExpSpritePool"/> 一样是**静态**池（按需惰性创建，不注册为全局服务）：
/// 池化是每种类型各自的生命周期问题，中心化的只该是规则（见 <see cref="ObjectPool{T}"/>）。
/// 静态状态跨 Play 会话存活，因此必须由 <see cref="Reset"/> 在启动时清空。
/// </para>
/// </summary>
public static class EnemyPool
{
    /// <summary>
    /// 单个 prefab 的池保留上限。
    /// 依据：同时存活上限是 100，但实际很少集中在单个 prefab 上；
    /// 50 足以覆盖「一波被清空后立刻再刷」的复用需求，
    /// 又不会让大量带 Rigidbody2D/Animator 的空闲敌人长期常驻。
    /// </summary>
    private const int MaxRetainedPerPrefab = 50;

    private static readonly Dictionary<GameObject, ObjectPool<EnemyController>> _pools = new();
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

    /// <summary>生成一个敌人（优先复用池中空闲实例）。</summary>
    public static EnemyController Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        EnemyController enemy = GetOrCreatePool(prefab).Get();
        if (enemy == null) return null;

        enemy.transform.SetPositionAndRotation(position, rotation);
        return enemy;
    }

    /// <summary>
    /// 敌人死亡时归还。
    /// 找不到归属池（例如 prefab 引用被换过）时**直接销毁**，不做静默泄漏。
    /// </summary>
    public static void Return(EnemyController enemy)
    {
        if (enemy == null) return;

        GameObject source = enemy.SourcePrefab;
        if (source != null && _pools.TryGetValue(source, out ObjectPool<EnemyController> pool))
        {
            pool.Release(enemy);
            return;
        }

        Object.Destroy(enemy.gameObject);
    }

    private static ObjectPool<EnemyController> GetOrCreatePool(GameObject prefab)
    {
        if (_pools.TryGetValue(prefab, out ObjectPool<EnemyController> pool))
            return pool;

        pool = new ObjectPool<EnemyController>(
            () => CreateInstance(prefab),
            MaxRetainedPerPrefab,
            $"EnemyPool[{prefab.name}]");

        _pools[prefab] = pool;
        return pool;
    }

    private static EnemyController CreateInstance(GameObject prefab)
    {
        if (_root == null)
            _root = new GameObject("[EnemyPool]").transform;

        GameObject instance = Object.Instantiate(prefab, _root);

        EnemyController controller = instance.GetComponent<EnemyController>();
        if (controller == null)
        {
            Debug.LogError($"[EnemyPool] prefab「{prefab.name}」上找不到 EnemyController，已销毁该实例。");
            Object.Destroy(instance);
            return null;
        }

        // 记录来源 prefab，归还时据此定位所属池
        controller.SourcePrefab = prefab;
        return controller;
    }
}
