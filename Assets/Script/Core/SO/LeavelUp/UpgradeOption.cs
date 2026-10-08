/// <summary>
/// 一个升级选项 = 要展示/应用的 <see cref="LevelUpSO"/> + 这次应用要落到**哪个实体**上。
///
/// <para>
/// <b>为什么必须带上目标：</b>升级来源是多样的 —— 玩家自身的 upgrades 打在玩家身上，
/// 武器的定向升级打在**那件武器**身上，塔的定向升级打在那座塔上。
/// 武器读的是**武器自己的** StatModel（<c>SpinWeaponSize</c> 这类数值只存在于武器模型里），
/// 只传 <see cref="LevelUpSO"/> 的话面板只能把武器升级也打在玩家身上 ——
/// 那正是「武器升级买得起、毫无效果」的原因。
/// </para>
/// <para>
/// 反过来也**不能**改成「武器读玩家的 StatModel」：<c>SpinWeapon</c> 订阅的是自己模型的
/// <c>OnStatChanged</c>（用于升级尺寸时同步已有火球），改读玩家会让这套订阅彻底失效。
/// </para>
/// </summary>
public readonly struct UpgradeOption
{
    /// <summary>选项数据：显示信息 + 数值修改 + 一次性回血。</summary>
    public readonly LevelUpSO So;

    /// <summary>应用目标：玩家 / 某个武器 / 某座塔。</summary>
    public readonly EntityBehaviour Target;

    public UpgradeOption(LevelUpSO so, EntityBehaviour target)
    {
        So = so;
        Target = target;
    }

    /// <summary>
    /// SO 与目标都有效才可购买。<c>default(UpgradeOption)</c>（空槽位）与两者任一缺失都返回 false，
    /// 面板据此置灰按钮。
    /// </summary>
    public bool IsValid => So != null && Target != null;
}
