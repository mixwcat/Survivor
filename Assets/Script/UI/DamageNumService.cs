using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 伤害数字服务（场景级服务，由场景持有并自注册，不由组合根创建）。
/// 命名与全局服务保持一致：<c>IDamageNumService</c> ↔ <c>DamageNumService</c>，
/// 通过静态 <see cref="Service"/> 访问（未注册返回 null）。
/// </summary>
[DefaultExecutionOrder(-100)]
public class DamageNumService : MonoBehaviour, IDamageNumService
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IDamageNumService Service =>
        ServiceLocator.TryGet<IDamageNumService>(out var svc) ? svc : null;

    /// <summary>
    /// 池保留上限。依据：伤害数字是最高频的池化对象（每次命中一个），
    /// 但同时存活量被 <c>lifeTime</c>（1s）限制；40 足以覆盖密集命中的并发峰值，
    /// 又不会让一批带 TMP 的 GameObject 长期常驻（TMP 对象本身不便宜）。
    /// </summary>
    private const int MaxRetained = 40;

    private ObjectPool<DamageNumText> _pool;

    private IAssetService _assetService;
    private GameObject _damageNumPrefab;
    private AsyncOperationHandle<GameObject> _damageNumPrefabHandle;

    /// <summary>prefab 是否已经**确定**加载不出来（区别于"还在加载中"）。</summary>
    private bool _loadFailed;

    /// <summary>
    /// 不可用的告警位。命中是最高频的事件，而"prefab 没就绪"会让**每一次命中**
    /// 都走到同一条分支 —— 不节流的话，慢加载或配置错误会退化成持续刷日志
    /// （字符串 + 堆栈），主线程压力远高于伤害数字本身。
    /// </summary>
    private bool _warnedUnavailable;

    private void Awake()
    {
        ServiceLocator.Register<IDamageNumService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.UnregisterIfSelf<IDamageNumService>(this);

        // 本服务随场景销毁重建，句柄必须归还，否则每次重开关卡都多留一份引用
        if (_damageNumPrefabHandle.IsValid())
            _damageNumPrefabHandle.Release();
    }

    private async void Start()
    {
        _assetService = AssetService.Service;
        if (_assetService == null)
        {
            _loadFailed = true;
            Debug.LogError("[DamageNumService] IAssetService 未注册，伤害数字无法加载。");
            return;
        }

        AsyncOperationHandle<GameObject> handle = _assetService.LoadAssetAsync<GameObject>(AssetKeys.DamageNumText);
        GameObject prefab = await handle.Task;

        // 加载期间场景可能已卸载（此时 OnDestroy 已跑过，句柄没人接手）→ 就地归还
        if (this == null)
        {
            if (handle.IsValid()) handle.Release();
            return;
        }

        _damageNumPrefabHandle = handle;
        _damageNumPrefab = prefab;

        if (prefab == null)
        {
            _loadFailed = true;
            Debug.LogError($"[DamageNumService] 伤害数字 prefab 加载失败：{AssetKeys.DamageNumText}");
        }
    }


    public DamageNumText SpawnDamageNum(Vector3 position, float damage, DamageNumType type = DamageNumType.white)
    {
        DamageNumText text = GetFromPool();

        // 原因已经由 GetFromPool 说过一次（且只一次），这里不再补一条 ——
        // 原先两条日志一起打，密集命中时是双倍的字符串与堆栈开销
        if (text == null) return null;

        text.transform.position = position;
        text.SetUp((int)damage, type);

        return text;
    }


    /// <summary>
    /// 从池中获取一个伤害数字对象，没有则创建。
    /// 幂等归还、保留上限、跳过被销毁条目都由 <see cref="ObjectPool{T}"/> 统一处理。
    /// </summary>
    public DamageNumText GetFromPool()
    {
        if (_pool == null)
        {
            if (_damageNumPrefab == null)
            {
                WarnUnavailableOnce();
                return null;
            }

            _pool = new ObjectPool<DamageNumText>(CreateInstance, MaxRetained, "DamageNumPool");
        }

        return _pool.Get();
    }

    /// <summary>
    /// 不可用时的**一次性**告警，并区分"还在加载"与"加载失败" ——
    /// 前者等一会儿就好，后者需要改配置，两者混在一起会让人以为再等等就行。
    /// </summary>
    private void WarnUnavailableOnce()
    {
        if (_warnedUnavailable) return;

        _warnedUnavailable = true;

        if (_loadFailed)
        {
            Debug.LogError("[DamageNumService] 伤害数字 prefab 不可用，本局所有命中都不会显示伤害数字" +
                           "（只提示一次）。请检查 Addressables 的 UI/DamageNumText。");
            return;
        }

        Debug.LogWarning("[DamageNumService] 伤害数字 prefab 尚未加载完成，期间的命中不会显示伤害数字" +
                         "（只提示一次）。");
    }

    private DamageNumText CreateInstance()
    {
        GameObject dmgNumObj = Instantiate(_damageNumPrefab, transform);
        return dmgNumObj.GetComponent<DamageNumText>();
    }


    /// <summary>归还伤害数字对象到池中（幂等，由 <see cref="ObjectPool{T}"/> 保证）。</summary>
    public void ReturnToPool(DamageNumText dmgNum)
    {
        _pool?.Release(dmgNum);
    }
}
