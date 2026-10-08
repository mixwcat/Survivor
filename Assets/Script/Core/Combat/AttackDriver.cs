using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 攻击执行器 —— 攻击系统里**唯一**持有实例状态的组件。
///
/// <para>职责：</para>
/// <list type="bullet">
/// <item>按 <see cref="AttackMethodSO.GetInterval"/> 累计计时，到点调用 <c>Execute</c>；</item>
/// <item>用自身的 trigger 回调维护范围内目标列表（按 <see cref="AttackMethodSO.TargetTag"/> 过滤）；</item>
/// <item>加载并归还攻击 prefab 的 Addressables 句柄；</item>
/// <item>把「瞄准方向」暴露给外部写入（玩家输入），塔不需要写；</item>
/// <item>攻击完成后发 <see cref="OnPerformed"/>，由实体自己播动画/音效；</item>
/// <item>启动时校验「攻击方式与载体读取的数值」是否都被本实体的 DataSO 覆盖。</item>
/// </list>
///
/// <para>
/// <b>一个实体一个攻击方式。</b>「同一实体同时拥有多种攻击」曾经由多槽支持（<c>AttackSlot</c> 列表），
/// 那条路径已随"塔武器化"删除 —— 现在 Teto 的子弹与炮弹是**两把独立的武器实例**，
/// 各自有自己的 prefab / data / 升级项。不要再把槽加回来：多槽会让"这次开火用的是哪一套数值"
/// 重新变成需要推断的事，而武器化之后每把武器的数值本来就是独立的。
/// </para>
///
/// <para>
/// 本组件不含任何「怎么打」的逻辑 —— 那在 <see cref="AttackMethodSO"/> 里。
/// 挂在实体根节点上（与 <see cref="EntityBehaviour"/> 同一物体）；塔和武器共用这一个组件。
/// </para>
///
/// <para>
/// <b>trigger 回调为什么能收到：</b>本组件所在物体上带着检测圈 —— 塔武器 prefab 根节点上的
/// trigger <c>CircleCollider2D</c>（layer <c>TowerDetector</c>）。武器实例挂在宿主（塔根）下，
/// 该碰撞体被宿主的 Kinematic <c>Rigidbody2D</c> 收编成同一个刚体；敌人是 Dynamic 刚体，
/// 于是触发事件成立，消息派发到**碰撞体所在的 GameObject**，也就是本组件这一层。
/// </para>
///
/// <para>
/// ⚠️ <b>武器实体默认没有检测圈</b>（玩家武器就是如此，它们的 <c>_targetsInRange</c> 恒为空）。
/// 枪/火球不需要目标列表（投射物只要方向、环绕物只要发射点），所以没问题；
/// 但给**没有检测圈**的武器挂上需要目标列表的攻击方式（<see cref="AreaDamageAttackSO"/> /
/// <see cref="HealAlliesAttackSO"/>）会**完全不工作且不报错**。
/// </para>
/// </summary>
public class AttackDriver : MonoBehaviour, IAttackSpawner
{
    [Header("攻击方式")]
    [Tooltip("为空则该实体不攻击。武器上留空是正常的 —— 唯一来源是 WeaponEntitySO.attack，装配时注入")]
    [SerializeField] private AttackMethodSO _attack;

    [Header("攻击资源")]
    [Tooltip("投射物/召唤物 prefab。句柄由本组件获取并归还；不需要资源的攻击方式可留空。\n" +
             "需要资源的攻击方式（投射物/环绕物）漏配会在启动时报红错，见 AttackMethodSO.RequiresPrefab")]
    [SerializeField] private AssetReferenceGameObject _prefab;

    [Header("发射点")]
    [Tooltip("为空时用实体自身 Transform")]
    [SerializeField] private Transform _muzzle;

