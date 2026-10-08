using UnityEngine;

/// <summary>
/// 塔的"改装武器"升级项 —— 花升级点把塔切换到**另一个攻击槽**。
///
/// <para>
/// <b>为什么做成升级项而不是切换按钮：</b>改装是一次**有代价的选择**（花升级点），
/// 与塔的其它升级走同一条路径（<c>TowerLevelUpPanel</c> → <c>LevelUpSO.ApplyTo</c>），
/// 不需要新增交互入口、能力位与一套切换 UI。
/// </para>
///
/// <para>
/// <b>买过就消失</b>：<see cref="IsAvailable"/> 在"已经处于目标槽"时返回 false，
/// 于是列表里不会留下一个点了没效果的项（也不会让玩家重复花点买同一件事）。
/// </para>
///
/// <para>
/// <b>一次只开火一把</b>（<see cref="TowerWeaponController.SetActiveWeapon"/>）——
/// 所以改装是"换武器形态"，不是"多一门炮"。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "TowerWeaponSwap", menuName = "Game/Selection/Tower Weapon Swap")]
public class TowerWeaponSwapUpgradeSO : LevelUpSO
{
    [Tooltip("改装后激活的武器下标（对应塔上 TowerWeaponController 的武器列表顺序，0 = 第一把）")]
    public int targetSlot = 1;

    /// <summary>
    /// 本升级的效果**不在数值上**，而在 <see cref="OnApplied"/>（切换激活武器）。
    ///
    /// <para>
    /// 体检（<c>EntitySOValidator</c>）据此豁免"statModifiers 为空且无回血 = 升级无任何效果"那条规则 ——
    /// 不豁免的话本资产会被误报成空升级，而它的效果确实存在（只是不体现在数值上）。
    /// </para>
    /// </summary>
    public override bool HasCustomEffect => true;

    /// <inheritdoc />
    public override bool IsAvailable(EntityBehaviour target)
    {
        // 目标缺失交给 UpgradeSelector 统一判无效（不要在这里吞掉选项）
        if (target == null) return true;

        TowerWeaponController controller = ResolveController(target);

        // 没有控制器 / 武器还没装配好时仍然列出：买下去会给出明确告警，
        // 比"选项凭空消失"更容易定位配置错误
        if (controller == null || controller.ActiveIndex < 0) return true;

        return controller.ActiveIndex != targetSlot;
    }

    /// <inheritdoc />
    protected override void OnApplied(EntityBehaviour entity)
    {
        TowerWeaponController controller = ResolveController(entity);
        if (controller == null)
        {
            Debug.LogWarning($"[{name}] 目标「{entity.name}」上没有 TowerWeaponController，改装未生效。", this);
            return;
        }

        if (!controller.SetActiveWeapon(targetSlot))
        {
            Debug.LogWarning($"[{name}] 「{entity.name}」没有下标为 {targetSlot} 的武器" +
                             $"（共 {controller.Count} 把，当前 {controller.ActiveIndex}），改装未生效。", this);
        }
    }

    private static TowerWeaponController ResolveController(EntityBehaviour target) =>
        target != null ? target.GetComponent<TowerWeaponController>() : null;
}
