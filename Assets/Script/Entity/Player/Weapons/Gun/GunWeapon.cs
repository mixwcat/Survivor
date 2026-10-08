using UnityEngine;

/// <summary>
/// 枪械武器 —— 只负责**输入与瞄准**。
///
/// <para>
/// 开火计时与子弹生成已移到 <see cref="AttackDriver"/> + <see cref="ProjectileAttackSO"/>：
/// 本类把瞄准方向写进 <c>driver.AimDirection</c>，driver 到点后自己发射。
/// 这样同一套投射物攻击方式既能给塔用，也能给枪用 —— 这是「攻击方式与载体解耦」的收益。
/// </para>
///
/// <para>
/// <b>平台差异不在本类</b>：「屏幕坐标 → 世界方向」的换算在
/// <see cref="IInputHandle.TryGetAimDirection"/> 里（PC 用鼠标 + 相机，Android 用攻击摇杆）。
/// 本类曾经自带 <c>#if UNITY_STANDALONE_WIN / #elif UNITY_ANDROID</c>，那条路径有两个人：
/// ① 平台判断被复制成两份（与 <c>InputHandleFactory</c> 各一份）必须同步维护；
/// ② Build Target 不是这两个平台时两个分支都不成立 → 方向恒为零向量，
/// 枪不跟鼠标转而且**没有任何报错**。
/// </para>
/// </summary>
public class GunWeapon : BaseWeapon
{
    /// <summary>朝向变化小于该角度（度）时不写 Transform。</summary>
    private const float AngleEpsilon = 0.1f;

    [Header("输入系统")]
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = InputHandleFactory.LocalId;
    private IInputHandle _inputHandle;

    /// <summary>本副本是不是本机玩家的枪（见 <see cref="LocalPlayerGuard"/>）。</summary>
    private LocalPlayerGuard _guard;

    [Header("开火表现")]
    [Tooltip("勾选后开火时播放下面的音效")]
    [SerializeField] private bool _playSfx = true;
    [SerializeField] private ResourceEnum _shootSfx = ResourceEnum.PlayerShoot;

    private Vector2 _direction;

    /// <summary>上次写入的朝向角（度），配合 <see cref="AngleEpsilon"/> 避免每帧重复写 Transform。</summary>
    private float _lastAngle;
    private bool _hasAimed;

    private void Start()
    {
        AttackDriver driver = Driver;
        if (driver == null)
        {
            Debug.LogError("[GunWeapon] 同物体上没有 AttackDriver，枪不会开火。");
        }
        else if (_playSfx)
        {
            // 表现订阅。Unity 保证所有 Start 先于所有 Update，不会漏掉第一次攻击。
            driver.OnPerformed += PlayShootSfx;
        }

        // 联机时同一把枪在每个端都有副本（服务端上也有"远程玩家的枪"）。
        // 只有本机拥有的那个副本才读本地设备输入 —— 判据见 LocalPlayerGuard
        // （武器实例挂在玩家根节点下面，所以那个结构体用 GetComponentInParent 找 NetworkIdentity）。
        _guard = new LocalPlayerGuard(gameObject);
        if (!_guard.IsLocal) return;

        _inputHandle = InputHandleFactory.GetInput(_inputHandleId);
        if (_inputHandle == null)
        {
            Debug.LogError("GunWeapon: Failed to create IInputHandle!");
        }
    }

    private void OnDestroy()
    {
        AttackDriver driver = Driver;
        if (driver != null && _playSfx)
            driver.OnPerformed -= PlayShootSfx;

        // 与 Start 的 GetInput 成对。没拿到过句柄就什么都不做，
        // 否则会把本地玩家的引用计数减掉（远程副本的 OnDestroy 也会走到这里）
        if (_inputHandle == null) return;

        InputHandleFactory.ReleaseInput(_inputHandleId);
        _inputHandle = null;
    }

    private void PlayShootSfx()
    {
        AudioService.Service?.PlaySfx(_shootSfx);
    }

    private void Update()
    {
        // 未激活的武器不参与逐帧工作：备用枪继续读输入会与激活武器抢瞄准方向，
        // 而"收起来的那把在转"在画面上完全看不出来（它本来就被停用了渲染之外的一切）
        if (!IsActiveSlot) return;

        RotateWeapon();
    }

    /// <summary>
    /// 按输入算朝向：转自己，并把方向写给 driver 用于发射。
    /// </summary>
    private void RotateWeapon()
    {
        if (_inputHandle == null) return;

        // 摇杆回中 / 没有有效指针：保持上一次朝向（与旧 Android 分支的行为一致）
        if (!_inputHandle.TryGetAimDirection(transform.position, out Vector2 direction)) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        _direction = direction;
        // 方向有效就写进 driver（与「要不要写 Transform」是两件事：角度差极小时朝向仍然最新）
        AttackDriver driver = Driver;
        if (driver != null) driver.AimDirection = _direction;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

        // 朝向没变就不写 Transform：每次写都会脏化层级并触发变换传播（见 CLAUDE.md 性能红线）。
        // SpinWeapon 的「转速为 0 就不写」是同一个道理。
        if (_hasAimed && Mathf.Abs(Mathf.DeltaAngle(_lastAngle, angle)) < AngleEpsilon) return;

        _hasAimed = true;
        _lastAngle = angle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
