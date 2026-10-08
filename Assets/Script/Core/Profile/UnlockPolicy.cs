/// <summary>
/// 内容解锁策略 —— **解锁逻辑暂缓期间"默认全都拥有"的唯一落点**。
///
/// <para>
/// <b>为什么要有这个类：</b>"默认全拥有"如果各写各的，就会出现两条不一致的路径 ——
/// 武器台按"已拥有"放行，进关卡装配时 <c>PlayerSpawner</c> 又按档案里的解锁位拒绝，
/// 玩家看到的是"大厅里装上了、关卡里没有"，而且不报错。判定只有一份，两边都问它。
/// </para>
///
/// <para>
/// <b>为什么不直接改 <c>PlayerProfileService.IsWeaponUnlocked</c>：</b>档案服务是持久数据层，
/// 它的购买事务（扣款 → 记账 → 落盘 → 失败回滚）有只读校验器
/// （<c>CartLogicVerifier</c>）在测。把 <c>IsWeaponUnlocked</c> 改成恒 true 会连带
/// 让"余额充足时购买成功"这类断言全部失败，等于用测试的可信度换一行代码。
/// </para>
///
/// <para>
/// <b>接回解锁时：</b>把 <see cref="UnlockAllWeapons"/> 改成 <c>false</c> 即可 ——
/// 面板上的购买分支与档案里的解锁数据一直都在，不需要改任何调用点。
/// </para>
/// </summary>
public static class UnlockPolicy
{
    /// <summary>解锁逻辑暂缓：一切武器按"已拥有"处理。</summary>
    public const bool UnlockAllWeapons = true;

    /// <summary>该武器当前是否可用（武器台装备 / 进关卡装配都问它）。</summary>
    public static bool IsWeaponAvailable(IPlayerProfileService profile, string weaponId)
    {
        if (UnlockAllWeapons) return !string.IsNullOrEmpty(weaponId);

        return profile != null && profile.IsWeaponUnlocked(weaponId);
    }
}