    [Header("权威归属")]
    [Tooltip("谁驱动计时。玩家武器选 Local（手感优先），塔选 Server（伤害结算权威）。" +
             "单机两者行为相同；联机时由网络层读本字段来设置 HasAuthority（唯一读取时机见 AttackAuthority）")]
    [SerializeField] private AttackAuthority _authority = AttackAuthority.Local;

    /// <summary>
    /// 本 driver 是否具备执行权威。**单机恒为 true**；
    /// 联机时由网络层在 spawn 之后设置（服务端权威的实体在客户端应设为 false，
    /// 否则两端各打一份伤害、敌人血量会分叉）。
    /// </summary>
    public bool HasAuthority { get; set; } = true;

    /// <summary>装配方声明的权威意图（见 <see cref="AttackAuthority"/>）。</summary>
    public AttackAuthority Authority => _authority;

    /// <summary>范围内目标。enter/exit 回调维护，每 0.5s 清一次已销毁项。</summary>
    private readonly List<BaseHealthController> _targetsInRange = new List<BaseHealthController>();

    /// <summary>
    /// 攻击期间交给 <see cref="AttackMethodSO.Execute"/> 遍历用的快照（复用，不分配）。
    /// 与 <see cref="_targetsInRange"/> 分开是必须的 —— 见 <see cref="BuildContext"/> 的说明。
    /// </summary>
    private readonly List<BaseHealthController> _targetsSnapshot = new List<BaseHealthController>();

    // ── 运行时状态（不序列化；与 SO 的无状态红线互为另一面）──

    /// <summary>距下次攻击的剩余时间（秒）。</summary>
    private float _timer;

    /// <summary>配置是否有效（启动体检的结论）。false = 本实体不攻击。</summary>
    private bool _configValid;

    /// <summary>攻击 prefab 的句柄，由本组件获取并归还。</summary>
    private AsyncOperationHandle<GameObject> _prefabHandle;

    /// <summary>加载好的攻击 prefab；未配置 / 未加载完成时为 null。</summary>
    private GameObject _loadedPrefab;

    private EntityBehaviour _self;

    /// <summary>
    /// 攻击归属者（武器实例的宿主）。未注入时等于 <see cref="_self"/>。
    /// 见 <see cref="SetHost"/>。
    /// </summary>
    private EntityBehaviour _host;

    /// <summary>
    /// 本驱动所属实体的攻击数值来源（武器实例 / 塔）。见 <see cref="SetSelf"/>。
    /// </summary>
    public EntityBehaviour Self => _self;

    /// <summary>
    /// **显式**指定攻击数值的来源实体。装配方在 <c>Instantiate</c> 之后、<c>Start</c> 之前调用。
    ///
    /// <para>
    /// 不注入时会退回到"同物体 → 父级"的层级推断，并打一条只发一次的告警 ——
    /// 那个回退让"驱动挂错一层就静默换一整套数值"，是最难归因的一类问题。
    /// 迁移完成后（武器与塔都显式注入）这条告警不应该再出现。
    /// </para>
    /// </summary>
    public void SetSelf(EntityBehaviour self)
    {
        if (self != null) _self = self;
    }

    /// <summary>
    /// 指定攻击的归属者（击杀统计与塔的投入账本按它结算）。
    /// 不指定时归属落在 <see cref="Self"/> 上（塔自己就是攻击者的情况）。
    /// </summary>
    public void SetHost(EntityBehaviour host)
    {
        _host = host;
    }

