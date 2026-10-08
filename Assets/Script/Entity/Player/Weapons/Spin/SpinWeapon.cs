using UnityEngine;

/// <summary>
/// 旋转火球武器 —— 只负责**自转与尺寸同步**。
///
/// <para>
/// 生成火球已移到 <see cref="AttackDriver"/> + <see cref="OrbitAttackSO"/>。
/// 「环绕」效果来自本类的自转：火球是发射点（<c>AttackDriver._muzzle</c>）的子物体，
/// 武器一转它们就绕圈。
/// </para>
///
/// <para>
/// <b>发射点不在本类上再配一份</b>：读 <see cref="AttackDriver.Muzzle"/>。
/// 两处各配一个 Transform 迟早会漂移（改了这边忘了那边，火球就会生成在错的地方），
/// 而漂移是静默的。
/// </para>
/// </summary>
public class SpinWeapon : BaseWeapon
{
    /// <summary>
    /// 转速是**本类自己**逐帧读的数值（<see cref="Update"/>），不在
    /// <see cref="OrbitAttackSO.RequiredStats"/> 里 —— 由这里声明，
    /// 让漏配变成启动期红错而不是"火球转得极慢"。
    /// </summary>
    public override StatType[] RequiredStats => new[] { StatType.SpinWeaponRotationSpeed };

    /// <summary>
    /// StatModel 就绪后订阅数值变化（用于「升级尺寸时同步已有火球」）。
    /// 订阅点必须走基类回调：本类自行声明 Start 会隐藏基类的 Start，
    /// 放在那里的订阅曾经从未建立过。
    /// </summary>
    protected override void OnStatModelInitialized()
    {
        StatModel.OnStatChanged += OnAnyStatChanged;
    }

    protected virtual void OnDestroy()
    {
        if (StatModel != null)
            StatModel.OnStatChanged -= OnAnyStatChanged;
    }

    private void Update()
    {
        // 未激活的武器不写 Transform：备用火球继续自转是纯浪费（写 rotation 会脏化子物体层级），
        // 而画面上它已经被藏起来了，没有任何线索指向这里
        if (!IsActiveSlot) return;

        float speed = GetStat(StatType.SpinWeaponRotationSpeed);

        // 转速为 0 时不要每帧读 eulerAngles + 写 rotation（写 Transform 会连带脏化子物体层级）
        if (speed != 0f)
        {
            transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + speed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 切换激活槽时停用 / 启用环绕物。
    ///
    /// <para>
    /// 火球是发射点下的**独立实例**，不会随武器组件的 <c>enabled</c> 一起停 ——
    /// 不停它们的话，未激活的火球武器还在转、还在造成伤害，
    /// 而画面上"武器已经收起来了"，很难归因。
    /// </para>
    /// </summary>
    protected override void OnActiveSlotChanged(bool active)
    {
        Transform orbitAnchor = Driver != null ? Driver.Muzzle : null;
        if (orbitAnchor == null) return;

        for (int i = 0; i < orbitAnchor.childCount; i++)
            orbitAnchor.GetChild(i).gameObject.SetActive(active);
    }

    /// <summary>
    /// 监听数值变化：尺寸变化时更新**已经存在**的火球（升级后不必等下一轮重新生成）。
    ///
    /// <para>
    /// <b>必须走 <see cref="SpinWeaponController.SetTargetSize"/>，不能直接写 <c>localScale</c>：</b>
    /// 火球的尺寸由它自己的 <c>Update</c> 用 <c>MoveTowards</c> 逐帧插值到 <c>targetSize</c>
    /// （那是"长出来"的表现）。直接写 <c>localScale</c> 会在下一帧被插值拉回**旧**的
    /// <c>targetSize</c> —— 表现是"升级了但当场没反应"，只有等下一轮火球生成才看得到变大。
    /// </para>
    /// </summary>
    private void OnAnyStatChanged(StatType type)
    {
        if (type != StatType.SpinWeaponSize) return;

        Transform orbitAnchor = Driver != null ? Driver.Muzzle : null;
        if (orbitAnchor == null) return;

        float size = GetStat(StatType.SpinWeaponSize);

        for (int i = 0; i < orbitAnchor.childCount; i++)
        {
            if (orbitAnchor.GetChild(i).TryGetComponent(out SpinWeaponController fireBall))
                fireBall.SetTargetSize(size);
        }
    }
}
