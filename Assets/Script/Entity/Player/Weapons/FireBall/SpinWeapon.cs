using UnityEngine;
using System.Collections;
using UnityEngine.Events;
public class SpinWeapon : BaseWeapon
{
    public Transform fireBallHolder;
    [Header("初始化火球参数")]
    public float rotationSpeed = 360f;
    public float fireBallInterval = 5f;
    public float fireBallSize = 1f;
    [Header("订阅SO")]
    public LevelUpSO fireBallSpeedSO;
    public LevelUpSO fireBallLifeTimeSO;
    public LevelUpSO fireBallSizeSO;


    void Start()
    {
        StartCoroutine(GenerateFireball());
    }

    void Update()
    {
        // 旋转武器
        transform.rotation = Quaternion.Euler(0, 0, transform.rotation.eulerAngles.z + rotationSpeed * Time.deltaTime);
    }


    /// <summary>
    /// 携程生成火球
    /// </summary>
    /// <returns></returns>
    IEnumerator GenerateFireball()
    {
        while (PlayerManager.Instance.player != null)
        {
            // 生成
            FireBallController fireBall = Instantiate(Resources.Load<GameObject>("Weapon/FireBall"), fireBallHolder.position, Quaternion.identity).GetComponent<FireBallController>();
            // 设置父位
            fireBall.transform.parent = fireBallHolder;
            // 初始化
            fireBall.Init(fireBallInterval, fireBallSize);
            // 等待间隔
            yield return new WaitForSeconds(fireBallInterval);
        }
    }


    #region 升级选项
    /// <summary>
    /// 火球生成间隔升级
    /// </summary>
    public void FireBallLifeTimeUpgrade()
    {
        fireBallInterval += .5f;
    }

    /// <summary>
    /// 火球大小升级
    /// </summary>
    public void FireBallSizeUpgrade()
    {
        fireBallSize += 0.2f;
        foreach (Transform fireBall in fireBallHolder)
        {
            fireBall.localScale = new Vector3(fireBallSize, fireBallSize, 1);
        }
    }

    /// <summary>
    /// 火球旋转速度升级
    /// </summary>
    public void FireBallRotationSpeedUpgrade()
    {
        rotationSpeed += 60f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        fireBallSpeedSO.onLevelUp += FireBallRotationSpeedUpgrade;
        fireBallLifeTimeSO.onLevelUp += FireBallLifeTimeUpgrade;
        fireBallSizeSO.onLevelUp += FireBallSizeUpgrade;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        fireBallSpeedSO.onLevelUp -= FireBallRotationSpeedUpgrade;
        fireBallLifeTimeSO.onLevelUp -= FireBallLifeTimeUpgrade;
        fireBallSizeSO.onLevelUp -= FireBallSizeUpgrade;
    }
    #endregion
}
