using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 切换角色台交互点 —— 进大厅时已经选过一次角色，这里让玩家在出发前改主意。
///
/// <para>
/// <b>为什么复用 <see cref="CharacterSelectPanel"/>：</b>它本来就只做一件事 ——
/// 把选中的 <see cref="CharacterDefinitionSO"/> 交给 <c>IRunSessionService.TrySetCharacter</c>，
/// 由后者**原子化规范装备**（切到工程师会把不合法的武器换掉）。新做一个"切换角色面板"
/// 只会得到第二份同样的写入逻辑，而两份迟早会漂移。
/// </para>
///
/// <para>
/// <b>出发倒计时期间不可交互：</b>session 一锁定，改角色会被服务拒绝；
/// 与其让玩家点开面板再被拒绝，不如提示都不显示（<see cref="CanInteract"/> 直接返回 false）。
/// </para>
/// </summary>
public class CharacterSwitchInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("重叠时的选中优先级：数值大的先被选中")]
    [SerializeField] private int _priority = 10;

    [Tooltip("可切换的角色（场景配置，与 PlayerSpawner / LobbyDirector 同一份数据来源）")]
    public List<CharacterDefinitionSO> Characters = new List<CharacterDefinitionSO>();

    private InteractionPromptView _prompt;

    /// <inheritdoc />
    public Transform Anchor => transform;

    /// <inheritdoc />
    public int Priority => _priority;

    private void Awake()
    {
        _prompt = InteractionPromptView.FindIn(this);
    }

    public bool CanInteract(IInteractor interactor, out string reason)
    {
        if (Characters.Count == 0)
        {
            reason = "切换角色台没有配置可选角色";
            return false;
        }

        IRunSessionService session = RunSessionService.Service;
        if (session == null)
        {
            reason = "IRunSessionService 未注册";
            return false;
        }

        if (session.IsLocked)
        {
            reason = "出行配置已锁定（出发倒计时已开始）";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public InteractionPrompt GetPrompt(IInteractor interactor)
    {
        return new InteractionPrompt("切换", "角色");
    }

    /// <inheritdoc />
    public void Interact(IInteractor interactor)
    {
        if (!CanInteract(interactor, out string reason))
        {
            Debug.LogWarning($"[CharacterSwitchInteractable] {reason}，切换角色面板不会打开。");
            return;
        }

        _ = UIService.Service?.ShowPanelAsync<CharacterSelectPanel>(panel => panel.SetCharacters(Characters));
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
