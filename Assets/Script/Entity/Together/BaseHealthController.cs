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

    protected virtual void Die()
    {
        gameObject.SetActive(false);
    }
}
