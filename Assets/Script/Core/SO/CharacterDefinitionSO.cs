using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 角色能力位 —— **领域层权限的唯一判据**。
///
/// <para>
/// <b>为什么要有它：</b>枪手不能造塔、工程师不能升武器，这是角色设计的核心。
/// 但"隐藏按钮"不是权限 —— 旧入口、快捷键、联机时伪造的命令都能绕过去。
/// 所以能力做成数据位，<b>每个领域命令执行前都要校验一次</b>：
/// UI 只负责让玩家看不见，领域层负责让命令不生效。
/// </para>
///
/// <para>
/// 用 <c>[Flags]</c> 而不是多个 bool：角色能力会随内容增加，
/// 位标记让"允许某一组能力"可以一次配置完（如未来的"支援兵：能升级武器 + 能拆塔"）。
/// </para>
/// </summary>
[System.Flags]
public enum CharacterCapability
{
    None = 0,

    /// <summary>能打开武器升级界面（花升级点定向升级当前武器）。</summary>
    WeaponUpgrade = 1 << 0,

    /// <summary>能在局内切换携带的武器。</summary>
    WeaponSwitch = 1 << 1,

    /// <summary>能建造防御塔。</summary>
    TowerBuild = 1 << 2,

    /// <summary>能升级防御塔。</summary>
    TowerUpgrade = 1 << 3,

    /// <summary>能拆除防御塔（返还投入）。</summary>
    TowerDismantle = 1 << 4,
}

/// <summary>
/// 角色定义 SO —— 枪手 / 工程师这类**可选角色**的配置单。
///
/// <para>
/// 与 <c>PlayerEntitySO</c> 的分工：后者是"玩家实体"的配置（数值 + 升级池），
/// 本类描述"这一局玩家扮演谁"（能力、外观、默认装备），并通过
/// <see cref="playerConfig"/> 指定**本职业**用哪一份实体配置 ——
/// 一个 Player prefab 服务多个职业，**数值与升级池由角色决定**。
/// </para>
///
/// <para>
/// <b>只读</b>：局内选中的角色由 <c>PlayerRoleController</c> 持有，不写回 SO
/// （SO 是常驻资产，写进去会跨 Play 会话与跨玩家污染）。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Game/Character/Definition")]
public class CharacterDefinitionSO : BaseEntitySO
{
    [Header("角色显示信息")]
    [Tooltip("大厅选角面板显示的名字")]
    public string displayName;

    [Tooltip("大厅选角面板显示的图标")]
    public Sprite displaySprite;

    [Header("角色本体")]
    [Tooltip("角色预制体（Addressable）。P2 的运行时生成入口按它生成玩家")]
    public AssetReferenceGameObject prefab;

    [Header("职业数值与升级池")]
    [Tooltip("本职业的玩家实体配置（数值 + 升级池）。生成玩家时由 PlayerSpawner 注入 —— " +
             "Player prefab 的根节点在资产里是**未激活**的，注入发生在 Awake 之前，" +
             "所以它是玩家 StatModel 的唯一来源")]
    public PlayerEntitySO playerConfig;

    [Header("能力与装备")]
    [Tooltip("该角色允许的系统能力。领域层必须校验，UI 隐藏只是表现")]
    public CharacterCapability capabilities = CharacterCapability.None;

    [Tooltip("出发配置为空时按顺序补的默认武器 id（枪手：weapon_gun + weapon_cannon）。" +
             "会一直补到携带上限 maxWeaponSlots，只补白名单内、不重复的项")]
    public List<string> defaultWeaponIds = new List<string>();

    [Tooltip("出发时最多携带的武器数（枪手 2；工程师固定 1）")]
    public int maxWeaponSlots = 2;

    [Tooltip("允许携带的武器 id 白名单。留空 = 不限制（只按 maxWeaponSlots 约束）。" +
             "切换角色时 RunSession 会据此清理不合法的装备并补默认值")]
    public List<string> allowedWeaponIds = new List<string>();
}
