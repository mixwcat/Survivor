using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 一个武器槽：候选武器配置 + 已装配实例。
///
/// <para>
/// 由 <see cref="PlayerWeaponController"/> 按候选列表建立（数据驱动，不再靠扫描子物体）。
/// 未装备时 <see cref="Runtime"/> 为 null，<see cref="IsEquipped"/> 为 false。
/// </para>
///
/// <para>
/// <b>配置在槽的生命周期内不变</b>：装配出来的实例、它的数值与升级项全部由
/// <see cref="Config"/> 决定（"一个槽 = 一份 <see cref="WeaponEntitySO"/>"）。
/// 要换一把武器就换一个槽 —— 大厅的武器台改的是 <c>RunSession</c> 的装备列表，
/// 关卡入口再按 id 找到对应槽装配（见 <c>PlayerSpawner.EquipLoadoutAsync</c>）。
/// </para>
/// </summary>
public sealed class WeaponSlot
{
    /// <summary>候选武器配置（数据：prefab / 数值 / 升级池 / 展示信息）。构造时保证非 null。</summary>
    public WeaponEntitySO Config { get; }

    /// <summary>
    /// 已装配的武器实例（配置 + 实例 + prefab 句柄）；未装备时为 null。
    /// 句柄由 <see cref="PlayerWeaponController"/> 持有并在宿主销毁时归还。
    /// </summary>
    public WeaponInstance Runtime { get; private set; }

    /// <summary>已装配的武器组件；未装备时为 null。</summary>
    public BaseWeapon Instance => Runtime != null ? Runtime.Weapon : null;

    /// <summary>该槽是否已经装备。</summary>
    public bool IsEquipped => Instance != null;

    /// <summary>
    /// 本槽是否**正在装配中**（<see cref="PlayerWeaponController.EquipAsync"/> 的重入闸）。
    ///
    /// <para>
    /// 没有这道闸时，两个并发 <c>EquipAsync</c> 会各装配一个实例、后者覆盖
    /// <see cref="Runtime"/> —— 被覆盖的那个既不销毁、句柄也不归还（实例与句柄双泄漏）。
    /// 装配是异步的（要等 Addressables），所以"点两下"就足以触发。
    /// </para>
    /// </summary>
    public bool IsAssembling { get; internal set; }

    public WeaponSlot(WeaponEntitySO config)
    {
        Config = config;
    }

    /// <summary>记录/清空装配结果（只有控制器调用）。</summary>
    internal void SetRuntime(WeaponInstance runtime) => Runtime = runtime;
}

/// <summary>
/// 玩家武器控制器：按玩家持有**候选武器数据**与已装配武器实例，挂在 Player 上。
///
/// <para>
/// 武器实例由 <see cref="WeaponAssembler"/> 从 <see cref="WeaponEntitySO.prefab"/> 实例化到挂点下
/// （旧实现是「场景里预先摆好 inactive 节点、选中时 SetActive」——那样新增一把武器必须改场景，
/// 武器数量也被场景节点数钉死）。
/// </para>
///
/// <para>
/// <b>装配序列与塔共用</b>（<see cref="WeaponAssembler"/>）：本类只负责玩家侧独有的概念 ——
/// 候选 / 已装备 / 切换激活槽，不重复实现"加载 + 注入 + 归还"那一段。
/// </para>
///
/// <para>
/// 联机时每个玩家实例各自持有一份，互不影响；「这个玩家有哪些武器」因此是纯数据，
/// 可存档、可同步。
/// </para>
///
/// <para>
/// <b>两套编号是分开的：</b>候选列表（<see cref="WeaponSlots"/>，Inspector 上配的全部可选武器）
/// 与已装备列表（<see cref="EquippedSlots"/>，本局实际带出门的那几把）。
/// 输入槽位 <c>0/1</c>、HUD 高亮、定向升级目标一律走**已装备序号**，
/// 不要拿候选下标当槽位用 —— 两者在"候选比装备多"时必然错位。
/// </para>
/// </summary>
public class PlayerWeaponController : MonoBehaviour, IWeaponManager, IWeaponHost
{
    [Header("武器挂点")]
    [Tooltip("装配出来的武器实例挂在这里；为空时用自身 Transform")]
    [SerializeField] private Transform _mount;

    private EntityBehaviour _entity;

    /// <inheritdoc />
    public Transform WeaponMount => _mount != null ? _mount : transform;

    /// <summary>宿主实体（归属落点：击杀统计按它结算）。</summary>
    public EntityBehaviour Entity => _entity;

    [Header("候选武器")]
    [Tooltip("本玩家可选的武器。加一把武器 = 往这里加一个 WeaponEntitySO，不需要改场景")]
    [SerializeField] private List<WeaponEntitySO> _candidates = new();

    [Header("装备上限")]
    [Tooltip("最多同时装备几把；0 = 用角色的 maxWeaponSlots（角色也没配则保守取 1）")]
    [SerializeField] private int _maxEquippedOverride;

    private readonly List<WeaponSlot> _slots = new List<WeaponSlot>();
    private readonly List<WeaponSlot> _equippedSlots = new List<WeaponSlot>();

    private int _activeEquippedSlot = -1;

    /// <inheritdoc />
    public int ActiveEquippedSlot => _activeEquippedSlot;

    /// <inheritdoc />
    public event System.Action<int> ActiveSlotChanged;

    /// <inheritdoc />
    public BaseWeapon ActiveWeapon
    {
        get
        {
            if (_activeEquippedSlot < 0 || _activeEquippedSlot >= _equippedSlots.Count) return null;

            return _equippedSlots[_activeEquippedSlot].Instance;
        }
    }

    /// <summary>当前已装备的武器数量。</summary>
    public int EquippedCount => _equippedSlots.Count;

