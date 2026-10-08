using UnityEngine;

/// <summary>
/// 实体基类 MonoBehaviour
/// - 持有 EntityStatModel 运行时数值容器
/// - 直接序列化持有 <see cref="BaseEntitySO"/>（数值 + 升级池 + 表现信息）
/// - 提供 GetStat 快捷访问（带缺配置兜底）
/// </summary>
public class EntityBehaviour : MonoBehaviour
{
    [Header("实体配置")]
    [Tooltip("本实体的 EntitySO，直接拖引用（不再经 SOManager 按枚举反查）")]
    [SerializeField] protected BaseEntitySO entityConfig;

    /// <summary>
    /// 配置是否**由装配方在运行时注入**（prefab 里刻意留空 <c>entityConfig</c>）。
    ///
    /// <para>
    /// 为 true 时 <c>Awake</c> 不再报「未配置 EntitySO」—— 那不是漏配，而是预期状态：
    /// 武器 prefab 留空、由 <see cref="WeaponAssembler"/> 注入，同一份 prefab 才能配不同的
    /// <c>WeaponEntitySO</c> 数值变体（prefab 预接会让注入被拒、静默跑成旧数值）。
    /// </para>
    ///
    /// <para>
    /// <b>代价是"装配方忘了注入"不再有一条醒目的报错</b>，所以必须有替代信号：
    /// 忘了注入时 <c>StatModel</c> 为 null，<see cref="AttackDriver"/> 会在启动时报红错。
    /// 新增这类实体时请一并确认那条检查还在。
    /// </para>
    /// </summary>
    protected virtual bool ConfigInjectedAtRuntime => false;

    /// <summary>基础数值已成功灌入 StatModel（失败分支不置位，允许后续重试）。</summary>
    private bool _statModelReady;
    private bool _statInitWarningLogged;
    private bool _statWarningLogged;

    public EntityStatModel StatModel { get; private set; }

    /// <summary>本实体的 EntitySO（由 Inspector 直接引用，Awake 起即可用）。</summary>
    public BaseEntitySO EntityConfig => entityConfig;

    /// <summary>
    /// 运行时注入实体配置（装配方在实例化之后、任何 <c>Start</c> 之前调用）。
    ///
    /// <para>
    /// <b>为什么需要它：</b>「prefab 里预先接好 <c>entityConfig</c>」要求 prefab 与 SO
    /// **双向**手工接线（SO 指向 prefab、prefab 又指回 SO），漏一边就是运行时按默认值静默跑。
    /// 装配方本来就拿着 SO（<c>WeaponEntitySO</c> / <c>TowerEntitySO</c>），由它注入更可靠，
    /// 也让同一份 prefab 能被多个 SO 复用（例如同一把枪的不同数值变体）。
    /// </para>
    /// <para>
    /// StatModel 已经建立过之后**不再接受注入**：重建模型会让子类已经建立的订阅指向旧实例
    /// （见 <see cref="InitStatModel"/> 的说明）。此时只告警，不静默改值。
    /// </para>
    /// </summary>
    /// <returns>注入是否生效。</returns>
    public bool SetEntityConfig(BaseEntitySO config)
    {
        if (config == null || ReferenceEquals(entityConfig, config)) return false;

        if (_statModelReady)
        {
            Debug.LogWarning($"[{nameof(EntityBehaviour)}] {gameObject.name} 的 StatModel 已建立，" +
                             $"忽略运行时注入的 EntitySO「{config.name}」。" +
                             "请在实例化之后、Start 之前注入。");
            return false;
        }

        entityConfig = config;
        InitStatModel();
        return _statModelReady;
    }

    protected virtual void Awake()
    {
        // 直接引用后不再有「SOManager 尚未 Awake」的时序问题，数值可以立刻建好：
        // 子类在 base.Awake() 之后即可安全使用 StatModel，也修掉了
        // 「子类自行声明 Start 会隐藏基类 Start、导致 StatModel 一直没建」的老陷阱。
        InitStatModel();
    }

