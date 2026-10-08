using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class GamePanel : BasePanel
{
    [Header("经验值与等级显示")]
    public Slider sldExp;
    public TMPro.TextMeshProUGUI txtLevel;

    [Header("血量显示（左上角）")]
    [Tooltip("玩家血条；由 PlayerHudBinder 在每次显示时绑定到本地玩家，血条自己订阅 HealthChanged")]
    public HealthPanel healthPanel;

    [Header("时间显示")]
    public TMPro.TextMeshProUGUI txtTime;
    public Button btnSetting;

    [Header("购买界面")]
    public TMPro.TextMeshProUGUI txtLevelPoint;
    public Button btnWeaponShop;
    public Button btnTowerShop;

    public Button btnTowerLevelUp;

    [Header("塔放置按钮")]
    public Button btnPlaceTowerConfirm;
    public Button btnPlaceTowerCancel;
    private TowerPlacementController currentPlacementController;

    [Header("武器槽按钮")]
    [Tooltip("HUD 上的武器槽按钮（移动端主要靠它，键鼠用数字键 1/2）。" +
             "两者走**同一个** SwitchWeaponSlot 领域入口")]
    public Button btnWeaponSlot1;
    public Button btnWeaponSlot2;

    /// <summary>激活槽高亮色 / 未激活色（用按钮自身的 Image 顶点色，不涉及材质实例）。</summary>
    [SerializeField] private Color _slotActiveColor = Color.white;
    [SerializeField] private Color _slotInactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private IWeaponManager _weapons;

    [Header("摇杆")]
    public Joystick joystickMove;
    public Joystick joystickWeapon;

    [Header("帧率")]
    private float fpsUpdateInterval = 0.5f;
    private float fpsTimer;
    public TMPro.TextMeshProUGUI txtFPS;

    [Tooltip("是否显示帧率。Release 构建下强制关闭（见 UpdateFps）")]
    [SerializeField] private bool _showFps = true;


    public override void Init()
    {
        // HUD 数据由绑定器订阅本地玩家事件驱动，面板本身不直接读 Manager
        PlayerHudBinder binder = GetComponent<PlayerHudBinder>();
        if (binder == null) binder = gameObject.AddComponent<PlayerHudBinder>();
        binder.Bind(this);

        // 槽位按钮先藏起来：玩家还没绑定完，此刻显示只会是一个点了没反应的按钮
        RefreshWeaponSlots();

        UpdateExp(0, 1, 1);
        // 开始游戏时，播放音效
        if (AudioService.Service != null) AudioService.Service.BgmMuted = true;
        AudioService.Service?.PlaySfx(ResourceEnum.StartGame);


        // 角色动作入口：**两个专用按钮**，各自按能力显隐。
        // 旧实现是"一个按钮按能力分支 + 另一个常隐"，问题是按钮文案没法同时对上两个角色 ——
        // 工程师看到的入口点开却是建塔面板，而"专门的建造防御塔按钮"根本不存在。
        // 领域面板内部仍会**再校验一次**能力，这里只负责"显不显示"。
        btnWeaponShop.onClick.AddListener(() => _ = UIService.Service?.ShowPanelAsync<WeaponUpgradePanel>());
        btnTowerShop.onClick.AddListener(() => _ = UIService.Service?.ShowPanelAsync<ChooseTowerPanel>());
        RefreshRoleEntry(null);
        btnSetting.onClick.AddListener(() =>
        {
            _ = UIService.Service.ShowPanelAsync<GameSettingPanel>();
        });

        // 通用升级入口（角色属性三选一，花升级点）。
        // 字段名仍是 btnTowerLevelUP —— 它原本是塔升级的 HUD 入口，那条入口已删除
        //（塔升级/拆除只有"靠近塔 + 交互"一条入口）；改名会让 prefab 引用静默丢失，
        // 所以保留字段名、在这里改成"通用升级"的语义。
        // 升级不再强制弹面板（见 PlayerProgressionController），入口只有这一个。
        if (btnTowerLevelUp != null)
            btnTowerLevelUp.onClick.AddListener(() => _ = UIService.Service?.ShowPanelAsync<LevelUpPanel>());

        // 移动端塔放置确认与取消按钮事件
        btnPlaceTowerConfirm.onClick.AddListener(() =>
        {
            currentPlacementController?.ConfirmPlacement();
        });
        btnPlaceTowerCancel.onClick.AddListener(() =>
        {
            currentPlacementController?.CancelPlacement();
        });
    }


    /// <summary>
    /// 更新经验值与等级显示
    /// </summary>
    /// <param name="currentExp"></param>
    /// <param name="maxExp"></param>
    /// <param name="Level"></param>
    public void UpdateExp(float currentExp, float maxExp, int Level)
    {
        // 必须先设上限：反过来 value 会被**旧的** maxValue 夹住，进度条显示偏小
        sldExp.maxValue = maxExp;
        sldExp.value = currentExp;
        txtLevel.text = "Level " + Level.ToString();
    }


    /// <summary>
    /// 更新时间显示
    /// </summary>
    /// <param name="timeInSeconds"></param>
    public void UpdateTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        txtTime.text = "Time: " + minutes.ToString("00") + ":" + seconds.ToString("00");
    }


    /// <summary>
    /// 按本地角色能力刷新两个升级入口的可见性。由 <see cref="PlayerHudBinder"/> 在拿到角色后调用
    /// （角色定义由场景入口按 RunSession 注入，可能晚于本面板的 Init）。
    ///
    /// <para>
    /// <b>三个入口各对应一件事</b>：<c>btnTowerLevelUp</c> = 通用升级（角色属性，人人都有）、
    /// <c>btnWeaponShop</c> = 武器升级（<see cref="CharacterCapability.WeaponUpgrade"/>）、
    /// <c>btnTowerShop</c> = 建造防御塔（<see cref="CharacterCapability.TowerBuild"/>）。
    /// 塔的**升级/拆除**不在这里 —— 它只有"靠近塔 + 交互"这一条入口（能力位在交互层校验）。
    /// </para>
    ///
    /// <para>
    /// <b>角色未就绪时三个都隐藏</b>：宁可让玩家点不到，也不要放行一个无主的命令 ——
    /// 后者会在领域层被拒绝，表现成"点了没反应"，比看不见更难查。
    /// </para>
    /// </summary>
    public void RefreshRoleEntry(PlayerRoleController role)
    {
        bool ready = role != null && role.IsReady;
        bool canUpgradeWeapon = role != null && role.Has(CharacterCapability.WeaponUpgrade);
        bool canBuildTower = role != null && role.Has(CharacterCapability.TowerBuild);

        if (btnTowerLevelUp != null) btnTowerLevelUp.gameObject.SetActive(ready);
        if (btnWeaponShop != null) btnWeaponShop.gameObject.SetActive(canUpgradeWeapon);
        if (btnTowerShop != null) btnTowerShop.gameObject.SetActive(canBuildTower);
    }

    /// <summary>
    /// 更新升级点显示。
    /// 注意字段名仍是 <c>txtLevelPoint</c>（改名会让 prefab 上的引用静默丢失），
    /// 但文案与语义已改为「升级点」—— 它是塔/武器升级的通用货币，与等级无关。
    /// </summary>
    public void UpdateUpgradePoints(int points)
    {
        txtLevelPoint.text = "升级点:" + points.ToString("00");
    }


    /// <summary>
    /// 控制摇杆显示：是否需要攻击摇杆由**数据**决定（**当前激活武器**的攻击方式要不要瞄准输入），
    /// 而不是由界面认识具体武器类 —— 新增一把需要瞄准的武器不必改这里。
    ///
    /// <para>
    /// <b>桌面平台一律不显示</b>：虚拟摇杆是触屏的替代输入，PC 上既没有输入来源
    /// （PC 走 <c>PCInputHandle</c>，不读摇杆），又会挡住画面。
    /// 旧实现无条件 <c>SetActive(true)</c>，于是键鼠玩家也会看到两个摇杆。
    /// </para>
    /// </summary>
    public void UpdateJoystickVisibility()
    {
#if UNITY_ANDROID
        if (joystickMove != null) joystickMove.gameObject.SetActive(true);

        bool needsAimJoystick = PlayerManager.Service?.LocalPlayer?.Weapons?.HasAimWeapon ?? false;
        if (joystickWeapon != null) joystickWeapon.gameObject.SetActive(needsAimJoystick);
#else
        if (joystickMove != null) joystickMove.gameObject.SetActive(false);
        if (joystickWeapon != null) joystickWeapon.gameObject.SetActive(false);
#endif
    }

    /// <summary>
    /// 绑定武器槽按钮（由 <see cref="PlayerHudBinder"/> 在拿到本地玩家后调用）。
    /// 按钮与数字键 1/2 最终都走 <see cref="SwitchWeaponSlot"/> —— 只留一条领域入口。
    /// </summary>
    public void BindWeaponSlots(IWeaponManager weapons)
    {
        _weapons = weapons;

        if (btnWeaponSlot1 != null)
        {
            btnWeaponSlot1.onClick.RemoveAllListeners();
            btnWeaponSlot1.onClick.AddListener(() => SwitchWeaponSlot(0));
        }

        if (btnWeaponSlot2 != null)
        {
            btnWeaponSlot2.onClick.RemoveAllListeners();
            btnWeaponSlot2.onClick.AddListener(() => SwitchWeaponSlot(1));
        }

        RefreshWeaponSlots();
    }

    /// <summary>
    /// 切换武器槽（**已装备槽序号**，与数字键 1/2 语义一致）。
    /// 界面不自己判断"能不能切"——越界、空槽、已是当前槽都由领域层拒绝。
    /// </summary>
    public void SwitchWeaponSlot(int equippedIndex)
    {
        _weapons?.SwitchToSlot(equippedIndex);
        RefreshWeaponSlots();
    }

    /// <summary>
    /// 刷新槽位按钮的可见性与高亮：只装备一把时第二个按钮藏起来，
    /// 而不是留一个点了没反应的按钮。
    /// </summary>
    public void RefreshWeaponSlots()
    {
        int count = _weapons?.EquippedSlots.Count ?? 0;
        int active = _weapons?.ActiveEquippedSlot ?? -1;

        RefreshSlotButton(btnWeaponSlot1, 0, count, active);
        RefreshSlotButton(btnWeaponSlot2, 1, count, active);

        // 瞄准摇杆跟着激活武器走：带"枪 + 火球"时，拿火球不该显示瞄准摇杆
        UpdateJoystickVisibility();
    }

    private void RefreshSlotButton(Button button, int index, int count, int active)
    {
        if (button == null) return;

        button.gameObject.SetActive(index < count);

        if (button.image != null)
            button.image.color = index == active ? _slotActiveColor : _slotInactiveColor;
    }

    /// <summary>
    /// 移动端确认放置后调用，隐藏按钮
    /// </summary>
    /// <param name="isActive"></param>
    /// <param name="controller"></param>
    public void SetTowerPlacementButtonsActive(bool isActive, TowerPlacementController controller = null)
    {
        if (controller != null)
            currentPlacementController = controller;

#if UNITY_ANDROID
        btnPlaceTowerConfirm.gameObject.SetActive(isActive);
        btnPlaceTowerCancel.gameObject.SetActive(isActive);
#endif
    }


    /// <summary>
    /// 每次显示都重新绑定 HUD。
    ///
    /// <para>
    /// <b>为什么不能只在 <see cref="Init"/> 里绑：</b>面板实例由 <see cref="UIService"/> 缓存，
    /// 切场景不会销毁它，<c>Init</c> 一辈子只跑一次 —— 于是第二局开始时 HUD 仍绑在
    /// **上一局那个已经被销毁的玩家**身上，等级、经验、升级点全部停在旧值，
    /// 而且不报任何错（事件不会从已销毁的对象上发出来）。
    /// </para>
    /// </summary>
    public override void ShowMe()
    {
        base.ShowMe();

        GetComponent<PlayerHudBinder>()?.Bind(this);
    }

    private void Update()
    {
        // 注意：基类已不再做逐帧 alpha 轮询（改由 DOTween 驱动），这里只保留帧率显示。
        // 面板被隐藏后 GameObject 仍在（UIService 会缓存面板实例直到服务销毁），
        // 不判 IsShown 的话它会在大厅/菜单里继续每帧空转
        if (!IsShown) return;

        UpdateFps();
    }

    /// <summary>
    /// 帧率显示。**只在开发构建 / 编辑器下刷新**：
    /// Release 包里每 0.5 秒一次 <c>ToString</c> 分配 + 一次 TMP 文本网格重建换不来任何东西，
    /// 却会在低端机上稳定产生周期性 GC 与重建尖峰。
    /// </summary>
    private void UpdateFps()
    {
        if (!_showFps || txtFPS == null) return;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        // Release：一次性关掉，避免每帧再判一次
        _showFps = false;
        txtFPS.gameObject.SetActive(false);
#else
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer <= fpsUpdateInterval) return;

        // 重置计时器
        fpsTimer = 0f;

        // 显示帧率。unscaledDeltaTime 可能为 0（同一帧内被多次调用），除法会得到 Infinity
        float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        txtFPS.text = Mathf.Ceil(1.0f / delta).ToString() + " FPS";
#endif
    }
}