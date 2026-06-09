using UnityEngine;
using System.Collections;

/// <summary>
/// 旋转火球武器
/// 所有数值从 StatModel 读取，不再持有独立字段
/// 监听 BulletSize 变化实时更新已有旋转武器
/// </summary>
public class SpinWeapon : BaseWeapon
{
    public Transform SpinWeaponPosition;

    protected override void Awake()
    {
        base.Awake();
        if (StatModel != null)
            StatModel.OnStatChanged += OnAnyStatChanged;
    }

    protected virtual void OnDestroy()
    {
        if (StatModel != null)
            StatModel.OnStatChanged -= OnAnyStatChanged;
    }

    void Start()
    {
        StartCoroutine(GenerateSpinWeapon());
    }

    void Update()
    {
        float speed = GetStat(StatType.SpinWeaponRotationSpeed);
        transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + speed * Time.deltaTime);
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
    /// 协程生成旋转武器
    /// </summary>
    IEnumerator GenerateSpinWeapon()
    {
        while (PlayerManager.Instance.player != null)
        {
            float interval = GetAttackInterval();
            float lifeTime = GetStat(StatType.SpinWeaponLifeTime);
            float size = GetStat(StatType.SpinWeaponSize);
            int damage = (int)GetBaseDamage();
            float hitImpactForce = GetStat(StatType.HitPushForce);

            SpinWeaponController spinWeapon = Instantiate(
                Resources.Load<GameObject>("Weapon/Spin"),
                SpinWeaponPosition.position,
                Quaternion.identity
            ).GetComponent<SpinWeaponController>();

            spinWeapon.transform.parent = SpinWeaponPosition;
            spinWeapon.Init(lifeTime, size, damage, hitImpactForce);

            yield return new WaitForSeconds(interval + lifeTime); // 等待攻击间隔 + 旋转武器存在时间，确保不重叠
        }
    }
}
