using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 玩家的网络状态载体 —— 挂在 Player prefab 的**根节点**（与 <c>NetworkIdentity</c> 同物体）。
///
/// <para>
/// <b>为什么单独一个组件而不是把 <c>PlayerController</c> 改成 <c>NetworkBehaviour</c>：</b>
/// <c>PlayerController : EntityBehaviour</c>，而 <c>EntityBehaviour</c> 是所有实体（敌人/塔/推车）
/// 的共同基类 —— 把它改成 <c>NetworkBehaviour</c> 会把三种实体一起拖下水，
/// 也会让"某个实体没挂 NetworkIdentity"从编译期问题变成运行时问题。C# 单继承下，
/// 用同物体上的一个 <c>NetworkBehaviour</c> 承载网络生命周期是代价最小的做法。
/// </para>
///
/// <para>
/// <b>它负责两件事：</b>
/// </para>
/// <list type="number">
/// <item><b>把"这个玩家是谁"同步下去</b>（<see cref="CharacterId"/>），并在客户端据它注入角色配置；</item>
/// <item><b>在正确的时机把玩家登记进 <c>PlayerManager</c></b> —— 联机对象的 <c>OnEnable</c>
/// 跑在 <c>SetActive(true)</c> 里，那时 <c>isLocalPlayer</c> **还没设置**
/// （<c>NetworkClient.cs:1151</c> vs <c>:1169</c>），在 <c>OnEnable</c> 里登记会把第一个注册的
/// 当成"本地玩家"，远程玩家先到时本地 HUD/相机就绑错了人。</item>
/// </list>
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkPlayerState : NetworkBehaviour
{
    /// <summary>
    /// 本玩家的角色 id（对应 <see cref="CharacterDefinitionSO.id"/>）。
    ///
    /// <para>
    /// 服务端在生成玩家**之前**写入，于是它随初始 <c>SpawnMessage</c> 的载荷一起下发 ——
    /// 客户端在 <c>ApplySpawnPayload</c> 里反序列化它时（<c>NetworkClient.cs:1186</c>）会触发 hook，
    /// 而那一刻 <c>Awake</c> 已经跑过、<c>Start</c> 还没跑，
    /// 正好是注入 <c>playerConfig</c> 的窗口（见 <see cref="OnCharacterIdChanged"/>）。
    /// </para>
    /// </summary>
    [SyncVar(hook = nameof(OnCharacterIdChanged))]
    [SerializeField] private string _characterId;

    /// <summary>本玩家的角色 id（服务端写入后同步；未设置时为空串）。</summary>
    public string CharacterId => _characterId;

    /// <summary>
    /// 本玩家的**权威血量**（服务端写，其余端读）。
    ///
    /// <para>
    /// <b>为什么血量必须同步：</b>敌人接触伤害从 P3 起就只在服务端结算，
    /// 客户端副本的血量自己永远不会变 —— 不补这一条的话，
    /// **客户端 HUD 血条一直是满的、角色也永远不会死**，而这一切完全静默。
    /// </para>
    ///
    /// <para>
    /// <c>-1</c> 是"还没写过"的哨兵值：它随初始 <c>SpawnMessage</c> 下发时，
    /// 客户端的 hook 会忽略它，等 <c>Start</c> 里那次 <c>RaiseHealthChanged</c> 把真值推过来。
    /// </para>
    /// </summary>
    [SyncVar(hook = nameof(OnSyncedHealthChanged))]
    [SerializeField] private float _syncedHealth = -1f;

    /// <summary>本玩家的权威血量（同步值；-1 表示尚未初始化）。</summary>
    public float SyncedHealth => _syncedHealth;

    /// <summary>
    /// 本玩家的**权威等级与经验**（服务端写，其余端读）。
    ///
    /// <para>
    /// <b>为什么必须同步：</b>联机时经验只在服务端累加（<c>EnemyHealthController.Die</c>
    /// 直接把经验给击杀者），客户端的 <c>AddExperience</c> 是空操作 ——
    /// 不补这一条的话**客户端永远停在 1 级、经验条不动、升级三选一永远不弹**。
    /// </para>
    ///
    /// <para><c>-1</c> 是"还没写过"的哨兵值，客户端会忽略它。</para>
    /// </summary>
    [SyncVar(hook = nameof(OnSyncedProgressChanged))]
    [SerializeField] private int _syncedLevel = -1;

    [SyncVar(hook = nameof(OnSyncedProgressChanged))]
    [SerializeField] private int _syncedExp = -1;

    /// <summary>本玩家的权威等级（同步值；-1 表示尚未初始化）。</summary>
    public int SyncedLevel => _syncedLevel;

    /// <summary>本玩家的权威经验（同步值；-1 表示尚未初始化）。</summary>
    public int SyncedExp => _syncedExp;

    /// <summary>
    /// 本玩家出场的武器 id 列表（逗号分隔）。
    ///
    /// <para>
    /// <b>为什么必须同步：</b>武器是**装配**上去的，而装配逻辑跑在服务端那份副本上 ——
    /// 客户端的每个玩家副本（**包括客户端自己的角色**）都不会自己装，
    /// 于是联机下**所有人都是空手的**：看得到人、看不到武器，客户端也开不了火。
    /// </para>
    ///
    /// <para>
    /// 用逗号分隔的字符串而不是 <c>SyncList&lt;string&gt;</c>：列表很短、只在生成时写一次，
    /// 而 <c>SyncList</c> 要处理初始化时序与增量同步两套语义。武器 id 里不含逗号
    ///（它们是 <c>AssetKeys</c> 风格的短标识）。
    /// </para>
    /// </summary>
    [SyncVar(hook = nameof(OnLoadoutChanged))]
    [SerializeField] private string _loadoutCsv;

    /// <summary>本玩家的武器 id 列表（逗号分隔；空串表示空手）。</summary>
    public string LoadoutCsv => _loadoutCsv;

    /// <summary>
    /// 本机的玩家对象（没有本地玩家时为 null）。
    ///
    /// <para>
    /// <c>[Command]</c> 只能由**自己拥有的**对象发出，所以客户端要上报任何东西
    /// （目前是伤害，见 <see cref="DamageRouter"/>）都得先找到自己的玩家。
    /// 在 <see cref="OnStartLocalPlayer"/> 里登记 —— 那是 <c>isLocalPlayer</c> 唯一被赋值的时刻。
    /// </para>
    /// </summary>
    public static NetworkPlayerState LocalSender { get; private set; }

    private PlayerController _player;
    private BaseHealthController _health;
    private ExperienceLevController _experience;
    private bool _warnedNoSpawner;
    private bool _warnedNoDefinition;

    private PlayerController Player
    {
        get
        {
            if (_player == null) _player = GetComponent<PlayerController>();
            return _player;
        }
    }

    // ── 服务端 ──

    /// <summary>
    /// 服务端写入本玩家的角色。**必须在生成之前调用**：初始载荷只发一次，
    /// 生成之后再改会走"变更同步"路径，客户端虽然也能收到，但注入时机就落到了激活之后。
    ///
    /// <para>
    /// ⚠️ <b>刻意不加 <c>[Server]</c> 特性：</b>Mirror 的 <c>[Server]</c> 会给方法体套一层
    /// <c>if (!isServer) return;</c>，而 <c>isServer</c> 来自 <c>netIdentity.isServer</c> ——
    /// 本方法恰恰要在 <c>AddPlayerForConnection</c>（也就是 spawn）**之前**调用，
    /// 那时 <c>isServer</c> 还是 false，加了特性就变成一个静默的空操作。
    /// </para>
    /// </summary>
    public void ServerSetCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return;

        _characterId = characterId;
    }

    /// <summary>
    /// 服务端写入本玩家的装备列表。与角色 id 一样**必须在生成之前调用**：
    /// 客户端的 <c>Start</c> 早于"生成后的变更同步"到达，
    /// 武器会在"已经 Start 过"之后才出现。
    /// </summary>
    public void ServerSetLoadout(IReadOnlyList<string> weaponIds)
    {
        if (weaponIds == null || weaponIds.Count == 0)
        {
            _loadoutCsv = string.Empty;
            return;
        }

        var builder = new System.Text.StringBuilder();

        for (int i = 0; i < weaponIds.Count; i++)
        {
            if (string.IsNullOrEmpty(weaponIds[i])) continue;
            if (builder.Length > 0) builder.Append(',');
            builder.Append(weaponIds[i]);
        }

        _loadoutCsv = builder.ToString();
    }

    /// <summary>
    /// 装备列表的 hook：客户端按它给这个玩家副本装上武器。
    ///
    /// <para>
    /// <b>两端都要装</b>：远程玩家要"看得见武器"，本地玩家要"真的能开火"。
    /// 远程副本的武器不会 tick（<c>GunWeapon.Start</c> 里的 <see cref="LocalPlayerGuard"/>
    /// 会挡掉），所以不存在"两端各打一次"。
    /// </para>
    /// </summary>
    private void OnLoadoutChanged(string oldValue, string newValue)
    {
        if (isServer) return;                                  // 服务端那份已经装过了
        if (string.IsNullOrEmpty(newValue)) return;

        PlayerSpawner spawner = PlayerSpawner.Current;
        if (spawner == null) return;

        PlayerController player = Player;
        if (player == null) return;

        _ = spawner.EquipLoadoutAsync(player, newValue.Split(','));
    }

    /// <summary>
    /// 客户端上报"我选了这个角色"。
    ///
    /// <para>
    /// <c>[Command]</c> 默认 <c>requiresAuthority = true</c>：只有这个玩家的拥有者调得动，
    /// 别人改不了他的职业。缺少权限时 Mirror 是**静默忽略**（客户端不报错），
    /// 所以调试时若"点了没反应"，先确认 <c>NetworkIdentity.isOwned</c>。
    /// </para>
    ///
    /// <para>
    /// <b>选角只改能力位，不改数值</b>：数值模型（<c>StatModel</c>）在对象生成时就建好了，
    /// 换角色要等下一次生成玩家才生效 —— 与 <c>PlayerSpawner.HandleCharacterChanged</c> 同一约定。
    /// </para>
    /// </summary>
    [Command]
    public void CmdSetCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return;

        NetworkSessionService.Service?.SetCharacter(connectionToClient.connectionId, characterId);

        PlayerSpawner spawner = PlayerSpawner.Current;
        CharacterDefinitionSO definition = spawner != null ? spawner.FindRole(characterId) : null;
        if (definition != null) PlayerSpawner.ApplyRole(gameObject, definition);

        // 写 SyncVar（会同步给其他客户端）。Host 模式下会本地立刻回调 hook，
        // 那里的 isServer 与 StatModel 判据保证不会重复注入。
        _characterId = characterId;
    }

    // ── 角色注入（客户端）──

    /// <summary>
    /// 角色 id 变化的 hook。
    ///
    /// <para>
    /// ⚠️ 它在客户端**初次同步时也会调用**（<c>oldValue</c> 是默认值），所以这里不能假设
    /// "这是一个变化"。服务端的对象也会走这个 hook（Host 模式下 Mirror 会在本地立即回调），
    /// 但服务端在生成前已经注入过，这里必须跳过 —— 否则 <c>SetEntityConfig</c> 会因为
    /// StatModel 已建立而拒绝并留一条告警。
    /// </para>
    /// </summary>
    private void OnCharacterIdChanged(string oldId, string newId)
    {
        if (string.IsNullOrEmpty(newId)) return;
        if (isServer) return;

        PlayerSpawner spawner = PlayerSpawner.Current;
        if (spawner == null)
        {
            // 逐帧路径之外，但仍按"只提示一次"处理：同一个玩家的远程副本会各报一次
            if (!_warnedNoSpawner)
            {
                _warnedNoSpawner = true;
                Debug.LogWarning($"[{nameof(NetworkPlayerState)}] 场景里没有 {nameof(PlayerSpawner)}，" +
                                 $"无法把角色「{newId}」解析成定义，玩家将以默认数值出场。");
            }

            return;
        }

        CharacterDefinitionSO definition = spawner.FindRole(newId);
        if (definition == null)
        {
            if (!_warnedNoDefinition)
            {
                _warnedNoDefinition = true;
                Debug.LogWarning($"[{nameof(NetworkPlayerState)}] 场景的角色列表里没有 id「{newId}」，" +
                                 "玩家将以默认数值出场。请检查 PlayerSpawner.AvailableRoles。");
            }

            return;
        }

        // 与 PlayerSpawner 的单机路径同一套注入（数值 + 能力位），顺序也必须一致：
        // 这里仍处于"已激活、尚未 Start"的窗口内，注入会被接受。
        //
        // ⚠️ 只在**数值模型还没建立**时注入：换角色（CmdSetCharacter 改 SyncVar）会让这个 hook
        // 再跑一次，那时 StatModel 已经建好，硬注入只会换来一条 SetEntityConfig 的拒绝告警。
        // 换角色只改能力位，数值等下次生成玩家才生效。
        var entity = GetComponent<EntityBehaviour>();
        if (entity != null && entity.StatModel == null)
            PlayerSpawner.InjectCharacterConfig(gameObject, definition);

        PlayerSpawner.ApplyRole(gameObject, definition);
    }

    // ── 生命周期：登记进 PlayerManager ──

    public override void OnStartServer()
    {
        // 服务端：玩家表 + 敌人目标表（敌人 AI 只在服务端选目标）
        Player?.RegisterSelf();

        // 服务端：把权威血量推给各端。订阅 HealthChanged 而不是在每个扣血点写 SyncVar ——
        // 扣血路径有好几条（接触伤害、投射物、将来的毒圈），漏一条就是"某种伤害客户端看不见"
        if (TryGetComponent(out BaseHealthController health))
        {
            _health = health;
            health.HealthChanged += OnServerHealthChanged;
        }

        // 服务端：把等级/经验推给各端。同样订阅事件而不是在每个加经验点写 SyncVar
        if (TryGetComponent(out ExperienceLevController experience))
        {
            _experience = experience;
            experience.OnExpChanged += OnServerProgressChanged;
            experience.OnLevelUp += OnServerProgressChanged;

            _syncedLevel = experience.CurrentLevel;
            _syncedExp = experience.CurrentExp;
        }
    }

    private void OnServerHealthChanged(float current, float max) => _syncedHealth = current;

    private void OnServerProgressChanged(int _) => SyncProgress();

    private void SyncProgress()
    {
        if (_experience == null) return;

        _syncedLevel = _experience.CurrentLevel;
        _syncedExp = _experience.CurrentExp;
    }

    /// <summary>
    /// 等级 / 经验同步的 hook。两个 SyncVar 共用它 —— 无论哪个变了都把两个值一起抄过去
    ///（它们本来就是一对：只抄一个会出现"等级涨了但经验条还是旧的"）。
    /// </summary>
    private void OnSyncedProgressChanged(int oldValue, int newValue)
    {
        if (isServer) return;
        if (_experience == null) TryGetComponent(out _experience);

        _experience?.ApplyNetworkProgress(_syncedLevel, _syncedExp);
    }

    /// <summary>
    /// 血量同步的 hook。服务端自己那份已经是权威值，跳过（否则会绕一圈回到 ApplyNetworkHealth）。
    /// </summary>
    private void OnSyncedHealthChanged(float oldValue, float newValue)
    {
        if (isServer) return;
        if (_health == null) TryGetComponent(out _health);

        _health?.ApplyNetworkHealth(newValue);
    }

    public override void OnStartClient()
    {
        // 客户端：只进玩家表。客户端不参与敌人 AI，不该往 EnemyTargetRegistry 里塞东西。
        // Register 内部按 isLocalPlayer 判定"谁是本地玩家"，所以远程玩家先到也不会绑错。
        PlayerManager.Service?.Register(Player);
    }

    /// <summary>
    /// 只有**拥有者**会收到这个回调（每个端最多一次）。
    /// 用它把"本机拥有"这件事告诉同物体上依赖本地输入的组件 —— 它们的 <c>Awake</c>
    /// 早于 <c>isLocalPlayer</c> 赋值，当时判不出来。
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        LocalSender = this;
        Player?.NotifyBecameLocalPlayer();
    }

    // ── 客户端上报的伤害 ──

    /// <summary>
    /// 客户端上报"我这一下打中了"。
    ///
    /// <para>
    /// <b>这里做的校验是形状校验，不是命中校验：</b>目标必须存在、必须是伤害目标、
    /// 不能是上报者自己、数值必须合理。它挡不住"这一枪其实没打中" ——
    /// 完整的服务端权威命中判定是 <c>Docs/MirrorPlan.md</c> 的 4.4，
    /// 本方法对应的是那条里的"上报"分支，取舍写在 <see cref="DamageRouter"/> 的类注释里。
    /// </para>
    /// </summary>
    [Command]
    public void CmdApplyDamage(uint targetNetId, float amount, float hitForce, byte source, uint attackerNetId)
    {
        if (!NetworkServer.active) return;
        if (targetNetId == 0u) return;

        // 不能打自己：否则"客户端上报"立刻变成自伤通道
        if (targetNetId == netId) return;

        if (!DamageRouter.TryResolveSpawned(targetNetId, out BaseHealthController health)) return;
        if (!DamageRouter.IsPlausible(amount, hitForce, out float safeHitForce)) return;

        // 玩家之间不互相伤害（合作模式）：目标是自己或别的玩家都丢掉。
        // 放在这里而不是让 PlayerHealthController 自己拒绝，是因为"谁能打玩家"是**规则**，
        // 规则应该只有一个地方说了算
        if (health is PlayerHealthController) return;

        DamageRouter.TryResolveSpawned(attackerNetId, out EntityBehaviour attacker);

        health.TakeDamage(new DamageInfo(amount, safeHitForce, attacker, (DamageSource)source));
    }

    public override void OnStopServer()
    {
        if (_health != null) _health.HealthChanged -= OnServerHealthChanged;

        if (_experience != null)
        {
            _experience.OnExpChanged -= OnServerProgressChanged;
            _experience.OnLevelUp -= OnServerProgressChanged;
        }

        Player?.UnregisterSelf();
    }

    public override void OnStopClient()
    {
        if (LocalSender == this) LocalSender = null;

        PlayerManager.Service?.Unregister(Player);
    }
}
