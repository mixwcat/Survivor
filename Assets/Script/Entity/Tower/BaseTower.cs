using UnityEngine;

/// <summary>
/// 防御塔基类 —— 塔的**身份与表现**，不含任何攻击逻辑，也**不含索敌**。
///
/// <para>保留的职责只有四件：</para>
/// <list type="bullet">
/// <item>生命周期 + 敌人目标表（<c>EnemyTargetRegistry</c>）注册；</item>
/// <item>选中与高亮（<c>TowerInteractable</c> 按本类型引用塔）；</item>
/// <item>攻击表现（动画 + 音效）—— 订阅**当前激活武器**的 <see cref="AttackDriver.OnPerformed"/>，
/// 换武器时改订；</item>
/// <item>范围可视化 —— 半径由渲染器向**当前激活武器**要（<c>TowerRangeVisualizer.Bind</c>），
/// 塔只负责"选中时显示、取消时渐隐"。</item>
/// </list>
///
/// <para>
/// <b>索敌与射程都属于武器</b>（2026-10 迁移）：每把武器自带检测圈
/// （layer <c>TowerDetector</c>，半径由 <c>TowerWeaponRangeSync</c> 从武器自己的数值写入），
/// 触发回调由武器上的 <see cref="AttackDriver"/> 接收并按 <c>AttackMethodSO.TargetTag</c> 过滤。
/// 塔身上原先的 <c>SearchRange</c> 已删除 —— 它当时已经没有任何消费者（塔上不再有 AttackDriver），
/// 只让"射程"出现两份值（塔 DataSO 一份、武器 DataSO 一份），改错一个就静默失效。
/// </para>
///
/// <para>
/// <b>攻击逻辑也不在这里。</b>「怎么打」在 <see cref="AttackMethodSO"/>，
/// 「什么时候打 + 打谁」在 <see cref="AttackDriver"/>。
/// 换攻击方式只需要换武器上的资产，**不要再写 <c>Xxx : BaseTower</c> 子类** ——
/// 子类会把「打什么」重新焊回塔的类型上，正是这次重构要拆掉的东西。
/// </para>
///
/// <para>
/// 本类仍是**具体类**且必须留在塔预制体上：<c>TowerInteractable</c> /
/// <c>EnemyTargetFinder</c> / <c>GamePanel</c> / <c>TowerLevelUpPanel</c> /
/// <c>PlayerUpgradeController</c> 全都按 <c>BaseTower</c> 引用塔。
/// </para>
/// </summary>
public class BaseTower : EntityBehaviour
{
    [Header("高亮材质")]
    [Tooltip("选中该塔时替换到所有子 SpriteRenderer 上的材质（sharedMaterial，不克隆实例）")]
    [SerializeField] private Material _highlightMaterial;
    private SpriteRenderer[] _spriteRenderers;
    private Material[] _originalMaterials;

    [Header("攻击表现（可选，与攻击方式无关）")]
    [Tooltip("攻击时播放动画的 Animator。Luo 的 Heal 触发器在子物体 Bao 上，直接拖那个 Animator")]
    [SerializeField] private Animator _attackAnimator;
    [Tooltip("攻击时触发的 Animator 参数名")]
    [SerializeField] private string _attackTrigger = "Attack";
    [Tooltip("勾选后攻击时播放下面的音效。Teto 没有攻击音效，保持不勾")]
    [SerializeField] private bool _playSfx;
    [SerializeField] private ResourceEnum _attackSfx;

    private TowerRangeVisualizer _rangeVisualizer;
    private TowerWeaponController _weapons;

    /// <summary>当前已订阅表现的武器（换武器时要退订旧的）。</summary>
    private BaseWeapon _feedbackWeapon;

    protected override void Awake()
    {
        base.Awake();

        _rangeVisualizer = GetComponent<TowerRangeVisualizer>();
        if (_rangeVisualizer == null)
            _rangeVisualizer = gameObject.AddComponent<TowerRangeVisualizer>();

        // 武器控制器：攻击表现与射程都从"当前激活武器"读
        _weapons = GetComponent<TowerWeaponController>();

        // 缓存所有 SpriteRenderer 的原始材质。
        // 必须用 sharedMaterial：访问 Renderer.material 会**为每个渲染器克隆一份材质实例**，
        // 每座塔就会凭空多出 N 份材质（内存泄漏 + 材质各不相同导致 SRP 合批失效）。
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalMaterials = new Material[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            _originalMaterials[i] = _spriteRenderers[i].sharedMaterial;
        }

        // 高亮材质直接配在塔自身。原先还有一条「从 SOManager 取全局统一材质」的回退，
        // 但三个塔预制体本来就指向同一份材质资源，那条回退既冗余又多一层全局依赖，已删除。
        if (_highlightMaterial == null)
        {
            Debug.LogWarning($"[{nameof(BaseTower)}] {gameObject.name} 未配置高亮材质，选中时不会有高亮反馈。");
        }
    }

    private void Start()
    {
        // 表现订阅走**当前激活武器**。武器由 TowerWeaponController 异步装配（Start 里 await），
        // 所以这里不赌 Start 顺序：订阅事件，装配完成时它会通知一次。
        if (_weapons != null)
        {
            _weapons.ActiveWeaponChanged += OnActiveWeaponChanged;
            OnActiveWeaponChanged(_weapons.ActiveWeapon);
        }
        else
        {
            Debug.LogWarning($"[{nameof(BaseTower)}] {gameObject.name} 上没有 TowerWeaponController，" +
                             "塔不会有任何攻击与攻击表现。");
        }
    }

