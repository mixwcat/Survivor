using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家交互系统
/// 管理附近可交互对象，处理交互输入触发
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = "local";
    private IInputHandle _inputHandle;

    private IInteractable _currentInteractable;
    private readonly List<IInteractable> _nearbyInteractables = new List<IInteractable>();

    /// <summary>
    /// 只读访问当前交互目标
    /// </summary>
    public IInteractable CurrentInteractable => _currentInteractable;

    void Awake()
    {
        _inputHandle = InputHandleFactory.GetInput(_inputHandleId);

        if (_inputHandle == null)
        {
            Debug.LogError("Failed to create IInputHandle! Check InputHandleFactory logs.");
        }
    }

    void OnEnable()
    {
        if (_inputHandle != null)
        {
            _inputHandle.OnInteract += HandleInteract;
        }
    }

    void OnDisable()
    {
        if (_inputHandle != null)
        {
            _inputHandle.OnInteract -= HandleInteract;
        }
    }

    #region 交互对象管理

    /// <summary>
    /// 注册附近的可交互对象
    /// </summary>
    public void RegisterInteractable(IInteractable interactable)
    {
        if (!_nearbyInteractables.Contains(interactable))
            _nearbyInteractables.Add(interactable);
        UpdateCurrentInteractable();
    }

    /// <summary>
    /// 注销可交互对象
    /// </summary>
    public void UnregisterInteractable(IInteractable interactable)
    {
        _nearbyInteractables.Remove(interactable);
        UpdateCurrentInteractable();
    }

    /// <summary>
    /// 更新当前交互目标（默认取最后进入范围的对象），并通知选中状态变更
    /// </summary>
    private void UpdateCurrentInteractable()
    {
        IInteractable newInteractable = _nearbyInteractables.Count > 0
            ? _nearbyInteractables[_nearbyInteractables.Count - 1]
            : null;

        if (newInteractable == _currentInteractable) return;

        _currentInteractable?.OnDeselected();
        _currentInteractable = newInteractable;
        _currentInteractable?.OnSelected();
    }

    #endregion

    /// <summary>
    /// 处理交互输入
    /// </summary>
    private void HandleInteract()
    {
        _currentInteractable?.Interact();
    }
}
