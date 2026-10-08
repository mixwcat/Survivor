using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 推车实体配置 —— 与塔/武器同样的「实体 SO + prefab 按需加载」模式。
///
/// <para>
/// id 用 <c>cart_hauler</c>：它是跨边界稳定标识（存档 / 联机同步要认出"这是同一辆推车"），
/// 而不是运行时查找键。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CartEntity", menuName = "Game/Entity/Cart")]
public class CartEntitySO : BaseEntitySO
{
    [Header("推车专属")]
    [Tooltip("推车预制体（Addressable）。句柄由场景生成方持有并在销毁时归还")]
    public AssetReferenceGameObject prefab;

    [Header("显示信息")]
    public string displayName;
}
