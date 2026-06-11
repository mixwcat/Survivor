using UnityEngine;

/// <summary>
/// 敌人控制器
/// 继承 EntityBehaviour，移速从 StatModel 读取
/// 寻敌逻辑由 EnemyTargetFinder 处理，本类只负责移动与击退
/// </summary>
[RequireComponent(typeof(EnemyTargetFinder))]
public class EnemyController : EntityBehaviour
{
    private float _originalMoveSpeed;
    private Vector2 _direction;
    private Rigidbody2D _rb;
    private EnemyTargetFinder _targetFinder;

    [Header("击退恢复")]
    [Tooltip("击退结束后速度渐变恢复到正常寻敌的持续时间")]
    [SerializeField] private float _knockbackRecoveryDuration = 0.1f;
    private float _knockbackEndTime;
    private float _knockbackRecoveryEndTime;
    private Vector2 _knockbackVelocity;

    [Header("游戏控制")]
    private bool _isQuitting = false;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _targetFinder = GetComponent<EnemyTargetFinder>();
        _originalMoveSpeed = GetStat(StatType.MoveSpeed);

        // 波次增强
        EnhanceWithWave();
    }

    /// <summary>
    /// 根据当前波次增强属性
    /// </summary>
    private void EnhanceWithWave()
    {
        int wave = GameLevelManager.Service.CurrentWave;
        StatModel.AddModifier(new StatModifier(StatType.MaxHealth, wave * 5, EModifierType.Add, GameLevelManager.Service));
        StatModel.AddModifier(new StatModifier(StatType.Damage, wave * 0.5f, EModifierType.Add, GameLevelManager.Service));
    }

    void FixedUpdate()
    {
        float now = Time.time;

        // 阶段1：纯击退阶段，保持击退速度，不执行寻敌移动
        if (now < _knockbackEndTime)
        {
            TowardsTarget();
            return;
        }

        // 阶段2：击退恢复阶段，速度从击退残余平滑过渡到正常寻敌速度
        if (now < _knockbackRecoveryEndTime)
        {
            float t = (now - _knockbackEndTime) / _knockbackRecoveryDuration;
            Vector2 chaseVelocity = GetChaseVelocity();
            _rb.linearVelocity = Vector2.Lerp(_knockbackVelocity, chaseVelocity, t);
            TowardsTarget();
            return;
        }

        // 阶段3：正常寻敌
        MoveTowardsTarget();
        TowardsTarget();
    }

    /// <summary>
    /// 受伤击退
    /// </summary>
    public void HitImpact(float hitForce, float hitDuration)
    {
        if (hitDuration <= 0) return; // 无击退时间则不执行击退
        float speed = GetStat(StatType.MoveSpeed);
        _knockbackVelocity = -_direction.normalized * speed * hitForce;
        _rb.linearVelocity = _knockbackVelocity;
        _knockbackEndTime = Time.time + hitDuration;
        _knockbackRecoveryEndTime = _knockbackEndTime + _knockbackRecoveryDuration;
    }

    private void TowardsTarget()
    {
        Transform target = _targetFinder != null ? _targetFinder.CurrentTarget : null;
        if (target == null) return;
        transform.localScale = new Vector3(target.position.x > transform.position.x ? -1 : 1, transform.localScale.y, transform.localScale.z);
    }

    private void MoveTowardsTarget()
    {
        Transform target = _targetFinder != null ? _targetFinder.CurrentTarget : null;
        if (target == null) return;

        if ((target.position - transform.position).magnitude < 1)
        {
            _direction = Vector2.zero;
            return;
        }

        _direction = (target.position - transform.position).normalized;
        _rb.linearVelocity = _direction * GetStat(StatType.MoveSpeed);
    }

    /// <summary>
    /// 计算当前正常寻敌的速度向量
    /// </summary>
    private Vector2 GetChaseVelocity()
    {
        Transform target = _targetFinder != null ? _targetFinder.CurrentTarget : null;
        if (target == null) return Vector2.zero;

        Vector2 dir = (target.position - transform.position).normalized;
        return dir * GetStat(StatType.MoveSpeed);
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
