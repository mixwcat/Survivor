using UnityEngine;

/// <summary>
/// 武器宿主 —— **持有一把或多把武器、并为其提供发射基准与归属的实体**。
///
/// <para>
/// <b>为什么需要它：</b>武器（<see cref="BaseWeapon"/> + <see cref="AttackDriver"/>）
/// 自带 prefab、<c>WeaponDataSO</c> 与 StatModel，数值完全独立；但"从哪儿发射""打死了算谁的"
/// 只有宿主知道。以前武器基类直接 <c>GetComponentInParent&lt;PlayerController&gt;()</c> ——
/// 那让武器**只能属于玩家**，塔要挂武器就得复制一套。
/// </para>
///
/// <para>
/// <b>谁实现它：</b>玩家（<c>PlayerController</c>）、塔（<c>BaseTower</c>）、
/// 未来的召唤物。实现方只需给出"挂点 + 实体身份"，武器不需要知道宿主是什么类型。
/// </para>
/// </summary>
public interface IWeaponHost
{
    /// <summary>武器实例的挂点（跟随宿主移动）。为 null 时武器挂到宿主自身。</summary>
    Transform WeaponMount { get; }

    /// <summary>宿主实体本身 —— 伤害归属（击杀统计、塔的投入账本）落在这里。</summary>
    EntityBehaviour Entity { get; }
}
