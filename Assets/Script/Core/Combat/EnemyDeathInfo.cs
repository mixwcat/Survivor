using UnityEngine;

/// <summary>
/// 敌人死亡时的**归属快照**。
///
/// <para>
/// <b>为什么需要它：</b>死亡发生在 <c>EnemyHealthController.Die()</c>，那里拿不到"谁打死的" ——
/// 伤害是一次次 <c>TakeDamage</c> 传来的，方法本身不带攻击者参数。
/// 而击杀归属决定了升级点给谁、统计算谁的、联机时由谁上报。
/// </para>
///
/// <para>
/// <b>解析不出归属是正常情况</b>（环境伤害、攻击者已销毁、塔的主人暂时未知）：
/// 此时 <see cref="Killer"/> 为 null，调用方应只记「团队击杀」，
/// 而不是随便发给最近的玩家 —— 错误的归属比没有归属更难查。
/// </para>
/// </summary>
public readonly struct EnemyDeathInfo
{
    /// <summary>被击杀的敌人实体（池化对象，**不要**在结算时长期持有）。</summary>
    public readonly EntityBehaviour Victim;

    /// <summary>归属玩家；无法解析时为 null。</summary>
    public readonly PlayerController Killer;

    /// <summary>死亡位置（掉落、统计用）。</summary>
    public readonly Vector3 Position;

    /// <summary>该敌人提供的经验值。</summary>
    public readonly int ExpReward;

    public bool HasKiller => Killer != null;

    public EnemyDeathInfo(EntityBehaviour victim, PlayerController killer, Vector3 position, int expReward)
    {
        Victim = victim;
        Killer = killer;
        Position = position;
        ExpReward = expReward;
    }
}
