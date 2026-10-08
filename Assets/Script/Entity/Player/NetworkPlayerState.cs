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

    private PlayerController _player;
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
        Player?.NotifyBecameLocalPlayer();
    }

    public override void OnStopServer() => Player?.UnregisterSelf();

    public override void OnStopClient()
    {
        PlayerManager.Service?.Unregister(Player);
    }
}
