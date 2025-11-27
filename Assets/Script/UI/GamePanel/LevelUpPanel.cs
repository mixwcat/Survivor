using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelUpPanel : BasePanel
{
    public Button btn1;
    public Button btn2;
    public Button btn3;
    public Button btnClose;
    public TextMeshProUGUI txt1;
    public TextMeshProUGUI txt2;
    public TextMeshProUGUI txt3;
    public Image img1;
    public Image img2;
    public Image img3;
    public TextMeshProUGUI txtConsumption1;
    public TextMeshProUGUI txtConsumption2;
    public TextMeshProUGUI txtConsumption3;

    private LevelUpSO[] levelUpSOs = new LevelUpSO[3];
    public override void Init()
    {
        InitLevelUpSOs();
        UpdateOptionsUI();
        GameLevelManager.Instance.PauseGame();

        btn1.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(levelUpSOs[0].cost))
            {
                levelUpSOs[0].RaiseEvent();
                GetRandomSOs();
                this.UpdateOptionsUI();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btn2.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(levelUpSOs[1].cost))
            {
                levelUpSOs[1].RaiseEvent();
                GetRandomSOs();
                this.UpdateOptionsUI();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btn3.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(levelUpSOs[2].cost))
            {
                levelUpSOs[2].RaiseEvent();
                GetRandomSOs();
                this.UpdateOptionsUI();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btnClose.onClick.AddListener(() =>
        {
            GameLevelManager.Instance.ResumeGame();
            UIManager.Instance.HidePanel<LevelUpPanel>();
        });
    }


    /// <summary>
    /// 更新升级选项UI
    /// </summary>
    private void UpdateOptionsUI()
    {
        img1.sprite = levelUpSOs[0].levelUpSprite;
        txt1.text = levelUpSOs[0].levelUpText;
        txtConsumption1.text = levelUpSOs[0].cost.ToString();

        img2.sprite = levelUpSOs[1].levelUpSprite;
        txt2.text = levelUpSOs[1].levelUpText;
        txtConsumption2.text = levelUpSOs[1].cost.ToString();

        img3.sprite = levelUpSOs[2].levelUpSprite;
        txt3.text = levelUpSOs[2].levelUpText;
        txtConsumption3.text = levelUpSOs[2].cost.ToString();
    }


    /// <summary>
    /// 获得保存过的升级SO列表，如果没有则随机获取
    /// </summary>
    private void InitLevelUpSOs()
    {
        levelUpSOs = SOManager.Instance.GetPreferSOs();

        if (levelUpSOs[0] == null)
        {
            GetRandomSOs();
        }
    }


    /// <summary>
    /// 随机获取升级SO列表，并保存
    /// </summary>
    private void GetRandomSOs()
    {
        levelUpSOs = SOManager.Instance.GetRandomPlayerLevelUpSOs(3);
        SOManager.Instance.StorePreferSOs(levelUpSOs);
    }

    public override void EscLogic()
    {
        GameLevelManager.Instance.ResumeGame();
        UIManager.Instance.HidePanel<LevelUpPanel>();
    }
}
