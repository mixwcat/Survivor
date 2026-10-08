using UnityEngine;

/// <summary>
/// 把武器自己的索敌半径写进它的检测圈（子物体上的 trigger <c>CircleCollider2D</c>）。
///
/// <para>
/// <b>为什么必须随武器走：</b>索敌圈原先长在塔身上，半径由 <c>BaseTower</c> 从
/// <c>StatType.AttackRange</c> 写入。武器独立之后，半径必须来自**武器自己的** StatModel ——
/// 否则"给这把武器升级射程"会升了没效果（写进了塔的数值，而圈是按武器读的）。
/// </para>
///
/// <para>
/// <b>塔身上已经没有索敌圈了</b>（2026-10 迁移）：<c>SearchRange</c> 与
/// <c>BaseTower._detectionCollider</c> 一并删除 —— 当时那个圈只是白写半径，没有任何消费者。
/// 现在**唯一**的索敌圈就是本组件维护的这一个，半径来自武器自己的 StatModel。
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class TowerWeaponRangeSync : MonoBehaviour, IStatRequirementsProvider
{
    [Tooltip("留空 = 在子物体里找第一个 isTrigger 的 CircleCollider2D")]
    [SerializeField] private CircleCollider2D _detectionCollider;

    /// <summary>
    /// 本组件读取的数值（供 <see cref="AttackDriver"/> 的启动体检，见
    /// <see cref="IStatRequirementsProvider"/>）。
    ///
    /// <para>
    /// <b>为什么由本组件声明、而不是 <c>BaseWeapon.RequiredStats</c>：</b>
    /// 读射程的是**这个独立组件**，不是武器基类（它连 <c>BaseWeapon</c> 的子类都不是）。
    /// 塞进武器基类会让玩家武器也声明一个它们根本不读的数值，
    /// 而"谁读它谁声明它"才能让"谁在读"这件事保持可见。
    /// </para>
    ///
    /// <para>
    /// 刻意不做缓存：每次访问返回新数组，调用点只有实体初始化一次
    /// （与 <see cref="AttackMethodSO.RequiredStats"/> 同一约定）。
    /// </para>
    /// </summary>
    public StatType[] RequiredStats => new[] { StatType.AttackRange };

    private EntityBehaviour _entity;
    private bool _subscribed;
    private bool _warnedNoCollider;

    private void Awake()
    {
        _entity = GetComponent<EntityBehaviour>();
        if (_detectionCollider == null) ResolveCollider();
    }

    private void OnEnable()
    {
        TrySubscribe();
        Apply();
    }

    /// <summary>
    /// 再套一次：<c>SetEntityConfig</c> 由装配方在 <c>Instantiate</c> 之后注入，
    /// 所以 <c>OnEnable</c> 那次读到的还是"没有配置"的兜底值 —— 少了这一行，
    /// 检测圈会停在 1 而不是武器自己的射程。
    ///
    /// <para>
    /// ⚠️ <b>订阅也必须在这里补</b>：<c>OnEnable</c> 跑在 <c>Instantiate</c> **期间**，
    /// 那时 <c>StatModel</c> 还不存在，<see cref="TrySubscribe"/> 会早退 ——
    /// 只在 <c>OnEnable</c> 订阅的话，射程升级**永远不会改检测圈**，
    /// 而范围圈照常重绘（两者读的是同一个数值），
    /// 表现为"圈变大了但打不到那么远"。别再把它简化回 <c>OnEnable</c> 一处。
    /// </para>
    /// </summary>
    private void Start()
    {
        TrySubscribe();
        Apply();
        WarnIfColliderOffRoot();
    }

    /// <summary>
    /// 索敌圈必须与武器本体**同物体**，否则告警一次。
    ///
    /// <para>
    /// <b>为什么：</b><c>BaseWeapon.SetActiveSlot</c> 切碰撞体用的是
    /// <c>GetComponents&lt;Collider2D&gt;()</c>（**仅同物体**），挂在子物体上的圈切不到。
    /// 后果是"换回这把武器后站着不打"：<c>AttackDriver</c> 被禁用期间收不到
    /// <c>OnTriggerEnter2D</c>，而重新启用时只有**碰撞体也一起被重新启用**才会补发 enter ——
    /// 圈没被切过，已经站在圈里的敌人就不会再触发一次。
    /// </para>
    ///
    /// <para>
    /// 这里只提示、不自动搬动：搬动会改变 prefab 结构，属于资产作者的决定。
    /// 本组件解析碰撞体时允许子物体（<c>GetComponentsInChildren</c>），
    /// 但那样解析出来的圈**是坏的** —— 所以两者的口径差异必须在这里说出来。
    /// </para>
    /// </summary>
    private void WarnIfColliderOffRoot()
    {
        if (_warnedColliderOffRoot || _detectionCollider == null) return;
        if (_detectionCollider.gameObject == gameObject) return;

        _warnedColliderOffRoot = true;
        Debug.LogWarning($"[{nameof(TowerWeaponRangeSync)}] {gameObject.name} 的索敌圈挂在子物体" +
                         $"「{_detectionCollider.name}」上 —— 切换武器时它不会被停用/启用，" +
                         "换回这把武器后会有一段时间不索敌（已经重叠的敌人不补发触发事件）。" +
                         "请把 trigger CircleCollider2D 放到武器根节点。", this);
    }

    private bool _warnedColliderOffRoot;

    private void OnDisable()
    {
        // 没订阅成功过就没什么可退（StatModel 建立后不会再变回 null，所以这里安全）
        if (!_subscribed) return;

        _entity.StatModel.OnStatChanged -= HandleStatChanged;
        _subscribed = false;
    }

    /// <summary>
    /// 建立订阅（**幂等**，可在 <c>OnEnable</c> 与 <c>Start</c> 各调一次）。
    /// <c>StatModel</c> 由装配方在 <c>Instantiate</c> 之后才建起来，所以第一次调用可能还没就绪。
    /// </summary>
    private void TrySubscribe()
    {
        if (_subscribed || _entity == null || _entity.StatModel == null) return;

        _entity.StatModel.OnStatChanged += HandleStatChanged;
        _subscribed = true;
    }

    /// <summary>升级改射程时同步（不订阅的话"升了射程圈没变"，只在视觉上看得出来）。</summary>
    private void HandleStatChanged(StatType type)
    {
        if (type == StatType.AttackRange) Apply();
    }

    private void ResolveCollider()
    {
        CircleCollider2D[] circles = GetComponentsInChildren<CircleCollider2D>(true);
        for (int i = 0; i < circles.Length; i++)
        {
            if (!circles[i].isTrigger) continue;

            _detectionCollider = circles[i];
            return;
        }
    }

    private void Apply()
    {
        if (_entity == null) return;

        // ⚠️ **必须静默跳过**：StatModel 由装配方在 Instantiate 之后注入，而 OnEnable
        // 跑在 Instantiate 期间 —— 那时读数值会打一条"无法获取 AttackRange（缺少 StatModel）"
        // 的告警。那是**预期状态**而不是配置错误（Start 会再套一次，见那里）。
        // 曾经漏了这道守卫，于是每次装配塔武器都在控制台留一条假告警。
        if (_entity.StatModel == null) return;

        if (_detectionCollider == null)
        {
            // 没有检测圈 = 这把武器**永远索不到敌**（AttackDriver 的目标列表恒为空），
            // 而它不报任何错。只提示一次（本方法在每次射程变化时都会走到）。
            if (!_warnedNoCollider)
            {
                _warnedNoCollider = true;
                Debug.LogWarning($"[{nameof(TowerWeaponRangeSync)}] {gameObject.name} 上找不到 trigger " +
                                 "CircleCollider2D，索敌半径无处可写 —— 这把武器将无法索敌（不会攻击）。", this);
            }
            return;
        }

        _detectionCollider.radius = _entity.GetStat(StatType.AttackRange);
    }
}
