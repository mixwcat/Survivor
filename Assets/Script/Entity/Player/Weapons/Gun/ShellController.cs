using UnityEngine;

/// <summary>
/// 炮弹：在 <see cref="BulletController"/> 的命中结算之上加**爆炸表现**（音效 / 未来的粒子与镜头震动）。
///
/// <para>
/// <b>为什么需要它：</b>炮弹与子弹共用同一份命中流程（伤害、溅射、归属、回收都在基类里，
/// 只有一份实现），但炮弹**独有**的表现会越加越多（爆炸音效、粒子、震屏、燃烧地面…）。
/// 把它们写在基类里会让每个新效果都变成基类里的一个 <c>if (是不是炮弹)</c>；
/// 覆写 <see cref="BulletController.OnHit"/> 则让这些效果留在一个只属于炮弹的类里。
/// </para>
///
/// <para>
/// <b>它不决定"是不是溅射"</b>：那由攻击方式 SO 的类型决定（<c>SplashProjectileAttackSO</c> 是炮弹、
/// <c>ProjectileAttackSO</c> 是单体）—— 半径是**武器的数值**，而投射物只收值，理由写在那个类的注释里。
/// 本类只负责表现，并在"挂了炮弹控制器却拿到半径 0"时报出来。
/// </para>
///
/// <para>
/// 目前的使用者：玩家的 cannon 炮弹（<c>Bullet_Shell.prefab</c>）与 Teto 的炮弹
/// （<c>TetoShell.prefab</c>，再往下派生 <c>TetoShellController</c> 补上贴图旋转）。
/// </para>
/// </summary>
public class ShellController : BulletController
{
    [Header("爆炸表现")]
    [Tooltip("勾选后命中时播放爆炸音效")]
    [SerializeField] private bool _playExplosionSfx;
    [SerializeField] private ResourceEnum _explosionSfx = ResourceEnum.OnMouseClickUI;

    /// <summary>
    /// 灌入参数并做**配置自检**：炮弹必须有溅射半径。
    ///
    /// <para>
    /// 半径由攻击方式 SO 给出（<c>SplashProjectileAttackSO</c> 读武器自己的数值，单体类恒传 0）。
    /// 挂了炮弹控制器却拿到 0，说明 prefab 与数据不一致 ——
    /// 表现是"炮弹像子弹一样只打一个目标"，而不会有任何报错。
    /// </para>
    /// </summary>
    public override void Init(int dmg, int force, float speed, Vector3 dir, float splashRadius = 0f,
                              EntityBehaviour attacker = null, IAttackSpawner spawner = null)
    {
        base.Init(dmg, force, speed, dir, splashRadius, attacker, spawner);

        if (SplashRadius <= 0f)
        {
            Debug.LogWarning($"[{nameof(ShellController)}]「{name}」是炮弹，但本次发射的溅射半径为 0 —— " +
                             "请检查这把武器的攻击方式是否用了 SplashProjectileAttackSO、" +
                             "以及它的 ShellExplosionRadius 是否配成了 0。", this);
        }
    }

    /// <inheritdoc />
    protected override void OnHit(BaseHealthController target)
    {
        // 伤害与溅射规则仍走基类那一份唯一实现（覆写只为加表现）
        base.OnHit(target);

        PlayExplosionFeedback();
    }

    /// <summary>
    /// 爆炸表现。默认只播音效；要加粒子/震屏请覆写这里。
    ///
    /// <para>
    /// ⚠️ <b>需要跨帧存活的特效不能挂在投射物下面</b>：命中后本实例立刻回对象池
    /// （<c>SetActive(false)</c>），挂着的粒子会跟着消失。要加爆炸粒子得让它脱离本物体并走对象池
    /// （参考 <c>ProjectilePool</c> / <c>DamageNumService</c> 的做法）。
    /// </para>
    /// </summary>
    protected virtual void PlayExplosionFeedback()
    {
        if (_playExplosionSfx) AudioService.Service?.PlaySfx(_explosionSfx);
    }
}
