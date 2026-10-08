using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投射物：直线飞行，撞到敌人后结算伤害并回收（池化复用，不再 Destroy）。
///
/// <para>
/// <b>单体与溅射共用这一份命中流程</b>：差别只在 <see cref="Init"/> 传入的 <c>splashRadius</c> ——
/// <c>&lt;= 0</c> 是单体（命中即消耗），<c>&gt; 0</c> 时对命中点周围的目标一起结算。
/// 因此换攻击方式不需要换 prefab，数值也不用复制一份。
/// </para>
///
/// <para>
/// <b>子类的扩展点是 <see cref="OnHit"/></b>，不要重写 <c>OnTriggerEnter2D</c>：
/// 命中流程还绑着目标解析（<see cref="DamageTargetResolver.ResolveAlive"/>）、
/// 归属（<c>_attacker</c>）、生成方回收（联机时是网络 UnSpawn）与池化复位 ——
/// 复制一份出来就是两条必须永远同步的路径，漏改一条是静默的。
/// 炮弹的表现（爆炸特效/音效）走 <see cref="ShellController"/> 覆写 <see cref="OnHit"/>。
/// </para>
///
/// <para>
/// <b>移动由 Kinematic <c>Rigidbody2D</c> 的速度驱动</b>，不再逐帧写 <c>transform.position</c>：
/// 后者等于每帧搬动一个**静态碰撞体**（物理引擎要同步 broadphase），而且没有连续碰撞检测 ——
/// 高速子弹在帧率下降时会直接穿过敌人。prefab 上的 <c>Collision Detection</c> 建议设为 Continuous。
/// </para>
/// </summary>
public class BulletController : MonoBehaviour, IPoolable
{
    /// <summary>超时自毁（秒）。池化后由 <see cref="OnGetFromPool"/> 重新计时 —— 不能挂在 Start 上。</summary>
    private const float LifeTime = 3f;

    private int _damage = 20;
    private int _hitForce = 20;
    private float _speed = 20f;
    private Vector3 _direction;
    private float _splashRadius;
    private EntityBehaviour _attacker;
    private IAttackSpawner _spawner;
    private Rigidbody2D _rb;

    /// <summary>创建它的源 prefab，归还时据此定位所属池（由 <see cref="ProjectilePool"/> 写入）。</summary>
    [System.NonSerialized] public GameObject SourcePrefab;

