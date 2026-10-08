using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器基类 —— 武器的**身份**：所属玩家、武器槽注册、EntitySO。
///
/// <para>
/// <b>攻击逻辑不在这里。</b>「怎么打」在 <see cref="AttackMethodSO"/>，
/// 「什么时候打 + 打谁 + 拿什么打」在 <see cref="AttackDriver"/>。
/// 本类只保留所有武器共用的身份部分；各武器**特有**的行为留在子类
/// （枪的输入瞄准、火球的自转与尺寸同步）。
/// </para>
///
/// <para>
/// 武器仍是 <see cref="EntityBehaviour"/>：数值由 <c>WeaponDataSO</c> 灌进**武器自己的**
/// StatModel，武器子类从这一份读（<c>SpinWeaponSize</c> 这类数值只存在于武器模型里）。
/// 升级池会把武器升级与玩家升级混成一份选项，所以 <see cref="UpgradeOption"/> 必须带上目标实体 ——
/// 面板若统一 <c>ApplyTo(玩家)</c>，武器升级就会「抽得到、买得起、毫无效果」。
/// </para>
/// </summary>
public class BaseWeapon : EntityBehaviour, IUpgradeStateHolder, IStatRequirementsProvider
{
    private IWeaponHost _host;
    private AttackDriver _driver;

    /// <summary>
    /// 同物体上的攻击执行器（没有则为 null）。**惰性取一次并缓存**：
    /// 载体（本类与子类）与 UI 都要读它的静态信息（发射点、攻击方式），
    /// 每处各 <c>GetComponent</c> 一遍既浪费又会出现"有的地方缓存了、有的地方没有"。
    /// </summary>
    public AttackDriver Driver
    {
        get
        {
            // 用 Unity 的 == 重载判空：组件被销毁时是"伪 null"，??= 识别不出来
            if (_driver == null) _driver = GetComponent<AttackDriver>();
            return _driver;
        }
    }

    /// <summary>
    /// 本武器上每个升级项**已经应用过几次**。
    ///
    /// <para>
    /// 按**武器实例**持有，不是按 SO —— 这正是「每把武器有自己的升级」的落点：
    /// 两个玩家各带一把 gun、或同一玩家两个槽都是 gun，各自 Instantiate 出独立实例，
    /// 升级互不影响（数值本身也在各自的 StatModel 上，这里只记"还能不能再买"）。
    /// </para>
    ///
    /// <para>
    /// 状态随实例销毁而消失（换下武器 / 切场景 / 重 spawn）。这是设计选择：
    /// 局内成长不跨局，见 <c>Docs/CartPlan.md</c> 的局内/局外分工。
    /// </para>
    /// </summary>
    private readonly Dictionary<LevelUpSO, int> _upgradeLevels = new();

    /// <summary>
    /// 武器 prefab 里刻意**不接** <c>entityConfig</c>：配置由装配方
    /// （<see cref="WeaponAssembler"/>）注入，这样同一份 prefab 才能配不同的数值变体 SO。
    /// 因此 Awake 时的"没有配置"是预期状态，不报错。
    /// </summary>
    protected override bool ConfigInjectedAtRuntime => true;

    /// <inheritdoc />
    public int GetUpgradeLevel(LevelUpSO option)
    {
        if (option == null) return 0;

        return _upgradeLevels.TryGetValue(option, out int level) ? level : 0;
    }

    /// <inheritdoc />
    public bool TryRecordUpgrade(LevelUpSO option)
    {
        if (option == null) return false;

        int level = GetUpgradeLevel(option);

        // 上限在这里再查一遍：调用方绕过 IsAvailable 直接 ApplyTo 时，这是最后一道闸
        if (option.MaxLevel > 0 && level >= option.MaxLevel) return false;

        _upgradeLevels[option] = level + 1;
        return true;
    }

    /// <summary>
    /// 武器宿主（玩家 / 塔 / 未来的召唤物）。由装配方在 <c>Instantiate</c> 后注入。
    ///
    /// <para>
    /// 以前这里是 <c>GetComponentInParent&lt;PlayerController&gt;()</c> ——
    /// 那让武器**只能属于玩家**，塔要挂武器就得复制一套装配逻辑。宿主只提供
    /// "挂点 + 实体身份"，武器不需要知道宿主是什么类型。
    /// </para>
    /// </summary>
    public IWeaponHost Host => _host;

    /// <summary>注入宿主。装配方（<c>PlayerWeaponController</c> / 塔的武器控制器）负责调用。</summary>
    public void SetHost(IWeaponHost host)
    {
        _host = host;
    }

    /// <summary>
    /// 把"数值来源""归属"与"攻击方式"一起告诉本武器上的 <see cref="AttackDriver"/>。
    ///
    /// <para>
    /// 数值来源是**武器自己**（它有独立的 <c>WeaponDataSO</c> 与 StatModel），
    /// 归属是宿主 —— 见 <c>AttackContext.Self/Host</c> 的分工。
    /// </para>
    ///
    /// <para>
    /// <b>攻击方式来自配置 SO</b>（<see cref="WeaponEntitySO.attack"/>）：武器 prefab 上不再预接
    /// <c>_attack</c>，否则"这把武器怎么打"会有两处描述，而从 <c>WeaponEntitySO</c> 看不出行为。
    /// 装配方的调用顺序保证 <c>EntityConfig</c> 此时已就位（<c>SetEntityConfig</c> 先于本方法）。
    /// </para>
    /// </summary>
    public void BindAttackDriver()
    {
        AttackDriver driver = Driver;
        if (driver == null) return;

        driver.SetSelf(this);
        driver.SetHost(_host?.Entity);

        if (EntityConfig is WeaponEntitySO config && config.attack != null)
            driver.SetAttack(config.attack);
    }

