using UnityEngine;

/// <summary>
/// 摄像机跟随 —— 跟随本地玩家，并支持横向推进的边界限制。
///
/// <para>
/// <b>跟随目标必须懒查找</b>：玩家现在由 <c>PlayerSpawner</c> 在运行时生成，
/// <c>Start</c> 时可能还不存在。原实现在 <c>Start</c> 里取一次就再也不查，
/// 结果是"相机一动不动"——而且不报错，看起来像相机坏了。
/// </para>
///
/// <para>
/// <b>在 <c>LateUpdate</c> 里跟随</b>：<c>FixedUpdate</c> 与渲染帧不同步，
/// 相机在物理帧更新会让画面在两个渲染帧之间来回抖。
/// </para>
///
/// <para>
/// <b>边界为什么需要</b>：推车模式是自左向右的横向推进，路线两端之外没有内容。
/// 不限制的话相机会跟着玩家跑进空白区域（玩家可以往回走）。
/// </para>
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("跟随")]
    [Tooltip("跟随目标；留空则自动跟随本地玩家（运行时生成，所以是懒查找）")]
    public Transform playerTransform;

    [Tooltip("跟随平滑速度（越大越紧）")]
    [SerializeField] private float _smoothSpeed = 5f;

    [Tooltip("相机 z（2D 正交下用于拉开与场景的距离）")]
    [SerializeField] private float _zOffset = -10f;

    [Header("横向边界（路线从左向右推进）")]
    [Tooltip("是否限制相机的 x 范围")]
    [SerializeField] private bool _clampX;

    [SerializeField] private float _minX;
    [SerializeField] private float _maxX = 200f;

    private void Start()
    {
        ResolveTarget();
    }

    private void LateUpdate()
    {
        if (playerTransform == null) ResolveTarget();
        if (playerTransform == null) return;

        Vector3 desired = playerTransform.position;
        if (_clampX) desired.x = Mathf.Clamp(desired.x, _minX, _maxX);
        desired.z = _zOffset;

        transform.position = Vector3.Lerp(transform.position, desired, _smoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 懒查找本地玩家。联机时应当改为"跟随本地玩家"而不是"最近玩家"——
    /// 现在取的就是 <c>LocalPlayer</c>，天然满足。
    /// </summary>
    private void ResolveTarget()
    {
        playerTransform = PlayerManager.Service?.LocalPlayer?.transform;
    }
}
