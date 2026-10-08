using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人目标搜索器
/// 负责定时查找最近的可攻击目标（玩家或塔）
/// 从 EnemyController 抽离，实现寻敌逻辑与移动控制解耦
/// </summary>
public class EnemyTargetFinder : MonoBehaviour
{
    [Header("寻敌配置")]
    [Tooltip("目标更新间隔（秒），不需要每帧都搜索")]
    [SerializeField] private float _updateInterval = 0.5f;

    private float _nextUpdateTime;
    private Transform _currentTarget;

    /// <summary>
    /// 本副本是否该跑寻敌。联机时只有服务端 —— 客户端的 <c>EnemyController</c> 本来就不跑 AI，
    /// 这里每 0.5 秒搜一次纯属白烧 CPU（敌人多时是每只一份）。
    /// </summary>
    private NetworkAuthority _authority;

    /// <summary>
    /// 当前锁定的目标，可能为 null
    /// </summary>
    public Transform CurrentTarget => _currentTarget;

    void Awake()
    {
        _authority = new NetworkAuthority(gameObject);
    }

    void Start()
    {
        if (!_authority.IsAuthority) return;

        // 立即执行一次寻敌，避免启动时的延迟
        _currentTarget = FindNearestTarget();
        _nextUpdateTime = Time.time + _updateInterval;
    }

    void Update()
    {
        if (!_authority.IsAuthority) return;

        if (Time.time >= _nextUpdateTime)
        {
            _currentTarget = FindNearestTarget();
            _nextUpdateTime = Time.time + _updateInterval;
        }
    }

    /// <summary>
    /// 强制立即刷新目标（外部调用，如目标死亡时）
    /// </summary>
    public void RefreshTarget()
    {
        _currentTarget = FindNearestTarget();
        _nextUpdateTime = Time.time + _updateInterval;
    }

    /// <summary>
    /// 找最近的可攻击目标。
    ///
    /// <para>
    /// 目标来自 <see cref="IEnemyTargetRegistry"/>（玩家 / 塔 / 推车都注册在那里），
    /// 这里不分别遍历各个 Manager —— 每加一种可攻击单位都要改一次这个方法，
    /// 而它本该只回答"最近的目标是谁"。
    /// </para>
    /// </summary>
    private Transform FindNearestTarget()
    {
        IEnemyTargetRegistry registry = EnemyTargetRegistry.Service;
        if (registry == null) return null;

        IReadOnlyList<Transform> targets = registry.Targets;
        Vector3 selfPos = transform.position;

        Transform nearest = null;
        float nearestDist = float.MaxValue;

        // 用 for + 索引器而不是 foreach：Targets 是 IReadOnlyList<T>，foreach 会走
        // IEnumerable<T>.GetEnumerator() 把 List 的结构体枚举器装箱，每次寻敌都产生堆分配
        // （敌人多、寻敌每 0.5s 一次，累计很可观）。
        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];
            if (target == null) continue;   // 已销毁但尚未注销（销毁顺序不确定）

            float dist = (target.position - selfPos).sqrMagnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = target;
            }
        }

        return nearest;
    }
}
