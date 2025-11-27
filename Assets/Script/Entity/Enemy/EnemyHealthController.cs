using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EnemyHealthController : BaseHealthController
{
    private List<GameObject> colliders = new List<GameObject>();

    void Start()
    {
        EnhanceWithWave();
        currentHealth = maxHealth;
        InvokeRepeating(nameof(HurtColliders), 0f, 1f);
    }


    /// <summary>
    /// 定时对碰撞的玩家和塔造成伤害
    /// </summary>
    private void HurtColliders()
    {
        for (int i = colliders.Count - 1; i >= 0; i--)
        {
            if (colliders[i] == null)
            {
                colliders.RemoveAt(i); // 安全移除
                continue;
            }
            colliders[i].GetComponent<BaseHealthController>().TakeDamage(damage);
        }
    }

    public override void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            CancelInvoke(nameof(HurtColliders));
            Die();
        }
        DamageNumManager.Instance.SpawnDamageNum(transform.position, damage);
    }


    /// <summary>
    /// 根据当前波次增强敌人属性
    /// </summary>
    private void EnhanceWithWave()
    {
        maxHealth += GameLevelManager.Instance.currentWave * 5;
        damage += GameLevelManager.Instance.currentWave * .5f;
    }


    /// <summary>
    /// 与玩家碰撞时造成伤害
    /// </summary>
    /// <param name="other"></param>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Tower"))
        {
            colliders.Add(other.gameObject);
            other.GetComponent<BaseHealthController>().TakeDamage(damage);
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Tower"))
        {
            colliders.Remove(other.gameObject);
        }
    }
}
