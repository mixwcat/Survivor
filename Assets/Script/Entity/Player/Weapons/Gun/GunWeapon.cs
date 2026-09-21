using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

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
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = "local";
    private IInputHandle _inputHandle;

    private GameObject _bulletPrefab;
    private AsyncOperationHandle<GameObject> _bulletHandle;

    private async void Start()
    {
        _inputHandle = InputHandleFactory.GetInput(_inputHandleId);

        if (_inputHandle == null)
        {
            Debug.LogError("GunWeapon: Failed to create IInputHandle!");
        }

        _bulletHandle = ServiceLocator.Get<IAssetService>().LoadAssetAsync<GameObject>(AssetKeys.Bullet);
        _bulletPrefab = await _bulletHandle.Task;
    }

    private void OnDestroy()
    {
        if (_bulletHandle.IsValid())
            ServiceLocator.Get<IAssetService>().Release(_bulletHandle);
    }

    void Update()
    {
        RotateWeapon();

        if (TryFire())
        {
            SpawnBullet();
        }
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

        if (_direction.sqrMagnitude < 0.01f) _direction = transform.up; // 避免零向量导致的旋转问题
        _angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, _angle));
    }

    /// <summary>
    /// 生成一颗子弹
    /// </summary>
    private void SpawnBullet()
    {
        if (_bulletPrefab == null) return;

        int damage = (int)GetBaseDamage();
        float hitForce = GetStat(StatType.BulletHitForce);
        float speed = GetStat(StatType.BulletSpeed);

        Instantiate(_bulletPrefab, firePoint.position, firePoint.rotation)
            .GetComponent<BulletController>()
            .Init(damage, (int)hitForce, speed, _direction);

        AudioService.Service?.PlaySfx(ResourceEnum.PlayerShoot);
    }
}
