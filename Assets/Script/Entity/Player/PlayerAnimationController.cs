using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = InputHandleFactory.LocalId;
    private IInputHandle _inputHandle;
    private Animator animator;

    // Animator 参数哈希缓存：SetBool(string, ...) 每次都要重算字符串哈希
    private static readonly int HashIsMoving = Animator.StringToHash("isMoving");
    private static readonly int HashRight = Animator.StringToHash("rightWalking");
    private static readonly int HashLeft = Animator.StringToHash("leftWalking");
    private static readonly int HashBack = Animator.StringToHash("backWalking");
    private static readonly int HashToward = Animator.StringToHash("towardWalking");

    private bool isMoving;
    private bool rightWalking;
    private bool leftWalking;
    private bool backWalking;
    private bool towardWalking;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning($"[{nameof(PlayerAnimationController)}] 找不到 Animator，动画参数将不会更新：{gameObject.name}");

        _inputHandle = InputHandleFactory.GetInput(_inputHandleId);

        if (_inputHandle == null)
        {
            Debug.LogError("PlayerAnimationController: Failed to create IInputHandle!");
        }
    }

    private void OnDestroy()
    {
        // 与 Awake 的 GetInput 成对，避免共享句柄的引用计数只增不减
        InputHandleFactory.ReleaseInput(_inputHandleId);
        _inputHandle = null;
    }

    void Update()
    {
        if (animator == null) return;

        GetWalkingState();
        SetAnimationParameters();
    }


    /// <summary>
    /// 获取行走状态
    /// </summary>
    private void GetWalkingState()
    {
        if (_inputHandle == null) return;

        // MoveInput 只读一次（原先连续读 4 次）
        Vector2 move = _inputHandle.MoveInput;

        rightWalking = move.x > 0;
        leftWalking = move.x < 0;
        backWalking = move.y > 0;
        towardWalking = move.y < 0;
        isMoving = rightWalking || leftWalking || backWalking || towardWalking;
    }

    /// <summary>
    /// 设置动画参数：只在状态真正变化时写入。
    /// 原实现每帧无条件写 5 次（含字符串哈希计算 + 原生调用），静止时也满额开销。
    /// </summary>
    private void SetAnimationParameters()
    {
        SetBoolIfChanged(HashIsMoving, isMoving);
        SetBoolIfChanged(HashRight, rightWalking);
        SetBoolIfChanged(HashLeft, leftWalking);
        SetBoolIfChanged(HashBack, backWalking);
        SetBoolIfChanged(HashToward, towardWalking);
    }

    private void SetBoolIfChanged(int hash, bool value)
    {
        if (animator.GetBool(hash) == value) return;
        animator.SetBool(hash, value);
    }
}
