using UnityEngine;

public class WeaponAttack : MonoBehaviour
{
    public int damage = 10;
    public float lifeTime = 4; // 火球存在时间，单位秒
    private Vector3 targetSize;
    public float growSpeed = 3f;
    

    /// <summary>
    /// 淡入淡出的初始化
    /// </summary>
    void Start()
    {
        targetSize = transform.localScale;
        transform.localScale = Vector3.zero;
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
            other.GetComponent<EnemyController>().GetHit(10f, 0.1f);
        }
    }
}
