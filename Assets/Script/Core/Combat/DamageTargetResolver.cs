using UnityEngine;

/// <summary>
/// 伤害目标解析 —— **所有"碰到了碰撞体 → 打到了谁"的判定都走这里**。
///
/// <para>
/// 判据只有一条：碰撞体所在对象上有 <see cref="Hurtbox"/> 标记。
/// "哪个碰撞体算本体"因此由**数据**（prefab 上的标记）回答，而不是由每个系统各自猜。
/// 历史上这里有过六套写法，对应四次线上问题（怪群互相掉血、玩家被火球代打、
/// 怪隔空打塔、溅射结算两次）—— 每加一种攻击方式就多一套判据，是"补丁感"的真正来源。
/// </para>
///
/// <para>
/// <b>它只回答"打到了谁"，不回答"该不该打"</b>：敌我区分、攻击方式的目标类型
/// 仍由调用方按 tag 判断（tag = 身份，属于玩法规则；本解析器只管几何）。
/// </para>
///
/// <para>
/// <b>为什么是"碰撞体自己所在对象"而不是向上找：</b>玩家的武器与召唤物挂在
/// <c>Player/Weapons/…</c> 下，向上找会把"怪碰到火球"解析成"玩家被碰到" ——
/// 表现就是玩家在几格之外掉血。塔武器的检测圈（半径 = 武器射程的触发体）同理：
/// 它是"索敌范围"，碰到它不等于打到了塔。
/// </para>
/// </summary>
public static class DamageTargetResolver
{
    /// <summary>解析碰撞体命中的实体；不是本体碰撞体（没有标记）时返回 null。</summary>
    public static BaseHealthController Resolve(Collider2D collider)
    {
        if (collider == null) return null;

        Hurtbox hurtbox = collider.GetComponent<Hurtbox>();
        return hurtbox != null ? hurtbox.Health : null;
    }

    /// <summary>解析并排除已死亡的目标（伤害路径几乎都要这一条）。</summary>
    public static BaseHealthController ResolveAlive(Collider2D collider)
    {
        BaseHealthController target = Resolve(collider);
        return target != null && !target.IsDead ? target : null;
    }
}
