using UnityEngine;

/// <summary>
/// 敌人控制器
/// 继承 EntityBehaviour，移速从 StatModel 读取
/// </summary>
public class EnemyController : EntityBehaviour
{
    private float _originalMoveSpeed;
    private Transform targetTransform;
    private Vector2 _direction;
    private Rigidbody2D _rb;

    [Header("游戏控制")]
    private bool _isQuitting = false;

    void Start()
    {
        targetTransform = FindTarget();
        _rb = GetComponent<Rigidbody2D>();
        _originalMoveSpeed = GetStat(StatType.BaseMoveSpeed);

        // 波次增强
        EnhanceWithWave();
    }

    /// <summary>
    /// 根据当前波次增强属性
    /// </summary>
    private void EnhanceWithWave()
    {
        int wave = GameLevelManager.Service.CurrentWave;
        StatModel.AddModifier(new StatModifier(StatType.BaseMaxHealth, wave * 5, EModifierType.Add, GameLevelManager.Service));
        StatModel.AddModifier(new StatModifier(StatType.BaseDamage, wave * 0.5f, EModifierType.Add, GameLevelManager.Service));
    }

    void FixedUpdate()
    {
        MoveTowardsTarget();
        TowardsTarget();
    }

    /// <summary>
    /// 受伤击退
    /// </summary>
    public void HitImpact(float hitForce, float hitDuration)
    {
        float speed = GetStat(StatType.BaseMoveSpeed);
        _rb.linearVelocity = -_direction.normalized * speed * hitForce;
        Invoke(nameof(ResetSpeed), hitDuration);
    }

    private void ResetSpeed()
    {
        _rb.linearVelocity = Vector2.zero;
    }

    private Transform FindTarget()
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
        foreach (var tower in TowerManager.Instance.towers)
        {
            if (tower == null) continue;
            targetTrans = Vector3.Distance(transform.position, tower.transform.position) < Vector3.Distance(transform.position, targetTrans.position) ? tower.transform : targetTrans;
        }
        return targetTrans;
    }

    private void TowardsTarget()
    {
        if (targetTransform == null) return;
        transform.localScale = new Vector3(targetTransform.position.x > transform.position.x ? -1 : 1, transform.localScale.y, transform.localScale.z);
    }

    private void MoveTowardsTarget()
    {
        if (targetTransform == null)
        {
            targetTransform = FindTarget();
            return;
        }
        _direction = (targetTransform.position - transform.position).normalized;
        _rb.linearVelocity = _direction * GetStat(StatType.BaseMoveSpeed);
    }

    void OnEnable()
    {
        GameLevelManager.Service.RegisterEnemy(this);
    }

    void OnDisable()
    {
        if (_isQuitting || !gameObject.scene.isLoaded) return;
        GameLevelManager.Service.UnregisterEnemy(this);
        if (ExpSpritePool.Instance != null)
            ExpSpritePool.Instance.SpawnExpSprite(transform);
    }

    void OnApplicationQuit()
    {
        _isQuitting = true;
    }
}
