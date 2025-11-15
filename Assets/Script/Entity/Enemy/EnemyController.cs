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
        targetTransform = PlayerManager.Instance.player.transform;
        rb = GetComponent<Rigidbody2D>();
        originalMoveSpeed = moveSpeed;
    }

    /// <summary>
    /// 向玩家移动
    /// </summary>
    void FixedUpdate()
    {
        if (targetTransform == null) return;
        MoveTowardsPlayer();
    }


    /// <summary>
    /// 受伤击退
    /// </summary>
    /// <param name="hitForce"></param>
    /// <param name="hitDuration"></param>
    public void GetHit(float hitForce, float hitDuration)
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
    /// 向玩家移动
    /// </summary>
    private void MoveTowardsPlayer()
    {
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
        if (isQuitting) return;
        GameLevelManager.Instance.UnregisterEnemy(this);
        ExperienceLevController.Instance.SpawnExpSprite(transform);
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }
}
