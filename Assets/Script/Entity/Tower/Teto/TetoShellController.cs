using UnityEngine;

/// <summary>
/// Teto 的炮弹：贴图朝向 + 爆炸表现。
///
/// <para>
/// 贴图旋转是 **Teto 专属**的（它的投射物贴图朝上，所以要按飞行方向补 90°），
/// 因此留在这个塔专属的类里，而不是塞进共用的 <see cref="ShellController"/>；
/// 爆炸表现则完全复用基类。
/// </para>
///
/// <para>
/// 与 <see cref="TetoBulletController"/> 的区别只有一个：那个是单体子弹，这个是溅射炮弹。
/// 两者的旋转都走 <see cref="BulletController.FaceDirection"/>，避免同一段三角函数抄两遍。
/// </para>
/// </summary>
public class TetoShellController : ShellController
{
    // 签名必须与基类一致（含 splashRadius / attacker / spawner），否则会变成隐藏基类的新方法、
    // 而不是重写 —— 那样炮弹走全参调用时就不会旋转了。
    public override void Init(int dmg, int force, float speed, Vector3 dir, float splashRadius = 0f,
                              EntityBehaviour attacker = null, IAttackSpawner spawner = null)
    {
        base.Init(dmg, force, speed, dir, splashRadius, attacker, spawner);

        FaceDirection(dir, -90f);
    }
}
