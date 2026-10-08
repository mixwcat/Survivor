using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器台交互点 —— 玩家靠近后按 E（移动端点提示）打开武器台面板挑武器。
///
/// <para>
/// <b>为什么不再是"进范围自动打开"：</b>大厅是"可以乱逛"的，自动弹面板会让玩家
/// 只是路过就被打断；而且自动打开没有"谁在交互"的概念，联机下没法只对某个人生效。
/// 现在统一走 <see cref="IInteractable"/>：靠近显示世界空间提示，按下才打开。
/// </para>
///
/// <para>
/// <b>本文件是 <c>WeaponBenchTrigger.cs</c> 改名而来（.meta 一起移动，GUID 未变）</b>，
/// 所以 Lobby 场景上的脚本引用与 <c>Weapons</c> 列表都没有丢。
/// </para>
///
/// <para>
/// 武器列表配在**场景**上（与 <c>LobbyDirector.Characters</c>、塔台同模式）：
/// prefab 是资产，把"这一局有哪些武器"配进资产会让场景与资产各持一份清单。
/// </para>
/// </summary>
public class WeaponBenchInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("重叠时的选中优先级：数值大的先被选中")]
    [SerializeField] private int _priority = 10;

    [Tooltip("武器台提供的可选武器（场景配置）")]
    public List<WeaponEntitySO> Weapons = new List<WeaponEntitySO>();

    private InteractionPromptView _prompt;

    /// <inheritdoc />
    public Transform Anchor => transform;

    /// <inheritdoc />
    public int Priority => _priority;

    private void Awake()
    {
        _prompt = InteractionPromptView.FindIn(this);
    }

    /// <summary>
    /// 角色能力前置：工程师固定携带火球、没有武器切换能力，武器台对他没有意义 ——
    /// 连提示都不该出现（打开再拒绝是更差的手感）。
    /// </summary>
    public bool CanInteract(IInteractor interactor, out string reason)
    {
        if (Weapons.Count == 0)
        {
            reason = "武器台没有配置可选武器";
            return false;
        }

        PlayerRoleController role = interactor != null && interactor.Player != null
            ? interactor.Player.Role
            : null;

        if (role == null || !role.IsReady)
        {
            reason = "角色未就绪";
            return false;
        }

        if (!role.Has(CharacterCapability.WeaponSwitch))
        {
            reason = "当前角色不能切换武器";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public InteractionPrompt GetPrompt(IInteractor interactor)
    {
        return new InteractionPrompt("挑选", "武器");
    }

    /// <inheritdoc />
    public void Interact(IInteractor interactor)
    {
        if (!CanInteract(interactor, out string reason))
        {
            Debug.LogWarning($"[WeaponBenchInteractable] {reason}，武器台不会打开。");
            return;
        }

        _ = UIService.Service?.ShowPanelAsync<WeaponBenchPanel>(panel => panel.SetWeapons(Weapons));
    }

    /// <inheritdoc />
    public void OnSelected(IInteractor interactor)
    {
        if (interactor == null || !interactor.IsLocal) return;

        if (_prompt != null) _prompt.Show(interactor, this);
    }

    /// <inheritdoc />
    public void OnDeselected(IInteractor interactor)
    {
        if (interactor == null || !interactor.IsLocal) return;

        if (_prompt != null) _prompt.Hide();
    }
}
