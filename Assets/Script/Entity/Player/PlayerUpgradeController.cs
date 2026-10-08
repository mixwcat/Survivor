using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家升级选项控制器 —— 挂在 Player 上，按玩家实例化
///（与 <see cref="PlayerWeaponController"/> 同级，经 <see cref="PlayerController.Upgrades"/> 访问）。
///
/// 职责边界（这也是它从 SOManager 里搬出来的原因）：
/// - 「组装这个玩家可用的升级来源」是**玩法逻辑**（要知道这一局这个玩家带了哪些武器），
///   不该塞进只读的 SO 配置服务；
/// - 「当前展示的 N 个选项」是**可变会话状态**，必须按玩家持有，
///   否则联机下两个玩家同时升级会互相覆盖对方的选项。
///
/// <para>
/// <b>随机池只含角色属性</b>（见 <see cref="CollectPlayerUpgradePool"/>）：武器与塔的成长走
/// **定向**升级（花升级点选具体项），两条路径混在一起会让同一把武器既能被随机抽到、
/// 又能花钱买到，数值叠得过快。
/// </para>
///
/// <para>
/// 每个选项仍然自带应用目标：定向升级里目标可能是玩家 / 某把武器 / 某座塔，
/// 面板必须按选项的目标调用 <c>LevelUpSO.ApplyTo</c>。
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerUpgradeController : MonoBehaviour, IPlayerUpgradeController
{
    /// <summary>面板 UI 的槽位数量固定为 3，抽取数量与之对应。</summary>
    public const int SlotCount = 3;

    private PlayerController _owner;
    private UpgradeOption[] _playerOptions;

    public int OptionCount => SlotCount;

    private void Awake()
    {
        _owner = GetComponent<PlayerController>();
    }

    public UpgradeOption[] GetPlayerOptions()
    {
        // 缓存优先：玩家关掉面板再打开时，应当还是同一批没花掉的选项
        if (_playerOptions == null)
            _playerOptions = RollPlayerOptions();

        return _playerOptions;
    }

    public UpgradeOption[] RerollPlayerOptions()
    {
        _playerOptions = RollPlayerOptions();
        return _playerOptions;
    }

    /// <summary>
    /// 列出目标塔**全部**可用的定向升级项。
    ///
    /// <para>
    /// 不抽取、不缓存：塔升级是"想看什么就看什么"的确定性列表，
    /// 每次打开都按 EntitySO 上的配置顺序重新列出（买过一项后由面板刷新）。
    /// </para>
    /// </summary>
    public UpgradeOption[] GetTowerOptions(BaseTower tower)
    {
        if (tower == null)
        {
            Debug.LogWarning("[PlayerUpgradeController] 未指定目标塔，无法列出塔升级选项。");
            return System.Array.Empty<UpgradeOption>();
        }

        var pool = new List<UpgradeOption>();

        // ① 塔自己的升级项（血量、塔专属能力）—— 目标 = 塔
        BaseEntitySO towerSO = tower.EntityConfig;
        AppendOptions(pool, towerSO != null ? towerSO.upgrades : null, tower);

        // ② **当前装配武器**的升级项 —— 目标 = 武器实例本身
        //
        // 武器的数值在它自己的 StatModel 上（自己的 WeaponDataSO 灌进去的那一份），
        // 所以攻击类升级必须落在武器上；写进塔的 StatModel 会「买了没效果」。
        // 「改装」切换武器后这一份会跟着变 —— 这正是"每把武器有自己的升级"。
        TowerWeaponController weapons = tower.GetComponent<TowerWeaponController>();
        BaseWeapon active = weapons != null ? weapons.ActiveWeapon : null;

        if (active != null)
        {
            BaseEntitySO weaponSO = active.EntityConfig;
            AppendOptions(pool, weaponSO != null ? weaponSO.upgrades : null, active);
        }

        if (pool.Count == 0)
        {
            Debug.LogWarning($"[PlayerUpgradeController] 塔「{(towerSO != null ? towerSO.name : tower.name)}」" +
                             "既没有自己的升级项，当前武器也没有 —— 面板会是空的。" +
                             "请检查 TowerEntitySO 与武器 EntitySO 的 upgrades。");
        }

        return pool.ToArray();
    }

    /// <summary>
    /// 列出某把武器**全部**可用的定向升级项（不抽取、不缓存）。
    /// 顺序与 EntitySO 上配置的一致，面板可据此分页。
    /// </summary>
    public UpgradeOption[] GetWeaponOptions(BaseWeapon weapon)
    {
        if (weapon == null) return System.Array.Empty<UpgradeOption>();

        BaseEntitySO weaponSO = weapon.EntityConfig;
        var pool = new List<UpgradeOption>();
        AppendOptions(pool, weaponSO != null ? weaponSO.upgrades : null, weapon);

        return pool.ToArray();
    }

    private UpgradeOption[] RollPlayerOptions()
    {
        return UpgradeSelector.PickDistinct(CollectPlayerUpgradePool(), SlotCount, "玩家的角色属性升级池");
    }

    /// <summary>
    /// 组装该玩家的**角色属性**升级池 = 玩家自身 EntitySO 的 upgrades。
    ///
    /// <para>
    /// <b>刻意不含武器</b>：武器成长走**定向**升级（花升级点选具体项），
    /// 两条路径混在一起会让同一把武器既能被随机抽到、又能花钱买到，数值叠得过快
    /// （见清单 D-02：随机池只含角色属性）。
    /// </para>
    /// </summary>
    private List<UpgradeOption> CollectPlayerUpgradePool()
    {
        var pool = new List<UpgradeOption>();

        BaseEntitySO playerSO = _owner != null ? _owner.EntityConfig : null;
        AppendOptions(pool, playerSO != null ? playerSO.upgrades : null, _owner);

        return pool;
    }

    /// <summary>
    /// 把一批 LevelUpSO 以 <paramref name="target"/> 为目标追加进池子。
    /// 目标为 null（如武器实例已被销毁）时照样入池，由 <see cref="UpgradeSelector"/> 判为无效项并告警。
    /// </summary>
    private static void AppendOptions(List<UpgradeOption> destination, List<LevelUpSO> upgrades, EntityBehaviour target)
    {
        if (destination == null || upgrades == null) return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            LevelUpSO so = upgrades[i];
            if (so == null) continue;

            // 已经不适用的一次性选择不入池（如塔的"改装为溅射炮弹"买过之后）——
            // 留着它等于让玩家重复花点买同一件事
            if (!so.IsAvailable(target)) continue;

            destination.Add(new UpgradeOption(so, target));
        }
    }
}
