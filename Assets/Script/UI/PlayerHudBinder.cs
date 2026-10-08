using System.Collections;
using UnityEngine;

/// <summary>
/// 本地玩家 HUD 绑定器。
/// 订阅本地玩家的强类型事件刷新 <see cref="GamePanel"/>，
/// 使经验/等级/升级点显示与核心状态解耦（核心只发事件，UI 只做表现）。
/// 联机模式下每个客户端只绑定自己的 <c>LocalPlayer</c>，天然隔离。
///
/// <para>
/// 经验与升级点来自**两个**组件：<see cref="IExperienceController"/>（等级/经验条）与
/// <see cref="IUpgradePointWallet"/>（升级点余额）。它们是独立状态，
/// 一次升级会同时改动两者，但消费只影响钱包。
/// </para>
/// </summary>
public class PlayerHudBinder : MonoBehaviour
{
    private GamePanel _panel;
    private IExperienceController _exp;
    private IUpgradePointWallet _wallet;
    private PlayerRoleController _role;
    private IWeaponManager _weapons;
    private Coroutine _routine;

    /// <summary>
    /// 由面板在初始化时调用，绑定目标并开始监听。
    ///
    /// <para>
    /// <b>它可以被调用多次</b>（面板实例跨场景复用，每次显示都会重新绑），
    /// 所以第一步必须是退订上一局的玩家 —— 否则 HUD 会一直挂在一个已销毁的对象上，
    /// 表现为"第二局的等级/升级点永远不变"，且不报错。
    /// </para>
    /// </summary>
    public void Bind(GamePanel panel)
    {
        _panel = panel;

        Unbind();

        if (!isActiveAndEnabled) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(BindRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        Unbind();
    }

    private IEnumerator BindRoutine()
    {
        // 等本地玩家出现（关卡入口生成玩家需要一帧以上）
        PlayerController local = null;
        while (local == null)
        {
            local = PlayerManager.Service?.LocalPlayer;
            if (local != null) break;
            yield return null;
        }

        _exp = local.ExperienceController;
        _wallet = local.GetComponent<UpgradePointWallet>();

        // 血量：左上角血条自己订阅 HealthChanged（核心只发事件，UI 只做表现）。
        // 组件可能挂在玩家根节点或子节点上，所以向下找
        PlayerHealthController health = local.GetComponentInChildren<PlayerHealthController>(true);
        if (health == null)
        {
            Debug.LogWarning("[PlayerHudBinder] 本地玩家上没有 PlayerHealthController，HUD 血条不会更新。");
        }

        if (_panel != null && _panel.healthPanel != null)
            _panel.healthPanel.Bind(health);

        if (_exp != null)
        {
            _exp.OnExpChanged += HandleExpChanged;
            _exp.OnLevelUp += HandleLevelUp;
        }

        if (_wallet != null)
        {
            _wallet.Changed += HandlePointsChanged;
            _wallet.Insufficient += HandleInsufficientPoints;
        }

        // 角色入口：角色定义由场景入口按 RunSession 注入，可能晚于本面板 Init，
        // 所以订阅事件而不是只读一次
        _role = local.Role;
        if (_role != null)
        {
            _role.RoleChanged += HandleRoleChanged;
            HandleRoleChanged(_role.Definition);
        }
        else
        {
            _panel?.RefreshRoleEntry(null);
        }

        // 武器槽：按钮与数字键 1/2 共用 SwitchToSlot；高亮跟着 ActiveSlotChanged 走。
        // 面板不自己轮询槽位状态 —— 那会让"切了槽但界面没变"只能靠调刷新频率掩盖
        _weapons = local.Weapons;
        if (_weapons != null)
        {
            _weapons.ActiveSlotChanged += HandleActiveSlotChanged;
            _panel?.BindWeaponSlots(_weapons);
        }

        RefreshExp();
        _panel?.UpdateUpgradePoints(_wallet != null ? _wallet.Balance : 0);

        // 协程结束：清掉句柄，下一次 Bind 才能重新起一条（否则 Bind 会以为还在跑）
        _routine = null;
    }

    private void Unbind()
    {
        // 血条订阅的是"上一局的玩家"，必须先退订再换绑（面板跨场景复用）
        if (_panel != null && _panel.healthPanel != null)
            _panel.healthPanel.Unbind();

        if (_exp != null)
        {
            _exp.OnExpChanged -= HandleExpChanged;
            _exp.OnLevelUp -= HandleLevelUp;
            _exp = null;
        }

        if (_wallet != null)
        {
            _wallet.Changed -= HandlePointsChanged;
            _wallet.Insufficient -= HandleInsufficientPoints;
            _wallet = null;
        }

        if (_role != null)
        {
            _role.RoleChanged -= HandleRoleChanged;
            _role = null;
        }

        if (_weapons != null)
        {
            _weapons.ActiveSlotChanged -= HandleActiveSlotChanged;
            _weapons = null;
        }
    }

    private void HandleActiveSlotChanged(int _) => _panel?.RefreshWeaponSlots();

    private void HandleRoleChanged(CharacterDefinitionSO _) => _panel?.RefreshRoleEntry(_role);

    private void HandleExpChanged(int _) => RefreshExp();
    private void HandleLevelUp(int _) => RefreshExp();
    private void HandlePointsChanged(int points) => _panel?.UpdateUpgradePoints(points);

    /// <summary>点数不足的提示表现（原先挂在经验控制器上，现在跟着钱包走）。</summary>
    private void HandleInsufficientPoints()
    {
        _ = UIService.Service?.ShowPanelAsync<TipsPanel>();
    }

    private void RefreshExp()
    {
        if (_exp == null || _panel == null) return;

        int max = _exp.ExpToNextLevel;
        if (max <= 0 || max == int.MaxValue) max = 1;

        _panel.UpdateExp(_exp.CurrentExp, max, _exp.CurrentLevel);
    }
}
