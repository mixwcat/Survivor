using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class ChooseTowerPanel : BasePanel
{
    public TowerEntitySO towerSO1;
    public TowerEntitySO towerSO2;
    public TowerEntitySO towerSO3;

    [Header("选择信息（名称/描述/图标/消耗）来自 LevelUpSO")]
    public LevelUpSO towerSelectInfo1;
    public LevelUpSO towerSelectInfo2;
    public LevelUpSO towerSelectInfo3;

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
        GameLevelManager.Service.PauseGame();

        button1.onClick.AddListener(() =>
        {
            int cost = towerSelectInfo1 != null ? towerSelectInfo1.cost : 0;
            if (ExperienceLevController.Service.CanUseLevelPoint(cost))
            {
                InstantiateTowerPlacementSprite(towerSO1, cost);
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                GameLevelManager.Service.ResumeGame();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        button2.onClick.AddListener(() =>
        {
            int cost = towerSelectInfo2 != null ? towerSelectInfo2.cost : 0;
            if (ExperienceLevController.Service.CanUseLevelPoint(cost))
            {
                InstantiateTowerPlacementSprite(towerSO2, cost);
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                GameLevelManager.Service.ResumeGame();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        button3.onClick.AddListener(() =>
        {
            int cost = towerSelectInfo3 != null ? towerSelectInfo3.cost : 0;
            if (ExperienceLevController.Service.CanUseLevelPoint(cost))
            {
                InstantiateTowerPlacementSprite(towerSO3, cost);
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                GameLevelManager.Service.ResumeGame();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        btnClose.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<ChooseTowerPanel>();
            GameLevelManager.Service.ResumeGame();
        });

#if UNITY_ANDROID
        // 移动端显示确认与取消按钮
        UIManager.Instance.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(true);
#endif
    }

    private void UpdateUI()
    {
        txtConsumption1.text = towerSelectInfo1 != null ? towerSelectInfo1.cost.ToString() : "0";
        txtConsumption2.text = towerSelectInfo2 != null ? towerSelectInfo2.cost.ToString() : "0";
        txtConsumption3.text = towerSelectInfo3 != null ? towerSelectInfo3.cost.ToString() : "0";
        txtDescription1.text = towerSelectInfo1 != null ? towerSelectInfo1.levelUpText : "";
        txtDescription2.text = towerSelectInfo2 != null ? towerSelectInfo2.levelUpText : "";
        txtDescription3.text = towerSelectInfo3 != null ? towerSelectInfo3.levelUpText : "";
        imgIcon1.sprite = towerSelectInfo1 != null ? towerSelectInfo1.levelUpSprite : null;
        imgIcon2.sprite = towerSelectInfo2 != null ? towerSelectInfo2.levelUpSprite : null;
        imgIcon3.sprite = towerSelectInfo3 != null ? towerSelectInfo3.levelUpSprite : null;
    }

    public override void EscLogic()
    {
        base.EscLogic();
        UIManager.Instance.HidePanel<ChooseTowerPanel>();
        GameLevelManager.Service.ResumeGame();
    }

    private void InstantiateTowerPlacementSprite(TowerEntitySO towerSO, int placementCost)
    {
        Vector3 spawnPosition;

#if UNITY_STANDALONE_WIN
        spawnPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
#elif UNITY_ANDROID
        spawnPosition = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height / 2, 0));
#else
        spawnPosition = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height / 2, 0));
#endif
        spawnPosition.z = 0;

        GameObject placementObj = Instantiate(Resources.Load<GameObject>("Prefabs/SpriteToHandle"), spawnPosition, Quaternion.identity);
        placementObj.GetComponent<TowerPlacementController>().Init(towerSO, placementCost);
    }
}
