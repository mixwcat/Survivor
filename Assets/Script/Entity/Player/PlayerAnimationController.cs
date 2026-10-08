using UnityEngine;

/// <summary>
/// 玩家行走动画：把"往哪走"翻成 <c>Animator</c> 的四个方向布尔量。
///
/// <para>
/// <b>本地玩家与远程副本的判据不同：</b>本地玩家读输入，远程副本**必须**改看实际位移 ——
/// 否则每个副本都会读到同一份本地设备输入，"我往右走，所有人的角色都播往右的动画"，
/// 而且没有任何报错。
/// </para>
/// </summary>
public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = InputHandleFactory.LocalId;
    private IInputHandle _inputHandle;

    /// <summary>是否已经尝试过获取输入句柄（避免逐帧重试）。</summary>
    private bool _inputResolved;

    /// <summary>本实例是不是本机玩家（见 <see cref="LocalPlayerGuard"/>）。</summary>
    private LocalPlayerGuard _guard;

    /// <summary>远程副本的朝向判据：上一帧的位置。</summary>
    private Vector3 _lastPosition;
    private bool _hasLastPosition;

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

        _guard = new LocalPlayerGuard(gameObject);

        // 输入**不在这里取**：联机对象的 Awake 跑在 isLocalPlayer 赋值之前
        //（NetworkClient.cs:1151 vs :1169），那时判不出自己是不是本地玩家。
        // 懒获取放在 InputHandle 属性里。
    }

    /// <summary>本实例的输入句柄（懒获取，只给本地玩家）。</summary>
    private IInputHandle InputHandle
    {
        get
        {
            if (_inputResolved) return _inputHandle;
            if (!_guard.IsLocal) return null;

            _inputResolved = true;
            _inputHandle = InputHandleFactory.GetInput(_inputHandleId);

            if (_inputHandle == null)
            {
                Debug.LogError($"[{nameof(PlayerAnimationController)}] 创建 IInputHandle 失败！" +
                               "检查 InputHandleFactory 的日志。");
            }

            return _inputHandle;
        }
    }

    private void OnDestroy()
    {
        // 与懒获取成对；没获取过就什么都不做，否则会减掉别人的引用计数
        if (!_inputResolved) return;

        InputHandleFactory.ReleaseInput(_inputHandleId);
        _inputHandle = null;
        _inputResolved = false;
    }

    void Update()
    {
        if (animator == null) return;

        GetWalkingState();
        SetAnimationParameters();
    }


    /// <summary>
    /// 获取行走状态：本地玩家看输入，远程副本看位移。
    /// </summary>
    private void GetWalkingState()
    {
        IInputHandle handle = InputHandle;

        if (handle != null)
        {
            // MoveInput 只读一次（原先连续读 4 次）
            Vector2 move = handle.MoveInput;

            rightWalking = move.x > 0;
            leftWalking = move.x < 0;
            backWalking = move.y > 0;
            towardWalking = move.y < 0;
            isMoving = rightWalking || leftWalking || backWalking || towardWalking;
            return;
        }

        if (!_guard.IsLocal)
        {
            GetWalkingStateFromMotion();
            return;
        }

        // 本地玩家但输入还没就绪（网络对象刚 spawn、尚未赋值 isLocalPlayer）：保持静止
        ClearWalkingState();
    }

    /// <summary>
    /// 远程副本的行走状态：按**每帧位移**反推方向。
    /// 副本的位置由 <c>NetworkTransform</c> 驱动，没有输入可读。
    /// </summary>
    private void GetWalkingStateFromMotion()
    {
        Vector3 position = transform.position;

        if (!_hasLastPosition)
        {
            _lastPosition = position;
            _hasLastPosition = true;
            ClearWalkingState();
            return;
        }

        Vector2 delta = position - _lastPosition;
        _lastPosition = position;

        // 阈值取得很小（约 1mm/帧）：同步插值在静止时也会有细微抖动，
        // 不设阈值会让待机的角色一直在播行走动画
        if (delta.sqrMagnitude < 0.000001f)
        {
            ClearWalkingState();
            return;
        }

        rightWalking = delta.x > 0f;
        leftWalking = delta.x < 0f;
        backWalking = delta.y > 0f;
        towardWalking = delta.y < 0f;
        isMoving = true;
    }

    private void ClearWalkingState()
    {
        isMoving = false;
        rightWalking = false;
        leftWalking = false;
        backWalking = false;
        towardWalking = false;
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
