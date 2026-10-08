using UnityEngine;

/// <summary>
/// 敌人控制器
/// 继承 EntityBehaviour，移速从 StatModel 读取
/// 寻敌逻辑由 EnemyTargetFinder 处理，本类只负责移动与击退
/// </summary>
[RequireComponent(typeof(EnemyTargetFinder))]
public class EnemyController : EntityBehaviour, IPoolable
{
    private Vector2 _direction;
    private Rigidbody2D _rb;
    private EnemyTargetFinder _targetFinder;

    /// <summary>本实例来源的 prefab；由 <see cref="EnemyPool"/> 在创建时写入，归还时据此定位所属池。</summary>
    public GameObject SourcePrefab { get; set; }

    [Header("击退")]
    [Tooltip("受击后的纯击退（硬直）时长；<=0 表示不击退")]
    [SerializeField] private float _hitStunDuration = 0.1f;

    [Header("击退恢复")]
    [Tooltip("击退结束后速度渐变恢复到正常寻敌的持续时间")]
    [SerializeField] private float _knockbackRecoveryDuration = 0.1f;
    private float _knockbackEndTime;
    private float _knockbackRecoveryEndTime;
    private Vector2 _knockbackVelocity;

    [Header("游戏控制")]
    private bool _isQuitting = false;

    /// <summary>
    /// 组件引用必须在 Awake 拿：本类会被对象池复用，而 <c>Start</c> 对池化对象**只执行第一次**。
    /// 注意是 <c>override</c> —— 写成同名非 override 方法会隐藏基类 Awake，
    /// 导致 <c>StatModel</c> 永远不被初始化。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        _rb = GetComponent<Rigidbody2D>();
        _targetFinder = GetComponent<EnemyTargetFinder>();
    }

    /// <summary>
    /// 每次从池中取出都重置运行时状态。
    /// 池化对象不会重跑 <c>Start</c>，所以「波次增强 / 血量 / 击退残留」都必须在这里重做，
    /// 否则复用时会带着上一次的数值与速度出场（增强还会不断累加）。
    /// </summary>
    public void OnGetFromPool()
    {
        _isQuitting = false;
        _direction = Vector2.zero;
        _knockbackVelocity = Vector2.zero;
        _knockbackEndTime = 0f;
        _knockbackRecoveryEndTime = 0f;

        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        // 清掉上一次的波次增强与哨站难度，否则同一实例被复用时数值会累加
        StatModel?.RemoveModifiersFromSource(GameLevelManager.Service);
        EnhanceWithWave();

        // 哨站难度是**另一个来源**：Apply 内部会先按自己的来源清一次，所以这里是幂等的
        OutpostDifficulty.Apply(StatModel);

        // 血量重置（Start 只跑第一次，池化复用必须显式重置）
        if (TryGetComponent(out BaseHealthController health))
            health.ResetHealth();
    }

    public void OnReturnToPool()
    {
        if (_rb != null)
            _rb.linearVelocity = Vector2.zero;

        _direction = Vector2.zero;
        _knockbackVelocity = Vector2.zero;
        _knockbackEndTime = 0f;
        _knockbackRecoveryEndTime = 0f;
    }

    /// <summary>
    /// 根据当前波次增强属性
    /// </summary>
    private void EnhanceWithWave()
    {
        if (StatModel == null) return;

        IGameLevelManager level = GameLevelManager.Service;
        if (level == null) return;

        int wave = level.CurrentWave;
        StatModel.AddModifier(new StatModifier(StatType.MaxHealth, wave * 5, EModifierType.Add, level));
        StatModel.AddModifier(new StatModifier(StatType.Damage, wave * 0.5f, EModifierType.Add, level));
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
    /// 受伤击退。
    /// 力度由攻击方传入（武器数值 <see cref="StatType.HitPushForce"/>），
    /// 方向由受击方现算、硬直/恢复时长走本组件的序列化字段 —— 攻击方无需知道这些。
    /// </summary>
    public void HitImpact(float hitForce)
    {
        if (hitForce <= 0f || _hitStunDuration <= 0f) return; // 无力度或无击退时长则不执行击退
        float speed = GetStat(StatType.MoveSpeed);

        // 击退方向必须现算：_direction 在贴身（距离 < 1）时会被置零，
        // 用它做方向会得到零向量 —— 敌人原地冻结整个硬直时长，恰恰在最需要击退时失效
        Transform target = _targetFinder != null ? _targetFinder.CurrentTarget : null;
        Vector2 awayFromTarget = target != null
            ? ((Vector2)transform.position - (Vector2)target.position).normalized
            : (_direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector2.zero);

        _knockbackVelocity = awayFromTarget * speed * hitForce;
        _rb.linearVelocity = _knockbackVelocity;
        _knockbackEndTime = Time.time + _hitStunDuration;
        _knockbackRecoveryEndTime = _knockbackEndTime + _knockbackRecoveryDuration;
    }

    private void TowardsTarget()
    {
        Transform target = _targetFinder != null ? _targetFinder.CurrentTarget : null;
        if (target == null) return;

        float facing = target.position.x > transform.position.x ? -1f : 1f;

        // 朝向没变就不要写 Transform：每次写 localScale 都会触发一次变换层级更新，
        // 几十上百个敌人逐帧写是实打实的开销。
        Vector3 scale = transform.localScale;
        if (scale.x == facing) return;

        scale.x = facing;
        transform.localScale = scale;
    }

    private void MoveTowardsTarget()
    {
        Transform target = _targetFinder != null ? _targetFinder.CurrentTarget : null;

        // 目标消失（死亡/回池）时必须**显式停住**：直接 return 会保留上一帧的速度，
        // 敌人继续沿原方向滑行，直到下一次寻敌（0.5s 间隔）才纠正 ——
        // 接触伤害与站位因此受帧率影响
        if (target == null)
        {
            _direction = Vector2.zero;
            StopMoving();
            return;
        }

        Vector2 delta = (Vector2)target.position - (Vector2)transform.position;

        // 用 sqrMagnitude 省掉每帧每敌人的一次开方
        if (delta.sqrMagnitude < 1f)
        {
            _direction = Vector2.zero;
            StopMoving();
            return;
        }

        _direction = delta.normalized;
        _rb.linearVelocity = _direction * GetStat(StatType.MoveSpeed);
    }

    /// <summary>停下（不写零速度会让"贴到目标"变成"推着目标走"）。</summary>
    private void StopMoving()
    {
        if (_rb == null) return;

        _rb.linearVelocity = Vector2.zero;
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
        GameLevelManager.Service?.RegisterEnemy(this);
    }

    void OnDisable()
    {
        if (_isQuitting || !gameObject.scene.isLoaded) return;

        // 只做注销。经验掉落已移到 Die()：池化归还同样会触发 OnDisable，
        // 留在这里会让「回收一个没死的敌人」也掉经验。
        GameLevelManager.Service?.UnregisterEnemy(this);
    }

    void OnApplicationQuit()
    {
        _isQuitting = true;
    }
}
