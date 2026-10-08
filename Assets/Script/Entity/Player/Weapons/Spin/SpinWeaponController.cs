using UnityEngine;

public class SpinWeaponController : MonoBehaviour
{
    /// <summary>从 0 长到目标尺寸所用的时间（秒）。</summary>
    private const float GrowDuration = 0.2f;

    private int damage;
    private float lifeTime = 1; // 旋转武器存在时间，单位秒
    private Vector3 targetSize;
    private float growSpeed;
    private float hitImpactForce;

    /// <summary>发射者（击杀归属 / 伤害统计 / 联机校验用），可能为 null。</summary>
    private EntityBehaviour _attacker;


    /// <summary>
    /// 初始化旋转武器参数
    /// </summary>
    public void Init(float lifeTime, float size, int dmg, float hitImpactForce,
                     EntityBehaviour attacker = null)
    {
        this.lifeTime = lifeTime;
        this.damage = dmg;
        this.hitImpactForce = hitImpactForce;
        this.targetSize = new Vector3(size, size, 1);
        _attacker = attacker;

        transform.localRotation = Quaternion.Euler(0, 0, -90);
    }

    /// <summary>
    /// 更新目标尺寸（尺寸升级时由 <c>SpinWeapon</c> 推给**已经存在**的火球）。
    ///
    /// <para>
    /// <b>为什么不能由外部直接写 <c>localScale</c>：</b>尺寸在这里是**逐帧插值**到
    /// <see cref="targetSize"/> 的（<c>Update</c> 里的 <c>MoveTowards</c>），
    /// 外部写进 <c>localScale</c> 的值下一帧就会被拉回旧的目标值 ——
    /// 于是"升级尺寸"对已存在的火球完全无效。改目标值才是唯一正确的入口。
    /// </para>
    ///
    /// <para>
    /// 正在消散（<c>lifeTime &lt;= 0</c>，目标尺寸已被置零）的火球不受影响：
    /// <c>Update</c> 每帧都会把目标重新置零，这里推的值会被下一帧覆盖 —— 那是对的，
    /// 即将消失的火球不该因为一次升级而复活。
    /// </para>
    /// </summary>
    public void SetTargetSize(float size)
    {
        targetSize = new Vector3(size, size, 1);

        // 生长速度按目标尺寸换算（与 Start 一致）：不重算的话，变大后的火球
        // 会以"旧尺寸对应的速度"去追新目标，越大越慢
        growSpeed = targetSize.magnitude / GrowDuration;
    }

    /// <summary>
    /// 淡入淡出的初始化
    /// </summary>
    void Start()
    {
        transform.localScale = Vector3.zero;
        growSpeed = targetSize.magnitude / GrowDuration;
    }

    /// <summary>
    /// 实现旋转武器的淡入淡出
    /// </summary>
    void Update()
    {
        // 缩放到达目标后不再逐帧写 Transform（每次写都会触发变换层级更新）
        Vector3 scale = transform.localScale;
        if (scale != targetSize)
        {
            transform.localScale = Vector3.MoveTowards(scale, targetSize, Time.deltaTime * growSpeed);
        }

        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0)
        {
            targetSize = Vector3.zero;
            if (transform.localScale == Vector3.zero)
            {
                Destroy(gameObject);
            }
        }
    }


    private float _nextHitSoundTime;

    /// <summary>
    /// 碰撞检测，攻击并击退
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        // "碰到了谁"走统一解析（本体碰撞体上的 Hurtbox 标记）；
        // "该不该打"由物理层保证：火球在 WeaponHitbox 层，只与 EnemyBody 层配对 ——
        // 于是"怪碰到火球"根本不会产生回调（那曾经被解析成"玩家被碰到"，玩家在几格外掉血）
        BaseHealthController target = DamageTargetResolver.ResolveAlive(other);
        if (target == null) return;

        // 攻击方只提供「伤害 + 击退力度」，击退的方向与时长由受击方统一处理
        // （EnemyHealthController.TakeDamage → EnemyController.HitImpact）
        target.TakeDamage(new DamageInfo(damage, hitImpactForce, _attacker, DamageSource.Orbit));

        // 节流：旋转火球是持续存在的触发体，敌人一多 OnTriggerEnter2D 会连续触发，
        // 每次都 PlayOneShot 会变成噪音墙并长时间占满音源池
        if (Time.time >= _nextHitSoundTime)
        {
            _nextHitSoundTime = Time.time + 0.15f;
            AudioService.Service?.PlaySfx(ResourceEnum.PlayerAttackEnemy);
        }
    }
}
