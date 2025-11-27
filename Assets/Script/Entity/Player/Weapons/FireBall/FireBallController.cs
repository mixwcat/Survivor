using UnityEngine;

public class FireBallController : MonoBehaviour
{
    private int damage = 5;
    private float lifeTime = 4; // 火球存在时间，单位秒
    private Vector3 targetSize;
    private float growSpeed;


    /// <summary>
    /// 初始化火球参数
    /// </summary>
    /// <param name="interval"></param>
    public void Init(float interval, float size)
    {
        lifeTime = interval - 1;
        transform.localScale = new Vector3(size, size, 1);

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
    /// 实现火球的淡入淡出
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
    /// <param name="other"></param>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyHealthController>().TakeDamage(damage);
            other.GetComponent<EnemyController>().HitImpact(10f, 0.1f);
            BKMusic.Instance.PlaySound(ResourceEnum.PlayerAttackEnemy);
        }
    }
}
