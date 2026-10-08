/// <summary>
/// 「**我读取这些数值**」的声明者 —— 挂在实体上、参与启动期数值体检的组件。
///
/// <para>
/// <b>为什么需要它：</b>攻击方式（<see cref="AttackMethodSO.RequiredStats"/>）只声明
/// <c>Execute</c>/<c>GetInterval</c> 读的数值，而**载体自己**读的那些不在它的视野里：
/// 武器本体的自转速率（<c>SpinWeapon</c> 读 <c>SpinWeaponRotationSpeed</c>）、
/// 索敌圈半径（<c>TowerWeaponRangeSync</c> 读 <c>AttackRange</c>）。
/// 漏配的后果是静默的 —— <c>EntityBehaviour.GetStat</c> 返回 <c>1f</c> 兜底，
/// 只留一条容易被淹没的告警（"火球转得极慢""圈只有 1 格"）。
/// </para>
///
/// <para>
/// <b>为什么是接口而不是让 <see cref="AttackDriver"/> 认识具体类型：</b>
/// 校验点在 <c>Core/Combat</c>，而读取方分散在武器与塔两个领域 ——
/// 直连具体类型会让核心层依赖塔的组件，每加一个"自己读数值"的组件就再连一次。
/// 有了本接口，<see cref="AttackDriver"/> 只问"这物体上有谁声明了自己读什么"，
/// 新增读取方只要实现它即可，校验代码一行都不用改。
/// </para>
///
/// <para>
/// <b>谁实现它：</b>武器本体（<c>BaseWeapon</c>）与挂在武器上的独立组件
/// （<c>TowerWeaponRangeSync</c>）。<b>谁读它，谁声明它</b> ——
/// 不要把别的组件的数值堆到自己的清单里。
/// </para>
/// </summary>
public interface IStatRequirementsProvider
{
    /// <summary>
    /// 本组件会读取的全部数值。默认应当是空数组；实现方**必须列全**，
    /// 漏一个就等于漏一条体检（症状见类注释）。
    /// </summary>
    StatType[] RequiredStats { get; }
}
