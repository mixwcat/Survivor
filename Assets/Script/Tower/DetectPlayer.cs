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
            TouchPlayerLogic(true);
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            TouchPlayerLogic(false);
        }
    }


    /// <summary>
    /// 接触玩家后的逻辑
    /// </summary>
    /// <param name="isTouching"></param>
    private void TouchPlayerLogic(bool isTouching)
    {
#if UNITY_STANDALONE_WIN
        SetButtonActive(isTouching);
#elif UNITY_ANDROID
        ShowTowerLevelUpButton(isTouching);
#endif
    }
    private void SetButtonActive(bool isActive)
    {
        arrow.SetActive(isActive);
        isPlayerInRange = isActive;
        txtE.SetActive(isActive);
    }
    private void ShowTowerLevelUpButton(bool isActive)
    {
        UIManager.Instance.GetPanel<GamePanel>().SetButtonTowerLevelUpActive(isActive, GetComponentInParent<BaseTower>());
    }

    void Update()
    {
#if UNITY_STANDALONE_WIN
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            UIManager.Instance.ShowPanel<TowerLevelUpPanel>().SetTowerType(GetComponentInParent<BaseTower>());
#endif
        }
    }
}
