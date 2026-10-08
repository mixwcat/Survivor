using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 塔的武器控制器：按数据装配**武器实例**，并管理"当前激活哪一把"。
///
/// <para>
/// <b>与 <see cref="PlayerWeaponController"/> 的关系：</b>两者是同一种东西的两侧 ——
/// 玩家侧多出"候选/已装备/切换槽位/换装"的完整流程，塔侧只需要"装配 + 切换激活"，
/// 所以没有强行合成一个类（合成会让玩家侧的多余概念渗进塔里）。
/// <b>共用的是装配序列</b>（<see cref="WeaponAssembler"/>）与武器契约
/// （<see cref="IWeaponHost"/> + <see cref="BaseWeapon"/> + <see cref="AttackDriver"/>）——
/// 塔与玩家武器走的是同一条加载/注入/归还路径，连失败分支都是同一份代码。
/// </para>
///
/// <para>
/// <b>为什么塔的武器不再是"塔身上的攻击槽"：</b>那样武器没有自己的 prefab / data / 升级项，
/// 也无法给某一把单独升级。现在每把武器都是完整实体（<see cref="WeaponEntitySO"/> →
/// <c>WeaponDataSO</c>（塔用 <c>TowerWeaponDataSO</c>）→ 自己的 StatModel），
/// "改装"升级退化成"切换激活项"，与玩家武器完全同构。
/// </para>
///
/// <para>
/// <b>句柄成对：</b>每把武器的 prefab 句柄记在 <see cref="WeaponInstance"/> 上、由本组件持有，
/// <c>OnDestroy</c> 统一归还（塔被拆除、切场景都走这一条路径）。
/// </para>
/// </summary>
public class TowerWeaponController : MonoBehaviour, IWeaponHost
{
    [Tooltip("武器挂点；留空 = 挂到自身")]
    [SerializeField] private Transform _mount;

    [Tooltip("本塔可用的武器。**第一项默认激活**；「改装」升级切换激活项。\n" +
             "⚠️ 列表里**每一把**都会在 Start 被装配成完整实体（prefab + 自己的数值 + 自己的检测圈）——\n" +
             "这是有意的取舍：「改装」因此是瞬时切换，代价是未激活的那些也占内存、也有自己的组件。\n" +
             "加武器前先确认这份常驻开销值得。")]
    [SerializeField] private List<WeaponEntitySO> _weapons = new();

    /// <summary>已装配的武器（下标与 <see cref="_weapons"/> 对齐；装配失败为 null 占位）。</summary>
    private readonly List<WeaponInstance> _instances = new();

    private EntityBehaviour _entity;
    private int _activeIndex = -1;

    /// <inheritdoc />
    public Transform WeaponMount => _mount != null ? _mount : transform;

    /// <inheritdoc />
    public EntityBehaviour Entity => _entity;

    /// <summary>当前激活的武器实例（未装配好时为 null）。</summary>
    public BaseWeapon ActiveWeapon
    {
        get
        {
            if (_activeIndex < 0 || _activeIndex >= _instances.Count) return null;

            return _instances[_activeIndex] != null ? _instances[_activeIndex].Weapon : null;
        }
    }

    /// <summary>当前激活的武器下标（-1 = 还没装配好）。</summary>
    public int ActiveIndex => _activeIndex;

    /// <summary>
    /// 激活武器发生变化（含首次装配完成）。<see cref="BaseTower"/> 订阅它改订攻击表现与范围圈 ——
    /// 攻击表现挂在**武器**的 <c>AttackDriver</c> 上，换武器就必须改订，否则"换了武器之后没有动画"。
    /// </summary>
    public event System.Action<BaseWeapon> ActiveWeaponChanged;

    /// <summary>
    /// 默认（第一把）武器的配置 —— 供**放置预览**读射程用。
    ///
    /// <para>
    /// 预览发生在塔实例化之前/装配之前，拿不到武器实例，只能从数据读：
    /// 射程属于武器（<c>WeaponDataSO.AttackRange</c>），塔本身不再持有射程。
    /// </para>
    /// </summary>
    public WeaponEntitySO DefaultWeaponConfig => _weapons.Count > 0 ? _weapons[0] : null;

    /// <summary>已装配的武器数量（含装配失败的占位）。</summary>
    public int Count => _instances.Count;

    private void Awake()
    {
        if (_mount == null) _mount = transform;

        _entity = GetComponent<EntityBehaviour>();
        if (_entity == null) _entity = GetComponentInParent<EntityBehaviour>();
    }

    private async void Start()
    {
        await EquipAllAsync();
    }

    private void OnDestroy()
    {
        // 实例随本对象销毁；句柄必须显式归还（直接 Destroy 不会递减引用计数）
        for (int i = 0; i < _instances.Count; i++)
            WeaponAssembler.ReleaseHandle(_instances[i]);

        _instances.Clear();
    }

    /// <summary>
    /// 装配**列表里的每一把**武器（不是只装激活的那把）。
    ///
    /// <para>
    /// <b>这是有意的取舍，不是漏优化</b>：「改装」升级（<see cref="TowerWeaponSwapUpgradeSO"/>）
    /// 因此是**瞬时切换**，不需要运行时加载（加载会带来一帧的"改装了但没反应"）。
    /// 代价是未激活的武器也是完整实体：prefab 实例、自己的 StatModel、自己的检测圈
    /// 都会常驻，只是被 <see cref="BaseWeapon.SetActiveSlot"/> 停掉了逐帧工作与渲染。
    /// </para>
    ///
    /// <para>
    /// 当前量级很小（Teto 2 把，Rin / Luo 各 1 把），但**数据上没有上限** ——
    /// 往 <see cref="_weapons"/> 里加第 N 把之前，先确认这份常驻开销值得。
    /// </para>
    /// </summary>
    private async Task EquipAllAsync()
    {
        for (int i = 0; i < _weapons.Count; i++)
        {
            WeaponEntitySO config = _weapons[i];
            if (config == null)
            {
                _instances.Add(null);
                continue;
            }

            // 与玩家侧共用同一条装配序列（加载 → 实例化 → 注入配置/宿主 → 停用）
            WeaponInstance runtime = await WeaponAssembler.AssembleAsync(this, config, WeaponMount);

            // await 期间塔可能已被拆除（拆塔 / 切场景）
            if (this == null) return;

            _instances.Add(runtime);
        }

        SetActiveWeapon(0);
    }

    /// <summary>
    /// 切换激活武器（「改装」升级的落点）。返回是否真的发生了变化。
    /// 下标非法或本来就是它时返回 false —— 升级项据此判断"这次改装没生效"。
    /// </summary>
    public bool SetActiveWeapon(int index)
    {
        if (index < 0 || index >= _instances.Count) return false;
        if (index == _activeIndex) return false;

        for (int i = 0; i < _instances.Count; i++)
        {
            BaseWeapon weapon = _instances[i] != null ? _instances[i].Weapon : null;
            if (weapon != null) weapon.SetActiveSlot(i == index);
        }

        _activeIndex = index;
        ActiveWeaponChanged?.Invoke(ActiveWeapon);
        return true;
    }
}
