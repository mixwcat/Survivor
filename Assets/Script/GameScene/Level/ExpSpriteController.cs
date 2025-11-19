using UnityEngine;

public class ExpSpriteController : MonoBehaviour
{
    private PlayerController player;
    public float moveSpeed = 5f;

    private void Start()
    {
        player = PlayerManager.Instance.player;
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
        
        if (Vector2.Distance(transform.position, player.transform.position) < player.pickRange)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.transform.position, moveSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ExperienceLevController.Instance.AddExperience(1);
            ExpSpritePool.Instance.ReturnToPool(this);
        }
    }
}
