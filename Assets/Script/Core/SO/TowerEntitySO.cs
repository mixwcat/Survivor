using UnityEngine;

/// <summary>
/// 塔实体配置 SO
/// 在 BaseEntitySO 基础上添加塔专属的 Prefab 引用和经验消耗
/// </summary>
[CreateAssetMenu(fileName = "TowerEntity", menuName = "Game/Entity/Tower")]
public class TowerEntitySO : BaseEntitySO
{
    [Header("塔专属")]
    public GameObject prefab;
}
