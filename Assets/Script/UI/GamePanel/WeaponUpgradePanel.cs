using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 武器定向升级面板（枪手专属）—— 花升级点升级**当前武器**。
///
/// <para>
/// 与 <see cref="LevelUpPanel"/>（三选一）的分工：
/// 那个是**随机三选一**（同样花升级点、池子只含角色属性），
/// 这个是**定向购买**（列全部合法项、不随机、可重复买）。
/// 两条路径分开，同一把武器才不会既被随机抽到、又能花钱买。
/// </para>
///
/// <para>
/// <b>权限在这里再校验一次</b>：UI 只在枪手身上显示入口，但入口之外的路径
/// （旧快捷键、联机伪造命令）不该能打开它。
/// </para>
/// </summary>
public class WeaponUpgradePanel : BaseOptionPanel
{
    public override bool CanHandleEscape => true;

    /// <summary>模态面板：显示期间暂停游戏。暂停令牌由 UIService 按本面板的生命周期申请/释放。</summary>
    public override bool WantsPause => true;

    public Button btnClose;

    private IUpgradePointWallet _wallet;
    private IPlayerUpgradeController _upgrades;
    private PlayerRoleController _role;

    public override void Init()
    {
        PlayerController player = PlayerManager.Service?.LocalPlayer;
        _wallet = player?.UpgradePoints;
        _upgrades = player?.Upgrades;
        _role = player != null ? player.GetComponent<PlayerRoleController>() : null;

        RefreshOptions();

        btn1.onClick.AddListener(() => TryUpgrade(0));
        btn2.onClick.AddListener(() => TryUpgrade(1));
        btn3.onClick.AddListener(() => TryUpgrade(2));
        btnClose.onClick.AddListener(CloseAndResume);
    }

    /// <summary>购买第 index 个选项：校验权限 → 扣升级点 → 应用到该选项自己的目标（那件武器）。</summary>
    private void TryUpgrade(int index)
    {
        // 领域层权限：UI 隐藏不是安全边界
        if (_role == null || !_role.Has(CharacterCapability.WeaponUpgrade))
        {
            Debug.LogWarning("[WeaponUpgradePanel] 当前角色没有武器升级能力，命令被拒绝。");
            return;
        }

        UpgradeOption option = GetOption(index);
        if (!option.IsValid || _wallet == null) return;

        // 上限 / 一次性项：列表可能是刷新前的旧数据，先筛掉（买满一项后该项会从列表消失）
        if (!option.So.IsAvailable(option.Target))
        {
            RefreshOptions();
            return;
        }

        if (!_wallet.TrySpend(option.So.cost)) return;

        // 武器实例可能在面板打开期间被换掉（SwapAsync）—— 那时目标已销毁，应用会失败
        if (!option.So.ApplyTo(option.Target))
            Debug.LogWarning($"[WeaponUpgradePanel] 升级点已扣除，但「{option.So.name}」应用失败" +
                             "（武器实例已被换下 / 已达上限）。");

        RefreshOptions();
        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
    }

    private void RefreshOptions()
    {
        UpgradeOption[] options = _upgrades?.GetWeaponOptions(FindUpgradeTarget());

        // 本面板是"商店"（列出全部可买项），但基类只有 3 个槽位 —— 超过 3 项时多出来的
        // **既不显示也买不到，且不报错**。至少让它可发现：真要让它们能买，得把本面板换成
        // TowerLevelUpPanel 那套可滚动动态列表（需要改 prefab，不是代码能单独解决的）。
        if (options != null && options.Length > SlotCount)
        {
            Debug.LogWarning($"[WeaponUpgradePanel] 当前武器有 {options.Length} 个可升级项，" +
                             $"但本面板只能显示 {SlotCount} 个 —— 超出的项玩家看不到也买不到。" +
                             "请减少该武器的升级项，或把本面板改成动态列表（参考 TowerLevelUpPanel）。");
        }

        SetOptions(options);
    }

    /// <summary>
    /// 取要升级的武器 = **当前激活**的那把。
    ///
    /// <para>
    /// 旧实现取"已装备列表里的第一把"：玩家按 <c>2</c> 切到第二把枪之后，
    /// 面板仍然在升级第一把 —— 抽得到、买得起、效果落在**另一把**武器上，
    /// 而界面上完全看不出来（两把枪的名字都可能一样）。
    /// </para>
    /// </summary>
    private static BaseWeapon FindUpgradeTarget()
    {
        return PlayerManager.Service?.LocalPlayer?.Weapons?.ActiveWeapon;
    }

    private void CloseAndResume()
    {
        // 只隐藏自己：时间由暂停令牌集合恢复（还有别的模态面板时不会恢复）
        UIService.Service?.HidePanel<WeaponUpgradePanel>();
    }

    public override void EscLogic()
    {
        CloseAndResume();
    }
}
