/// <summary>
/// 攻击计时的权威归属 —— 声明「谁负责驱动这个 <see cref="AttackDriver"/> 的计时」。
///
/// <para>
/// 单机下两种取值行为完全相同；它的意义在联机（<c>Docs/MirrorPlan.md</c> M4）：
/// 玩家武器的节奏由**各客户端本地**驱动（手感优先，4.2），
/// 而塔的生成与命中判定必须由**服务端**驱动（4.1），否则两端各打一份伤害、敌人血量会分叉。
/// </para>
///
/// <para>
/// <b>唯一的读取时机：</b>网络层在实体 spawn 之后读一次 <see cref="AttackDriver.Authority"/>，
/// 据此设置 <see cref="AttackDriver.HasAuthority"/>。除此之外**不要**有第二个地方写
/// <c>HasAuthority</c>：prefab 上的 <c>_authority</c> 是装配方的**声明**，
/// 运行时状态只有 <c>HasAuthority</c> 一份。两处各写一次就会出现
/// "prefab 说 Server、运行时按 Local tick"这种分叉 —— 与"同一件事两个真相源"是同一类错误。
/// </para>
///
/// <para>
/// <b>谁声明什么：</b>塔武器 prefab 上是 <see cref="Server"/>
/// （由 <c>TowerWeaponSetup</c> 生成/修复时写入），玩家武器是 <see cref="Local"/>。
/// </para>
/// </summary>
public enum AttackAuthority : byte
{
    /// <summary>本地驱动 —— 玩家武器。联机时每个客户端都跑自己的。</summary>
    Local = 0,

    /// <summary>服务端驱动 —— 塔/召唤物。联机时只有服务端 tick，客户端仅表现。</summary>
    Server = 1,
}
