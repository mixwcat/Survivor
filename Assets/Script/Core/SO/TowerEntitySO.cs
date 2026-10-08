using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 塔实体配置 SO：在 <see cref="BaseEntitySO"/> 之上添加塔本体的 prefab 引用与显示信息。
///
/// <para>
/// <b>为什么是 <see cref="AssetReferenceGameObject"/> 而不是 <c>GameObject</c> 直接引用：</b>
/// 直接引用会让塔 prefab 随本 SO 一起被加载 —— 只要 <c>ChooseTowerPanel</c> 读了
/// <c>icon</c>/<c>displayName</c>，整份 prefab 及其依赖就进了内存，按需加载名存实亡。
/// AssetReference 还要满足一个前提：目标必须在 Addressables 组里（Tower 组）。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "TowerEntity", menuName = "Game/Entity/Tower")]
public class TowerEntitySO : BaseEntitySO
{
    [Header("塔专属")]
    [Tooltip("塔本体预制体（Addressable）。放置流程开始时加载，幽灵与最终放置共用同一份；" +
             "句柄由 TowerPlacementController 持有并在幽灵销毁时归还")]
    public AssetReferenceGameObject prefab;

    [Header("塔显示信息")]
    public string displayName;
    [TextArea]
    public string description;
    public Sprite icon;
}
