using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReader", menuName = "Game/Input Reader")]
public class InputReader : ScriptableObject, InputSystem_Actions.IPlayerActions
{
    // 事件
    public event System.Action EPressEvent;
    public event System.Action EscapePressEvent;

    // 移动直接暴露值，供 Polling
    public Vector2 MoveInput { get; private set; }
    public Vector2 AttackInput { get; private set; }

    private InputSystem_Actions _inputActions;

    private void OnEnable()
    {
        if (_inputActions == null)
        {
            _inputActions = new InputSystem_Actions();
            _inputActions.Player.SetCallbacks(this); // 自动绑定接口方法
        }
        _inputActions.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Disable();
    }


    #region 接口实现
    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }
    public void SetMoveInput(Vector2 input)
    {
        MoveInput = input;
    }

    public void OnEPress(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
            EPressEvent?.Invoke();
    }

    public void OnEscapePress(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
            EscapePressEvent?.Invoke();
    }
    #endregion


    #region 输入管理
    public void EnableKey()
    {
        // _inputActions.Enable();  允许所有输入
    }
    public void DisableKey()
    {
        //_inputActions.Player.EPress.Disable();  禁用E键输入
    }
    #endregion


    #region Map切换
    public void SwitchToPlayerMap()
    {
        _inputActions.Player.Enable();
        _inputActions.UI.Disable();
    }
    public void SwitchToUIMap()
    {
        _inputActions.UI.Enable();
        _inputActions.Player.Disable();
    }
    #endregion
}