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
    private EntityBehaviour _player;

    public override void Init()
    {
        _player = PlayerManager.Service.LocalPlayer;
        InitLevelUpSOs();
        UpdateOptionsUI();
        GameLevelManager.Service.PauseGame();

        btn1.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Service.CanUseLevelPoint(levelUpSOs[0].cost))
            {
                levelUpSOs[0].ApplyTo(_player);
                GetRandomSOs();
                this.UpdateOptionsUI();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btn2.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Service.CanUseLevelPoint(levelUpSOs[1].cost))
            {
                levelUpSOs[1].ApplyTo(_player);
                GetRandomSOs();
                this.UpdateOptionsUI();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btn3.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Service.CanUseLevelPoint(levelUpSOs[2].cost))
            {
                levelUpSOs[2].ApplyTo(_player);
                GetRandomSOs();
                this.UpdateOptionsUI();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btnClose.onClick.AddListener(() =>
        {
            GameLevelManager.Service.ResumeGame();
            UIManager.Instance.HidePanel<LevelUpPanel>();
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

    private void InitLevelUpSOs()
    {
        levelUpSOs = SOManager.Instance.GetPreferSOs();
        if (levelUpSOs[0] == null)
        {
            GetRandomSOs();
        }
    }

    private void GetRandomSOs()
    {
        levelUpSOs = SOManager.Instance.GetRandomPlayerLevelUpSOs(3);
        SOManager.Instance.StorePreferSOs(levelUpSOs);
    }

    public override void EscLogic()
    {
        base.EscLogic();
        GameLevelManager.Service.ResumeGame();
        UIManager.Instance.HidePanel<LevelUpPanel>();
    }
}
