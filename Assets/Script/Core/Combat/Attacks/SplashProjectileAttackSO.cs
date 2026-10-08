using UnityEngine;

/// <summary>
/// 溅射投射物（炮弹）—— 与 <see cref="ProjectileAttackSO"/> 的唯一差别是
/// **命中时对半径内的全部目标一起结算**，半径读本类指定的那个数值。
///
/// <para>
/// <b>为什么"是不是溅射"是子类，而不是基类上的一个 bool：</b>
/// bool 会让单体武器的资产上留下一份**永远不会被读**的配置
/// （<c>SplashRadiusStat</c> 指向一个它的 DataSO 根本不提供的数值），
/// 而"配了没反应"的字段最容易被误认为承重 —— 这正是
/// <see cref="CannonWeaponDataSO"/> 当初拆出子类时写下的同一条理由（数据侧按类型拆，SO 侧同理）。
/// 附带收益：<see cref="RequiredStats"/> 因此可以是**每类静态**的清单，
/// 而不是"翻一个 bool 就换一套校验"。
/// </para>
///
/// <para>
/// <b>为什么不能改由 prefab（<see cref="ShellController"/>）决定：</b>
/// 半径是**武器自己的数值**（<c>StatType.ShellExplosionRadius</c>，
/// "扩大爆炸范围"升级改的就是它），而投射物只收**值**
/// （<see cref="BulletController.Init"/> 的参数里没有 StatModel 引用，<c>attacker</c> 是宿主而不是武器）。
/// 让炮弹控制器自己决定，就只有两条路：把半径写死在 prefab 上（升级失效 + 半径出现两个来源，
/// 与已删除的 <c>Scale</c> 是同一类错误），或让子弹持有武器的 StatModel 引用
/// （子弹比武器活得久、池化复用要清引用、联机生成时引用不可序列化）。
/// 所以**规则**留在 SO 侧，prefab 只管**表现**。
/// </para>
///
/// <para>
/// 两侧错配是可发现的：SO 是本类但数值漏配 → <c>AttackDriver</c> 启动期红错
/// （<see cref="RequiredStats"/> 含半径）；prefab 挂了炮弹控制器但拿到半径 0 →
/// <see cref="ShellController.Init"/> 告警。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "AttackSplashProjectile", menuName = "Game/Attack/Splash Projectile")]
public class SplashProjectileAttackSO : ProjectileAttackSO
{
    [Header("溅射")]
    [Tooltip("溅射半径读哪个数值。<= 0 会退化成单体（与 BulletController 的约定一致）")]
    public StatType SplashRadiusStat = StatType.ShellExplosionRadius;

    /// <summary>
    /// 比单体多一项：溅射半径。**无条件列进来** —— 选了本类就说明要用它，
    /// 漏配必须变成启动期红错，而不是静默退化成单体。
    /// </summary>
    public override StatType[] RequiredStats => new[]
    {
        DamageStat, SpeedStat, ForceStat, SplashRadiusStat, StatType.AttackSpeed,
    };

    /// <summary>半径由武器自己的数值决定，生成流程仍走基类那一份。</summary>
    public override bool Execute(in AttackContext ctx) => Fire(ctx, ctx.Self.GetStat(SplashRadiusStat));
}
