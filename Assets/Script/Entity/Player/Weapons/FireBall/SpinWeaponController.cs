using UnityEngine;

public class SpinWeaponController : MonoBehaviour
{
    private int damage;
    private float lifeTime = 1; // 旋转武器存在时间，单位秒
    private Vector3 targetSize;
    private float growSpeed;
    private float hitImpactForce;


    /// <summary>
    /// 初始化旋转武器参数
    /// </summary>
    public void Init(float lifeTime, float size, int dmg, float hitImpactForce)
    {
        this.lifeTime = lifeTime;
        this.damage = dmg;
        this.hitImpactForce = hitImpactForce;
        this.targetSize = new Vector3(size, size, 1);

        transform.localRotation = Quaternion.Euler(0, 0, -90);
    }

    /// <summary>
    /// 淡入淡出的初始化
    /// </summary>
    void Start()
    {
        targetSize = transform.localScale;
        transform.localScale = Vector3.zero;
        growSpeed = Vector3.Distance(transform.localScale, targetSize) / 0.2f; // 1 秒完成
    }

    /// <summary>
    /// 实现旋转武器的淡入淡出
    /// </summary>
    void Update()
    {
        transform.localScale = Vector3.MoveTowards(transform.localScale, targetSize, Time.deltaTime * growSpeed);

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


    /// <summary>
    /// 碰撞检测，攻击并击退
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyHealthController>().TakeDamage(damage);
            other.GetComponent<EnemyController>().HitImpact(hitImpactForce, 0.1f);
            BKMusic.Instance.PlaySound(ResourceEnum.PlayerAttackEnemy);
        }
    }
}
