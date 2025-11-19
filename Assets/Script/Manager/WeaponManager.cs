using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("单例模式")]
    private static WeaponManager instance;
    public static WeaponManager Instance => instance;
    void Awake()
    {
        instance = this;
    }

    public List<BaseWeapon> weapons = new List<BaseWeapon>();

    public void RegisterWeapon(BaseWeapon weapon)
    {
        if (!weapons.Contains(weapon))
        {
            weapons.Add(weapon);
        }
    }

    public void UnregisterWeapon(BaseWeapon weapon)
    {
        if (weapons.Contains(weapon))
        {
            weapons.Remove(weapon);
        }
    }

    public T GetWeapon<T>() where T : BaseWeapon
    {
        foreach (var weapon in weapons)
        {
            if (weapon is T)
            {
                return weapon as T;
            }
        }
        return null;
    }
}
