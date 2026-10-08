using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人可攻击目标的注册表 —— 玩家 / 塔 / 推车都注册到这里，寻敌只查这一份列表。
///
/// <para>
/// <b>为什么不再往寻敌器里加分支：</b><see cref="EnemyTargetFinder"/> 原本硬编码了
/// "先找最近的玩家、再找最近的塔"。加推车意味着第三次改同一个方法，
/// 而且每加一种可攻击单位都要动寻敌逻辑 —— 它本该只回答"最近的目标是谁"，
/// 不该知道世界上有哪些种类的单位。
/// </para>
///
/// <para>
/// <b>注册的是 Transform 而不是实体类型</b>：寻敌只需要位置；
/// 而"能不能被打"由 <c>BaseHealthController</c> 回答（接触伤害侧）。
/// 两者分开后，新增单位类型不需要改这个接口。
/// </para>
///
/// <para>
/// 它是**场景级** Manager（不进组合根）：目标列表每局都要重建，
/// 跨场景保留一份会带着上一局的已销毁对象。
/// </para>
/// </summary>
public interface IEnemyTargetRegistry
{
    /// <summary>当前所有可攻击目标。调用方遍历时用 for + 索引器（foreach 会装箱枚举器）。</summary>
    IReadOnlyList<Transform> Targets { get; }

    /// <summary>注册（幂等：重复注册同一目标不会产生重复项）。</summary>
    void Register(Transform target);

    /// <summary>注销。</summary>
    void Unregister(Transform target);
}