    /// <summary>
    /// **注入攻击方式** —— 装配方在 <c>Instantiate</c> 之后、<c>Start</c> 之前调用
    /// （与 <see cref="SetSelf"/> 同一时机）。
    ///
    /// <para>
    /// <b>为什么需要注入点：</b>「这把武器怎么打」属于武器配置，
    /// 唯一来源是 <c>WeaponEntitySO.attack</c>。序列化在 prefab 上会让同一件事有两处描述，
    /// 而且从 <c>WeaponEntitySO</c> 根本看不出这把武器是投射物还是光束 ——
    /// 改行为必须打开 prefab，做"同一份 prefab 的不同行为变体"也只能复制 prefab。
    /// </para>
    ///
    /// <para>
    /// prefab 上若预接了**别的**攻击方式，以注入的为准并打一条告警 ——
    /// 否则"改了 prefab 的 _attack 却没生效"会变成查不出来的静默问题。
    /// </para>
    /// </summary>
    /// <returns>是否注入成功。Start 之后再调用会被拒绝（运行时不支持切换攻击方式）。</returns>
    public bool SetAttack(AttackMethodSO method)
    {
        if (method == null) return false;

        if (_started)
        {
            Debug.LogWarning($"[{nameof(AttackDriver)}] {gameObject.name} 已经开始攻击，" +
                             $"忽略运行时注入的「{method.name}」。攻击方式必须在 Start 之前注入。", this);
            return false;
        }

        if (_attack != null && !ReferenceEquals(_attack, method))
        {
            Debug.LogWarning($"[{nameof(AttackDriver)}] {gameObject.name} 的 prefab 预接了攻击方式" +
                             $"「{_attack.name}」，已被配置 SO 注入的「{method.name}」覆盖" +
                             "（WeaponEntitySO.attack 是唯一来源，prefab 上应留空）。", this);
        }

        _attack = method;

        return true;
    }

    /// <summary>Start 是否已经跑过（注入必须在它之前，见 <see cref="SetAttack"/>）。</summary>
    private bool _started;

    private IAssetService _assetService;
    private float _cleanupTimer = 0.5f;

    /// <summary>与配置无关的一次性告警位（逐条去重；配置类的告警在 Start 里各发一次即可）。</summary>
    private int _warned;

    private const int WarnNoEntity = 1 << 0;
    private const int WarnNoAssetService = 1 << 1;
    private const int WarnImplicitSelf = 1 << 2;

    /// <summary>配置的发射点（未配置时为 null）。供同物体的武器读取，例如 <c>SpinWeapon</c>
    /// 要遍历这个 Transform 下的环绕物做尺寸同步。
    /// <b>刻意不暴露「带 <c>transform</c> 回退的 Origin」</b>：那会让「忘了配发射点」
    /// 变成一个看不出来的默认行为。</summary>
    public Transform Muzzle => _muzzle;

    /// <summary>
    /// 攻击方式（可能为 null）。供载体与 UI 读取**静态信息**（例如「这种攻击要不要瞄准输入」），
    /// 不要让 UI 去 <c>GetWeapon&lt;GunWeapon&gt;()</c> 认具体武器类型。
    /// </summary>
    public AttackMethodSO Attack => _attack;

    /// <summary>
    /// 攻击 prefab 的引用（可能为空）。供**编辑器体检**读取：
    /// 「攻击方式需要资源、但 prefab 上没接」是一条静默故障（武器永不开火），
    /// 运行时由 <see cref="SetupAsync"/> 报红错，提交前由 <c>EntitySOValidator</c> 拦一道。
    /// </summary>
    public AssetReferenceGameObject ProjectilePrefab => _prefab;

    /// <summary>
    /// 外部写入的瞄准方向（玩家输入）。为零向量时自动改为指向当前目标。
    /// 塔没有输入，保持默认即可。
    /// </summary>
    public Vector2 AimDirection { get; set; }

    /// <summary>
    /// 完成一次攻击后触发。动画与音效属于**表现**，由实体订阅后自己播：
    /// 放进 <see cref="AttackMethodSO"/> 会让常驻资产持有 animator 参数名与音效枚举，
    /// 放进本组件又会让「攻击执行器」承担表现职责。
    /// 委托只能挂在 MonoBehaviour 上 —— SO 跨 Play 会话存活，持有委托会变成伪 null 闭包。
    /// </summary>
    public event System.Action OnPerformed;

