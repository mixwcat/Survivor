using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class ChooseTowerPanel : BasePanel
{
    public TowerSO towerSO1;
    public TowerSO towerSO2;
    public TowerSO towerSO3;
    public Button button1;
    public Button button2;
    public Button button3;
    public Image imgIcon1;
    public Image imgIcon2;
    public Image imgIcon3;
    public TextMeshProUGUI txtConsumption1;
    public TextMeshProUGUI txtConsumption2;
    public TextMeshProUGUI txtConsumption3;
    public TextMeshProUGUI txtDescription1;
    public TextMeshProUGUI txtDescription2;
    public TextMeshProUGUI txtDescription3;
    public Button btnClose;
    public override void Init()
    {
        UpdateUI();
        GameLevelManager.Instance.PauseGame();

        button1.onClick.AddListener(() =>
        {
            // 检查是否有足够的等级点数
            if (ExperienceLevController.Instance.CanUseLevelPoint(towerSO1.expConsumption))
            {
                // 获取防御塔，随鼠标移动
                MouseHandleController.Instance.HandleSprite(towerSO1);
                // 隐藏面板
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                // 恢复游戏
                GameLevelManager.Instance.ResumeGame();
                // 播放音效
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        button2.onClick.AddListener(() =>
        {
            // 检查是否有足够的等级点数
            if (ExperienceLevController.Instance.CanUseLevelPoint(towerSO2.expConsumption))
            {
                // 获取防御塔，随鼠标移动
                MouseHandleController.Instance.HandleSprite(towerSO2);
                // 隐藏面板
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                // 恢复游戏
                GameLevelManager.Instance.ResumeGame();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        button3.onClick.AddListener(() =>
        {
            // 检查是否有足够的等级点数
            if (ExperienceLevController.Instance.CanUseLevelPoint(towerSO3.expConsumption))
            {
                // 获取防御塔，随鼠标移动
                MouseHandleController.Instance.HandleSprite(towerSO3);
                // 隐藏面板
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                // 恢复游戏
                GameLevelManager.Instance.ResumeGame();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btnClose.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<ChooseTowerPanel>();
            GameLevelManager.Instance.ResumeGame();
        });
    }

    private void UpdateUI()
    {
        txtConsumption1.text = towerSO1.expConsumption.ToString();
        txtConsumption2.text = towerSO2.expConsumption.ToString();
        txtConsumption3.text = towerSO3.expConsumption.ToString();
        txtDescription1.text = towerSO1.description;
        txtDescription2.text = towerSO2.description;
        txtDescription3.text = towerSO3.description;
        imgIcon1.sprite = towerSO1.towerIcon;
        imgIcon2.sprite = towerSO2.towerIcon;
        imgIcon3.sprite = towerSO3.towerIcon;
    }

    public override void EscLogic()
    {
        UIManager.Instance.HidePanel<ChooseTowerPanel>();
        GameLevelManager.Instance.ResumeGame();
    }
}
