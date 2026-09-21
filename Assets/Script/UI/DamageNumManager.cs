using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

[DefaultExecutionOrder(-100)]
public class DamageNumManager : MonoBehaviour, IDamageNumService
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IDamageNumService Service =>
        ServiceLocator.TryGet<IDamageNumService>(out var svc) ? svc : null;

    [Header("池")]
    [SerializeField] private List<DamageNumText> damageNumPool = new List<DamageNumText>();
    private DamageNumText damageNumToSpawn;

    private IAssetService _assetService;
    private GameObject _damageNumPrefab;
    private AsyncOperationHandle<GameObject> _damageNumPrefabHandle;

    private void Awake()
    {
        ServiceLocator.Register<IDamageNumService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IDamageNumService>();
    }

    private async void Start()
    {
        _assetService = ServiceLocator.Get<IAssetService>();
        _damageNumPrefabHandle = _assetService.LoadAssetAsync<GameObject>(AssetKeys.DamageNumText);
        _damageNumPrefab = await _damageNumPrefabHandle.Task;
    }


    public DamageNumText SpawnDamageNum(Vector3 position, float damage, DamageNumType type = DamageNumType.white)
    {
        DamageNumText text = GetFromPool();
        if (text == null)
        {
            Debug.LogError("DamageNumManager: Failed to spawn damage number. Prefab not loaded.");
            return null;
        }

        text.transform.position = position;
        text.GetComponent<DamageNumText>().SetUp((int)damage, type);

        return text;
    }


    /// <summary>
    /// 从池中获取一个伤害数字对象，没有则创建
    /// </summary>
    /// <returns></returns>
    public DamageNumText GetFromPool()
    {
        damageNumToSpawn = null;

        if (damageNumPool.Count == 0)
        {
            if (_damageNumPrefab == null)
            {
                Debug.LogError("DamageNumManager: DamageNumText prefab not loaded yet.");
                return null;
            }

            GameObject dmgNumObj = Instantiate(_damageNumPrefab, transform);
            damageNumToSpawn = dmgNumObj.GetComponent<DamageNumText>();
        }
        else
        {
            damageNumToSpawn = damageNumPool[0];
            damageNumPool.RemoveAt(0);
            damageNumToSpawn.gameObject.SetActive(true);
        }
        return damageNumToSpawn;
    }

    /// <summary>
    /// 归还伤害数字对象到池中
    /// </summary>
    /// <param name="dmgNum"></param>
    public void ReturnToPool(DamageNumText dmgNum)
    {
        dmgNum.gameObject.SetActive(false);

        damageNumPool.Add(dmgNum);
    }
}