    private void Awake()
    {
        // 显式注入（SetSelf）优先；未注入时才退回层级推断，并告警一次 ——
        // 见 SetSelf 的说明：回退路径让"挂错一层就换一整套数值"变得无声无息
        if (_self == null)
        {
            _self = GetComponent<EntityBehaviour>();
            if (_self == null) _self = GetComponentInParent<EntityBehaviour>();

            if (_self != null && GetComponent<BaseWeapon>() == null)
            {
                // 武器上的驱动走"装配方稍后注入"（Instantiate → Awake → SetSelf），
                // 那条路径是正常的，不该告警；只有**非武器**的驱动才说明装配方漏了注入
                WarnOnce(WarnImplicitSelf,
                         $"没有显式注入数值来源（SetSelf），已按层级推断为「{_self.name}」。" +
                         "装配方应在 Instantiate 后调用 SetSelf，避免层级变化时静默换一套数值。");
            }
        }
    }

    private async void Start()
    {
        _started = true;

        if (_self == null)
        {
            WarnOnce(WarnNoEntity, "同物体及父级都找不到 EntityBehaviour，无法读取攻击数值。");
            return;
        }

        _assetService = AssetService.Service;

        // 数值模型没建起来 = **装配方没有注入 EntitySO / DataSO**（武器 prefab 刻意留空 entityConfig，
        // 由 WeaponAssembler 注入）。这是装配失败而不是"数值配得小"：不报的话攻击会拿 GetStat 的
        // 1f 兜底值静默跑（伤害 1、间隔 1 秒），只能靠手感发现。启动期报一次，不进逐帧路径。
        if (_self.StatModel == null)
        {
            Debug.LogError($"[{nameof(AttackDriver)}] {gameObject.name} 的数值模型没有建立" +
                           "（装配方未注入 EntitySO，或 EntitySO 的 dataRef 为空），" +
                           "本次攻击的数值会全部退化成 1。请检查装配路径是否调用了 SetEntityConfig。", this);
        }

        await SetupAsync();
    }

    /// <summary>
    /// 启动流程：配置体检 → 记录间隔是否有效 → 加载资源。
    /// 都在 <see cref="Start"/> 同步段或首个 await 之前完成，所以不需要逐帧节流。
    /// </summary>
    private async Task SetupAsync()
    {
        if (_attack == null)
        {
            WarnConfig("未配置 AttackMethodSO，该实体不会攻击。");
            return;
        }

        ValidateRequiredStats();

        // 间隔由「速率」换算而来：速率 <= 0 会得到 +∞（永不攻击）。判定为配置错误并停手。
        // 只查基础值：升级把速率叠加到 <= 0 走 Tick 里的下限保护。
        float baseInterval = _attack.GetInterval(_self);
        _configValid = baseInterval > 0f && !float.IsPositiveInfinity(baseInterval);
        if (!_configValid)
        {
            WarnConfig($"攻击速率为 0 或未配置（{_attack.name}），该实体已停止攻击。" +
                       "请检查对应 DataSO 的数值。");
        }

        bool prefabConfigured = _prefab != null && _prefab.RuntimeKeyIsValid();

        if (!prefabConfigured)
        {
            // 「需要资源却没配」是静默故障：Execute 会因为 ctx.Prefab 为 null 而空转，
            // 表现是"武器永不开火"且一条日志都没有（详见 AttackMethodSO.RequiresPrefab）。
            // 不需要资源的攻击方式（范围伤害/治疗/光束）留空是正常配置，不报。
            if (_attack.RequiresPrefab)
            {
                Debug.LogError($"[{nameof(AttackDriver)}] {gameObject.name} 的攻击方式「{_attack.name}」" +
                               "需要投射物/召唤物 prefab，但本武器没有配置（AttackDriver 的「攻击资源」为空" +
                               "或不是 Addressable）。该武器不会开火。", this);
            }

            return;
        }

        if (_assetService == null)
        {
            WarnOnce(WarnNoAssetService, "IAssetService 未注册，攻击 prefab 无法加载。");
            return;
        }

        _prefabHandle = _assetService.LoadAssetAsync<GameObject>(_prefab);
        _loadedPrefab = await _prefabHandle.Task;

        // await 期间宿主可能已被销毁（切场景 / 塔被拆）：此时 OnDestroy 已经归还过句柄，
        // 续体首行必须判宿主，否则会往已销毁的对象上写字段。
        if (this == null) return;

        // 加载失败 → ctx.Prefab 为 null → 攻击方式直接 return false：表现是"武器不开火、不报错"。
        // 这里把它变成启动期的一条红错，并给出最可能的原因（多为资产不在 Addressables 组里）。
        if (_loadedPrefab == null)
        {
            Debug.LogError($"[{nameof(AttackDriver)}] {gameObject.name}：攻击 prefab 加载失败" +
                           $"（key={_prefab.RuntimeKey}）。{WeaponAssembler.DescribeLoadFailure(_prefabHandle)}",
                           this);
        }
    }

