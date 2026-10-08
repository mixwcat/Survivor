/// <summary>
/// 「升级状态」持有者 —— 记录**某个升级项在它身上已经应用过几次**。
///
/// <para>
/// <b>为什么需要它：</b>升级项的数值修改（<see cref="StatModifier"/>）本来就是"加到谁的 StatModel 上"，
/// 所以隔离天然成立；但"这一项还能不能再买"没有任何地方记录 ——
/// <see cref="LevelUpSO.IsAvailable"/> 默认恒为 true，于是同一项可以被无限重复购买，
/// 面板也无法显示「Lv 2/3」。
/// </para>
///
/// <para>
/// <b>为什么状态挂在实例上而不是 SO 上：</b>SO 是常驻资产（关闭 Domain Reload 时跨 Play 会话存活），
/// 在上面写任何运行时计数都会串局、串玩家。武器实例（<see cref="BaseWeapon"/>）是 MonoBehaviour，
/// 一实例一份状态 —— 这正是「每把武器有自己的升级」所要求的最小粒度：
/// 两个玩家各带一把 gun、或同一玩家两把 gun，升级互不影响。
/// </para>
///
/// <para>
/// <b>谁实现它：</b>武器实例（<see cref="BaseWeapon"/>）。玩家实体刻意**不实现** ——
/// 玩家的三选一升级走随机池，重复抽到同一项是设计允许的（见 PlayerUpgradeController 的说明）。
/// </para>
/// </summary>
public interface IUpgradeStateHolder
{
    /// <summary>该升级项已经应用在它身上的次数（没应用过返回 0）。</summary>
    int GetUpgradeLevel(LevelUpSO option);

    /// <summary>
    /// 记一次应用。<b>返回 false = 拒绝</b>（已达该项的上限，或参数非法）。
    ///
    /// <para>
    /// 上限判定在这里再查一遍（<see cref="LevelUpSO.ApplyTo"/> 之前已经用
    /// <see cref="LevelUpSO.IsAvailable"/> 筛过一次）：调用方绕过 IsAvailable 直接 ApplyTo 时，
    /// 这一步是最后一道闸 —— 否则"上限"只在 UI 上成立。
    /// </para>
    /// </summary>
    bool TryRecordUpgrade(LevelUpSO option);
}
