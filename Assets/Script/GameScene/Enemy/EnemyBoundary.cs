using UnityEngine;

public class EnemyBoundary : MonoBehaviour
{
    public void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject); // 销毁离开触发器的敌人
        }
    }
}