    private void OnDestroy()
    {
        if (_assetService == null || !_prefabHandle.IsValid()) return;

        // 释放句柄前先处理**池里还留着的实例**：
        // ProjectilePool 是静态的、比句柄活得久，最后一个持有者释放后 bundle 可能被卸载，
        // 池中实例的 Sprite / 材质会变成空壳，下次建同种塔复用它们时"子弹看不见"。
        // 顺序必须是"先销毁实例，再释放句柄"。
        if (_loadedPrefab != null && ProjectilePool.RetainsInstancesOf(_loadedPrefab))
            ProjectilePool.DropPool(_loadedPrefab);

        _assetService.Release(_prefabHandle);
    }

    private void Update()
    {
        if (_self == null) return;

        // 权威检查：服务端权威的实体在客户端不 tick，避免两端各结算一份伤害。
        // 单机下恒为 true；玩家武器默认 Local，不受影响。
        if (!HasAuthority) return;

        _cleanupTimer -= Time.deltaTime;
        if (_cleanupTimer <= 0f)
        {
            _cleanupTimer = 0.5f;
            RemoveDestroyedTargets();
        }

        Tick();
    }

    private void Tick()
    {
        if (_attack == null || !_configValid) return;

        float interval = _attack.GetInterval(_self);

        // 下限保护：升级把速率叠加到 0/负数时取 MinInterval，而不是停火。
        // 逐帧路径上不做任何日志（漏配数值的告警已在 Start 发过）。
        if (interval < AttackMethodSO.MinInterval)
            interval = AttackMethodSO.MinInterval;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        // 刻意用「= interval」而不是「+= interval」：前者每发最多丢掉不到一帧的余量
        // （低帧率下实际射速略低于配置值），后者会在长时间卡顿 / 后台恢复后攒出一串连发 ——
        // 那是玩家能看见的伤害尖峰，比射速差几个百分点严重得多。
        _timer = interval;

        // 只有真的打出去了才发表现事件：范围内没目标时不该对着空气播攻击动画
        if (_attack.Execute(BuildContext()))
            OnPerformed?.Invoke();
    }

    private AttackContext BuildContext()
    {
        Transform origin = _muzzle != null ? _muzzle : transform;

        BaseHealthController target = FindTarget();

        // 没有外部瞄准方向时退回「指向目标」——塔走的就是这条；
        // 玩家武器会自己写 AimDirection，因此不受影响。
        Vector2 direction = AimDirection;
        if (direction.sqrMagnitude < 0.0001f && target != null)
        {
            Vector2 delta = (Vector2)target.transform.position - (Vector2)origin.position;
            if (delta.sqrMagnitude > 0.0001f) direction = delta.normalized;
        }

        // 拍一份快照交给攻击方式遍历：范围攻击打死目标时，敌人回池会让碰撞体失效，
        // 塔上随即收到 OnTriggerExit2D 修改 _targetsInRange —— 直接按索引遍历会越界
        // （BaseTower.ForEachValidTarget 当年就是踩了这个坑才改成快照的）。
        // 快照列表复用，AddRange 到已扩容的 List 不产生分配。
        _targetsSnapshot.Clear();
        _targetsSnapshot.AddRange(_targetsInRange);

        return new AttackContext(_self, _host, origin, direction, target, _targetsSnapshot,
                                 _loadedPrefab, this);
    }

