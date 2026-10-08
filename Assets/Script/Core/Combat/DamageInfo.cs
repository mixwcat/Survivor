/// <summary>
/// 一次伤害结算的全部输入。
///
/// <para>
/// <b>为什么要一个结构体而不是两个 float：</b>击杀归属（经验给谁）、伤害统计、仇恨、
/// 以及联机下的反作弊校验，都需要知道「这一下是谁打的」。
/// 旧签名 <c>TakeDamage(float damage, float hitForce)</c> 让攻击者在调用点就被丢掉，
/// 等联机（`Docs/MirrorPlan.md` M3.4「伤害结算服务端权威」）再补，就要改所有攻击方式与投射物。
/// </para>
///
/// <para>
/// <c>readonly struct</c> + <c>in</c> 传参：不产生堆分配，可以安全地用在逐帧/命中路径上。
/// </para>
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>伤害数值。</summary>
    public readonly float Amount;

    /// <summary>
    /// 攻击方给出的**击退力度**。方向与时长由受击方决定
    /// （击退是「受击结果」，属于受击方的内部状态）。0 表示不击退。
    /// </summary>
    public readonly float HitForce;

    /// <summary>
    /// 攻击者实体；环境伤害（如将来的毒圈）为 null。
    /// 联机时它是**本地实例**，稳定身份取 <c>Attacker.EntityConfig.id</c>。
    /// </summary>
    public readonly EntityBehaviour Attacker;

    /// <summary>伤害来源类别，用于统计与筛选（例如「只反弹投射物伤害」）。</summary>
    public readonly DamageSource Source;

    public DamageInfo(float amount, float hitForce = 0f,
                      EntityBehaviour attacker = null, DamageSource source = DamageSource.Unknown)
    {
        Amount = amount;
        HitForce = hitForce;
        Attacker = attacker;
        Source = source;
    }

    /// <summary>攻击者是否已知（联机下远程玩家的实例可能尚未解析出来）。</summary>
    public bool HasAttacker => Attacker != null;
}

/// <summary>伤害来源类别。</summary>
public enum DamageSource : byte
{
    Unknown = 0,

    /// <summary>投射物命中（子弹 / 炮弹）。</summary>
    Projectile = 1,

    /// <summary>环绕物（旋转火球）接触。</summary>
    Orbit = 2,

    /// <summary>范围伤害（`AreaDamageAttackSO`）。</summary>
    Area = 3,

    /// <summary>敌人接触伤害（`EnemyHealthController` 的周期结算）。</summary>
    Contact = 4,

    /// <summary>环境伤害（预留：毒圈、陷阱等）。</summary>
    Environment = 5,
}
