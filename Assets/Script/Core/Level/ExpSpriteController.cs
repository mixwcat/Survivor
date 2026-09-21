using UnityEngine;

public class ExpSpriteController : MonoBehaviour
{
    private PlayerController player;
    public float moveSpeed = 5f;

    private void Start()
    {
        player = PlayerManager.Service.LocalPlayer;
        Invoke(nameof(DestorySelf), 20f);
    }

    void Update()
    {
        MoveTowardsPlayer();
    }

    private void MoveTowardsPlayer()
    {
        if (player == null)
        {
            return;
        }

        if (Vector2.Distance(transform.position, player.transform.position) < player.GetStat(StatType.PlayerPickRange))
        {
            transform.position = Vector2.MoveTowards(transform.position, player.transform.position, moveSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 给碰撞到的玩家自身加经验（按玩家实例化）
            var player = other.GetComponent<PlayerController>();
            if (player != null && player.ExperienceController != null)
            {
                player.ExperienceController.AddExperience(1);
            }

            ExpSpritePool.Instance.ReturnToPool(this);
            AudioService.Service?.PlaySfx(ResourceEnum.PickExp);
        }
    }

    private void DestorySelf()
    {
        ExpSpritePool.Instance.ReturnToPool(this);
    }
}
