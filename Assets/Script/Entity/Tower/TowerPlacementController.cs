using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 塔放置幽灵 —— 拖动预览、网格吸附、确认/取消，以及**事务收尾**。
///
/// <para>
/// <b>它同时是一次放置事务的执行者</b>：付款在打开幽灵之前就已经发生（<c>ChooseTowerPanel</c>），
/// 而终结这次放置的路径有四条（确认、取消、加载失败、切场景销毁）。
/// 四条路径都必须走到同一个一次性状态上，否则要么丢点、要么重复退点。
/// 详见 <see cref="TowerPlacementTransaction"/>。
/// </para>
///
/// <para>
/// <b>句柄与实例必须成对归还</b>：塔 prefab 的 <c>LoadAssetAsync</c> 句柄、
/// 幽灵自身的 <c>InstantiateAsync</c> 实例，两者都在这里释放；
/// 场景切换会直接销毁幽灵、走不到 Confirm/Cancel，所以 <c>OnDestroy</c> 必须补一次。
/// </para>
/// </summary>
public class TowerPlacementController : MonoBehaviour
{
    /// <summary>
    /// 拖动跟随的刷新间隔（秒）。
    ///
    /// <para>
    /// 旧实现的计时条件是"累计超过 0.1 秒就 <c>return</c> 一次"——它只跳过了一帧，
    /// 其余帧照常全跑，注释里写的"0.1 秒更新一次"从未成立。
    /// 现在按真正的固定频率刷新：屏幕坐标换算与 Transform 写入是这里的主要开销，
    /// 30Hz 在拖动时已经看不出台阶，而输入轮询仍保持逐帧（否则会丢点击）。
    /// </para>
    /// </summary>
    private const float DragUpdateInterval = 1f / 30f;

    private List<Collider2D> invalidColliders = new List<Collider2D>();
    private bool canPlace = true;
    private TowerEntitySO currentTowerSO;
    private SpriteRenderer spriteRenderer;
    private IInputHandle _inputHandle;
    private int _placementCost;

    [Header("网格设置")]
    public float gridSize = 1f;
    private Vector2 gridOrigin = Vector2.zero;
    private HashSet<Vector2> occupiedCells = new HashSet<Vector2>();

    [Header("攻击范围显示")]
    private float attackRange = 2;
    private TowerRangeVisualizer _rangeVisualizer;
    private Vector3 lastTransformPosition = Vector3.zero;

    private Vector3 _lastTouchWorldPos = Vector3.zero;
    private bool _ownsInputHandle;
    private bool _ghostReleased;
    private float _dragTimer;

    /// <summary>
    /// 场景中正在进行的放置流程数量。
    ///
    /// <para>
    /// 移动端的交互提示靠"点世界里的提示"触发，而放置流程的点击也走世界指针 ——
    /// 两者同时响应会让"在已有塔附近放塔"顺手打开塔面板。计数由幽灵自身的
    /// <c>OnEnable</c>/<c>OnDisable</c> 维护（成对调用，不会漏减），
    /// 提示据此避让。
    /// </para>
    /// </summary>
    public static int ActiveCount { get; private set; }

    private void OnEnable()
    {
        ActiveCount++;
    }

    private void OnDisable()
    {
        if (ActiveCount > 0) ActiveCount--;
    }

    /// <summary>主摄像机缓存。拖动期间逐帧 <c>Camera.main</c> 是一次标签查找 + 一次 FindGameObjectsWithTag。</summary>
    private Camera _camera;

    /// <summary>本次放置的事务（付款人 + 一次性状态）。为 null 时不会退款。</summary>
    private TowerPlacementTransaction _transaction;

    /// <summary>塔 prefab 的加载句柄与结果（幽灵外观与最终放置共用），幽灵销毁时归还。</summary>
    private AsyncOperationHandle<GameObject> _towerPrefabHandle;
    private GameObject _towerPrefab;

