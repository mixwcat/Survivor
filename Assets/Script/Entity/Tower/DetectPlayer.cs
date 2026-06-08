using UnityEngine;

public class DetectPlayer : MonoBehaviour, IInteractable
{
    public GameObject arrow;
    public GameObject txtE;
    private bool isPlayerInRange = false;


    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ShowInteractTips(true);
            other.GetComponent<PlayerController>().currentInteractable = this;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerController>();
            if ((object)player.currentInteractable == this) 
            {
                ShowInteractTips(false);
                player.currentInteractable = null; 
            }
        }
    }


    /// <summary>
    /// 实现接口
    /// </summary>
    public void Interact()
    {
        UIManager.Instance.ShowPanel<TowerLevelUpPanel>().SetTowerType(GetComponentInParent<BaseTower>());
    }


    /// <summary>
    /// win环境下，显示交互提示；安卓环境下，显示升级按钮
    /// </summary>
    /// <param name="isTouching"></param>
    private void ShowInteractTips(bool isTouching)
    {
#if UNITY_STANDALONE_WIN
        SetButtonActive(isTouching);
#elif UNITY_ANDROID
        ShowTowerLevelUpButton(isTouching);
#endif
    }
    // 显示或隐藏交互提示
    private void SetButtonActive(bool isActive)
    {
        arrow.SetActive(isActive);
        isPlayerInRange = isActive;
        txtE.SetActive(isActive);
    }
    // 显示或隐藏塔升级按钮
    private void ShowTowerLevelUpButton(bool isActive)
    {
        UIManager.Instance.GetPanel<GamePanel>().SetButtonTowerLevelUpActive(isActive, GetComponentInParent<BaseTower>());
    }
}
