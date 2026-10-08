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

    /// <summary>模态面板：显示期间暂停游戏（令牌由 UIService 按本面板生命周期管理）。</summary>
    public override bool WantsPause => true;

    public override void Init()
    {
        UpdateUI();

        button1.onClick.AddListener(() => OnTowerSelected(towerSO1));
        button2.onClick.AddListener(() => OnTowerSelected(towerSO2));
        button3.onClick.AddListener(() => OnTowerSelected(towerSO3));

        btnClose.onClick.AddListener(() => UIService.Service.HidePanel<ChooseTowerPanel>());

#if UNITY_ANDROID
        // 移动端显示确认与取消按钮
        UIService.Service.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(true);
#endif
    }

    /// <summary>
    /// 从 TowerEntitySO 读取名称/描述/图标，从 BaseTowerDataSO 读取消耗
    /// （攻击塔与 Luo 都挂在 BaseTowerDataSO 下，这里用基类才能同时覆盖两者）。
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
        if (towerSO?.dataRef is BaseTowerDataSO towerData)
        {
            return towerData.Cost;
        }
        return 0;
    }

    private void OnTowerSelected(TowerEntitySO towerSO)
    {
        if (towerSO == null) return;

        PlayerController player = PlayerManager.Service?.LocalPlayer;
        if (player == null)
        {
            Debug.LogWarning("[ChooseTowerPanel] 本地玩家不存在，建塔命令被拒绝。");
            return;
        }

        // 领域层权限：UI 隐藏不是安全边界（旧快捷键、联机伪造命令都能绕过来）。
        // 角色未就绪时**默认拒绝** —— 放行一个无主命令只会表现成"点了没反应"，
        // 而且会先扣掉点数
        PlayerRoleController role = player.Role;
        if (role == null || !role.IsReady || !role.Has(CharacterCapability.TowerBuild))
        {
            Debug.LogWarning("[ChooseTowerPanel] 当前角色没有建塔能力，命令被拒绝。");
            return;
        }

        IUpgradePointWallet wallet = player.UpgradePoints;
        if (wallet == null)
        {
            Debug.LogError("[ChooseTowerPanel] 本地玩家没有升级点钱包，无法建塔。");
            return;
        }

        int cost = GetTowerCost(towerSO);
        if (!wallet.TrySpend(cost)) return;

        // 扣款成功后立刻建立事务：付款人与金额都记在里面，
        // 之后无论是确认、取消、加载失败还是切场景，退款都只认它（一次性）
        var transaction = new TowerPlacementTransaction(player, cost);

        UIService.Service?.HidePanel<ChooseTowerPanel>();
        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);

        _ = InstantiateTowerPlacementSprite(towerSO, transaction);
    }

    public override void EscLogic()
    {
        base.EscLogic();
        UIService.Service.HidePanel<ChooseTowerPanel>();
    }

    /// <summary>
    /// 创建放置幽灵并把事务交给它。
    ///
    /// <para>
    /// <b>每一条失败分支都必须终结事务</b>（退款 + 归还实例）：
    /// 扣款发生在异步加载之前，漏掉任何一条就是"点扣了、塔没出来"，而且不报错。
    /// </para>
    /// </summary>
    private async Task InstantiateTowerPlacementSprite(TowerEntitySO towerSO, TowerPlacementTransaction transaction)
    {
        IAssetService assets = AssetService.Service;
        if (assets == null)
        {
            Debug.LogError("[ChooseTowerPanel] IAssetService 未注册，无法创建塔放置预览。");
            transaction.TryCancel();
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition();

        GameObject placementObj = await assets.InstantiateAsync(AssetKeys.SpriteToHandle);

        // await 期间面板可能已随场景销毁：实例与事务都必须收尾
        if (this == null)
        {
            if (placementObj != null) assets.ReleaseInstance(placementObj);
            transaction.TryCancel();
            return;
        }

        if (placementObj == null)
        {
            Debug.LogError("[ChooseTowerPanel] 放置幽灵实例化失败，本次放置已取消并退款。");
            transaction.TryCancel();
            return;
        }

        placementObj.transform.position = spawnPosition;
        placementObj.transform.rotation = Quaternion.identity;

        TowerPlacementController controller = placementObj.GetComponent<TowerPlacementController>();
        if (controller == null)
        {
            Debug.LogError("[ChooseTowerPanel] 放置幽灵上没有 TowerPlacementController，已归还实例并退款。");
            assets.ReleaseInstance(placementObj);
            transaction.TryCancel();
            return;
        }

        // 塔 prefab 的加载在 InitAsync 里。失败时它**自己**会退款并归还幽灵 ——
        // 这里不要再 ReleaseInstance（两处各还一次会让引用计数被多减一次）
        await controller.InitAsync(towerSO, transaction);
    }

    /// <summary>
    /// 幽灵的初始位置。
    /// 平台差异只有"从哪取屏幕坐标"这一点，屏幕→世界的换算与相机查找都只有一份。
    /// </summary>
    private static Vector3 ResolveSpawnPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

#if UNITY_STANDALONE_WIN
        Vector3 screen = Input.mousePosition;
#else
        // 触屏在手指按下去之前没有"当前指针"，从屏幕中心开始
        Vector3 screen = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
#endif

        Vector3 world = cam.ScreenToWorldPoint(screen);
        world.z = 0f;
        return world;
    }
}
