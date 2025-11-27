using UnityEngine;

public class BaseWeapon : MonoBehaviour
{
    protected virtual void OnEnable()
    {
        WeaponManager.Instance.RegisterWeapon(this);
    }
    
    protected virtual void OnDisable()
    {
        WeaponManager.Instance.UnregisterWeapon(this);
    }
}
