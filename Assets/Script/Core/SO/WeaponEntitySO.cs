using UnityEngine;

/// <summary>
/// 武器实体配置 SO
/// 在 BaseEntitySO 基础上添加武器专属的 Prefab 引用
/// </summary>
[CreateAssetMenu(fileName = "WeaponEntity", menuName = "Game/Entity/Weapon")]
public class WeaponEntitySO : BaseEntitySO
{
    [Header("武器专属")]
    public GameObject prefab;
}