    /// <summary>
    /// 本武器当前是否是激活槽。
    ///
    /// <para>
    /// <b>子类必须在逐帧入口用它快速返回。</b>停用只关 <see cref="AttackDriver"/> 是不够的：
    /// 武器自身的输入/瞄准/自转仍在跑 —— 备用枪会继续每帧读输入并写朝向（与激活武器抢输入），
    /// 备用火球会继续写 Transform（脏化层级）。武器一多就是纯浪费，而且"没拿在手上的武器
    /// 在转"很难归因。
    /// </para>
    ///
    /// <para>默认 true：<c>Instantiate</c> 出来的实例本来就是激活的，与 prefab 的初始状态一致。</para>
    /// </summary>
    protected bool IsActiveSlot { get; private set; } = true;

    /// <summary>
    /// 激活 / 停用这把武器（由宿主的武器控制器在切换槽位时调用）。
    ///
    /// <para>
    /// <b>停用必须停掉攻击、召唤物、检测体与自身的逐帧工作，而不是只隐藏 Sprite：</b>
    /// 未激活的武器若还在跑 <see cref="AttackDriver"/>，玩家会看到"没拿在手上的武器还在开火"，
    /// 而且伤害照常结算 —— 这类问题很难归因，因为画面上的武器确实消失了。
    /// </para>
    ///
    /// <para>
    /// <b>检测体也要一起切</b>（塔武器的索敌圈）：<c>AttackDriver</c> 被禁用期间收不到
    /// <c>OnTriggerEnter2D</c>，而重新启用一个"已经和敌人重叠"的碰撞体时 Unity 会补发 enter ——
    /// 于是切回这把武器时目标列表立刻重建。不切的话，换武器后"已经在圈里的敌人"不会补发事件，
    /// 表现为新武器有一段时间**站着不打**。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>检测体必须挂在本组件所在的物体上</b>：下面按 <c>GetComponents</c>（**仅同物体**）取，
    /// 挂在子物体上的碰撞体切不到。塔武器的索敌圈因此在 prefab 根节点
    /// （<c>TowerWeaponRangeSync</c> 若发现它在子物体上会告警）。
    /// </para>
    ///
    /// <para>
    /// <b>表现也要一起切</b>：武器外观是子物体上的 <c>SpriteRenderer</c>，不切的话
    /// 两把已装备的武器会**同时画在手上**（收起来的那把只是不再开火）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>只切 <see cref="SpriteRenderer"/></b>（外观），不碰 <c>LineRenderer</c> /
    /// <c>TrailRenderer</c> / 粒子这类**由表现组件自己管状态**的渲染器：
    /// 例如 <c>BeamVisual</c> 的光束线平时靠自己的计时器关着，
    /// 这里一刀切地打开会让它一直亮着（它的 <c>Update</c> 在没有攻击时不会去关）。
    /// 只切 <c>enabled</c>，不碰材质实例（见 CLAUDE.md 性能红线）。
    /// </para>
    /// </summary>
    public virtual void SetActiveSlot(bool active)
    {
        IsActiveSlot = active;

        AttackDriver driver = Driver;
        if (driver != null) driver.enabled = active;

        // 缓存一次：本方法在装配/切换时调用（不是逐帧），但没必要每次都分配数组。
        // 玩家武器没有碰撞体，这里是空操作。
        _slotColliders ??= GetComponents<Collider2D>();
        for (int i = 0; i < _slotColliders.Length; i++)
        {
            if (_slotColliders[i] != null) _slotColliders[i].enabled = active;
        }

        // 同理缓存。**只覆盖装配时就存在的子物体**：运行时才生成的召唤物
        // （如环绕火球）由子类的 OnActiveSlotChanged 用 SetActive 处理 —— 两者不要互相替代。
        _slotRenderers ??= GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < _slotRenderers.Length; i++)
        {
            if (_slotRenderers[i] != null) _slotRenderers[i].enabled = active;
        }

        OnActiveSlotChanged(active);
    }

    /// <summary>本物体上的碰撞体（检测体），首次切换时缓存。</summary>
    private Collider2D[] _slotColliders;

    /// <summary>本物体及子物体上的外观渲染器（<c>SpriteRenderer</c>），首次切换时缓存。</summary>
    private SpriteRenderer[] _slotRenderers;

    /// <summary>
    /// 本武器**自己**（而不是它的攻击方式）会读取的数值，供 <see cref="AttackDriver"/>
    /// 的启动体检校验。默认空数组。
    ///
    /// <para>
    /// <b>为什么需要它：</b><see cref="AttackMethodSO.RequiredStats"/> 只覆盖攻击方式
    /// <c>Execute</c>/<c>GetInterval</c> 读的数值，而载体逐帧读的那些（例如
    /// <see cref="SpinWeapon"/> 的转速）不在它的视野里 —— 漏配只会退化成
    /// <c>EntityBehaviour.GetStat</c> 的 <c>1f</c> 兜底 + 一条容易被淹没的告警。
    /// </para>
    ///
    /// <para>
    /// <b>子类覆写时必须列出全部自读数值</b>；挂在同一物体上的**独立组件**（如
    /// <c>TowerWeaponRangeSync</c>）自己实现 <see cref="IStatRequirementsProvider"/>，
    /// 由 <c>AttackDriver</c> 一并校验。读它的组件负责声明它 —— 不要把所有数值都堆到本属性上。
    /// </para>
    /// </summary>
    public virtual StatType[] RequiredStats => System.Array.Empty<StatType>();

    /// <summary>
    /// 子类处理自己的召唤物（如环绕物）。它们挂在发射点下、是独立实例，
    /// 不会随本组件的 <c>enabled</c> 一起停下。
    /// </summary>
    protected virtual void OnActiveSlotChanged(bool active)
    {
    }
}
