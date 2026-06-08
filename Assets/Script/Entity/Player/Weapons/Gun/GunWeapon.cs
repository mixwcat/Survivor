using UnityEngine;
using System.Collections;

/// <summary>
/// 枪械武器
/// 所有数值从 StatModel 读取，不再持有独立字段
/// </summary>
public class GunWeapon : BaseWeapon
{
    [Header("鼠标旋转参数")]
    private Vector3 _mousePosition;
    private Vector3 _direction;
    private float _angle;

    [Header("枪械参数")]
    public Transform firePoint;

    [Header("输入系统")]
    private IInputHandle _inputHandle;

    private void Start()
    {
        _inputHandle = InputHandleFactory.CreateLocalInput();

        if (_inputHandle == null)
        {
            Debug.LogError("GunWeapon: Failed to create IInputHandle!");
        }

        StartCoroutine(GenerateBullet());
    }

    void Update()
    {
        RotateWeapon();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }

    private void RotateWeapon()
    {
        if (_inputHandle == null) return;

#if UNITY_STANDALONE_WIN
        // Windows: 使用鼠标位置计算方向
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(_inputHandle.ScreenPointerPosition);
        mouseWorld.z = 0;
        _direction = (mouseWorld - transform.position).normalized;
#elif UNITY_ANDROID
        // Android: 直接使用攻击摇杆方向
        _direction = _inputHandle.AttackDirectionInput;
        if (_direction.sqrMagnitude < 0.01f) return;
#endif

        _angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, _angle));
    }

    /// <summary>
    /// 协程生成子弹
    /// </summary>
    IEnumerator GenerateBullet()
    {
        while (PlayerManager.Instance.player != null)
        {
            float interval = GetStat(StatType.TowerAttackInterval);
            float damage = GetStat(StatType.BulletDamage);
            float hitForce = GetStat(StatType.BulletHitForce);

            Instantiate(Resources.Load<GameObject>("Weapon/Bullet"), firePoint.position, firePoint.rotation)
                .GetComponent<BulletController>()
                .Init((int)damage, (int)hitForce, _direction);

            BKMusic.Instance.PlaySound(ResourceEnum.PlayerShoot);
            yield return new WaitForSeconds(interval);
        }
    }
}
