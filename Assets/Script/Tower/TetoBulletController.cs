using UnityEngine;

public class TetoBulletController : BulletController
{
    public override void Init(int dmg, int force, Vector3 dir)
    {
        base.Init(dmg, force, dir);

        // 设置子弹旋转方向
        transform.Rotate(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90);
    }
}
