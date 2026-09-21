using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Teto 塔 — 远程射击
/// 所有数值属性从 StatModel 读取
/// </summary>
public class Teto : BaseTower
{
    private GameObject _bulletPrefab;
    private AsyncOperationHandle<GameObject> _bulletHandle;

    protected override async void Start()
    {
        base.Start();
        _bulletHandle = ServiceLocator.Get<IAssetService>().LoadAssetAsync<GameObject>(AssetKeys.TetoBullet);
        _bulletPrefab = await _bulletHandle.Task;
    }

    protected override void OnDestroy()
    {
        if (_bulletHandle.IsValid())
            ServiceLocator.Get<IAssetService>().Release(_bulletHandle);
        base.OnDestroy();
    }

    protected override float GetOperateInterval()
    {
        return GetStat(StatType.AttackInterval);
    }

    protected override void OnOperate()
    {
        if (_bulletPrefab == null) return;

        Transform target = FindTarget();
        if (target == null) return;

        Vector3 direction = (target.position - transform.position).normalized;

        int damage = (int)GetStat(StatType.Damage);
        int hitForce = (int)GetStat(StatType.TowerHitForce);
        float speed = GetStat(StatType.BulletSpeed);

        Instantiate(_bulletPrefab, transform.position, Quaternion.identity)
            .GetComponent<TetoBulletController>()
            .Init(damage, hitForce, speed, direction);
    }
}
