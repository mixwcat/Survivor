using System.Collections.Generic;

/// <summary>
/// 武器注册表服务，按玩家实例化（由 <see cref="PlayerWeaponController"/> 实现）。
/// 每个玩家持有自己的武器槽与已激活武器。
/// </summary>
public interface IWeaponManager
{
    /// <summary>可选择的武器槽配置（初始 inactive）</summary>
    IReadOnlyList<WeaponSlot> WeaponSlots { get; }

    /// <summary>已激活的武器实例</summary>
    IReadOnlyList<BaseWeapon> Weapons { get; }

    void RegisterWeapon(BaseWeapon weapon);
    void UnregisterWeapon(BaseWeapon weapon);

    /// <summary>按类型取第一个已注册武器，不存在返回 null</summary>
    T GetWeapon<T>() where T : BaseWeapon;

    /// <summary>激活指定武器槽对应的武器。</summary>
    void SelectWeapon(WeaponSlot slot);
}
