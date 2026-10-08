using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 场景入口的玩家生成器 —— 按 <c>RunSession</c> 生成玩家，而不是把玩家摆在场景里。
///
/// <para>
/// <b>为什么必须生成而不是摆节点：</b>大厅里选的角色要带进关卡，而"场景里的固定玩家节点"
/// 只能有一份写死的配置 —— 要么两套场景各摆一个（改一处漏一处），
/// 要么在运行时改节点上的组件（把只读资产当配置用）。
/// 生成入口同时解决了未来 Mirror 的 <c>playerPrefab</c> 前置问题。
/// </para>
///
/// <para>
/// <b>幂等保护</b>：场景里已经有玩家时直接跳过 —— 迁移期间 Level0 仍保留着 Player 节点，
/// 没有这道保护会生成出第二个玩家（摄像机跟谁、升级面板显示谁都会变得不确定）。
/// </para>
///
/// <para>
/// 字段是 <c>public</c>（而不是 <c>[SerializeField] private</c>）：这是**场景配置组件**，
/// 字段本来就该在 Inspector 里配；而且脚本化接线时直接赋值比走 <c>SerializedObject</c> 可靠 ——
/// 后者对「数组元素的对象引用」的赋值实测不落盘（`arraySize` 写了、元素仍是 null）。
/// </para>
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    /// <summary>
    /// 当前场景的生成器（场景级单例，由 <c>OnEnable</c>/<c>OnDisable</c> 维护）。
    ///
    /// <para>
    /// <b>为什么需要一个进程可见的入口：</b>联机时"给某个连接生成玩家"的请求来自 Mirror 的回调
    /// （<c>SurvivorNetworkManager.OnServerReady</c>），那个回调只知道连接、不知道场景 ——
    /// 而出生点、角色候选表、玩家 prefab 都是**场景配置**。让回调去场景里找生成器，
    /// 比反过来让每个场景的生成器都去订阅连接事件要少一半分支。
    /// </para>
    ///
    /// <para>同一场景出现第二个生成器时以后注册的为准并告警 —— 与 <c>ManagerSingleton</c> 的重复实例同一类问题。</para>
    /// </summary>
    public static PlayerSpawner Current { get; private set; }

    /// <summary>联机出生点的环形散布槽位数（多个玩家不能在同一个点上叠着）。</summary>
    private const int SpawnRingSlots = 4;

    /// <summary>环形散布半径（米）。出生点周围空间不大，1.2 米足够分开且不会撞墙。</summary>
    private const float SpawnRingRadius = 1.2f;

    [Header("生成配置")]
    [Tooltip("出生点；留空则用本物体位置")]
    public Transform SpawnPoint;

    [Tooltip("玩家 prefab（Addressable，Common/Player）")]
    public AssetReferenceGameObject PlayerPrefab;

    [Header("角色")]
    [Tooltip("RunSession 没有选角色时用它。留空则用 prefab 上 PlayerRoleController 的默认角色")]
    public CharacterDefinitionSO FallbackRole;

    [Tooltip("按 RunSession.characterId 在这里查角色定义。与武器候选列表同模式：场景配置，" +
             "不做全局「id → 资产」注册表")]
    public List<CharacterDefinitionSO> AvailableRoles = new List<CharacterDefinitionSO>();

    private AsyncOperationHandle<GameObject> _prefabHandle;
    private IRunSessionService _session;

    /// <summary>单机路径生成出来的那个玩家实例；切到联机时要销毁它（生成权转交给服务端）。</summary>
    private GameObject _offlineInstance;

    private void OnEnable()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning($"[{nameof(PlayerSpawner)}] 场景中存在多个生成器，以后注册的「{name}」为准：" +
                             "另一个是「" + Current.name + "」。");
        }

        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }

    /// <summary>
    /// 销毁单机路径生成出来的玩家（如果有）。
    ///
    /// <para>
    /// 由 <see cref="NetworkBootstrap"/> 在建房/加入**之前**调用。理由是生成权会易主：
    /// 离线时是 <c>PlayerSpawner</c> 自己生成，联机后必须由服务端生成（要经过
    /// <c>AddPlayerForConnection</c> 才能成为"某个连接的玩家对象"）。
    /// 同一个玩家对象没法两者兼任 —— 它是在网络启动之前实例化的，Mirror 当它是普通克隆。
    /// </para>
    /// </summary>
    public void DiscardOfflinePlayer()
    {
        if (_offlineInstance == null) return;

        Debug.Log("[PlayerSpawner] 进入联机模式，销毁离线玩家实例（改由服务端生成）。");
        Destroy(_offlineInstance);
        _offlineInstance = null;
    }

    private async void Start()
    {
        // 等全局服务就绪：Addressables 与 PlayerProfile 都要能用。
        // 关键服务失败时不生成玩家 —— 生成出来也装不上武器，还会连带把失败原因埋掉
        if (!await GameBootstrap.TryWaitReadyAsync()) return;
        if (this == null) return;

        // 大厅里玩家在选角**之前**就已生成：之后换角色要同步到它身上，
        // 否则能力位停在生成时的回退角色（工程师被当成枪手，且不报错）
        _session = RunSessionService.Service;
        if (_session != null) _session.CharacterChanged += HandleCharacterChanged;

        // ── 联机：生成权归服务端 ──
        // 本组件退化为"出生点 + 角色候选表 + 装配序列"的提供者，由 SurvivorNetworkManager
        // 在 OnServerReady 里回调 SpawnForConnectionAsync。
        // 纯客户端在这里直接返回 —— 它的玩家副本由 Mirror 从 spawn 消息实例化，
        // 角色由 NetworkPlayerState 的 SyncVar hook 注入。
        if (NetworkBootstrap.IsActive) return;

        if (HasPlayerAlready())
        {
            Debug.Log("[PlayerSpawner] 场景里已经有玩家，跳过生成。");
            return;
        }

        Vector3 position = SpawnPoint != null ? SpawnPoint.position : transform.position;

        // 角色必须在**实例化之前**解析好：它的 playerConfig 要在玩家激活之前注入
        // （见 InjectCharacterConfig 的说明）
        CharacterDefinitionSO definition = ResolveDefinition();

        GameObject instance = await SpawnAsync(position);
        if (this == null || instance == null) return;

        InjectCharacterConfig(instance, definition);

        // prefab 根节点在资产里是未激活的：上面注入完配置才激活，
        // 这样 Awake/Start 看到的就是本职业的数值（血量上限、移速都不会先拿到兜底值）
        instance.SetActive(true);

        ApplyRole(instance, definition);

        _offlineInstance = instance;

        // 注册由 PlayerController 自己在生命周期里完成，这里不重复注册
        //
        // 单机/离线路径的装备来源是本机的 RunSession（联机那条读的是会话表）。
        // 空装备在这里是**正常状态**：大厅里玩家在选装备之前就已生成；
        // 只有"出行已锁定"（= 正在进关卡）还空着手才是配置错误
        IReadOnlyList<string> offlineLoadout = RunSessionService.Service?.Current.WeaponLoadoutIds;
        if ((offlineLoadout == null || offlineLoadout.Count == 0) &&
            RunSessionService.Service?.IsLocked == true)
        {
            Debug.LogWarning("[PlayerSpawner] 出行已锁定但没有装备列表，玩家将空手出场。");
        }

        await EquipLoadoutAsync(instance.GetComponent<PlayerController>(), offlineLoadout);
    }

    // ── 联机：服务端为一个连接生成玩家 ──

    /// <summary>
    /// 服务端为某个连接生成玩家对象并把它交给 Mirror。
    /// 由 <see cref="SurvivorNetworkManager.OnServerReady"/> 调用（那里已判过 <c>conn.identity</c>）。
    ///
    /// <para>
    /// <b>顺序不可颠倒：</b><c>Instantiate</c>（未激活）→ 注入数值与能力位 → 写角色 id
    /// → **自己** <c>SetActive(true)</c> → <c>AddPlayerForConnection</c>。
    /// <c>NetworkServer.Spawn</c> 内部也会 <c>SetActive(true)</c>（<c>NetworkServer.cs:1766</c>），
    /// 依赖它就等于"先激活、后注入" —— 那时 <c>EntityBehaviour.Awake</c> 已经按"没有配置"
    /// 失败过一次，数值会停在 1f 兜底值。
    /// </para>
    /// </summary>
    /// <returns>是否成功生成了玩家。</returns>
    public async Task<bool> SpawnForConnectionAsync(NetworkConnectionToClient conn)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning($"[{nameof(PlayerSpawner)}] 非服务端不能生成玩家（conn={conn?.connectionId}）。");
            return false;
        }

        if (conn == null) return false;

        // 伪 null 判断：切场景后旧玩家对象已销毁，但 conn.identity 仍指向它
        if (conn.identity != null) return false;

        if (!await GameBootstrap.TryWaitReadyAsync()) return false;
        if (this == null) return false;

        // 兜底：确保离线路径生成的那个玩家已经让位。
        // NetworkBootstrap 在建房/加入**之前**已经调过一次，但这里再调一次是幂等的，
        // 而且能盖住"网络在别处被启动"（例如直接调 StartHost）的情形 ——
        // 漏掉的后果是同一个大厅里同时存在一个离线玩家和一个网络玩家（两个人影，
        // 其中之一永远不联网），排查起来非常费劲。
        DiscardOfflinePlayer();

        CharacterDefinitionSO definition = ResolveNetworkDefinition(conn);

        GameObject instance = await SpawnAsync(ResolveSpawnPosition(conn));
        if (this == null || instance == null) return false;

        NetworkPlayerState state = instance.GetComponent<NetworkPlayerState>();
        if (state == null)
        {
            Debug.LogError($"[{nameof(PlayerSpawner)}] 玩家 prefab 上没有 {nameof(NetworkPlayerState)}，" +
                           "角色无法同步到客户端，已放弃生成该玩家。");
            Destroy(instance);
            return false;
        }

        // 1) 数值必须在**激活之前**注入：Awake 会按 prefab 上的 entityConfig（空）建一次 StatModel，
        //    先激活再注入就晚了（见 InjectCharacterConfig 的说明）
        InjectCharacterConfig(instance, definition);

        // 2) 自己激活：NetworkServer.Spawn 内部也会 SetActive（NetworkServer.cs:1766），
        //    但那样激活发生在 spawn 过程中，注入就已经晚了
        instance.SetActive(true);

        // 3) 能力位与激活顺序无关，放在激活之后与单机路径保持一致
        ApplyRole(instance, definition);

        // 4) 角色 id 必须在 **spawn 之前**写入 —— 它随初始 SpawnMessage 的载荷一起下发，
        //    客户端在 ApplySpawnPayload 反序列化它时触发 hook，那正是注入 playerConfig 的窗口。
        //
        //    ⚠️ 但**不能**放在 SetActive 之前：SyncVar 的赋值走 Weaver 生成属性
        //    （set_Network_characterId），它要用 NetworkBehaviour.netIdentity，
        //    而未激活的对象 Awake 还没跑、netIdentity 还是 null —— 直接抛 NullReferenceException。
        if (definition != null) state.ServerSetCharacter(definition.id);

        // 5) 装备列表同样在 spawn 之前写入（进初始载荷）。
        //
        //    客户端要靠它给自己这边的**每一个**玩家副本装上武器 ——
        //    不写的话联机下**所有人都是空手的**（包括客户端自己的角色，
        //    因为装配逻辑跑在服务端那份副本上）。
        //    放在初始载荷里而不是"生成后再同步"：客户端的 `Start` 早于变更同步到达，
        //    武器会在"已经 Start 过"之后才出现
        IReadOnlyList<string> loadout = ResolveNetworkLoadout(conn);
        state.ServerSetLoadout(loadout);

        // 6) AddPlayerForConnection 内部会 Spawn，并自动把这个连接标记为 ready
        if (!NetworkServer.AddPlayerForConnection(conn, instance))
        {
            Debug.LogError($"[{nameof(PlayerSpawner)}] AddPlayerForConnection 失败（conn={conn.connectionId}）。");
            Destroy(instance);
            return false;
        }

        await EquipLoadoutAsync(instance.GetComponent<PlayerController>(), loadout);
        return true;
    }

    /// <summary>联机路径的角色解析：优先读会话表（服务端权威），没有记录时用回退角色。</summary>
    private CharacterDefinitionSO ResolveNetworkDefinition(NetworkConnectionToClient conn)
    {
        INetworkSessionService session = NetworkSessionService.Service;

        if (session != null && session.TryGetCharacter(conn.connectionId, out string characterId))
        {
            CharacterDefinitionSO fromSession = FindRole(characterId);
            if (fromSession != null) return fromSession;

            Debug.LogWarning($"[{nameof(PlayerSpawner)}] 连接 {conn.connectionId} 的角色 id「{characterId}」" +
                             "不在可选角色列表里，使用回退角色。");
        }

        return FallbackRole;
    }

    /// <summary>
    /// 联机出生点：以配置的出生点为中心散在一个小圆周上。
    /// 多个玩家生成在同一个点上会被物理互相顶开，看起来像"进场瞬移"。
    /// </summary>
    private Vector3 ResolveSpawnPosition(NetworkConnectionToClient conn)
    {
        Vector3 origin = SpawnPoint != null ? SpawnPoint.position : transform.position;

        int slot = Mathf.Abs(conn.connectionId) % SpawnRingSlots;
        float angle = slot * Mathf.PI * 2f / SpawnRingSlots;

        return origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * SpawnRingRadius;
    }

    /// <summary>
    /// 联机路径的装备列表来源：会话表优先；**只有 Host 自己的连接**才回退到本机的
    /// <see cref="RunSessionService"/>（那是 Host 玩家在大厅里的选择）。
    /// 远程玩家的连接没有记录就是空手 —— 不能让 Host 的装备被套到别人身上。
    /// </summary>
    private static IReadOnlyList<string> ResolveNetworkLoadout(NetworkConnectionToClient conn)
    {
        INetworkSessionService session = NetworkSessionService.Service;
        IReadOnlyList<string> fromSession = session?.GetLoadout(conn.connectionId);

        if (fromSession != null && fromSession.Count > 0) return fromSession;

        bool isHostLocalConnection = NetworkServer.localConnection != null &&
                                     conn.connectionId == NetworkServer.localConnection.connectionId;

        if (isHostLocalConnection) return RunSessionService.Service?.Current.WeaponLoadoutIds;

        return null;
    }

    /// <summary>
    /// 解析本局角色：<see cref="IRunSessionService"/> 里选中的优先，否则用 Inspector 上的回退。
    /// </summary>
    private CharacterDefinitionSO ResolveDefinition()
    {
        CharacterDefinitionSO definition = FallbackRole;
        string characterId = RunSessionService.Service?.Current.CharacterId;

        if (!string.IsNullOrEmpty(characterId))
        {
            CharacterDefinitionSO fromSession = FindRole(characterId);
            if (fromSession != null) definition = fromSession;
            else Debug.LogWarning($"[PlayerSpawner] RunSession 里的角色 id「{characterId}」不在可选角色列表里，使用回退角色。");
        }

        return definition;
    }

    /// <summary>
    /// 注入职业配置（数值 + 升级池）。
    ///
    /// <para>
    /// <b>为什么必须在激活之前：</b>Player prefab 的根节点在资产里是**未激活**的，
    /// 所以 <c>Instantiate</c> 之后 <c>Awake</c> 还没跑 —— 这里注入的
    /// <see cref="CharacterDefinitionSO.playerConfig"/> 会成为玩家 StatModel 的唯一来源。
    /// 反过来（先激活再注入）会踩 <see cref="EntityBehaviour.SetEntityConfig"/> 的拒绝分支：
    /// 那时 StatModel 已经按"没有配置"失败过一次，血量上限/移速都已经拿过 1f 兜底值。
    /// </para>
    /// </summary>
    internal static void InjectCharacterConfig(GameObject instance, CharacterDefinitionSO definition)
    {
        var entity = instance.GetComponent<EntityBehaviour>();
        if (entity == null)
        {
            Debug.LogError("[PlayerSpawner] 玩家 prefab 上没有 EntityBehaviour，无法注入职业配置。");
            return;
        }

        if (definition == null || definition.playerConfig == null)
        {
            Debug.LogError("[PlayerSpawner] 角色" +
                           (definition != null ? $"「{definition.id}」" : "未解析到") +
                           "没有配置 PlayerEntitySO，玩家将以默认数值（全 1）出场。" +
                           "请检查角色 SO 的 playerConfig。");
            return;
        }

        if (!entity.SetEntityConfig(definition.playerConfig))
        {
            Debug.LogWarning($"[PlayerSpawner] 职业配置「{definition.playerConfig.id}」注入失败，" +
                             "玩家将使用 prefab 上预接的配置。");
        }
    }

    /// <summary>
    /// 按 <see cref="RunSession"/> 的装备列表装配武器。
    ///
    /// <para>
    /// <b>为什么在这里而不是让玩家自己装配：</b>"带哪几把出门"是**大厅的决策**（RunSession），
    /// 玩家 prefab 只是执行者。让玩家组件去读 RunSession 会让它同时依赖大厅与档案。
    /// </para>
    ///
    /// <para>
    /// <b>三道拒绝：重复 id / 未解锁 / 候选列表里没有</b>，每一种都留明确日志 ——
    /// 静默跳过会让"大厅里配了两把、进关卡只有一把"变成无从定位的问题。
    /// </para>
    /// </summary>
    private async Task EquipLoadoutAsync(PlayerController player, NetworkConnectionToClient conn)
    {
        // 联机时装备来源是**会话表**（每个连接各自的），不能读本机的 RunSession ——
        // 那是 Host 玩家自己的大厅选择，套到远程玩家身上会让所有人带同一套武器
        await EquipLoadoutAsync(player, ResolveNetworkLoadout(conn));
    }

    /// <summary>
    /// 按给定的武器 id 列表装配。
    ///
    /// <para>
    /// <b>服务端与客户端共用</b>：服务端用它给每个连接生成的角色装配；
    /// 客户端用它给**本地副本**装配（见 <c>NetworkPlayerState</c> 的装备同步 hook）——
    /// 联机下客户端的每个玩家副本都得自己装一遍，否则所有人都是空手的。
    /// </para>
    ///
    /// <para>
    /// 客户端装上之后**只有自己的那把会真的开火**：远程副本的武器在
    /// <c>GunWeapon.Start</c> 里就被 <see cref="LocalPlayerGuard"/> 挡掉了（不 tick），
    /// 服务端那份同理 —— 所以不存在"两端各打一次"。
    /// </para>
    /// </summary>
    public async Task EquipLoadoutAsync(PlayerController player, IReadOnlyList<string> loadout)
    {
        if (player == null) return;

        IWeaponManager weapons = player.Weapons;
        if (weapons == null)
        {
            Debug.LogError("[PlayerSpawner] 玩家上没有 IWeaponManager，无法装配武器。");
            return;
        }

        if (loadout == null || loadout.Count == 0) return;

        var equipped = new HashSet<string>();

        for (int i = 0; i < loadout.Count; i++)
        {
            string id = loadout[i];
            if (string.IsNullOrEmpty(id)) continue;

            if (!equipped.Add(id))
            {
                Debug.LogWarning($"[PlayerSpawner] 装备列表里有重复的武器 id「{id}」，已跳过。");
                continue;
            }

            // 可用性问 UnlockPolicy（当前"默认全都拥有"）：这里若只看档案的解锁位，
            // 会出现"大厅里装上了、关卡里没有"，而且不报错
            if (!UnlockPolicy.IsWeaponAvailable(PlayerProfileService.Service, id))
            {
                Debug.LogWarning($"[PlayerSpawner] 武器「{id}」未解锁，拒绝装配。");
                continue;
            }

            WeaponSlot slot = FindWeaponSlot(weapons, id);
            if (slot == null)
            {
                Debug.LogWarning($"[PlayerSpawner] 玩家候选列表里没有武器「{id}」，无法装配。" +
                                 "（武器候选配在玩家 prefab 上）");
                continue;
            }

            await weapons.EquipAsync(slot);

            // await 期间可能已切场景
            if (this == null || player == null) return;
        }
    }

    private static WeaponSlot FindWeaponSlot(IWeaponManager weapons, string id)
    {
        IReadOnlyList<WeaponSlot> slots = weapons.WeaponSlots;
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot?.Config != null && slot.Config.id == id) return slot;
        }

        return null;
    }

    private static bool HasPlayerAlready()
    {
        IPlayerManager players = PlayerManager.Service;
        return players != null && players.AllPlayers.Count > 0;
    }

    private async Task<GameObject> SpawnAsync(Vector3 position)
    {
        IAssetService assets = AssetService.Service;
        if (assets == null)
        {
            Debug.LogError("[PlayerSpawner] IAssetService 未注册，无法生成玩家。");
            return null;
        }

        if (PlayerPrefab == null || !PlayerPrefab.RuntimeKeyIsValid())
        {
            Debug.LogError("[PlayerSpawner] 未配置玩家 prefab，无法生成玩家。");
            return null;
        }

        // 句柄要持有到场景结束：实例依赖 prefab，提前 Release 可能让资源被卸载
        _prefabHandle = assets.LoadAssetAsync<GameObject>(PlayerPrefab);
        GameObject prefab = await _prefabHandle.Task;

        if (this == null)
        {
            if (_prefabHandle.IsValid()) assets.Release(_prefabHandle);
            return null;
        }

        if (prefab == null)
        {
            Debug.LogError("[PlayerSpawner] 玩家 prefab 加载失败。");
            if (_prefabHandle.IsValid()) assets.Release(_prefabHandle);
            return null;
        }

        return Instantiate(prefab, position, Quaternion.identity);
    }

    /// <summary>把已解析的角色写进玩家的 <see cref="PlayerRoleController"/>（能力位）。</summary>
    internal static void ApplyRole(GameObject instance, CharacterDefinitionSO definition)
    {
        PlayerRoleController role = instance.GetComponent<PlayerRoleController>();
        if (role == null)
        {
            Debug.LogWarning("[PlayerSpawner] 玩家 prefab 上没有 PlayerRoleController，角色能力将不可用。");
            return;
        }

        if (definition != null) role.SetDefinition(definition);
    }

    /// <summary>
    /// 按稳定 id 在**场景配置的**角色列表里查定义。
    /// 刻意不做全局注册表：那会重新引入「id → 资产」的反查，
    /// 而本工程的做法是"谁需要就自己序列化引用"（与武器的 `_candidates` 一致）。
    ///
    /// <para>
    /// <c>public</c> 是因为联机时客户端也要用同一套解析：它的玩家副本没有经过服务端的
    /// 装配流程，只能靠 <see cref="NetworkPlayerState"/> 的 <c>SyncVar</c> hook 拿到角色 id 后
    /// 回来查这张表（同一个 id 必须在两端解析成同一份定义）。
    /// </para>
    /// </summary>
    public CharacterDefinitionSO FindRole(string characterId)
    {
        for (int i = 0; i < AvailableRoles.Count; i++)
        {
            CharacterDefinitionSO candidate = AvailableRoles[i];
            if (candidate != null && candidate.id == characterId) return candidate;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (_session != null)
        {
            _session.CharacterChanged -= HandleCharacterChanged;
            _session = null;
        }

        if (_prefabHandle.IsValid())
            AssetService.Service?.Release(_prefabHandle);
    }

    /// <summary>
    /// 角色变更 → 同步到**已生成的**玩家身上。
    ///
    /// <para>
    /// 只写 RunSession 是不够的：大厅的玩家实例在选角之前就存在，
    /// 它的 <see cref="PlayerRoleController"/> 会一直停在生成时的回退角色上 ——
    /// 于是"切到工程师"之后，塔台提示不出现、武器台仍按枪手放行，
    /// 而这一切都没有任何报错（能力位是个看起来正常的错值）。
    /// </para>
    ///
    /// <para>
    /// <b>只更新能力位，不重注入数值</b>：StatModel 已经建好，
    /// <see cref="EntityBehaviour.SetEntityConfig"/> 按设计拒绝换配置（子类订阅指向旧模型）。
    /// 所以大厅里切角色后，<b>数值要等下次生成玩家才生效</b> —— 要立刻生效得重建玩家实例。
    /// </para>
    /// </summary>
    private void HandleCharacterChanged(CharacterDefinitionSO definition)
    {
        if (definition == null) return;

        PlayerController player = PlayerManager.Service?.LocalPlayer;
        PlayerRoleController role = player != null ? player.Role : null;
        if (role == null) return;

        role.SetDefinition(definition);
    }
}