    /// <summary>激活武器变化（含首次装配完成）：改订攻击表现，并把范围圈的半径来源换成新武器。</summary>
    private void OnActiveWeaponChanged(BaseWeapon weapon)
    {
        SubscribeWeapon(weapon);

        // 范围圈自己向武器要半径（含射程升级时的重绘），塔不需要知道射程是哪个数值
        if (_rangeVisualizer != null) _rangeVisualizer.Bind(weapon);
    }

    /// <summary>
    /// 把攻击表现挂到指定武器的 <see cref="AttackDriver.OnPerformed"/> 上。
    ///
    /// <para>
    /// <b>必须跟着武器走：</b>武器化之后 <c>AttackDriver</c> 在**武器实例**上，塔身上取不到 ——
    /// 旧实现取的是 <c>GetComponent&lt;AttackDriver&gt;()</c>（恒为 null），
    /// 表现是"塔在打，但既没有动画也没有音效"，而且不报错。
    /// </para>
    /// </summary>
    private void SubscribeWeapon(BaseWeapon weapon)
    {
        // 先退订旧的（换武器 / 拆除）
        if (_feedbackWeapon != null)
        {
            AttackDriver old = _feedbackWeapon.GetComponent<AttackDriver>();
            if (old != null) old.OnPerformed -= PlayAttackFeedback;
        }

        _feedbackWeapon = weapon;
        if (weapon == null) return;

        AttackDriver driver = weapon.GetComponent<AttackDriver>();
        if (driver != null) driver.OnPerformed += PlayAttackFeedback;
    }

    void OnEnable()
    {
        // 敌人目标表：塔也是可攻击目标。
        // 走注册表而不是让寻敌方遍历场景 —— 否则每加一种可攻击单位都要改一次寻敌逻辑
        EnemyTargetRegistry.Service?.Register(transform);
    }

    void OnDisable()
    {
        EnemyTargetRegistry.Service?.Unregister(transform);
    }

    /// <summary>
    /// 拆除前调用：**立即**停止攻击与索敌并注销。
    ///
    /// <para>
    /// <c>Destroy</c> 要到帧末才生效，这期间塔仍会被敌人当成合法目标、攻击也照常结算 ——
    /// 表现是"塔已经消失了但这一帧还打了一下"，或者更糟：拆掉的塔在帧末前又结算了一次伤害。
    /// 所以必须在调用 <c>Destroy</c> 之前显式收尾，而不是依赖 <c>OnDisable</c>。
    /// </para>
    /// </summary>
    public void PrepareForRemoval()
    {
        enabled = false;

        // 停火：AttackDriver 在**武器实例**上（塔身上取不到）。走 SetActiveSlot(false) 而不是只关 driver ——
        // 它同时停掉检测圈与召唤物；否则拆塔后到帧末前还能再打一下、检测圈也还在索敌。
        BaseWeapon weapon = _weapons != null ? _weapons.ActiveWeapon : null;
        if (weapon != null) weapon.SetActiveSlot(false);

        EnemyTargetRegistry.Service?.Unregister(transform);
    }

    /// <summary>
    /// 退订。注意 <see cref="EntityBehaviour"/> **没有** OnDestroy 可重写，
    /// 所以这里是普通私有方法而不是 override（写 override 会 CS0115）。
    /// </summary>
    private void OnDestroy()
    {
        if (_weapons != null) _weapons.ActiveWeaponChanged -= OnActiveWeaponChanged;

        SubscribeWeapon(null);
    }

    /// <summary>
    /// 攻击表现：动画 + 音效。
    /// 由 <see cref="AttackDriver.OnPerformed"/> 驱动，而后者只在攻击**真的打出去**时才触发，
    /// 所以范围内没目标时不会对着空气播攻击动画（与旧 Rin/Luo「先判空再播」一致）。
    /// </summary>
    private void PlayAttackFeedback()
    {
        if (_attackAnimator != null && !string.IsNullOrEmpty(_attackTrigger))
            _attackAnimator.SetTrigger(_attackTrigger);

        if (_playSfx)
            AudioService.Service?.PlaySfx(_attackSfx);
    }

    #region 选中与高亮

    /// <summary>
    /// 玩家选中该塔（由 TowerInteractable 调用）
    /// </summary>
    public void OnSelected()
    {
        SetHighlight(true);
        if (_rangeVisualizer != null)
        {
            // 半径由渲染器向"当前绑定的武器"要（见 TowerRangeVisualizer.Bind）
            _rangeVisualizer.Show();
        }
    }

    /// <summary>
    /// 玩家取消选中该塔（由 TowerInteractable 调用）
    /// </summary>
    public void OnDeselected()
    {
        SetHighlight(false);
        _rangeVisualizer?.FadeOut();
    }

    /// <summary>
    /// 设置高亮状态：true 切换为高亮材质，false 恢复原始材质。
    /// 两侧都用 sharedMaterial —— 高亮材质是全局共享资源，赋值 sharedMaterial 不会克隆实例。
    /// </summary>
    private void SetHighlight(bool active)
    {
        if (_spriteRenderers == null || _spriteRenderers.Length == 0) return;
        if (active && _highlightMaterial == null) return;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] == null) continue;
            _spriteRenderers[i].sharedMaterial = active ? _highlightMaterial : _originalMaterials[i];
        }
    }

    #endregion
}