    /// <summary>
    /// 初始化放置幽灵。塔 prefab 在这里**按需加载**（Addressables）——
    /// 旧实现用 <c>GameObject</c> 直接引用，读一下 sprite 就把整份 prefab 拉进了内存。
    ///
    /// <para>
    /// <b>失败时它已经自行收尾</b>（退款 + 归还幽灵），调用方不要再 <c>ReleaseInstance</c>：
    /// 幽灵的实例归还只有 <see cref="_ghostReleased"/> 一个判据，两处各还一次会让
    /// Addressables 的引用计数被多减一次。
    /// </para>
    /// </summary>
    /// <returns>加载成功返回 true；失败返回 false（此时本次放置事务已终结）。</returns>
    public async Task<bool> InitAsync(TowerEntitySO towerSO, TowerPlacementTransaction transaction,
                                      IInputHandle inputHandle = null)
    {
        _transaction = transaction;

        if (towerSO == null)
        {
            AbortPlacement("没有指定塔类型，放置已取消。");
            return false;
        }

        currentTowerSO = towerSO;
        _placementCost = transaction != null ? transaction.Cost : 0;
        spriteRenderer = GetComponent<SpriteRenderer>();

        IAssetService assetService = AssetService.Service;
        if (assetService == null || towerSO.prefab == null || !towerSO.prefab.RuntimeKeyIsValid())
        {
            AbortPlacement($"[TowerPlacementController] 塔「{towerSO.name}」没有可加载的 prefab，" +
                           "放置预览无法显示。请在 TowerEntitySO 上指定塔预制体。");
            return false;
        }

        _towerPrefabHandle = assetService.LoadAssetAsync<GameObject>(towerSO.prefab);
        _towerPrefab = await _towerPrefabHandle.Task;

        // await 期间幽灵可能已被销毁（切场景 / 取消），续体首行必须判宿主。
        // 此时无法再走 AbortPlacement（实例方法已不可用），只能就地归还句柄 + 退款
        if (this == null)
        {
            if (_towerPrefabHandle.IsValid()) assetService.Release(_towerPrefabHandle);
            transaction?.TryCancel();
            return false;
        }

        if (_towerPrefab == null)
        {
            AbortPlacement($"[TowerPlacementController] 塔 prefab 加载失败：{towerSO.name}");
            return false;
        }

        SpriteRenderer prefabRenderer = _towerPrefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && prefabRenderer != null)
            spriteRenderer.sprite = prefabRenderer.sprite;

        // 攻击范围属于**武器**（塔身上已经没有射程了）：预览阶段还没有武器实例，
        // 只能从"默认那把武器"的配置读 —— 与放置出来的塔实际用的圈一致。
        ResolveAttackRangeFromWeapon();

        // 支持外部注入输入源（联机模式下服务器可能传入虚拟输入）；
        // 自己取的句柄要自己还，否则引用计数只增不减
        if (inputHandle != null)
        {
            _inputHandle = inputHandle;
        }
        else
        {
            _inputHandle = InputHandleFactory.GetInput(InputHandleFactory.LocalId);
            _ownsInputHandle = _inputHandle != null;
        }

        if (_inputHandle == null)
        {
            Debug.LogError("TowerPlacementController: Failed to create IInputHandle!");
        }

        _rangeVisualizer = gameObject.AddComponent<TowerRangeVisualizer>();
        _rangeVisualizer.Show();
        _rangeVisualizer.Refresh(attackRange);

        // 事务进入 Ready：从这里开始，确认与取消才有意义
        transaction?.TryMarkReady();

#if UNITY_ANDROID
        UIService.Service?.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(true, this);
#endif