    /// <summary>
    /// 最多同时装备几把：Inspector 覆盖值 → 角色定义 → 保守取 1。
    /// 角色未就绪时取 1 而不是 2 —— 宁可少带，也不要让"没选角色"变成多带一把的理由。
    /// </summary>
    private int MaxEquipped
    {
        get
        {
            if (_maxEquippedOverride > 0) return _maxEquippedOverride;

            PlayerRoleController role = GetComponent<PlayerRoleController>();
            if (role != null && role.IsReady) return Mathf.Max(1, role.Definition.maxWeaponSlots);

            return 1;
        }
    }

    public IReadOnlyList<WeaponSlot> WeaponSlots => _slots;

    /// <inheritdoc />
    public IReadOnlyList<WeaponSlot> EquippedSlots => _equippedSlots;

    /// <inheritdoc />
    public bool HasAimWeapon
    {
        get
        {
            BaseWeapon weapon = ActiveWeapon;
            if (weapon == null) return false;

            AttackDriver driver = weapon.Driver;
            return driver != null && driver.Attack != null && driver.Attack.RequiresAimInput;
        }
    }

    private void Awake()
    {
        if (_mount == null) _mount = transform;

        // 归属落点：宿主实体就是本玩家。武器打死敌人时按它结算击杀统计
        _entity = GetComponent<EntityBehaviour>();
        if (_entity == null) _entity = GetComponentInParent<EntityBehaviour>();

        for (int i = 0; i < _candidates.Count; i++)
        {
            WeaponEntitySO config = _candidates[i];
            if (config == null) continue;

            _slots.Add(new WeaponSlot(config));
        }
    }

    private void OnDestroy()
    {
        // 与装配时的 LoadAssetAsync 成对。武器实例是**手动 Instantiate** 出来的
        // （不被 Addressables 跟踪），随本对象销毁即可，所以这里只归还句柄、不单独销毁实例。
        for (int i = 0; i < _slots.Count; i++)
            WeaponAssembler.ReleaseHandle(_slots[i].Runtime);
    }

    /// <summary>
    /// 装配一把武器：加载 prefab → 实例化到挂点 → 注入配置/宿主 → 记录进已装备列表。
    /// 装配序列本身在 <see cref="WeaponAssembler"/>（与塔共用）。
    ///
    /// <para>
    /// <b>为什么用「手动 LoadAssetAsync + 同步 Instantiate」而不是 <c>InstantiateAsync</c>：</b>
    /// ① 加载与实例化解耦，失败时停在「已加载但未装备」这个可回滚的中间态；
    /// ② 句柄按「每个玩家 × 每把已装备武器」持有一份并在这里归还，
    /// 而 <c>InstantiateAsync</c> 要求每个实例逐个 <c>ReleaseInstance</c>
    /// （返回 false 时还得自己 Destroy），武器实例存活整局、销毁路径有四条，漏一条就是 bundle 永不卸载；
    /// ③ 联机时实例要交给 Mirror 的 Spawn/UnSpawn，两套生命周期管理不能叠加。
    /// </para>
    /// </summary>
    public async Task<bool> EquipAsync(WeaponSlot slot)
    {
        if (slot == null || slot.Config == null) return false;
        if (slot.IsEquipped) return false;

        // 重入闸：装配要等 Addressables，同一槽被并发装配两次时，后一次会覆盖前一次的
        // Runtime —— 被覆盖的实例既不销毁、句柄也不归还（见 WeaponSlot.IsAssembling）
        if (slot.IsAssembling) return false;

        if (EquippedCount >= MaxEquipped)
        {
            Debug.LogWarning($"[PlayerWeaponController] 装备上限为 {MaxEquipped} 把，" +
                             "无法再装配。");
            return false;
        }

        slot.IsAssembling = true;
        try
        {
            WeaponInstance runtime = await WeaponAssembler.AssembleAsync(this, slot.Config, WeaponMount);

            // await 期间玩家可能已被销毁（回菜单 / 死亡重开）
            if (this == null) return false;
            if (runtime == null) return false;

            slot.SetRuntime(runtime);
            _equippedSlots.Add(slot);

            // 第一把自动激活；后续装上的保持停用 ——
            // 装配器已经把实例停用，这里不必再关一次
            if (_activeEquippedSlot < 0)
                SetActiveEquippedSlot(_equippedSlots.Count - 1);
            else
                ActiveSlotChanged?.Invoke(_activeEquippedSlot);

            return true;
        }
        finally
        {
            // 失败分支也要复位：否则一次加载失败会让这个槽永久不可装配
            slot.IsAssembling = false;
        }
    }

    /// <inheritdoc />
    public bool SwitchToSlot(int equippedIndex)
    {
        if (equippedIndex < 0 || equippedIndex >= _equippedSlots.Count) return false;
        if (_equippedSlots[equippedIndex].Instance == null) return false;
        if (equippedIndex == _activeEquippedSlot) return false;

        SetActiveEquippedSlot(equippedIndex);
        return true;
    }

    /// <summary>
    /// 切换激活槽：停用旧武器、启用新武器，并广播变化。
    /// 停用走 <see cref="BaseWeapon.SetActiveSlot"/> —— 它还会处理召唤物（环绕物等）。
    /// </summary>
    private void SetActiveEquippedSlot(int equippedIndex)
    {
        for (int i = 0; i < _equippedSlots.Count; i++)
        {
            BaseWeapon weapon = _equippedSlots[i].Instance;
            if (weapon == null) continue;

            weapon.SetActiveSlot(i == equippedIndex);
        }

        _activeEquippedSlot = equippedIndex;
        ActiveSlotChanged?.Invoke(equippedIndex);
    }
}
