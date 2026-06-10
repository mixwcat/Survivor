using UnityEngine;

/// <summary>
/// 玩家控制器
/// 继承 EntityBehaviour，所有数值从 StatModel 读取
/// 负责：移动控制、输入系统初始化
/// </summary>
public class PlayerController : EntityBehaviour
{
    [Header("组件")]
    public Rigidbody2D rb;

    [Header("输入系统")]
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = "local";
    private IInputHandle _inputHandle;
    private Vector2 inputVector;

    [Header("经验系统")]
    [SerializeField]
    [Tooltip("玩家自身的经验控制器；为空时回退到全局 Service")]
    private ExperienceLevController _experienceController;

    /// <summary>玩家经验控制器（优先自身，回退全局）</summary>
    public IExperienceController ExperienceController => _experienceController ?? ExperienceLevController.Service;

    protected override void Awake()
    {
        base.Awake();

        // 通过工厂按 ID 获取输入处理器
        _inputHandle = InputHandleFactory.GetInput(_inputHandleId);

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
        rb.linearVelocity = inputVector.normalized * speed;
    }

    private void OnEnable()
    {
        PlayerManager.Service?.Register(this);
    }

    private void OnDisable()
    {
        PlayerManager.Service?.Unregister(this);
    }
}
