using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 塔管理面板 —— **确定性定向列表**：列出目标塔全部可升级项，花升级点买想要的那一项。
///
/// <para>
/// <b>为什么它不再是 <see cref="BaseOptionPanel"/> 的三选一：</b>
/// 三选一是"随机抽 3 项"的交互，塔升级需要的是"这座塔能升的都在这里"。
/// 两者装不进同一套 UI —— 固定三个槽位在 Teto（5 项）/ Rin / Luo（各 4 项）上必然装不下，
/// 而把 <see cref="BaseOptionPanel"/> 改成"既能三选一又能列全表"会让它同时服务两种交互，
/// 两边都改不动。所以本面板自带可滚动列表（<see cref="itemTemplate"/> 克隆出行）。
/// </para>
///
/// <para>
/// <b>权限在这里再校验一次</b>：UI 只在工程师身上显示入口，但入口之外的路径
/// （PC 上按 E 交互、旧快捷键、联机伪造命令）都能打开它。
/// 升级与拆除各自校验「能力位 + 账本归属」，缺一不可；角色未就绪时一律拒绝。
/// </para>
/// </summary>
public class TowerLevelUpPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    /// <summary>模态面板：显示期间暂停游戏（令牌由 UIService 按本面板生命周期管理）。</summary>
    public override bool WantsPause => true;

    [Header("列表")]
    [Tooltip("列表容器（VerticalLayoutGroup + ContentSizeFitter）")]
    public RectTransform content;

    [Tooltip("行模板：面板按需克隆它。模板自身会被隐藏，不参与显示")]
    public TowerOptionItem itemTemplate;

    public ScrollRect scrollRect;

    [Header("按钮")]
    public Button btnClose;

    [Tooltip("拆除按钮；留空则不提供拆除功能（而不是让玩家点了没反应）")]
    [SerializeField] private Button _btnDismantle;

    [Header("文案")]
    [Tooltip("点数不足时显示在消耗位置的提示")]
    [SerializeField] private string _insufficientText = "点数不足";

    [Tooltip("列表为空时的提示")]
    [SerializeField] private string _emptyText = "这座塔没有可升级项";

    private BaseTower _towerType;
    private IUpgradePointWallet _wallet;
    private IPlayerUpgradeController _upgrades;

    /// <summary>已生成的行（复用；刷新时按需增减，不销毁重建）。</summary>
    private readonly List<TowerOptionItem> _items = new List<TowerOptionItem>();

    public override void Init()
    {
        PlayerController player = PlayerManager.Service?.LocalPlayer;
        _wallet = player?.UpgradePoints;
        _upgrades = player?.Upgrades;

        if (btnClose != null) btnClose.onClick.AddListener(CloseAndResume);

        // 拆除入口按能力开放：没有拆除能力的角色（枪手）连按钮都不该看到。
        // 领域层在 TryDismantle 里还会再校验一次归属 —— UI 隐藏不是安全边界。
        if (_btnDismantle != null)
        {
            bool canDismantle = HasCapability(CharacterCapability.TowerDismantle, "拆除", log: false);

            _btnDismantle.gameObject.SetActive(canDismantle);
            _btnDismantle.onClick.AddListener(TryDismantle);
        }

        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);

        RefreshOptions();
    }

    /// <summary>按当前目标塔重新列出全部选项并刷新 UI。</summary>
    private void RefreshOptions()
    {
        if (itemTemplate == null || content == null)
        {
            Debug.LogError("[TowerLevelUpPanel] 面板没有配置 itemTemplate / content，" +
                           "塔升级列表无法显示（prefab 是否被换过？）。");
            return;
        }

        UpgradeOption[] options = _upgrades?.GetTowerOptions(_towerType)
                                  ?? System.Array.Empty<UpgradeOption>();

        EnsureItemCount(options.Length);

        int balance = _wallet != null ? _wallet.Balance : 0;

        for (int i = 0; i < _items.Count; i++)
        {
            if (i >= options.Length)
            {
                _items[i].gameObject.SetActive(false);
                continue;
            }

            BindItem(_items[i], i, options[i], balance);
        }

        if (options.Length == 0)
            Debug.LogWarning($"[TowerLevelUpPanel] 目标塔没有可升级项：{_emptyText}");
    }

    /// <summary>
    /// 行数按需增减。**复用已有行**而不是每次销毁重建：
    /// 买一项就重建整张列表会让滚动位置跳回顶部，玩家每买一次都要重新滚下去。
    /// </summary>
    private void EnsureItemCount(int count)
    {
        while (_items.Count < count)
        {
            TowerOptionItem item = Instantiate(itemTemplate, content);
            item.gameObject.SetActive(true);
            _items.Add(item);
        }
    }

    private void BindItem(TowerOptionItem item, int index, in UpgradeOption option, int balance)
    {
        item.gameObject.SetActive(true);
        item.Bind(option);

        LevelUpSO so = option.So;
        if (item.Icon != null) item.Icon.sprite = so != null ? so.levelUpSprite : null;
        if (item.Title != null) item.Title.text = so != null ? so.levelUpText : "";

        bool affordable = so != null && balance >= so.cost;

        // 状态要明确：买不起不是"点了没反应"，而是按钮置灰 + 文案直说
        if (item.Cost != null)
            item.Cost.text = so == null ? "" : affordable ? so.cost.ToString() : _insufficientText;

        if (item.Button != null)
        {
            item.Button.interactable = option.IsValid && affordable;

            // 闭包捕获 index（每次刷新都会重建监听，所以先清空）
            item.Button.onClick.RemoveAllListeners();
            item.Button.onClick.AddListener(() => TryUpgrade(index));
        }
    }

    /// <summary>购买第 index 个选项：校验权限与归属 → 扣升级点 → 应用到该选项自己的目标（即目标塔）→ 刷新。</summary>
    private void TryUpgrade(int index)
    {
        if (index < 0 || index >= _items.Count) return;

        if (!HasCapability(CharacterCapability.TowerUpgrade, "升级")) return;
        if (!IsOwnedByLocalPlayer("升级")) return;

        UpgradeOption option = _items[index].Option;
        if (!option.IsValid || _wallet == null) return;

        // 上限 / 一次性项：列表可能是刷新前的旧数据，先筛掉 —— 否则会"扣了点才发现买不了"
        if (!option.So.IsAvailable(option.Target))
        {
            RefreshOptions();
            return;
        }

        if (!_wallet.TrySpend(option.So.cost)) return;

        // 只有**成功**的升级才累加账本：失败路径也记的话，账本会与实际投入对不上，
        // 拆除时就会退出一笔从来没花过的点数（ApplyTo 现在会回答"到底应用了没有"）
        if (!option.So.ApplyTo(option.Target))
        {
            Debug.LogWarning($"[TowerLevelUpPanel] 升级点已扣除，但「{option.So.name}」应用失败" +
                             "（目标已失效 / 已达上限），本次不计入塔的投入账本。");
            RefreshOptions();
            return;
        }

        _towerType?.GetComponent<TowerLedger>()?.RecordUpgrade(option.So.cost);

        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
        RefreshOptions();
    }

    /// <summary>
    /// 拆除目标塔：校验能力与归属 → 一次性退款 → 立即收尾 → 销毁。
    ///
    /// <para>
    /// 入口按钮由 prefab 提供（<c>_btnDismantle</c>）；没配就不显示拆除功能，
    /// 而不是"点了没反应"。
    /// </para>
    /// </summary>
    private void TryDismantle()
    {
        BaseTower tower = _towerType;
        if (tower == null) return;

        if (!HasCapability(CharacterCapability.TowerDismantle, "拆除")) return;
        if (!IsOwnedByLocalPlayer("拆除")) return;

        TowerLedger ledger = tower.GetComponent<TowerLedger>();
        if (ledger == null)
        {
            Debug.LogError("[TowerLevelUpPanel] 目标塔没有投入账本，无法确定退款归属（旧存档或手工放置的塔？）。");
            return;
        }

        // 一次性退款（幂等由账本保证：拆除、切场景、异步失败可能走到同一条路径）
        ledger.TryRefund(out int refunded);

        // 先收尾再销毁：Destroy 帧末才生效，期间塔仍会攻击、仍被敌人当作目标
        tower.PrepareForRemoval();

        Destroy(tower.gameObject);

        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
        Debug.Log($"[TowerLevelUpPanel] 已拆除防御塔，退还 {refunded} 点。");

        CloseAndResume();
    }

    /// <summary>
    /// 能力位校验。角色为 null / 未就绪时**拒绝** ——
    /// 宁可什么都不发生，也不要放行一个无主的特权命令。
    /// </summary>
    private static bool HasCapability(CharacterCapability capability, string action, bool log = true)
    {
        PlayerRoleController role = PlayerManager.Service?.LocalPlayer?.Role;
        if (role == null || !role.IsReady)
        {
            if (log) Debug.LogWarning($"[TowerLevelUpPanel] 本地玩家角色未就绪，{action}命令被拒绝。");
            return false;
        }

        if (role.Has(capability)) return true;

        if (log) Debug.LogWarning($"[TowerLevelUpPanel] 当前角色没有塔{action}能力，命令被拒绝。");
        return false;
    }

    /// <summary>
    /// 归属校验：只有建造者能升级与拆除自己的塔。
    /// 账本缺失或 owner 为空一律拒绝（fail closed）—— 放行"无主塔"等于让任何人都能退掉别人的投入。
    /// </summary>
    private bool IsOwnedByLocalPlayer(string action)
    {
        TowerLedger ledger = _towerType != null ? _towerType.GetComponent<TowerLedger>() : null;
        if (ledger == null)
        {
            Debug.LogError($"[TowerLevelUpPanel] 目标塔没有投入账本，无法校验归属，{action}命令被拒绝。");
            return false;
        }

        PlayerController player = PlayerManager.Service?.LocalPlayer;
        if (player == null || ledger.Owner == null || ledger.Owner != player)
        {
            Debug.LogWarning($"[TowerLevelUpPanel] 不是这座塔的建造者，无法{action}。");
            return false;
        }

        return true;
    }

    private void CloseAndResume()
    {
        // 只隐藏自己：时间由暂停令牌集合恢复（还有别的模态面板时不会恢复）
        UIService.Service?.HidePanel<TowerLevelUpPanel>();
    }

    public override void EscLogic()
    {
        CloseAndResume();
    }

    /// <summary>
    /// 设置升级目标塔。
    /// 面板尚未 Init 时只记录（Init 会据此列出选项）；
    /// 面板已打开时立刻重新列出，用于切换目标塔。
    /// </summary>
    public void SetTowerType(BaseTower tower)
    {
        _towerType = tower;

        if (IsInitialized)
            RefreshOptions();
    }
}
