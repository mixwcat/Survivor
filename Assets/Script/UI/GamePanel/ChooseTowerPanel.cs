using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class ChooseTowerPanel : BasePanel
{
    public TowerInfoSO towerSO1;
    public TowerInfoSO towerSO2;
    public TowerInfoSO towerSO3;
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
            if (ExperienceLevController.Service.CanUseLevelPoint(towerSO1.expConsumption))
            {
                InstantiateTowerPlacementSprite(towerSO1);
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                GameLevelManager.Service.ResumeGame();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        button2.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Service.CanUseLevelPoint(towerSO2.expConsumption))
            {
                InstantiateTowerPlacementSprite(towerSO2);
                UIManager.Instance.HidePanel<ChooseTowerPanel>();
                GameLevelManager.Service.ResumeGame();
                BKMusic.Instance.PlaySound(ResourceEnum.OnMouseClickUI);
            }
        });
        button3.onClick.AddListener(() =>
        {
            if (ExperienceLevController.Service.CanUseLevelPoint(towerSO3.expConsumption))
            {
                InstantiateTowerPlacementSprite(towerSO3);
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
        base.EscLogic();
        UIManager.Instance.HidePanel<ChooseTowerPanel>();
        GameLevelManager.Service.ResumeGame();
    }

    private void InstantiateTowerPlacementSprite(TowerInfoSO towerSO)
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
        placementObj.GetComponent<TowerPlacementController>().Init(towerSO);
    }
}
