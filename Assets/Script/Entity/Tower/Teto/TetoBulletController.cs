using UnityEngine;

public class TetoBulletController : BulletController
{
    // 签名必须与基类一致（含 splashRadius / attacker / spawner），否则会变成隐藏基类的新方法、
    // 而不是重写 —— 那样炮弹走全参调用时就不会旋转了。
    public override void Init(int dmg, int force, float speed, Vector3 dir, float splashRadius = 0f,
                              EntityBehaviour attacker = null, IAttackSpawner spawner = null)
    {
        base.Init(dmg, force, speed, dir, splashRadius, attacker, spawner);

        // Teto 的投射物贴图朝上，所以要额外补 90°（几何计算在基类，避免抄两遍）
        FaceDirection(dir, -90f);
    }
}
