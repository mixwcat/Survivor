using UnityEngine;

/// <summary>
/// 玩家控制器
/// 继承 EntityBehaviour，所有数值从 StatModel 读取
/// </summary>
public class PlayerController : EntityBehaviour
{
    [Header("组件")]
    public Rigidbody2D rb;

    [Header("输入系统")]
    private IInputHandle _inputHandle;
    private Vector2 inputVector;
    public IInteractable currentInteractable;

    protected override void Awake()
    {
        base.Awake();

        // 通过工厂创建平台对应的输入处理器
        _inputHandle = InputHandleFactory.CreateLocalInput();

        if (_inputHandle == null)
        {
            Debug.LogError("Failed to create IInputHandle! Check InputHandleFactory logs.");
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        Move();
    }

    /// <summary>
    /// 玩家移动（速度从 StatModel 读取）
    /// </summary>
    private void Move()
    {
        if (_inputHandle == null) return;

        inputVector = _inputHandle.MoveInput;
        float speed = GetStat(StatType.BaseMoveSpeed);
        rb.linearVelocity = new Vector2(inputVector.x, inputVector.y).normalized * speed;
    }

    private void InteractWithObject()
    {
        if (currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }

    private void OnEnable()
    {
        PlayerManager.Instance.FindPlayer(this);

        if (_inputHandle != null)
        {
            _inputHandle.OnInteract += InteractWithObject;
        }
    }

    private void OnDisable()
    {
        PlayerManager.Instance.MissPlayer();

        if (_inputHandle != null)
        {
            _inputHandle.OnInteract -= InteractWithObject;
        }
    }
}
