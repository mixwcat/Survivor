using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 武器注册表服务，按玩家实例化（由 <see cref="PlayerWeaponController"/> 实现）。
/// 每个玩家持有自己的候选武器与已装配武器。
///
/// <para>
/// <b>本接口只覆盖"装备"这一件事。</b>「卸下 / 换装」曾经在这里
/// （<c>Unequip</c> / <c>SwapAsync</c>），随局内 <c>ChooseWeaponPanel</c> 删除而失去调用方后已移除 ——
/// 现在"带哪几把出门"由大厅的 <c>RunSession</c> 决定、<c>PlayerSpawner.EquipLoadoutAsync</c> 执行。
/// 要重新引入运行时换装，请连**入口 UI 一起**设计，不要再往本接口上加无人调用的方法。
/// </para>
/// </summary>
public interface IWeaponManager
{
    /// <summary>本玩家的全部武器槽（候选 + 已装备状态）。</summary>
    IReadOnlyList<WeaponSlot> WeaponSlots { get; }

    /// <summary>
    /// 已装备的槽位，**按装备顺序**排列。输入槽位 <c>0/1</c> 对应的就是这里的第 1/2 项。
    /// </summary>
    IReadOnlyList<WeaponSlot> EquippedSlots { get; }

    /// <summary>
    /// 当前激活的**已装备槽序号**（0 = 第一把装备的武器；-1 = 一把都没装备）。
    ///
    /// <para>
    /// <b>它不是候选列表 <see cref="WeaponSlots"/> 的下标。</b>
    /// 旧实现直接把按键 <c>1/2</c> 当成候选下标用，于是「候选是 Gun/Spin/Cannon、
    /// 实际装备 Gun/Cannon」时按 2 会去访问候选第 2 项（Spin）—— 未装备 → 切换被拒绝，
    /// 表现成"Cannon 切不过去"，而且不报错。
    /// </para>
    /// </summary>
    int ActiveEquippedSlot { get; }

    /// <summary>
    /// 当前激活的武器实例（没有装备任何武器时为 null）。
    /// 只有激活槽的武器会攻击；HUD、瞄准摇杆与定向升级都以它为准。
    /// </summary>
    BaseWeapon ActiveWeapon { get; }

    /// <summary>激活槽变化（参数：新的已装备槽序号，-1 表示无）。UI 订阅它刷新槽位高亮，不要轮询。</summary>
    event System.Action<int> ActiveSlotChanged;

    /// <summary>
    /// 切换到第 <paramref name="equippedIndex"/> 个**已装备**槽。
    /// 越界、该槽为空、或已经是当前槽时返回 false（不产生任何变化）。
    /// </summary>
    bool SwitchToSlot(int equippedIndex);

    /// <summary>
    /// 当前**激活武器**是否需要瞄准输入（看攻击方式的
    /// <see cref="AttackMethodSO.RequiresAimInput"/>）。UI 据此决定是否显示攻击摇杆。
    ///
    /// <para>
    /// <b>只查激活武器</b>：备用武器需要瞄准不代表现在要用摇杆 ——
    /// 查全部已装备武器会让"带一把枪 + 一把火球"时永远显示瞄准摇杆，
    /// 而当前拿在手上的火球根本不吃瞄准输入。
    /// </para>
    ///
    /// <para>
    /// 用**数据**判断而不是 <c>GetWeapon&lt;GunWeapon&gt;()</c> 这种类型查找：后者让界面认识
    /// 具体武器类，新增一把需要瞄准的武器就得改 UI 代码。
    /// </para>
    /// </summary>
    bool HasAimWeapon { get; }

    /// <summary>
    /// 装配指定武器槽（加载 prefab → 实例化到挂点）。
    /// 已装配、未配置 prefab、服务未就绪都会返回 false 并留下明确日志。
    /// </summary>
    Task<bool> EquipAsync(WeaponSlot slot);
}
