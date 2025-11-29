using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed;
    private Vector2 moveInput;

    [Header("组件")]
    public Rigidbody2D rb;
    public Joystick joystickMove;
    public Joystick joystickWeapon;

    [Header("属性")]
    public int pickRange;

    [Header("事件")]
    public LevelUpSO pickRangeLevelUpSO;
    public LevelUpSO moveSpeedLevelUpSO;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        Move();
    }


    /// <summary>
    /// 玩家移动
    /// </summary>
    private void Move()
    {
#if UNITY_STANDALONE_WIN
        // Windows 环境使用键盘输入
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
#elif UNITY_ANDROID
        // 安卓环境使用虚拟摇杆
        if (joystick == null) return;
        moveInput.x = joystick.Horizontal;
        moveInput.y = joystick.Vertical;
#endif

        moveInput.Normalize();
        rb.linearVelocity = moveInput * moveSpeed;
    }
    public void GetJoystick(Joystick jsMove, Joystick jsWeapon = null)
    {
        joystickMove = jsMove;
        joystickWeapon = jsWeapon;
    }


    /// <summary>
    /// 订阅升级事件
    /// </summary>
    private void IncreasePickRange()
    {
        // 拾取范围
        pickRange += 1;
    }
    private void IncreaseMoveSpeed()
    {
        // 移动速度
        moveSpeed *= 1.5f;
    }


    private void OnEnable()
    {
        PlayerManager.Instance.FindPlayer(this);
        pickRangeLevelUpSO.onLevelUp += IncreasePickRange;
        moveSpeedLevelUpSO.onLevelUp += IncreaseMoveSpeed;
    }

    private void OnDisable()
    {
        PlayerManager.Instance.MissPlayer();
        pickRangeLevelUpSO.onLevelUp -= IncreasePickRange;
        moveSpeedLevelUpSO.onLevelUp -= IncreaseMoveSpeed;
    }
}
