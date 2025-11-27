using UnityEngine;

public class DetectPlayer : MonoBehaviour
{
    public GameObject arrow;
    public GameObject txtE;
    private bool isPlayerInRange = false;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            arrow.SetActive(true);
            isPlayerInRange = true;
            txtE.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            arrow.SetActive(false);
            isPlayerInRange = false;
            txtE.SetActive(false);
        }
    }

    void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            UIManager.Instance.ShowPanel<TowerLevelUpPanel>().SetTowerType(GetComponentInParent<BaseTower>());
        }
    }
}
