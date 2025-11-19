using UnityEngine;

public class BulletController : MonoBehaviour
{
    private int damage = 20;
    private int hitForce = 20;
    private float speed = 30f;
    private Vector3 direction;


    public void Init(int dmg, int force, Vector3 dir)
    {
        damage = dmg;
        hitForce = force;
        direction = dir.normalized + new Vector3(0, 0, 45);
    }


    private void Start()
    {
        Invoke(nameof(DestroySelf), 3f);
    }

    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }


    /// <summary>
    /// 子弹自毁
    /// </summary>
    private void DestroySelf()
    {
        Destroy(gameObject);
    }


    /// <summary>
    /// 碰撞检测，攻击并击退
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            other.GetComponent<EnemyHealthController>().TakeDamage(damage);
            other.GetComponent<EnemyController>().HitImpact(hitForce, 0.1f);
            Destroy(gameObject);
        }
    }
}
