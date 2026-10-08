/// <summary>
/// 玩家升级选项提供者：为**单个玩家**产出并缓存「升级选一」的可选项。
///
/// 按玩家实例化（挂在 Player 上），联机时各玩家互不干扰——
/// 这正是它不能留在全局配置服务里的原因：选项缓存是**可变会话状态**，
/// 放在全局单份会被不同玩家互相覆盖。
///
/// 每个选项都自带**应用目标**（<see cref="UpgradeOption.Target"/>）：
/// 玩家自身的升级目标是玩家，武器升级的目标是**那个武器**。
/// 面板必须按选项的目标调用 <c>LevelUpSO.ApplyTo</c>，不能统一打在玩家身上。
/// </summary>
public interface IPlayerUpgradeController
{
    /// <summary>一次展示的选项槽位数量（与面板 UI 槽位一一对应）。</summary>
    int OptionCount { get; }

    /// <summary>
    /// 取玩家升级选项：已有缓存则复用（关掉面板再打开仍是同一批未消费的选项），
    /// 没有缓存时抽取一批并缓存。
    /// </summary>
    UpgradeOption[] GetPlayerOptions();

    /// <summary>重新抽取玩家升级选项并覆盖缓存（消费掉一个选项后调用）。</summary>
    UpgradeOption[] RerollPlayerOptions();

    /// <summary>
    /// 列出指定塔**全部**可用的升级项（不抽取、不缓存；目标即该塔）。
    ///
    /// <para>
    /// 塔升级是**确定性定向列表**：玩家看到的是这座塔能升的全部项，想升哪个升哪个。
    /// 早先它和玩家升级共用"随机三选一"，结果是"想升的那一项一直抽不到"——
    /// 而塔是玩家花点数造的，成长路径不该由随机数决定。
    /// </para>
    ///
    /// <para>顺序与 EntitySO 上配置的一致，便于面板分页或滚动。</para>
    /// </summary>
    UpgradeOption[] GetTowerOptions(BaseTower tower);

    /// <summary>
    /// 列出指定武器**全部**可用的定向升级项（不抽取、不缓存；目标即该武器）。
    ///
    /// <para>
    /// 与 <see cref="GetPlayerOptions"/> 的区别是"定向"：那个是随机三选一（花升级点、只含角色属性），
    /// 这个是玩家主动挑一项花升级点买。返回顺序稳定，便于面板做分页。
    /// </para>
    /// </summary>
    UpgradeOption[] GetWeaponOptions(BaseWeapon weapon);
}
