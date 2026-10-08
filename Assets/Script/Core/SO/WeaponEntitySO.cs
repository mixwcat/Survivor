using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 武器实体配置 SO：**一把武器 = 这一个 SO** —— 数值、行为、展示、解锁、升级池都在这里。
///
/// <para>
/// <b>武器是 prefab 而不是场景节点</b>：装配由 <c>WeaponAssembler</c>
/// 在运行时按需加载实例化。这样「玩家有哪些武器」是**数据**（可由存档/联机同步决定），
/// 新增一把武器不必再改场景与玩家预制体。
/// </para>
///
/// <para>
/// <b>为什么是 <see cref="AssetReferenceGameObject"/> 而不是 <c>GameObject</c> 直接引用：</b>
/// 直接引用会让 prefab 随本 SO 一起被加载（只要读了 upgrades 就会把武器 prefab 拉进内存），
/// 按需加载名存实亡。AssetReference 还要满足一个前提：目标必须是 Addressable。
/// </para>
///
/// <para>
/// <b>prefab 只是"骨架"</b>（外观 + <c>BaseWeapon</c> + <c>AttackDriver</c> + 发射点），
/// 里面**不接** <c>entityConfig</c> 也不接 <c>_attack</c>：两者都由装配方从本 SO 注入
/// （见 <c>WeaponAssembler</c> 与 <c>BaseWeapon.BindAttackDriver</c>）。
/// 这样同一份 prefab 能配出不同的数值变体与不同的攻击方式，而不必复制 prefab。
/// </para>
///
/// <para>
/// <b>展示信息为什么直接放在这里：</b>与 <see cref="TowerEntitySO"/> 保持一致 ——
/// 「实体 SO 就是它自己的配置单」。曾经多套一层 <c>WeaponSelectSO</c>，代价是
/// ① 每把武器多一个只装两个字段的资产；② 关联关系被拆到两处，容易误以为
/// 「展示信息那个 SO 应该指向 prefab」。那个类连同它的资产已经删除。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "WeaponEntity", menuName = "Game/Entity/Weapon")]
public class WeaponEntitySO : BaseEntitySO
{
    [Header("武器专属")]
    [Tooltip("武器本体预制体（Addressable）。句柄由装配方按「每个持有者 × 每把已装备武器」持有并归还")]
    public AssetReferenceGameObject prefab;

    [Header("攻击方式")]
    [Tooltip("这把武器**怎么打**（策略 SO：投射物 / 环绕 / 光束 / 范围伤害 / 治疗）。\n" +
             "它是唯一来源 —— 装配时注入 AttackDriver，武器 prefab 上的 _attack 留空即可。\n" +
             "留空的表现是「武器不会攻击」，AttackDriver 启动时会报出来。")]
    public AttackMethodSO attack;

    [Header("武器显示信息")]
    [Tooltip("武器选择面板显示的名称")]
    public string displayName;

    [Tooltip("武器选择面板显示的图标")]
    public Sprite displaySprite;

    [Header("解锁")]
    [Tooltip("武器台解锁所需金币。默认武器（枪手的 gun、工程师的 spin）在默认档案里已解锁，这里填多少都不影响")]
    public int unlockPrice = 800;
}