    // ── IAttackSpawner ──

    /// <summary>
    /// 生成攻击实体。有父级 = 跟随载体的召唤物（环绕物：随载体销毁，不池化）；
    /// 无父级 = 独立飞行的投射物（走 <see cref="ProjectilePool"/>）。
    ///
    /// <para>
    /// 联机时这一处会被替换成网络实现（<c>NetworkServer.Spawn</c>），
    /// 攻击方式（SO）与它们的资产完全不受影响 —— 这正是 <see cref="IAttackSpawner"/> 存在的意义。
    /// </para>
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        if (parent != null)
            return Instantiate(prefab, position, rotation, parent);

        BulletController bullet = ProjectilePool.Spawn(prefab, position, rotation);
        return bullet != null ? bullet.gameObject : null;
    }

    /// <summary>回收：投射物归还对象池，其余（召唤物等）直接销毁。</summary>
    public void Despawn(GameObject instance)
    {
        if (instance == null) return;

        if (instance.TryGetComponent<BulletController>(out var bullet))
        {
            ProjectilePool.Return(bullet);
            return;
        }

        Destroy(instance);
    }

    /// <summary>
    /// 范围内最先进入的存活目标。取法与旧 <c>BaseTower.FindTarget</c> 一致，
    /// 但改成循环而不是递归（旧实现遇到连续多个空引用会递归下去）。
    /// </summary>
    private BaseHealthController FindTarget()
    {
        for (int i = 0; i < _targetsInRange.Count; i++)
        {
            if (_targetsInRange[i] != null) return _targetsInRange[i];
        }
        return null;
    }

    private void RemoveDestroyedTargets()
    {
        for (int i = _targetsInRange.Count - 1; i >= 0; i--)
        {
            if (_targetsInRange[i] == null) _targetsInRange.RemoveAt(i);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        BaseHealthController target = ResolveTarget(other);
        if (target == null) return;

        // 去重：同一实体若有多个碰撞体落在范围内会重复入列，后果是范围伤害对同一目标
        // 结算多次（EnemyHealthController 已经被这个问题咬过，见那里的去重注释）。
        if (_targetsInRange.Contains(target)) return;
        _targetsInRange.Add(target);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        BaseHealthController target = ResolveTarget(other);
        if (target != null) _targetsInRange.Remove(target);
    }

    /// <summary>
    /// 把触发碰撞体归一成目标实体。分两步，各管一件事：
    /// <list type="number">
    /// <item><b>"碰到了谁"</b>交给 <see cref="DamageTargetResolver"/>（判据：本体碰撞体上的
    /// <see cref="Hurtbox"/> 标记）—— 每个实体恰好入列一次，enter/exit 天然对称。
    /// 这正是旧实现靠"只认自己身上既带 tag 又挂组件"来保证的性质，现在改由数据保证；</item>
    /// <item><b>"该不该打"</b>由攻击方式给出的目标 tag 决定（tag = 身份/玩法规则）。</item>
    /// </list>
    ///
    /// <para>
    /// 攻击方式的目标 tag 就是本实体的目标类型 —— 一个实体只有一个攻击方式（见类注释）。
    /// </para>
    /// </summary>
    private BaseHealthController ResolveTarget(Collider2D other)
    {
        BaseHealthController target = DamageTargetResolver.Resolve(other);
        if (target == null) return null;

        // tag 由攻击方式给出：给塔换上治疗攻击方式后，这里会自动改成找 "Tower"，
        // 不会出现「治疗技能去治敌人」。
        string tag = _attack != null ? _attack.TargetTag : null;
        if (string.IsNullOrEmpty(tag) || !target.CompareTag(tag)) return null;

        return target;
    }

    /// <summary>
    /// 启动期体检：攻击方式与**载体**读取的每个数值都必须由本实体的 DataSO 提供。
    ///
    /// <para>
    /// 不做校验的后果是静默的：<c>EntityBehaviour.GetStat</c> 对缺失数值返回 <c>1f</c>
    /// 且只告警一次（很容易被当成「数值就是这么小」），表现是「伤害永远是 1」「间隔 1 秒」。
    /// 这里把它变成启动期的一条红错，并一次列出全部缺失项。
    /// </para>
    ///
    /// <para>
    /// <b>为什么要查两份清单：</b><see cref="AttackMethodSO.RequiredStats"/> 只覆盖
    /// <c>Execute</c>/<c>GetInterval</c> 读的数值；载体自己逐帧读的那些（如
    /// <c>SpinWeapon</c> 的转速、<c>TowerWeaponRangeSync</c> 的射程）不在攻击方式的视野里，
    /// 由读取方通过 <see cref="IStatRequirementsProvider"/> 声明，这里一并校验 ——
    /// 「谁读它，谁声明它」，校验点仍然只有这一处。
    /// </para>
    /// </summary>
    private void ValidateRequiredStats()
    {
        EntityStatModel model = _self.StatModel;
        // StatModel 缺失（装配方没注入 EntitySO）已由 Start 里的红错报过，这里不重复报
        if (model == null) return;

        System.Text.StringBuilder missing = null;

        AppendMissing(model, _attack.RequiredStats, ref missing);

        // 载体/读取方的声明：武器本体 + 挂在同一物体上的独立组件（如塔武器的索敌圈）。
        // 用接口而不是直连具体类型 —— 见 IStatRequirementsProvider 的类注释。
        IStatRequirementsProvider[] providers = GetComponents<IStatRequirementsProvider>();
        for (int i = 0; i < providers.Length; i++)
            AppendMissing(model, providers[i].RequiredStats, ref missing);

        if (missing == null) return;

        BaseEntitySO config = _self.EntityConfig;
        string dataName = config != null && config.dataRef != null ? config.dataRef.name : "未配置 DataSO";

        Debug.LogError($"[{nameof(AttackDriver)}] {gameObject.name}：攻击方式「{_attack.name}」" +
                       $"需要以下数值，但 {dataName} 没有提供：{missing}。这些数值会退化成 1，攻击表现会静默错误。");
    }

    /// <summary>把 <paramref name="required"/> 里模型未提供的数值追加进缺失清单（去重靠调用方保证清单本身无重复）。</summary>
    private static void AppendMissing(EntityStatModel model, StatType[] required, ref System.Text.StringBuilder missing)
    {
        if (required == null) return;

        for (int i = 0; i < required.Length; i++)
        {
            if (model.HasStat(required[i])) continue;

            missing ??= new System.Text.StringBuilder();
            if (missing.Length > 0) missing.Append('、');
            missing.Append(required[i]);
        }
    }

    /// <summary>配置相关告警（Start 里每项最多一条，不在逐帧路径上）。</summary>
    private void WarnConfig(string message)
    {
        Debug.LogWarning($"[{nameof(AttackDriver)}] {gameObject.name}：{message}");
    }

    /// <summary>
    /// 与配置无关的告警，同一条只发一次（按 <paramref name="flag"/> 去重）。
    /// 逐帧路径上的告警必须节流，否则漏配会退化成每帧一条 LogWarning + 字符串拼接。
    /// </summary>
    private void WarnOnce(int flag, string message)
    {
        if ((_warned & flag) != 0) return;
        _warned |= flag;

        Debug.LogWarning($"[{nameof(AttackDriver)}] {gameObject.name}：{message}");
    }
}