    /// <summary>
    /// 建立 StatModel 并灌入 DataSO 的基础数值。只在**成功建过一次之前**执行。
    /// <para>
    /// 它能兜住的是「<c>Awake</c> 没跑到」（工具脚本、运行时才 AddComponent 出来的实体），
    /// **不是**「运行时把 entityConfig 换成另一个 SO」——后者不会刷新基础值：
    /// 子类已经拿到的订阅指向旧模型实例，重建模型会让订阅静默失效、并丢掉运行时修饰符。
    /// 真要换配置，请重建实体。
    /// </para>
    /// </summary>
    private void InitStatModel()
    {
        if (_statModelReady) return;

        // 失败分支会被 GetStat 反复走到（逐帧热路径），告警必须只出一次，
        // 否则配置缺失会退化成每帧一次 LogWarning + 字符串拼接。
        if (entityConfig == null)
        {
            // 预期由装配方注入的实体（武器）不在这里报错，见 ConfigInjectedAtRuntime
            if (!ConfigInjectedAtRuntime)
                WarnStatInitFailed(gameObject.name + " 未配置 EntitySO，无法初始化 StatModel");

            return;
        }

        if (entityConfig.dataRef == null)
        {
            WarnStatInitFailed(gameObject.name + " 的 EntitySO 未配置 DataSO，无法初始化 StatModel");
            return;
        }

        StatModel = new EntityStatModel();
        entityConfig.dataRef.FillStatModel(StatModel);
        _statModelReady = true;

        // 一个数值都没填 = DataSO 忘了调用 SetBaseValue（例如新增子类没接 base.FillStatModel）。
        // 后果是 GetStat 全部走 1f 兜底，只在首次读取时提示一次，很容易被当成「数值就是这么小」。
        if (!StatModel.HasAnyStat())
        {
            Debug.LogWarning($"{entityConfig.dataRef.name} 没有提供任何基础数值，" +
                             $"{gameObject.name} 的所有数值都会退化成默认值 1。" +
                             "请检查该 DataSO 的 FillStatModel 是否漏调 base 或漏写 SetBaseValue。");
        }

        // 通知子类「现在可以安全订阅 StatModel 了」。
        // 订阅点必须由这里统一提供：子类可能自行声明 Start（会隐藏基类的 Start），
        // 所以放在子类 Awake/Start 的订阅都不可靠。
        OnStatModelInitialized();
    }

    /// <summary>
    /// 初始化失败告警（同一实体只出一次）。初始化失败会被 <see cref="GetStat"/> 反复重试，
    /// 这里不做节流就会变成逐帧日志。
    /// </summary>
    private void WarnStatInitFailed(string message)
    {
        if (_statInitWarningLogged) return;
        _statInitWarningLogged = true;

        Debug.LogWarning(message + "。同一实体只提示一次，请检查预制体上的 EntitySO 引用。");
    }

    /// <summary>
    /// StatModel 首次成功初始化后回调（每个实体至多一次）。
    /// 子类在此订阅 <see cref="EntityStatModel.OnStatChanged"/>。
    /// </summary>
    protected virtual void OnStatModelInitialized() { }

    /// <summary>
    /// 获取最终数值（快捷方法）。逐帧热路径：内部不含 Linq / 装箱 / 堆分配。
    /// </summary>
    public float GetStat(StatType type)
    {
        // 兜底：Awake 没跑到时（工具脚本 / 运行时才 AddComponent 出来的实体）仍能建起来。
        // 注意它不会因为「运行时换了 entityConfig」而重新灌值，见 InitStatModel 的说明。
        if (StatModel == null)
            InitStatModel();

        if (StatModel == null)
        {
            // 「由装配方注入配置」的实体（武器）在 Instantiate 之后、注入之前没有 StatModel，
            // 这是**预期状态**而不是配置错误 —— 与 Awake 里那条被 ConfigInjectedAtRuntime
            // 抑制的告警同一个道理。这里不报，是为了不让每次装配都在控制台留一条假告警；
            // 装配**失败**（始终没注入）另有红错：见 AttackDriver 启动时的检查。
            if (!ConfigInjectedAtRuntime) WarnStatUnavailable(type, "缺少 StatModel");

            return 1f;
        }
        else if (entityConfig == null || entityConfig.dataRef == null)
        {
            WarnStatUnavailable(type, "缺少 DataSO");
            return 1f;
        }
        else if (!StatModel.HasStat(type))
        {
            WarnStatUnavailable(type, "DataSO 未定义该数值");
            return 1f;
        }
        return StatModel.GetStat(type);
    }

    /// <summary>
    /// 数值不可用时告警。
    /// 本方法在逐帧路径上被调用（如敌人每帧读 MoveSpeed），配置缺失时必须**只提示一次**，
    /// 否则会变成每帧一次的 LogWarning + 字符串拼接，在真机上直接拖垮性能。
    /// </summary>
    private void WarnStatUnavailable(StatType type, string reason)
    {
        if (_statWarningLogged) return;
        _statWarningLogged = true;

        Debug.LogWarning($"{gameObject.name} 无法获取 {type}（{reason}），返回默认值 1。" +
                         "同一实体只提示一次，请检查 EntitySO / DataSO 配置。");
    }
}
