using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChooseTowerPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    public TowerEntitySO towerSO1;
    public TowerEntitySO towerSO2;
    public TowerEntitySO towerSO3;

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

        button1.onClick.AddListener(() => OnTowerSelected(towerSO1));
        button2.onClick.AddListener(() => OnTowerSelected(towerSO2));
        button3.onClick.AddListener(() => OnTowerSelected(towerSO3));

        btnClose.onClick.AddListener(() =>
        {
            UIManager.Service.HidePanel<ChooseTowerPanel>();
            GameLevelManager.Service.ResumeGame();
        });

#if UNITY_ANDROID
        // 移动端显示确认与取消按钮
        UIManager.Service.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(true);
#endif
    }

    /// <summary>
    /// 从 TowerEntitySO 读取名称/描述/图标，从 TowerDataSO 读取消耗。
    /// </summary>
    private void UpdateUI()
    {
        BindTowerInfo(towerSO1, imgIcon1, txtDescription1, txtConsumption1);
        BindTowerInfo(towerSO2, imgIcon2, txtDescription2, txtConsumption2);
        BindTowerInfo(towerSO3, imgIcon3, txtDescription3, txtConsumption3);
    }

    private void BindTowerInfo(TowerEntitySO towerSO, Image icon, TextMeshProUGUI description, TextMeshProUGUI consumption)
    {
        if (towerSO == null)
        {
            if (icon != null) icon.sprite = null;
            if (description != null) description.text = "";
            if (consumption != null) consumption.text = "0";
            return;
        }

        if (icon != null) icon.sprite = towerSO.icon;

        if (description != null)
        {
            string text = string.IsNullOrEmpty(towerSO.displayName) ? "" : towerSO.displayName;
            if (!string.IsNullOrEmpty(towerSO.description))
            {
                if (!string.IsNullOrEmpty(text)) text += "\n";
                text += towerSO.description;
            }
            description.text = text;
        }

        if (consumption != null)
        {
            int cost = GetTowerCost(towerSO);
            consumption.text = cost.ToString();
        }
    }

    private int GetTowerCost(TowerEntitySO towerSO)
    {
        if (towerSO?.dataRef is TowerDataSO towerData)
        {
            return towerData.Cost;
        }
        return 0;
    }

    private void OnTowerSelected(TowerEntitySO towerSO)
    {
        if (towerSO == null) return;

        int cost = GetTowerCost(towerSO);
        IExperienceController exp = PlayerManager.Service?.LocalPlayer?.ExperienceController;
        if (exp != null && exp.CanUseLevelPoint(cost))
        {
            InstantiateTowerPlacementSprite(towerSO, cost);
            UIManager.Service.HidePanel<ChooseTowerPanel>();
            GameLevelManager.Service.ResumeGame();
            AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
        }
    }

    public override void EscLogic()
    {
        base.EscLogic();
        UIManager.Service.HidePanel<ChooseTowerPanel>();
        GameLevelManager.Service.ResumeGame();
    }

    private async void InstantiateTowerPlacementSprite(TowerEntitySO towerSO, int placementCost)
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

        GameObject placementObj = await ServiceLocator.Get<IAssetService>().InstantiateAsync(AssetKeys.SpriteToHandle);
        placementObj.transform.position = spawnPosition;
        placementObj.transform.rotation = Quaternion.identity;
        placementObj.GetComponent<TowerPlacementController>().Init(towerSO, placementCost);
    }
}
