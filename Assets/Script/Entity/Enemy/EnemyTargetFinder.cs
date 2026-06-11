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
    /// 当前锁定的目标，可能为 null
    /// </summary>
    public Transform CurrentTarget => _currentTarget;

    void Start()
    {
        // 立即执行一次寻敌，避免启动时的延迟
        _currentTarget = FindNearestTarget();
        _nextUpdateTime = Time.time + _updateInterval;
    }

    void Update()
    {
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

    private Transform FindNearestTarget()
    {
        var pm = PlayerManager.Service;
        if (pm == null) return null;

        // 联机兼容：从所有玩家中找最近的
        PlayerController nearestPlayer = null;
        float nearestDist = float.MaxValue;
        foreach (var player in pm.AllPlayers)
        {
            if (player == null) continue;
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestPlayer = player;
            }
        }

        if (nearestPlayer == null) return null;

        Transform targetTrans = nearestPlayer.transform;

        var tm = TowerManager.Instance;
        if (tm != null && tm.towers != null)
        {
            foreach (var tower in tm.towers)
            {
                if (tower == null) continue;
                float distToTower = Vector3.Distance(transform.position, tower.transform.position);
                float distToCurrent = Vector3.Distance(transform.position, targetTrans.position);
                if (distToTower < distToCurrent)
                {
                    targetTrans = tower.transform;
                }
            }
        }

        return targetTrans;
    }
}
