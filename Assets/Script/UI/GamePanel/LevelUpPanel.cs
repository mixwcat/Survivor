using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用升级面板（三选一）—— 角色属性升级，**花升级点**。
///
/// <para>
/// <b>按需打开，不再强制</b>：升级只发升级点（<c>PlayerProgressionController</c>），
/// 面板由 HUD 的"通用升级"按钮打开。旧实现是升级即弹面板、且**有待选时不允许关闭**，
/// 玩家必须当场选完才能继续 —— 打断玩法，连升多级时尤其难受。
/// </para>
///
/// <para>
/// <b>为什么要花点</b>：不花点的话，按钮可以无限点、无限白嫖属性，
/// 而它和专属升级（武器 / 塔）本该共用同一种货币。价格写在 <see cref="LevelUpSO.cost"/>，
/// 与定向面板是同一个字段，改价不用改代码。
/// </para>
///
/// <para>
/// <b>选项是缓存的一批</b>：<see cref="IPlayerUpgradeController.GetPlayerOptions"/> 会复用缓存，
/// 所以"关掉再打开"看到的是同一批（否则可以反复开关刷出想要的组合）；
/// 买掉一项后才换一批（<see cref="IPlayerUpgradeController.RerollPlayerOptions"/>）。
/// </para>
/// </summary>
public class LevelUpPanel : BaseOptionPanel
{
    public override bool CanHandleEscape => true;

    /// <summary>模态面板：显示期间暂停游戏（令牌由 UIService 按本面板生命周期管理）。</summary>
    public override bool WantsPause => true;

    public Button btnClose;

    private IUpgradePointWallet _wallet;
    private IPlayerUpgradeController _upgrades;

    public override void Init()
    {
        BindPlayer();

        btn1.onClick.AddListener(() => TryUpgrade(0));
        btn2.onClick.AddListener(() => TryUpgrade(1));
        btn3.onClick.AddListener(() => TryUpgrade(2));
        btnClose.onClick.AddListener(CloseAndResume);
    }

    /// <summary>
    /// 每次显示都重新绑定玩家并刷新选项。
    ///
    /// <para>
    /// <b>为什么不能只在 <see cref="Init"/> 里做：</b>面板实例由 <c>UIService</c> 缓存、
    /// 切场景不会销毁它，<c>Init</c> 一辈子只跑一次 —— 第二局打开时会拿着上一局
    /// 那个已销毁玩家的选项（点了没反应，也不报错）。
    /// </para>
    /// </summary>
    public override void ShowMe()
    {
        base.ShowMe();

        BindPlayer();
        ShowOptions();
    }

    private void BindPlayer()
    {
        PlayerController player = PlayerManager.Service?.LocalPlayer;
        _wallet = player?.UpgradePoints;
        _upgrades = player?.Upgrades;
    }

    /// <summary>购买第 index 个选项：扣升级点 → 应用到该选项自己的目标 → 换一批选项。</summary>
    private void TryUpgrade(int index)
    {
        UpgradeOption option = GetOption(index);
        if (!option.IsValid) return;

        // 上限 / 一次性项：列表可能是刷新前的旧数据，先筛掉 ——
        // 否则会"扣了点才发现应用不了"，而玩家只看到点数少了
        if (!option.So.IsAvailable(option.Target))
        {
            RerollOptions();
            return;
        }

        if (_wallet == null || !_wallet.TrySpend(option.So.cost))
        {
            // 点了没反应是最难查的一类问题：说清是点数不够
            Debug.Log("[LevelUpPanel] 升级点不足，本次升级未生效。");
            _ = UIService.Service?.ShowPanelAsync<TipsPanel>();
            return;
        }

        // 上面已经筛过可用性，这里再失败说明状态在两次检查之间变了（重复点击 / 目标被销毁）
        if (!option.So.ApplyTo(option.Target))
            Debug.LogWarning($"[LevelUpPanel] 升级点已扣除，但「{option.So.name}」应用失败（目标缺失或已达上限）。");
        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);

        // **不关面板**，原地换一批：关掉再开会走一遍"隐藏 → 销毁 → 重新加载 → 显示"，
        // 中间那一帧没有模态面板持有暂停，玩法会推进一帧
        RerollOptions();
    }

    /// <summary>
    /// 显示选项：用**缓存**的那一批。
    ///
    /// <para>
    /// 关掉再打开看到的是同一批 —— 这是刻意的：否则玩家可以反复开关面板刷出想要的组合，
    /// 随机三选一就变成了自选。
    /// </para>
    /// </summary>
    private void ShowOptions()
    {
        SetOptions(_upgrades?.GetPlayerOptions());
        RefreshAffordability();
    }

    /// <summary>
    /// 换一批：从**该职业 EntitySO 上的全部升级 SO** 里重新抽 3 个。
    /// 买掉一项后调用 —— 不重抽的话刚买过的那项还在面板上，
    /// 看起来"点了没变化"，而玩家会以为购买没生效。
    /// </summary>
    private void RerollOptions()
    {
        SetOptions(_upgrades?.RerollPlayerOptions());
        RefreshAffordability();
    }

    /// <summary>买不起的置灰：点了只会弹"点数不足"，不如直接看得出来。</summary>
    private void RefreshAffordability()
    {
        int balance = _wallet != null ? _wallet.Balance : 0;
        DisableIfTooExpensive(btn1, 0, balance);
        DisableIfTooExpensive(btn2, 1, balance);
        DisableIfTooExpensive(btn3, 2, balance);
    }

    private void DisableIfTooExpensive(Button button, int index, int balance)
    {
        if (button == null || !button.interactable) return;

        UpgradeOption option = GetOption(index);
        if (option.IsValid && option.So.cost > balance) button.interactable = false;
    }

    private void CloseAndResume()
    {
        // 只隐藏自己：时间由暂停令牌集合恢复（还有别的模态面板时不会恢复）
        UIService.Service?.HidePanel<LevelUpPanel>();
    }

    public override void EscLogic()
    {
        CloseAndResume();
    }
}
