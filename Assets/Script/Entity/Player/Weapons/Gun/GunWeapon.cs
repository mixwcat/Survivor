using UnityEngine;
using System.Collections;

public class GunWeapon : BaseWeapon
{
    [Header("鼠标旋转参数")]
    private Vector3 mousePosition;
    private Vector3 direction;
    private float angle;

    [Header("枪械参数")]
    public Transform firePoint;
    public int bulletDamage = 8;
    public int bulletHitForce = 20;
    private float fireInterval = 1f;

    [Header("升级SO")]
    public LevelUpSO reduceFireIntervalSO;
    public LevelUpSO increaseBulletDamageSO;
    public LevelUpSO increaseBulletHitForceSO;

    private void Start()
    {
        StartCoroutine(GenerateBullet());
    }

    void Update()
    {
        RotateWithMouse();
    }


    private void ReduceFireInterval()
    {
        fireInterval = fireInterval * .9f;
    }
    private void IncreaseBulletDamage()
    {
        bulletDamage += 4;
    }
    private void IncreaseBulletHitForce()
    {
        bulletHitForce += 10;
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        reduceFireIntervalSO.onLevelUp += ReduceFireInterval;
        increaseBulletDamageSO.onLevelUp += IncreaseBulletDamage;
        increaseBulletHitForceSO.onLevelUp += IncreaseBulletHitForce;
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        reduceFireIntervalSO.onLevelUp -= ReduceFireInterval;
        increaseBulletDamageSO.onLevelUp -= IncreaseBulletDamage;
        increaseBulletHitForceSO.onLevelUp -= IncreaseBulletHitForce;
    }


    /// <summary>
    /// 根据鼠标位置旋转武器
    /// </summary>
    private void RotateWithMouse()
    {
        mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0; // 确保 Z 分量为 0
        direction = (mousePosition - transform.position).normalized;
        angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }


    /// <summary>
    /// 携程生成子弹
    /// </summary>
    /// <returns></returns>
    IEnumerator GenerateBullet()
    {
        while (PlayerManager.Instance.player != null)
        {
            // 生成子弹
            Instantiate(Resources.Load<GameObject>("Weapon/Bullet"), firePoint.position, firePoint.rotation).GetComponent<BulletController>().Init(bulletDamage, bulletHitForce, direction);
            // 播放音效
            BKMusic.Instance.PlaySound(ResourceEnum.PlayerShoot);
            // 等待间隔
            yield return new WaitForSeconds(fireInterval);
        }
    }
}
