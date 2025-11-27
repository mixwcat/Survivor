using UnityEngine;

public class BaseHealthController : MonoBehaviour
{
    public float maxHealth, currentHealth, damage;


    void Start()
    {
        currentHealth = maxHealth;
    }


    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="damage"></param>
    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 治疗
    /// </summary>
    public virtual void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        DamageNumManager.Instance.SpawnDamageNum(transform.position, amount, DamageNumType.green);
    }


    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}
