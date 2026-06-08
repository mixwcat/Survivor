using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人血量控制器
/// 波次增强已移至 EnemyController.EnhanceWithWave()
/// </summary>
public class EnemyHealthController : BaseHealthController
{
    private List<GameObject> _colliders = new();

    void Start()
    {
        // 增强已在 EnemyController.Start 中完成，这里只初始化血量
        CurrentHealth = MaxHealth;
        InvokeRepeating(nameof(HurtColliders), 0f, 1f);
    }

    /// <summary>
    /// 定时对碰撞的玩家和塔造成伤害
    /// </summary>
    private void HurtColliders()
    {
        for (int i = _colliders.Count - 1; i >= 0; i--)
        {
            if (_colliders[i] == null)
            {
                _colliders.RemoveAt(i);
                continue;
            }
            _colliders[i].GetComponent<BaseHealthController>()?.TakeDamage(Damage);
        }
    }

    public override void TakeDamage(float damage)
    {
        CurrentHealth -= damage;
        if (CurrentHealth <= 0)
        {
            CancelInvoke(nameof(HurtColliders));
            Die();
        }
        DamageNumManager.Instance.SpawnDamageNum(transform.position, damage);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Tower"))
        {
            _colliders.Add(other.gameObject);
            other.GetComponent<BaseHealthController>()?.TakeDamage(Damage);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Tower"))
        {
            _colliders.Remove(other.gameObject);
        }
    }
}
