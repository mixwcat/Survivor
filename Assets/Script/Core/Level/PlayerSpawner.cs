using System.Collections.Generic;
using System.Threading.Tasks;
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

        // 注册由 PlayerController 自己在生命周期里完成，这里不重复注册
        await EquipLoadoutAsync(instance.GetComponent<PlayerController>());
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
    private static void InjectCharacterConfig(GameObject instance, CharacterDefinitionSO definition)
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
    private async Task EquipLoadoutAsync(PlayerController player)
    {
        if (player == null) return;

        IWeaponManager weapons = player.Weapons;
        if (weapons == null)
        {
            Debug.LogError("[PlayerSpawner] 玩家上没有 IWeaponManager，无法装配武器。");
            return;
        }

        IReadOnlyList<string> loadout = RunSessionService.Service?.Current.WeaponLoadoutIds;
        if (loadout == null || loadout.Count == 0)
        {
            // 大厅里玩家在选装备**之前**就已生成，空装备是正常状态，不是告警；
            // 只有"出行已锁定"（= 正在进关卡）还空着手才是配置错误
            if (RunSessionService.Service?.IsLocked == true)
            {
                Debug.LogWarning("[PlayerSpawner] 出行已锁定但没有装备列表，玩家将空手出场。");
            }
            return;
        }

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
            if (this == null) return;
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
    private static void ApplyRole(GameObject instance, CharacterDefinitionSO definition)
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
    /// </summary>
    private CharacterDefinitionSO FindRole(string characterId)
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
