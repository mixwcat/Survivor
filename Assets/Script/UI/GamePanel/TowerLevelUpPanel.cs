using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerLevelUpPanel : BasePanel
{
    public Image img1;
    public Image img2;
    public Image img3;
    public TextMeshProUGUI txt1;
    public TextMeshProUGUI txt2;
    public TextMeshProUGUI txt3;
    public Button btn1;
    public Button btn2;
    public Button btn3;
    public Button btnExit;
    private LevelUpSO[] levelUpSOs = new LevelUpSO[3];

    public override void Init()
    {
        GetRandomSOs();
        GameLevelManager.Instance.PauseGame();

        btn1.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(levelUpSOs[0].cost))
            {
                levelUpSOs[0].RaiseEvent();
                GetRandomSOs();
            }
        });
        btn2.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(levelUpSOs[1].cost))
            {
                levelUpSOs[1].RaiseEvent();
                GetRandomSOs();
            }
        });
        btn3.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(levelUpSOs[2].cost))
            {
                levelUpSOs[2].RaiseEvent();
                GetRandomSOs();
            }
        });
        btnExit.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<TowerLevelUpPanel>();
            GameLevelManager.Instance.ResumeGame();
        });
    }

    /// <summary>
    /// 更新升级选项UI
    /// </summary>
    private void UpdateOptionsUI()
    {
        img1.sprite = levelUpSOs[0].levelUpSprite;
        txt1.text = levelUpSOs[0].levelUpText;
        img2.sprite = levelUpSOs[1].levelUpSprite;
        txt2.text = levelUpSOs[1].levelUpText;
        img3.sprite = levelUpSOs[2].levelUpSprite;
        txt3.text = levelUpSOs[2].levelUpText;
    }

    private void GetRandomSOs()
    {
        levelUpSOs = SOManager.Instance.GetRandomTowerLevelUpSOs(3);
        UpdateOptionsUI();
    }

    public override void EscLogic()
    {
        UIManager.Instance.HidePanel<TowerLevelUpPanel>();
        GameLevelManager.Instance.ResumeGame();
    }
}
