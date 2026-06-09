using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WeaponSlot —— 武器槽配置
/// 新增武器时只需在 Inspector 的 weaponSlots 列表中加一条配置，无需修改代码
/// </summary>
[System.Serializable]
public class WeaponSlot
{
    public string weaponId;           // 标识，如 "FireBall", "Gun"
    public GameObject weaponRoot;     // 挂载点（初始 inactive）
    public WeaponSelectSO weaponSelectSO;  // 选择时触发的 SO
}

public class WeaponManager : MonoBehaviour
{
    [Header("单例模式")]
    private static WeaponManager instance;
    public static WeaponManager Instance => instance;
    void Awake()
    {
        instance = this;
    }

    [Header("武器槽列表")]
    [Tooltip("在 Inspector 中配置所有可用武器，无需修改代码即可新增武器")]
    public List<WeaponSlot> weaponSlots = new List<WeaponSlot>();

    [Header("已注册武器实例")]
    public List<BaseWeapon> weapons = new List<BaseWeapon>();

    private void OnEnable()
    {
        foreach (var slot in weaponSlots)
        {
            if (slot.weaponSelectSO != null)
            {
                // 用局部变量捕获，避免闭包陷阱
                var capturedSlot = slot;
                capturedSlot.weaponSelectSO.OnSelect += () => OnChooseWeapon(capturedSlot);
            }
        }
    }

    private void OnDisable()
    {
        foreach (var slot in weaponSlots)
        {
            if (slot.weaponSelectSO != null)
            {
                var capturedSlot = slot;
                capturedSlot.weaponSelectSO.OnSelect -= () => OnChooseWeapon(capturedSlot);
            }
        }
    }

    private void OnChooseWeapon(WeaponSlot slot)
    {
        if (slot.weaponRoot != null)
            slot.weaponRoot.SetActive(true);
    }

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

    /// <summary>
    /// 根据 weaponId 查找武器槽配置
    /// </summary>
    public WeaponSlot GetWeaponSlot(string weaponId)
    {
        return weaponSlots.Find(s => s.weaponId == weaponId);
    }
}
