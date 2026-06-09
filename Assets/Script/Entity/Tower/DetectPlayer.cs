using UnityEngine;

public class DetectPlayer : MonoBehaviour, IInteractable
{
    public GameObject arrow;
    public GameObject txtE;
    private bool isPlayerInRange = false;


    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent<PlayerInteraction>(out var interaction))
        {
            interaction.RegisterInteractable(this);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent<PlayerInteraction>(out var interaction))
        {
            interaction.UnregisterInteractable(this);
        }
    }


    /// <summary>
    /// 实现接口 — 交互行为
    /// </summary>
    public void Interact()
    {
        UIManager.Instance.ShowPanel<TowerLevelUpPanel>().SetTowerType(GetComponentInParent<BaseTower>());
    }

    /// <summary>
    /// 实现接口 — 成为当前交互目标时显示提示+高亮
    /// </summary>
    public void OnSelected()
    {
        ShowInteractTips(true);
        GetComponentInParent<BaseTower>()?.SetHighlight(true);
    }

    /// <summary>
    /// 实现接口 — 不再是当前交互目标时隐藏提示+取消高亮
    /// </summary>
    public void OnDeselected()
    {
        ShowInteractTips(false);
        GetComponentInParent<BaseTower>()?.SetHighlight(false);
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