        return true;
    }

    /// <summary>
    /// 从塔 prefab 上「默认那把武器」的数据里取射程，用于放置预览的范围圈。
    ///
    /// <para>
    /// 射程是**武器**属性（<c>WeaponDataSO.AttackRange</c> → <c>StatType.AttackRange</c>），
    /// 塔的 DataSO 里已经不再持有它 —— 两份值并存时改错一个不会报错，
    /// 只会表现成"预览圈和实际索敌圈不一样"。
    /// </para>
    /// </summary>
    private void ResolveAttackRangeFromWeapon()
    {
        TowerWeaponController weapons = _towerPrefab != null
            ? _towerPrefab.GetComponent<TowerWeaponController>()
            : null;

        WeaponEntitySO config = weapons != null ? weapons.DefaultWeaponConfig : null;

        if (config != null && config.dataRef is TowerWeaponDataSO weaponData)
        {
            attackRange = weaponData.AttackRange;
            return;
        }

        Debug.LogWarning($"[TowerPlacementController] 塔「{(currentTowerSO != null ? currentTowerSO.name : "?")}」" +
                         $"没有可读射程的默认武器（TowerWeaponController.DefaultWeaponConfig 为空，" +
                         $"或它的 dataRef 不是 TowerWeaponDataSO），预览范围圈退回 {attackRange}。", this);
    }

    private void OnDestroy()
    {
        if (_ownsInputHandle)
        {
            InputHandleFactory.ReleaseInput(InputHandleFactory.LocalId);
            _ownsInputHandle = false;
        }

        // 外部销毁路径（拖动中途回菜单/重开/切场景）走不到 Confirm/Cancel：
        // ① 退款（事务未终结时）—— 否则就是"点扣了、塔没造出来"；
        // ② 归还塔 prefab 句柄与幽灵实例 —— 否则 bundle 永不卸载，且只在真机包暴露。
        // 成功确认过的事务已经 Committed，这里的 TryCancel 是空操作，不会把塔的钱退回去。
        _transaction?.TryCancel();
        ReleaseGhostIfNeeded();
    }

    /// <summary>
    /// 初始化失败的统一收尾：退款（一次性）→ 关掉移动端按钮 → 归还幽灵。
    /// 所有失败分支都走它，避免"某条路径忘了退款"。
    /// </summary>
    public void AbortPlacement(string reason)
    {
        if (!string.IsNullOrEmpty(reason))
            Debug.LogWarning(reason);

        _transaction?.TryCancel();

#if UNITY_ANDROID
        UIService.Service?.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(false);
#endif

        DestroyGhost();
    }

    /// <summary>
    /// 归还 Addressables 实例并销毁自身。
    /// 幽灵由 <c>InstantiateAsync</c> 创建，**直接 Destroy 不会递减引用计数**，
    /// 每放一次塔就会让源 prefab 及其依赖永久多留一份引用。
    /// </summary>
    private void DestroyGhost()
    {
        if (_ghostReleased) return;

        // 先置位：ReleaseInstance 会走销毁流程（Destroy 帧末生效并触发 OnDestroy），
        // 标记必须在调用前落下，避免 OnDestroy 的兜底路径重复归还同一个句柄
        _ghostReleased = true;

        ReleaseTowerPrefab();

        IAssetService assets = AssetService.Service;

        // 返回 false = 不是 Addressables 实例（未被跟踪、函数不会销毁它），退化为普通销毁
        if (assets == null || !assets.ReleaseInstance(gameObject))
            Destroy(gameObject);
    }

    /// <summary>外部销毁路径（如场景切换）的补归还，只在尚未归还时执行。</summary>
    private void ReleaseGhostIfNeeded()
    {
        if (_ghostReleased) return;
        _ghostReleased = true;

        ReleaseTowerPrefab();
        AssetService.Service?.ReleaseInstance(gameObject);
    }

    /// <summary>
    /// 归还塔 prefab 的句柄（与 <see cref="InitAsync"/> 的 LoadAssetAsync 成对）。
    /// 幽灵无论是「确认放置」「取消」还是「切场景被销毁」，都必须走到这里 ——
    /// 漏一条就是 bundle 永不卸载，而且只在真机包暴露。
    /// </summary>
    private void ReleaseTowerPrefab()
    {
        if (!_towerPrefabHandle.IsValid()) return;

        AssetService.Service?.Release(_towerPrefabHandle);
        _towerPrefabHandle = default;
        _towerPrefab = null;
    }

    private void Update()
    {
        // Init 是异步实例化后调用的，防止首帧早于 Init 运行时 _inputHandle 为空
        if (_inputHandle == null) return;

        // 事务已终结（确认/取消，幽灵正等帧末销毁）：不再处理任何输入。
        // 少了这一条，确认之后的同一帧里取消按钮仍会把状态改回去
        if (_transaction != null && _transaction.IsFinalized) return;

        // 输入必须**逐帧**轮询：PC 的"按下确认"只在按下的那一帧为真，
        // 和拖动跟随一起节流会丢点击（表现成"点了没反应"）
        bool hasPointer = _inputHandle.TryGetWorldPointer(out Vector2 screenPos, out bool isDown, out bool isUp);

#if UNITY_STANDALONE_WIN
        if (_inputHandle.HasCancelInput)
        {
            CancelPlacement();
            return;
        }

        if (hasPointer && isDown && canPlace)
        {
            // 先把幽灵对齐到指针当前位置再确认：拖动跟随是节流的，
            // 直接用 transform.position 会把塔放在最多 1/30 秒之前的位置上
            if (spriteRenderer != null) transform.position = ResolvePlacementPosition(screenPos);
            ConfirmPlacement();
            return;
        }
#endif

        // 位置跟随按固定频率刷新（屏幕→世界换算 + Transform 写入是主要开销）
        _dragTimer -= Time.deltaTime;
        if (_dragTimer > 0f) return;
        _dragTimer = DragUpdateInterval;

        if (hasPointer)
        {
            Vector3 worldPos = ResolvePlacementPosition(screenPos);

#if UNITY_ANDROID
            // 移动端是**相对拖动**：手指按住幽灵拖动，幽灵跟着位移
            if (_lastTouchWorldPos == Vector3.zero)
                _lastTouchWorldPos = worldPos;

            transform.position = GetGridCenter(transform.position + (worldPos - _lastTouchWorldPos));
            _lastTouchWorldPos = worldPos;
#else
            // 键鼠是**绝对定位**：幽灵直接跟到指针处
            transform.position = worldPos;
#endif
        }
        else
        {
            _lastTouchWorldPos = Vector3.zero;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = canPlace ? Color.green : Color.red;

        if (transform.position != lastTransformPosition)
        {
            lastTransformPosition = transform.position;
            _rangeVisualizer?.Refresh(attackRange);
        }
    }

    /// <summary>
    /// 屏幕坐标 → 世界坐标 → 网格中心。
    /// **平台差异只在这里**（相机由 <see cref="_camera"/> 缓存，不再逐帧 <c>Camera.main</c>）。
    /// </summary>
    private Vector3 ResolvePlacementPosition(Vector2 screenPos)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return transform.position;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        world.z = 0f;
        return GetGridCenter(world);
    }

    private Camera ResolveCamera()
    {
        // 缓存判据用"引用是否仍有效"而不是"是否查过"：切场景后旧相机已被销毁
        if (_camera != null) return _camera;

        _camera = Camera.main;
        return _camera;
    }

    /// <summary>
    /// 确认放置。**先提交事务再实例化** —— 提交是同步的，
    /// 同帧的第二次点击会因为状态已终结而被拒绝，从而只生成一座塔。
    /// </summary>
    public void ConfirmPlacement()
    {
        if (!canPlace) return;

        // 事务缺失（被别的入口直接调用）：不能放置 —— 没有付款人就没有归属，也无法退款
        if (_transaction == null)
        {
            Debug.LogError("[TowerPlacementController] 放置事务缺失，本次放置被拒绝。");
            return;
        }

        if (_towerPrefab == null)
        {
            // prefab 还没加载完（点得太快）：本次不放置，也**不终结事务** ——
            // 幽灵还在，玩家再点一次即可；直接终结会连带退款，而玩家并没有取消，只是点早了
            Debug.LogWarning("[TowerPlacementController] 塔 prefab 尚未加载完成，本次放置已忽略，请再点一次。");
            return;
        }

        // 提交在实例化之前：提交是同步的，同帧的第二次点击会因为状态已终结而被拒绝，
        // 从而只生成一座塔（退款同理只会发生一次）
        if (!_transaction.TryCommit())
        {
            // 已经确认或取消过：同帧双击、或取消后仍收到确认
            return;
        }

        GameObject tower = Instantiate(_towerPrefab, transform.position, Quaternion.identity);

        // 注入配置（与武器装配同理）：塔 prefab 里不必预先接好 entityConfig。
        // Instantiate 返回时 Awake 已经跑过，但 StatModel 只在「配置缺失」时才没建起来，
        // 所以这里的注入会即时补上（SetEntityConfig 内部会重新 InitStatModel）。
        BaseTower towerComponent = tower.GetComponent<BaseTower>();
        if (towerComponent != null)
            towerComponent.SetEntityConfig(currentTowerSO);
        else
            Debug.LogError($"[TowerPlacementController] 塔 prefab「{_towerPrefab.name}」上没有 BaseTower，" +
                           "该塔不会注册进敌人目标表（敌人不会打它）。");

        // 投入账本：记录谁建的、花了多少，供拆除时退款回投入者。
        // 运行时 AddComponent 而不是挂在 prefab 上 —— 它是每座塔独有的运行时状态，
        // 挂到 prefab 上会让人以为"塔的归属"是可以在 Inspector 里配的。
        // 归属取**事务记录的付款人**，不重新读 LocalPlayer：异步期间它可能已经变了
        TowerLedger ledger = tower.AddComponent<TowerLedger>();
        ledger.RecordBuild(_transaction.Payer, _transaction.Cost);

#if UNITY_ANDROID
        UIService.Service?.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(false);
#endif

        DestroyGhost();
    }

    /// <summary>
    /// 取消放置：退款（一次性，先判状态再退）→ 关按钮 → 归还幽灵。
    /// 加载失败与切场景也走同一套收尾，保证只退一次。
    /// </summary>
    public void CancelPlacement()
    {
        _transaction?.TryCancel();

#if UNITY_ANDROID
        UIService.Service?.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(false);
#endif

        DestroyGhost();
    }


    /// <summary>
    /// 网格对齐
    /// </summary>
    /// <param name="worldPosition"></param>
    /// <returns></returns>
    private Vector2 GetGridCenter(Vector3 worldPosition)
    {
        float x = Mathf.Floor((worldPosition.x - gridOrigin.x) / gridSize) * gridSize + gridSize / 2 + gridOrigin.x;
        float y = Mathf.Floor((worldPosition.y - gridOrigin.y) / gridSize) * gridSize + gridSize / 2 + gridOrigin.y;
        return new Vector2(x, y);
    }


    /// <summary>
    /// 放置阻挡的判据：只看三个**本体**层（玩家 / 敌人 / 塔）。
    ///
    /// <para>
    /// 以前用 tag 判断，等于把"哪些东西挡放置"与"实体的身份"绑在一起 ——
    /// 而挡不挡是**几何**问题。按层判断同时排除掉武器判定体、检测圈、交互区
    /// （它们在别的层上，本来也不该挡放置）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>必须懒解析</b>：<c>LayerMask.NameToLayer</c>/<c>GetMask</c> 在**类型初始化**
    /// （静态字段初始化器）与 MonoBehaviour 构造阶段调用会被 Unity 拒绝，抛
    /// <c>UnityException</c> 并让整个类型初始化失败 —— 表现是"点 UI 后无法拖动建造，
    /// 直接在鼠标位置落塔"，而且不指向真正的原因。
    /// </para>
    /// </summary>
    private static int BlockingMask
    {
        get
        {
            if (_blockingMask != 0) return _blockingMask;

            _blockingMask = LayerMask.GetMask("PlayerBody", "EnemyBody", "TowerBody");
            return _blockingMask;
        }
    }

    private static int _blockingMask;

    /// <summary>碰撞检测：与玩家、敌人、其他塔的本体重叠时禁止放置。</summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || (BlockingMask & (1 << other.gameObject.layer)) == 0) return;

        invalidColliders.Add(other);
        canPlace = false;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || (BlockingMask & (1 << other.gameObject.layer)) == 0) return;

        invalidColliders.Remove(other);
        if (invalidColliders.Count == 0)
        {
            canPlace = true;
        }
    }
}
