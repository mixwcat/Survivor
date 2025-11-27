using System;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed;
    private float originalMoveSpeed;
    public Transform targetTransform;
    private Vector2 direction;
    private Rigidbody2D rb;

    [Header("游戏控制")]
    private bool isQuitting = false;


    void Start()
    {
        targetTransform = FindTarget();
        rb = GetComponent<Rigidbody2D>();
        originalMoveSpeed = moveSpeed;
    }

    /// <summary>
    /// 向玩家移动
    /// </summary>
    void FixedUpdate()
    {
        MoveTowardsTarget();
        TowardsTarget();
    }


    /// <summary>
    /// 受伤击退
    /// </summary>
    /// <param name="hitForce"></param>
    /// <param name="hitDuration"></param>
    public void HitImpact(float hitForce, float hitDuration)
    {
        moveSpeed = originalMoveSpeed;
        moveSpeed = -moveSpeed * hitForce;
        Invoke(nameof(ResetSpeed), hitDuration);
    }

    void ResetSpeed()
    {
        moveSpeed = originalMoveSpeed;
    }


    /// <summary>
    /// 寻找目标
    /// </summary>
    private Transform FindTarget()
    {
        if (PlayerManager.Instance.player != null)
        {
            // 挑选距离敌人近的目标
            Transform targetTrans = PlayerManager.Instance.player.transform;
            foreach (var tower in TowerManager.Instance.towers)
            {
                if (tower == null) continue;
                targetTrans = Vector3.Distance(transform.position, tower.transform.position) < Vector3.Distance(transform.position, targetTrans.position) ? tower.transform : targetTrans;
            }

            return targetTrans;
        }

        return null;
    }


    private void TowardsTarget()
    {
        if (targetTransform == null) return;
        transform.localScale = new Vector3(targetTransform.position.x > transform.position.x ? -1 : 1, transform.localScale.y, transform.localScale.z);
    }


    /// <summary>
    /// 向目标移动
    /// </summary>
    private void MoveTowardsTarget()
    {
        if (targetTransform == null)
        {
            targetTransform = FindTarget();
            if (targetTransform == null) return;
        }
        direction = (targetTransform.position - transform.position).normalized;
        rb.linearVelocity = direction * moveSpeed;
    }


    /// <summary>
    /// 在GameLevManager中注册
    /// </summary>
    void OnEnable()
    {
        GameLevelManager.Instance.RegisterEnemy(this);
    }

    void OnDisable()
    {

        // 正在退出时不执行逻辑
        if (isQuitting || !gameObject.scene.isLoaded) return;
        GameLevelManager.Instance.UnregisterEnemy(this);

        // 生成经验精灵
        if (ExpSpritePool.Instance != null)
            ExpSpritePool.Instance.SpawnExpSprite(transform);
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }
}
