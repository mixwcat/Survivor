using UnityEngine;

public class EnemyHealthController : BaseHealthController
{
    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        DamageNumManager.Instance.SpawnDamageNum(transform.position, damage);
    }


    /// <summary>
    /// 与玩家碰撞时造成伤害
    /// </summary>
    /// <param name="other"></param>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerHealthController>().TakeDamage(damage);
        }
    }
}
