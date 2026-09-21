using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 旋转火球武器
/// 所有数值从 StatModel 读取，不再持有独立字段
/// 监听 BulletSize 变化实时更新已有旋转武器
/// </summary>
public class SpinWeapon : BaseWeapon
{
    public Transform SpinWeaponPosition;

    private GameObject _spinPrefab;
    private AsyncOperationHandle<GameObject> _spinHandle;

    protected override void Awake()
    {
        base.Awake();
        if (StatModel != null)
            StatModel.OnStatChanged += OnAnyStatChanged;
    }

    private async void Start()
    {
        _spinHandle = ServiceLocator.Get<IAssetService>().LoadAssetAsync<GameObject>(AssetKeys.Spin);
        _spinPrefab = await _spinHandle.Task;
    }

    protected virtual void OnDestroy()
    {
        if (StatModel != null)
            StatModel.OnStatChanged -= OnAnyStatChanged;

        if (_spinHandle.IsValid())
            ServiceLocator.Get<IAssetService>().Release(_spinHandle);
    }

    /// <summary>
    /// 火球实际发射间隔 = 攻击间隔 + 火球存在时间，确保新旧火球不重叠。
    /// </summary>
    protected override float GetFireInterval()
    {
        return GetAttackInterval() + GetStat(StatType.SpinWeaponLifeTime);
    }

    void Update()
    {
        float speed = GetStat(StatType.SpinWeaponRotationSpeed);
        transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + speed * Time.deltaTime);

        if (TryFire())
        {
            SpawnSpinWeapon();
        }
    }

    /// <summary>
    /// 监听数值变化：BulletSize 变化时更新已有旋转武器
    /// </summary>
    private void OnAnyStatChanged(StatType type)
    {
        if (type == StatType.SpinWeaponSize)
        {
            float size = GetStat(StatType.SpinWeaponSize);
            foreach (Transform fireBall in SpinWeaponPosition)
            {
                fireBall.localScale = new Vector3(size, size, 1);
            }
        }
    }

    /// <summary>
    /// 生成一个旋转火球
    /// </summary>
    private void SpawnSpinWeapon()
    {
        if (_spinPrefab == null) return;

        float lifeTime = GetStat(StatType.SpinWeaponLifeTime);
        float size = GetStat(StatType.SpinWeaponSize);
        int damage = (int)GetBaseDamage();
        float hitImpactForce = GetStat(StatType.HitPushForce);

        SpinWeaponController spinWeapon = Instantiate(
            _spinPrefab,
            SpinWeaponPosition.position,
            Quaternion.identity
        ).GetComponent<SpinWeaponController>();

        spinWeapon.transform.SetParent(SpinWeaponPosition, false);
        spinWeapon.transform.position = SpinWeaponPosition.position;
        spinWeapon.Init(lifeTime, size, damage, hitImpactForce);
    }
}