    /// <summary>
    /// 溅射查询的复用缓冲。static 是因为它只在一次同步查询内有效，
    /// 每次用前先 <c>Clear()</c>，所以不需要接入 <c>GameBootstrap.ResetStatics()</c>。
    /// <para>
    /// ⚠️ 若将来物理查询改为并行（job 化），这个共享缓冲会变成竞态，必须改成实例字段。
    /// </para>
    /// </summary>
    private static readonly List<Collider2D> SplashHits = new List<Collider2D>(32);
    private static ContactFilter2D _splashFilter;
    private static bool _splashFilterReady;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
        {
            Debug.LogError($"[{nameof(BulletController)}]「{name}」没有 Rigidbody2D，投射物不会移动。" +
                           "请在 prefab 上加 Kinematic Rigidbody2D（Collision Detection 建议 Continuous）。");
        }
    }

    /// <summary>
    /// 灌入本次发射的参数。<paramref name="splashRadius"/> 与 <paramref name="attacker"/>
    /// 都是带默认值的后加参数，既有调用点不需要改动。
    /// </summary>
    public virtual void Init(int dmg, int force, float speed, Vector3 dir,
                             float splashRadius = 0f, EntityBehaviour attacker = null,
                             IAttackSpawner spawner = null)
    {
        _damage = dmg;
        _hitForce = force;
        _speed = speed;
        _direction = dir;
        _splashRadius = splashRadius;
        _attacker = attacker;
        _spawner = spawner;

        if (_rb != null)
            _rb.linearVelocity = new Vector2(dir.x, dir.y) * speed;
    }

    // ── IPoolable ──

    /// <summary>
    /// 取出时复位。复用的是同一个实例：上一发可能是炮弹（有溅射半径）、
    /// 也可能来自另一个发射者，所以状态必须清干净（<c>Init</c> 随后会灌入本次的值）。
    /// </summary>
    public void OnGetFromPool()
    {
        _splashRadius = 0f;
        _attacker = null;
        _spawner = null;

        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        // 超时计时必须在这里重挂：Start 只跑第一次，池化复用时不会重跑
        CancelInvoke();
        Invoke(nameof(ReturnSelf), LifeTime);
    }

    /// <summary>归还前停表并清零速度，避免残留状态影响下一个使用者。</summary>
    public void OnReturnToPool()
    {
        CancelInvoke();

        if (_rb != null) _rb.linearVelocity = Vector2.zero;
    }

    /// <summary>超时回收（不是销毁）—— 走池才不会有每次射击一次的分配。</summary>
    private void ReturnSelf()
    {
        // 优先交给**生成方**回收：联机时它是网络实现（UnSpawn），单机时就是本地的池。
        // 没有生成方（例如手动挂到场景里的调试用子弹）才退回池。
        if (_spawner != null)
        {
            _spawner.Despawn(gameObject);
            return;
        }

        ProjectilePool.Return(this);
    }

    /// <summary>
    /// 碰撞检测 → 交给 <see cref="OnHit"/> 结算 → 回收。
    /// <b>不要在子类里重写本方法</b>（理由见类注释）。
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // "该不该打"由物理层保证：攻击判定体在 WeaponHitbox 层，只与 EnemyBody 层配对 ——
        // 子弹在物理上就碰不到玩家、塔、推车、检测圈，所以这里不再需要 tag 判定
        BaseHealthController target = DamageTargetResolver.ResolveAlive(other);
        if (target == null) return;

        OnHit(target);
        ReturnSelf();
    }

    /// <summary>
    /// **命中一个目标后的结算 —— 子类唯一的扩展点。**
    ///
    /// <para>
    /// 默认行为：有溅射半径就对命中点周围结算，否则只打这一个目标。
    /// 炮弹（<see cref="ShellController"/>）覆写它来加爆炸表现；覆写时**先调 <c>base.OnHit</c>**，
    /// 伤害与溅射的规则就只有这一份实现。
    /// </para>
    /// </summary>
    protected virtual void OnHit(BaseHealthController target)
    {
        if (_splashRadius > 0f)
        {
            ApplySplashDamage();
            return;
        }

        // 攻击方只提供「伤害 + 击退力度」，击退的方向与时长由受击方统一处理
        // （EnemyHealthController.TakeDamage → EnemyController.HitImpact）
        target.TakeDamage(new DamageInfo(_damage, _hitForce, _attacker, DamageSource.Projectile));
    }

    /// <summary>本次发射的溅射半径（&lt;= 0 = 单体）。供子类做配置自检。</summary>
    protected float SplashRadius => _splashRadius;

    /// <summary>
    /// 把投射物朝向对齐飞行方向。<paramref name="angleOffsetDegrees"/> 用于美术资源的
    /// "正前方"不是 +X 的情况（Teto 的子弹贴图朝上，所以传 -90）。
    /// </summary>
    protected void FaceDirection(Vector3 direction, float angleOffsetDegrees)
    {
        transform.Rotate(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + angleOffsetDegrees);
    }

    /// <summary>
    /// 对命中点周围 <c>splashRadius</c> 内的全部敌人结算伤害。
    ///
    /// <para>
    /// 用非分配的 <c>OverlapCircle</c> + 复用 List，避免每发炮弹一次数组分配。
    /// 每个敌人只结算一次由 <see cref="DamageTargetResolver"/> 保证
    /// （只有本体碰撞体带 <see cref="Hurtbox"/> 标记，敌人的触发体不带）——
    /// 这正是以前靠 <c>useTriggers = false</c> 绕开、却总在新增实体时踩坑的地方。
    /// </para>
    /// <para>
    /// 直击的那个敌人也在半径内，因此这里**不**额外结算一次单体伤害，否则会打两下。
    /// </para>
    /// </summary>
    private void ApplySplashDamage()
    {
        SplashHits.Clear();
        int count = Physics2D.OverlapCircle(transform.position, _splashRadius, SplashFilter, SplashHits);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = SplashHits[i];
            if (hit == null) continue;

            BaseHealthController target = DamageTargetResolver.ResolveAlive(hit);
            if (target == null) continue;

            target.TakeDamage(new DamageInfo(_damage, _hitForce, _attacker, DamageSource.Projectile));
        }
    }

    /// <summary>
    /// 溅射查询用的过滤器：**只查 EnemyBody 层**。
    ///
    /// <para>
    /// <c>ContactFilter2D</c> 默认构造出的 <c>layerMask</c> 是 0，会过滤掉**所有**层，
    /// 所以必须显式设置（CLAUDE.md 记过这一条：查询会**静默**返回 0 个结果）。
    /// </para>
    /// <para>
    /// 按层过滤后不必再判 tag，也不必靠"只查非触发体"绕开敌人的触发体 ——
    /// 每个敌人只有本体碰撞体在 EnemyBody 层，天然只结算一次。
    /// </para>
    /// </summary>
    private static ContactFilter2D SplashFilter
    {
        get
        {
            if (!_splashFilterReady)
            {
                _splashFilter = new ContactFilter2D
                {
                    useTriggers = true,
                    useLayerMask = true,
                    layerMask = 1 << LayerMask.NameToLayer("EnemyBody"),
                };
                _splashFilterReady = true;
            }
            return _splashFilter;
        }
    }
}
