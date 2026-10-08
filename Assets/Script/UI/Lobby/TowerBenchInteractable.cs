using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 塔台交互点 —— 工程师靠近后按 E（移动端点提示）打开塔台面板看三座塔。
///
/// <para>
/// <b>为什么与武器台分开：</b>两者的目标人群互斥（枪手能切武器、工程师能建塔），
/// 合成一个交互物会让"面板里显示什么"取决于角色，而提示文案、权限、选中逻辑
/// 都要跟着分支；分开之后各自只有一条直线逻辑，玩家看到的提示也直说"挑选武器"/"查看防御塔"。
/// </para>
///
/// <para>
/// <b>商店语义保留</b>（图标 / 名称 / 描述 / 消耗 / 状态），但**解锁逻辑暂缓** ——
/// 三座塔默认已拥有，面板上显示"已拥有"且不可点。接回解锁时只需要让
/// <see cref="TowerBenchPanel"/> 把按钮接上购买事务。
/// </para>
/// </summary>
public class TowerBenchInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("重叠时的选中优先级：数值大的先被选中")]
    [SerializeField] private int _priority = 10;

    [Tooltip("塔台展示的防御塔（场景配置）")]
    public List<TowerEntitySO> Towers = new List<TowerEntitySO>();

    private InteractionPromptView _prompt;

    /// <inheritdoc />
    public Transform Anchor => transform;

    /// <inheritdoc />
    public int Priority => _priority;

    private void Awake()
    {
        _prompt = InteractionPromptView.FindIn(this);
    }

    /// <summary>只有能建塔的角色（工程师）才看得到塔台提示。</summary>
    public bool CanInteract(IInteractor interactor, out string reason)
    {
        if (Towers.Count == 0)
        {
            reason = "塔台没有配置可选防御塔";
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

        if (!role.Has(CharacterCapability.TowerBuild))
        {
            reason = "当前角色不能建造防御塔";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public InteractionPrompt GetPrompt(IInteractor interactor)
    {
        return new InteractionPrompt("查看", "防御塔");
    }

    /// <inheritdoc />
    public void Interact(IInteractor interactor)
    {
        if (!CanInteract(interactor, out string reason))
        {
            Debug.LogWarning($"[TowerBenchInteractable] {reason}，塔台不会打开。");
            return;
        }

        _ = UIService.Service?.ShowPanelAsync<TowerBenchPanel>(panel => panel.SetTowers(Towers));
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
