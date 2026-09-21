using UnityEngine;

/// <summary>
/// 伤害数字服务（纯本地表现）。
/// 联机模式下可注册空实现或仅本地玩家可见的实现。
/// </summary>
public interface IDamageNumService
{
    DamageNumText SpawnDamageNum(Vector3 position, float damage, DamageNumType type = DamageNumType.white);

    /// <summary>归还到对象池</summary>
    void ReturnToPool(DamageNumText dmgNum);
}
