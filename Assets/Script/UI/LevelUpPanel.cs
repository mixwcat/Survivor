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
    private LevelUpSO[] levelUpSOs = new LevelUpSO[3];
    public override void Init()
    {
        GetLevelUpOptions();

        btn1.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(1))
            {
                levelUpSOs[0].RaiseEvent();
            }
        });
        btn2.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(1))
            {
                levelUpSOs[1].RaiseEvent();
            }
        });
        btn3.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Instance.CanUseLevelPoint(1))
            {
                levelUpSOs[2].RaiseEvent();
            }
        });
        btnClose.onClick.AddListener(() =>
        {
            CloseThisPanel();
        });
    }

    protected override void Start()
    {
        base.Start();
        GameLevelManager.Instance.PauseGame();
    }

    private void CloseThisPanel()
    {
        GameLevelManager.Instance.ResumeGame();
        UIManager.Instance.HidePanel<LevelUpPanel>();
    }


    /// <summary>
    /// 读取升级SO，获取图片、文本等信息
    /// </summary>
    private void GetLevelUpOptions()
    {
        // 随机获取三个升级选项
        levelUpSOs = SOManager.Instance.GetRandomLevelUpSOs(3);

        // 设置UI
        img1.sprite = levelUpSOs[0].levelUpSprite;
        txt1.text = levelUpSOs[0].levelUpText;
        img2.sprite = levelUpSOs[1].levelUpSprite;
        txt2.text = levelUpSOs[1].levelUpText;
        img3.sprite = levelUpSOs[2].levelUpSprite;
        txt3.text = levelUpSOs[2].levelUpText;
    }
}
