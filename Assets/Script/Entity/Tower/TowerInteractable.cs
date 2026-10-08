using UnityEngine;

/// <summary>
/// 防御塔的交互点 —— 玩家靠近后按 E（移动端点提示）打开塔管理面板。
///
/// <para>
/// <b>它只做交互，不做触发：</b>进出范围由同物体上的 <see cref="InteractionSensor"/> 负责，
/// 选中/取消由 <see cref="IInteractor"/> 驱动。旧 <c>DetectPlayer</c> 把注册、玩家解析、
/// 能力校验、面板打开、平台提示 UI 全塞在一个脚本里，于是"平台判断"和"谁在交互"都写死在里面。
/// </para>
///
/// <para>
/// <b>能力校验前置：</b>没有塔管理能力的角色（枪手）连提示都不会出现 ——
/// 由 <see cref="CanInteract"/> 返回 false，交互者就不会把它选为当前目标。
/// 面板内部仍会再校验一次（旧快捷键与联机伪造命令能绕过提示）。
/// </para>
///
/// <para>
/// <b>本文件是 <c>DetectPlayer.cs</c> 改名而来（.meta 一起移动，GUID 未变）</b>，
/// 所以三个塔 prefab 上的脚本引用没有断；只有 <c>arrow</c>/<c>txtE</c> 两个字段
/// 变成了孤儿键（提示已交给 <see cref="InteractionPromptView"/>）。
/// </para>
/// </summary>
public class TowerInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("重叠时的选中优先级：数值大的先被选中")]
    [SerializeField] private int _priority = 10;

    private BaseTower _tower;
    private InteractionPromptView _prompt;

    /// <inheritdoc />
    public Transform Anchor => _tower != null ? _tower.transform : transform;

    /// <inheritdoc />
    public int Priority => _priority;

    private void Awake()
    {
        // 塔的身份组件在根节点，本组件挂在子物体 PlayerDetectRange 上
        _tower = GetComponentInParent<BaseTower>();
        _prompt = InteractionPromptView.FindIn(this);

        if (_tower == null)
        {
            Debug.LogError($"[TowerInteractable] {gameObject.name} 不在任何 BaseTower 之下，" +
                           "塔交互不可用（塔 prefab 结构是否被改过？）。");
        }
    }

    /// <summary>
    /// 能力校验前置：枪手按 E 不该打开一个"什么都点不动"的塔面板。
    ///
    /// <para>
    /// 这只是表现层的第一道门 —— <c>TowerLevelUpPanel</c> 在执行升级/拆除时还会再校验一次，
    /// 因为旧快捷键与联机伪造命令都能绕过这里。
    /// </para>
    /// </summary>
    public bool CanInteract(IInteractor interactor, out string reason)
    {
        if (_tower == null)
        {
            reason = "没有关联的防御塔";
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

        if (!role.Has(CharacterCapability.TowerUpgrade) && !role.Has(CharacterCapability.TowerDismantle))
        {
            reason = "当前角色没有塔管理能力";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public InteractionPrompt GetPrompt(IInteractor interactor)
    {
        return new InteractionPrompt("管理", "防御塔");
    }

    /// <inheritdoc />
    public void Interact(IInteractor interactor)
    {
        if (!CanInteract(interactor, out string reason))
        {
            Debug.LogWarning($"[TowerInteractable] {reason}，塔面板不会打开。");
            return;
        }

        _ = UIService.Service?.ShowPanelAsync<TowerLevelUpPanel>(panel => panel.SetTowerType(_tower));
    }

    /// <inheritdoc />
    public void OnSelected(IInteractor interactor)
    {
        // 提示与高亮是本地表现：远程玩家的交互者不该点亮本地屏幕
        if (interactor == null || !interactor.IsLocal) return;

        if (_prompt != null) _prompt.Show(interactor, this);

        // BaseTower 只负责身份与表现（高亮 + 范围圈），交互逻辑不回到塔身上
        if (_tower != null) _tower.OnSelected();
    }

    /// <inheritdoc />
    public void OnDeselected(IInteractor interactor)
    {
        if (interactor == null || !interactor.IsLocal) return;

        if (_prompt != null) _prompt.Hide();

        if (_tower != null) _tower.OnDeselected();
    }
}
