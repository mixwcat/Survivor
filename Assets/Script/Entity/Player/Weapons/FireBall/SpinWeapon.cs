using UnityEngine;
using System.Collections;

/// <summary>
/// 旋转火球武器
/// 所有数值从 StatModel 读取，不再持有独立字段
/// 监听 BulletSize 变化实时更新已有火球
/// </summary>
public class SpinWeapon : BaseWeapon
{
    public Transform fireBallHolder;

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
        StartCoroutine(GenerateFireball());
    }

    void Update()
    {
        float speed = GetStat(StatType.FireBallRotationSpeed);
        transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + speed * Time.deltaTime);
    }

    /// <summary>
    /// 监听数值变化：BulletSize 变化时更新已有火球
    /// </summary>
    private void OnAnyStatChanged(StatType type)
    {
        if (type == StatType.FireBallSize)
        {
            float size = GetStat(StatType.FireBallSize);
            foreach (Transform fireBall in fireBallHolder)
            {
                fireBall.localScale = new Vector3(size, size, 1);
            }
        }
    }

    /// <summary>
    /// 协程生成火球
    /// </summary>
    IEnumerator GenerateFireball()
    {
        while (PlayerManager.Instance.player != null)
        {
            float interval = GetStat(StatType.TowerAttackInterval);
            float lifeTime = GetStat(StatType.FireBallLifeTime);
            float size = GetStat(StatType.FireBallSize);

            FireBallController fireBall = Instantiate(
                Resources.Load<GameObject>("Weapon/FireBall"),
                fireBallHolder.position,
                Quaternion.identity
            ).GetComponent<FireBallController>();

            fireBall.transform.parent = fireBallHolder;
            fireBall.Init(lifeTime, size);

            yield return new WaitForSeconds(interval);
        }
    }
}
