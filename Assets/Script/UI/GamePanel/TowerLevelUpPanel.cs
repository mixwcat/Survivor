using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerLevelUpPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    public Image img1;
    public Image img2;
    public Image img3;
    public TextMeshProUGUI txt1;
    public TextMeshProUGUI txt2;
    public TextMeshProUGUI txt3;
    public TextMeshProUGUI txtConsumption1;
    public TextMeshProUGUI txtConsumption2;
    public TextMeshProUGUI txtConsumption3;
    public Button btn1;
    public Button btn2;
    public Button btn3;
    public Button btnExit;
    private LevelUpSO[] levelUpSOs = new LevelUpSO[3];
    private BaseTower _towerType;
    private IExperienceController _exp;

    public override void Init()
    {
        _exp = PlayerManager.Service?.LocalPlayer?.ExperienceController;
        GetRandomSOs();
        GameLevelManager.Service.PauseGame();

        btn1.onClick.AddListener(() =>
        {
            if (_exp != null && _exp.CanUseLevelPoint(levelUpSOs[0].cost))
            {
                levelUpSOs[0].ApplyTo(_towerType);
                GetRandomSOs();
                AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
            }
        });
        btn2.onClick.AddListener(() =>
        {
            if (_exp != null && _exp.CanUseLevelPoint(levelUpSOs[1].cost))
            {
                levelUpSOs[1].ApplyTo(_towerType);
                GetRandomSOs();
                AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
            }
        });
        btn3.onClick.AddListener(() =>
        {
            if (_exp != null && _exp.CanUseLevelPoint(levelUpSOs[2].cost))
            {
                levelUpSOs[2].ApplyTo(_towerType);
                GetRandomSOs();
                AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
            }
        });
        btnExit.onClick.AddListener(() =>
        {
            UIManager.Service.HidePanel<TowerLevelUpPanel>();
            GameLevelManager.Service.ResumeGame();
        });
    }

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

    private void GetRandomSOs()
    {
        levelUpSOs = SOManager.Service.GetRandomTowerLevelUpSOs(3, _towerType);
        UpdateOptionsUI();
    }

    public override void EscLogic()
    {
        UIManager.Service.HidePanel<TowerLevelUpPanel>();
        GameLevelManager.Service.ResumeGame();
    }

    public void SetTowerType(BaseTower tower)
    {
        _towerType = tower;
    }
}
