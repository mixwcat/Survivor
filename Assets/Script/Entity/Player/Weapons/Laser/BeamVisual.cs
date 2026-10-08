using UnityEngine;

/// <summary>
/// 光束的视觉载体 —— 订阅 <see cref="AttackDriver.OnPerformed"/>，闪一条线。
///
/// <para>
/// <b>为什么表现要放在载体上而不是 SO 里：</b><see cref="BeamAttackSO"/> 的伤害是
/// **瞬时**结算的（<c>CircleCast</c> 一帧打完），它不生成任何实体，也就没有"回收"这一步；
/// 而玩家需要看见"打出去了"。把 <c>LineRenderer</c> 塞进 SO 会让常驻资产持有场景对象引用，
/// 关闭 Domain Reload 时跨 Play 会话存活 —— 正是攻击系统里明令禁止的。
/// </para>
///
/// <para>
/// <b>方向取 <c>driver.AimDirection</c></b>（与伤害判定用的是同一个值）：
/// 自己再算一遍朝向迟早会漂移，表现与判定不一致时玩家只会认为"打中了却没伤害"。
/// </para>
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BeamVisual : MonoBehaviour
{
    [Header("表现")]
    [Tooltip("一次光束的显示时长（秒）")]
    [SerializeField] private float _flashSeconds = 0.08f;

    [Tooltip("线宽")]
    [SerializeField] private float _width = 0.28f;

    [SerializeField] private Color _startColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color _endColor = new Color(1f, 0.2f, 0.2f, 0.15f);

    [Tooltip("攻击方式不是 BeamAttackSO 时的兜底射程")]
    [SerializeField] private float _fallbackRange = 10f;

    private LineRenderer _line;
    private AttackDriver _driver;
    private float _timer;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = 2;
        _line.startWidth = _width;
        _line.endWidth = _width;
        _line.enabled = false;
    }

    private void Start()
    {
        _driver = GetComponent<AttackDriver>();
        if (_driver == null)
        {
            Debug.LogError("[BeamVisual] 同物体上没有 AttackDriver，光束不会显示。");
            return;
        }

        // Unity 保证所有 Start 先于所有 Update，所以第一次攻击不会漏掉
        _driver.OnPerformed += ShowBeam;
    }

    private void OnDestroy()
    {
        if (_driver != null) _driver.OnPerformed -= ShowBeam;
    }

    private void ShowBeam()
    {
        if (_line == null) return;

        Vector2 direction = _driver.AimDirection;
        // 零方向会让线朝着世界的 +X 拉出去 —— 表现上就是"没瞄准却打了一条线"
        if (direction.sqrMagnitude < 0.0001f) return;

        Transform muzzle = _driver.Muzzle != null ? _driver.Muzzle : transform;
        float range = _driver.Attack is BeamAttackSO beam ? beam.Range : _fallbackRange;

        Vector3 origin = muzzle.position;
        _line.SetPosition(0, origin);
        _line.SetPosition(1, origin + (Vector3)(direction.normalized * range));
        _line.startColor = _startColor;
        _line.endColor = _endColor;
        _line.enabled = true;

        _timer = _flashSeconds;
    }

    private void Update()
    {
        if (_timer <= 0f) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        // 只是关掉渲染，不销毁也不改状态 —— 下一次攻击直接复用同一条线
        _line.enabled = false;
    }
}
