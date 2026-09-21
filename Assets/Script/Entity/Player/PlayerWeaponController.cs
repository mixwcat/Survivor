using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器槽配置（武器根节点 + 选择用 SO）。
/// </summary>
[System.Serializable]
public class WeaponSlot
{
    public GameObject weaponRoot;
    public WeaponSelectSO weaponSelectSO;
}

/// <summary>
/// 玩家武器控制器：按玩家持有武器槽与已激活武器，挂在 Player 上。
/// 武器根节点为玩家子物体；槽位通过子物体上的 <see cref="BaseWeapon"/> → <see cref="WeaponEntitySO.weaponSelect"/>
/// 自动建立，无需 Inspector 逐个拖拽。
/// 联机时每个玩家实例各自持有一份，互不影响。
/// </summary>
public class PlayerWeaponController : MonoBehaviour, IWeaponManager
{
    private readonly List<WeaponSlot> _slots = new List<WeaponSlot>();
    private readonly List<BaseWeapon> _weapons = new List<BaseWeapon>();

    public IReadOnlyList<WeaponSlot> WeaponSlots => _slots;
    public IReadOnlyList<BaseWeapon> Weapons => _weapons;

    private void Awake()
    {
        foreach (BaseWeapon weapon in GetComponentsInChildren<BaseWeapon>(true))
        {
            WeaponEntitySO entitySO = weapon.EntityConfig as WeaponEntitySO;
            _slots.Add(new WeaponSlot
            {
                weaponRoot = weapon.gameObject,
                weaponSelectSO = entitySO != null ? entitySO.weaponSelect : null
            });
        }
    }

    public void RegisterWeapon(BaseWeapon weapon)
    {
        if (weapon != null && !_weapons.Contains(weapon))
            _weapons.Add(weapon);
    }

    public void UnregisterWeapon(BaseWeapon weapon)
    {
        _weapons.Remove(weapon);
    }

    public T GetWeapon<T>() where T : BaseWeapon
    {
        foreach (BaseWeapon weapon in _weapons)
        {
            if (weapon is T typed) return typed;
        }
        return null;
    }

    public void SelectWeapon(WeaponSlot slot)
    {
        if (slot?.weaponRoot != null)
            slot.weaponRoot.SetActive(true);
    }
}
