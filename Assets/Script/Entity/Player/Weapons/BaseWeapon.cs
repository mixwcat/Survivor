using UnityEngine;

/// <summary>
/// 武器基类
/// 继承 EntityBehaviour，持有 WeaponDataSO 来初始化 StatModel
/// </summary>
public class BaseWeapon : EntityBehaviour
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected virtual void OnEnable()
    {
        WeaponManager.Instance.RegisterWeapon(this);
    }

    protected virtual void OnDisable()
    {
        WeaponManager.Instance.UnregisterWeapon(this);
    }
}
