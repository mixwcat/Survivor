using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人生成器 —— **生成表的执行器**，挂在场景里（`Level0` 的 `Spawner` 对象）。
///
/// <para>
/// <b>它自己从推车推导"现在该跑哪些规则"</b>，不需要外部喂状态：
/// </para>
/// <list type="bullet">
/// <item><b>停在节点</b>（充能点，且车停着）→ 跑该节点的规则；**该节点的怪全部刷完且全部清掉**后
/// 发 <see cref="NodeCleared"/>，由 <c>StageDirector</c> 决定让车继续；</item>
/// <item><b>在路上</b>（其余时候，含停摆）→ 跑当前弧长所在那一段的规则。</item>
/// </list>
/// <para>
/// 两种状态**互斥**：车停在节点时段规则不生效 —— 否则段规则会一直产出新怪，
/// "节点整体清完"就永远不成立，车会卡死在充能点。
/// </para>
///
/// <para>
/// <b>Boss 与普通小怪走两条生成路径，判据是 prefab 上的 <c>EnemyHealthController.UsePool</c>：</b>
/// 小怪走对象池（prefab 已预接 EntitySO）；Boss 不走池（<c>UsePool = false</c>），
/// 需要 <c>Instantiate</c> + 注入 EntitySO + 套哨站难度 —— 因为 Boss prefab 的
/// <c>entityConfig</c> 是**空的**（它是"内容"而不是"配置"，同一份 prefab 按关卡换数值）。
/// 以前这段逻辑在 <c>BossEncounter</c> 里，现在随"统一由表刷怪"一起搬到这里。
/// </para>
///
/// <para>
/// <b>随机源由外部注入</b>（<c>RunSession.seed</c> 建的 <c>System.Random</c>）：
/// <c>UnityEngine.Random</c> 是进程级全局状态，联机时各端各刷各的、也无法复现。
/// 权重抽取同样走这个 <c>rng</c>。
/// </para>
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    /// <summary>判定"车停在这个节点"的弧长容差（米）。车到点会停在节点距离上（误差远小于它）。</summary>
    private const float NodeTolerance = 0.75f;

    [Header("生成表")]
    [Tooltip("本关的生成规则（骨架由 CartRouteBaker 从路径烘焙生成）。为空 = 不生成任何敌人（启动期红错）")]
    public EnemySpawnTableSO Table;

    [Header("全局上限")]
    [Tooltip("场上敌人数量上限（**含 Boss**，读 GameLevelManager）。每一行自己的上限见生成表")]
    public int maxEnemies = 20;

    private System.Random _rng;
    private CartController _cart;

    private SegmentState[] _segments = Array.Empty<SegmentState>();
    private NodeState[] _nodes = Array.Empty<NodeState>();

    private bool _prepared;
    private bool _warnedNoRng;
    private int _activeNodeIndex = -1;
    private readonly HashSet<EnemyEntitySO> _warnedEntities = new HashSet<EnemyEntitySO>();

    /// <summary>当前停在哪个节点（-1 = 没停；下标对应 <see cref="EnemySpawnTableSO.Nodes"/>）。</summary>
    public int ActiveNodeIndex => _activeNodeIndex;

    /// <summary>
    /// 某个节点**刷完且清完**（参数：节点类型）。由 <c>StageDirector</c> 订阅后决定让车继续 ——
    /// 生成器只报告状态，不做"要不要走"的决策。
    /// </summary>
    public event Action<CartNodeKind> NodeCleared;

    /// <summary>一条规则的运行时状态（SO 上不放这些）。</summary>
    private sealed class RuleState
    {
        /// <summary>上一次求值时是否生效 —— 用于"离开生效范围就重新计时"。</summary>
        public bool Active;

        public float Accumulator;
        public float WaveTimer;

        /// <summary>本行**累计**生成过多少只（与 <see cref="SpawnRule.TotalCount"/> 比对）。</summary>
        public int SpawnedTotal;

        /// <summary>本行当前还在场上的实例（会顺手剪掉失效项）。</summary>
        public readonly List<EnemyController> Alive = new List<EnemyController>();
    }

    private sealed class SegmentState
    {
        public RuleState[] Rules = Array.Empty<RuleState>();
    }

    private sealed class NodeState
    {
        public RuleState[] Rules = Array.Empty<RuleState>();

        /// <summary>是否已经报过"清完"（一次性事件，别重复报）。</summary>
        public bool Reported;
    }

    /// <summary>
    /// 注入随机源与推车。由 <c>StageDirector</c> 在 Start 调用。
    /// 未注入前不生成 —— 宁可没有敌人，也不要各端各自随机。
    /// </summary>
    public void Configure(System.Random rng, CartController cart)
    {
        _rng = rng;
        _cart = cart;
    }

    private void Update()
    {
        EnsurePrepared();

        if (Table == null) return;

        if (_rng == null)
        {
            // 逐帧路径上的告警必须节流，否则会按帧数刷屏
            if (!_warnedNoRng)
            {
                _warnedNoRng = true;
                Debug.LogWarning($"[{nameof(EnemySpawner)}] 尚未注入随机源（Configure），敌人不会生成（只提示一次）。");
            }

            return;
        }

        float distance = _cart != null ? _cart.TravelledDistance : 0f;
        int globalAlive = GameLevelManager.Service?.GetEnemyCount() ?? 0;

        _activeNodeIndex = ResolveActiveNode(distance);

        if (_activeNodeIndex >= 0)
        {
            TickNode(_activeNodeIndex, globalAlive);
            return;      // 停在节点时**段规则不生效**（见类注释）
        }

        TickSegment(distance, globalAlive);
    }

    /// <summary>
    /// 车是否正停在某个节点上。判据是"没在移动 + 弧长贴着该节点" ——
    /// **不订阅到点事件**：订阅要处理顺序与退订，而这个状态本来就能从推车推出来。
    /// 终点不算（到终点即胜利，没有"停留期间"）。
    /// </summary>
    private int ResolveActiveNode(float distance)
    {
        if (_cart == null || _cart.IsMoving) return -1;

        for (int i = 0; i < _nodes.Length; i++)
        {
            CartNodeKind kind = Table.Nodes[i].Kind;

            if (kind == CartNodeKind.None || kind == CartNodeKind.Destination) continue;
            if (Mathf.Abs(distance - Table.Nodes[i].Distance) <= NodeTolerance) return i;
        }

        return -1;
    }

    private void TickSegment(float distance, int globalAlive)
    {
        int index = ResolveSegmentIndex(distance);

        for (int i = 0; i < _segments.Length; i++)
        {
            if (i == index) TickRules(Table.Segments[i].Rules, _segments[i].Rules, globalAlive);
            else ResetRules(_segments[i].Rules);
        }
    }

    /// <summary>弧长落在哪一段。边界归**后一段**（`[start, end)`），最后一段兜住右端。</summary>
    private int ResolveSegmentIndex(float distance)
    {
        for (int i = 0; i < _segments.Length; i++)
        {
            if (distance < Table.Segments[i].EndDistance) return i;
        }

        return _segments.Length - 1;
    }

    private void TickNode(int nodeIndex, int globalAlive)
    {
        NodeState state = _nodes[nodeIndex];

        TickRules(Table.Nodes[nodeIndex].Rules, state.Rules, globalAlive);

        if (state.Reported) return;

        // 放行条件：**该节点的规则全部刷完**（TotalCount 耗尽）且**场上一只不剩**。
        // 这也是"节点规则必须有限"的原因：有无限规则就永远清不完（启动期体检会红错）
        if (!AllExhausted(Table.Nodes[nodeIndex].Rules, state.Rules)) return;
        if (AliveCount(state.Rules) > 0) return;

        state.Reported = true;

        CartNodeKind kind = Table.Nodes[nodeIndex].Kind;
        Debug.Log($"[{nameof(EnemySpawner)}] 节点「{Table.Nodes[nodeIndex].Label}」已刷完并清空，报告放行。", this);
        NodeCleared?.Invoke(kind);
    }

    /// <summary>逐行求值（段与节点共用）。</summary>
    private void TickRules(SpawnRule[] rules, RuleState[] states, int globalAlive)
    {
        for (int i = 0; i < rules.Length; i++)
            TickRule(rules[i], states[i], globalAlive);
    }

    private void TickRule(in SpawnRule rule, RuleState state, int globalAlive)
    {
        if (!state.Active)
        {
            state.Active = true;
            state.WaveTimer = 0f;      // 进入生效范围立刻出第一波（见 SpawnRule.WaveInterval）
        }

        // 本行总量已耗尽：彻底停手（但**已经生成的怪还要等清掉**，那是节点门槛的事）
        if (rule.TotalCount > 0 && state.SpawnedTotal >= rule.TotalCount) return;

        int room = Room(rule, state, globalAlive);

        // 满员时本行**完全暂停**（连计时也停）：否则解封瞬间会攒出一波爆发
        if (room <= 0) return;

        if (rule.Mode == SpawnMode.Wave)
        {
            state.WaveTimer -= Time.deltaTime;
            if (state.WaveTimer > 0f) return;

            state.WaveTimer = rule.WaveInterval;

            int count = ClampToTotal(rule, state, Mathf.Min(rule.WaveCount, room));
            for (int i = 0; i < count; i++) SpawnOne(rule, state);

            return;
        }

        state.Accumulator += Time.deltaTime * rule.Rate;

        int budget = Mathf.Min(room, ClampToTotal(rule, state, int.MaxValue));

        while (state.Accumulator >= 1f && budget > 0)
        {
            state.Accumulator -= 1f;
            budget--;
            SpawnOne(rule, state);
        }
    }

    /// <summary>把一次生成量裁剪到"本行总量还剩多少"。不限量时原样返回。</summary>
    private static int ClampToTotal(in SpawnRule rule, RuleState state, int count)
    {
        if (rule.TotalCount <= 0) return count;

        int remaining = rule.TotalCount - state.SpawnedTotal;
        return remaining < count ? Mathf.Max(0, remaining) : count;
    }

    private void ResetRules(RuleState[] states)
    {
        for (int i = 0; i < states.Length; i++)
        {
            states[i].Active = false;
            states[i].Accumulator = 0f;
            states[i].WaveTimer = 0f;
        }
    }

    /// <summary>本行还能生成几只：全局上限与行上限共同决定（任一为 0 表示不限，取更紧的）。</summary>
    private int Room(in SpawnRule rule, RuleState state, int globalAlive)
    {
        int room = int.MaxValue;

        if (maxEnemies > 0) room = maxEnemies - globalAlive;
        if (room <= 0) return 0;

        if (rule.MaxAlive > 0) room = Mathf.Min(room, rule.MaxAlive - AliveCount(state.Alive));

        return room;
    }

    /// <summary>
    /// 一组规则里"还在场上"的数量。**两种消失方式都要算**：
    /// 池化归还 = 失活（对象还在），被边界回收 = 销毁（伪 null）。
    /// </summary>
    private static int AliveCount(RuleState[] states)
    {
        int count = 0;

        for (int i = 0; i < states.Length; i++)
            count += AliveCount(states[i].Alive);

        return count;
    }

    private static int AliveCount(List<EnemyController> alive)
    {
        int count = 0;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = alive[i];

            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                alive.RemoveAt(i);
                continue;
            }

            count++;
        }

        return count;
    }

    /// <summary>一组规则是否都已刷完（不限量的行永远算"没刷完" —— 体检会先把这种节点拦下来）。</summary>
    private static bool AllExhausted(SpawnRule[] rules, RuleState[] states)
    {
        for (int i = 0; i < rules.Length; i++)
        {
            if (rules[i].TotalCount <= 0) return false;
            if (states[i].SpawnedTotal < rules[i].TotalCount) return false;
        }

        return true;
    }

    /// <summary>
    /// 生成一只。**走不走对象池由 prefab 决定**（见类注释）：小怪走池，Boss 走
    /// <c>Instantiate</c> + 注入配置。
    /// </summary>
    private void SpawnOne(in SpawnRule rule, RuleState state)
    {
        EnemyEntitySO so = PickWeighted(rule.Entries);
        if (so == null || so.prefab == null) return;

        EnemyController enemy = Spawn(so, SelectSpawnPoint(rule));
        if (enemy == null) return;

        state.SpawnedTotal++;
        state.Alive.Add(enemy);
    }

    private EnemyController Spawn(EnemyEntitySO so, Vector3 position)
    {
        EnemyHealthController prefabHealth = so.prefab.GetComponent<EnemyHealthController>();
        if (prefabHealth == null)
        {
            WarnOncePerEntity(so, $"{so.name} 的 prefab「{so.prefab.name}」上没有 EnemyHealthController，" +
                                  "无法判定死活，已跳过。");
            return null;
        }

        if (prefabHealth.UsePool) return EnemyPool.Spawn(so.prefab, position, Quaternion.identity);

        return SpawnOutsidePool(so, position);
    }

    /// <summary>
    /// 非池化生成（Boss）：<c>Instantiate</c> → 注入 EntitySO → 套哨站难度。
    ///
    /// <para>
    /// 这段逻辑原先在 <c>BossEncounter</c> 里。**不能只 Instantiate 就完事**：
    /// Boss prefab 的 <c>entityConfig</c> 是空的，不注入的话 <c>StatModel</c> 建不起来，
    /// 所有数值走 <c>GetStat</c> 的默认值 1（Boss 会以 1 点血出场，一枪就死，且不报错）。
    /// 注入必须早于它的 Start —— 这里紧跟 <c>Instantiate</c>，满足。
    /// </para>
    /// </summary>
    private EnemyController SpawnOutsidePool(EnemyEntitySO so, Vector3 position)
    {
        GameObject instance = Instantiate(so.prefab, position, Quaternion.identity);

        EnemyController controller = instance.GetComponent<EnemyController>();
        if (controller == null)
        {
            WarnOncePerEntity(so, $"prefab「{so.prefab.name}」上没有 EnemyController，无法注入数值，已销毁该实例。");
            Destroy(instance);
            return null;
        }

        controller.SetEntityConfig(so);

        if (controller.StatModel == null)
        {
            WarnOncePerEntity(so, $"「{so.name}」的数值未能注入（EntitySO 的 dataRef 是否配置？），已销毁该实例。");
            Destroy(instance);
            return null;
        }

        // 非池化实例永远走不到 EnemyController.OnGetFromPool 那条注入路径：
        // 哨站难度必须在这里套，否则"小怪随哨站变强、Boss 还是老样子"
        OutpostDifficulty.Apply(controller.StatModel);

        return controller;
    }

    /// <summary>
    /// 按权重抽一种敌人。**走注入的 <see cref="System.Random"/>**：
    /// 同一 seed 在任意机器上给出同一串结果（联机与复现都靠它）。
    /// </summary>
    private EnemyEntitySO PickWeighted(SpawnEntry[] entries)
    {
        if (entries == null || entries.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Entity == null || entries[i].Weight <= 0f) continue;
            total += entries[i].Weight;
        }

        if (total <= 0f) return null;

        double roll = _rng.NextDouble() * total;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Entity == null || entries[i].Weight <= 0f) continue;

            roll -= entries[i].Weight;
            if (roll <= 0.0) return entries[i].Entity;
        }

        // 浮点兜底：落在最后一项（正常不会走到）
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            if (entries[i].Entity != null && entries[i].Weight > 0f) return entries[i].Entity;
        }

        return null;
    }

    /// <summary>
    /// 从锚点（推车）**前方堵截**或**后方追击**生成（<see cref="SpawnSide.Both"/> 时各 50%）。
    /// 用 <see cref="System.Random"/> 而不是 <c>UnityEngine.Random</c>：同一 seed 结果必须一致。
    /// </summary>
    private Vector3 SelectSpawnPoint(in SpawnRule rule)
    {
        Vector3 center = _cart != null ? _cart.transform.position : transform.position;

        bool fromFront;
        switch (rule.Side)
        {
            case SpawnSide.Front: fromFront = true; break;
            case SpawnSide.Back: fromFront = false; break;
            default: fromFront = _rng.Next(2) == 0; break;
        }

        float x = center.x + (fromFront ? rule.DistanceX : -rule.DistanceX);
        float y = center.y + (float)(_rng.NextDouble() * 2.0 - 1.0) * rule.RangeY;

        return new Vector3(x, y, 0f);
    }

    // ── 启动期体检 ──

    /// <summary>同一个敌人配置只抱怨一次（每次生成都刷屏没有意义）。</summary>
    private void WarnOncePerEntity(EnemyEntitySO so, string message)
    {
        if (so != null && !_warnedEntities.Add(so)) return;

        Debug.LogError($"[{nameof(EnemySpawner)}] {message}", this);
    }

    /// <summary>
    /// 建运行时状态并体检（**懒执行**，不赌 Awake/Start 顺序；每个实例只跑一次）。
    ///
    /// <para>
    /// 配置错误一律点名，而不是让它表现成"怪就是少"或"车卡在充能点不动" ——
    /// 后者尤其难查：玩家只会看到关卡停住了。
    /// </para>
    /// </summary>
    private void EnsurePrepared()
    {
        if (_prepared) return;
        _prepared = true;

        if (Table == null)
        {
            Debug.LogError($"[{nameof(EnemySpawner)}] 没有配置生成表（Table）—— 本关不会生成任何敌人。" +
                           "请对路径对象执行一次「烘焙到资产」。", this);
            return;
        }

        SpawnSegment[] segments = Table.Segments ?? Array.Empty<SpawnSegment>();
        SpawnNode[] nodes = Table.Nodes ?? Array.Empty<SpawnNode>();

        _segments = new SegmentState[segments.Length];
        for (int i = 0; i < segments.Length; i++)
            _segments[i] = new SegmentState { Rules = NewStates(segments[i].Rules) };

        _nodes = new NodeState[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
            _nodes[i] = new NodeState { Rules = NewStates(nodes[i].Rules) };

        if (segments.Length == 0 && nodes.Length == 0)
        {
            Debug.LogError($"[{nameof(EnemySpawner)}] 生成表「{Table.name}」里既没有段也没有节点 —— " +
                           "本关不会生成敌人。请对路径对象重新烘焙。", this);
            return;
        }

        for (int i = 0; i < segments.Length; i++)
            ValidateRules(segments[i].Rules, $"段 {i}（{segments[i].Label}）", false);

        for (int i = 0; i < nodes.Length; i++)
            ValidateNode(nodes[i], i);

        if (_cart == null)
        {
            Debug.LogWarning($"[{nameof(EnemySpawner)}] 还没注入推车（Configure）—— " +
                             "段与节点的判定都依赖推车的弧长，在推车就绪前不会生成。", this);
        }
    }

    private static RuleState[] NewStates(SpawnRule[] rules)
    {
        int count = rules != null ? rules.Length : 0;
        var states = new RuleState[count];

        for (int i = 0; i < count; i++)
            states[i] = new RuleState();

        return states;
    }

    private void ValidateNode(in SpawnNode node, int index)
    {
        string where = $"节点 {index}（{node.Label}）";
        bool hasRules = node.Rules != null && node.Rules.Length > 0;

        if (node.Kind == CartNodeKind.None)
        {
            if (hasRules)
            {
                Debug.LogError($"[{nameof(EnemySpawner)}] {where}：这是普通折点（Kind = None），" +
                               "车不会在这里停车 —— 它的规则永远不会生效。请把规则移到相邻的段上。", this);
            }

            return;
        }

        if (node.Kind == CartNodeKind.Destination)
        {
            if (hasRules)
            {
                Debug.LogWarning($"[{nameof(EnemySpawner)}] {where}：终点不会停留（到点即胜利），" +
                                 "它的规则永远不会生效。", this);
            }

            return;
        }

        if (!hasRules)
        {
            Debug.LogWarning($"[{nameof(EnemySpawner)}] {where}：没有配置任何规则 —— " +
                             "车到这里会立刻继续（没有东西要清）。", this);
            return;
        }

        ValidateRules(node.Rules, where, true);
    }

    private void ValidateRules(SpawnRule[] rules, string where, bool isNode)
    {
        if (rules == null) return;

        for (int i = 0; i < rules.Length; i++)
        {
            SpawnRule rule = rules[i];
            string at = $"{where} 的第 {i} 条规则";

            if (!HasUsableEntry(rule.Entries))
                Debug.LogError($"[{nameof(EnemySpawner)}] {at}：没有任何可用的敌人" +
                               "（要么 Entries 为空，要么全是空引用 / 权重 <= 0）—— 这一条不会生成。", this);

            if (rule.Mode == SpawnMode.Rate && rule.Rate <= 0f)
                Debug.LogError($"[{nameof(EnemySpawner)}] {at}：Rate 模式的速率为 {rule.Rate} —— 不会生成。", this);

            if (rule.Mode == SpawnMode.Wave)
            {
                if (rule.WaveCount <= 0)
                    Debug.LogError($"[{nameof(EnemySpawner)}] {at}：Wave 模式的每波数量为 {rule.WaveCount} —— " +
                                   "不会生成。", this);

                if (rule.WaveInterval <= 0f)
                    Debug.LogError($"[{nameof(EnemySpawner)}] {at}：Wave 模式的波间隔为 {rule.WaveInterval} —— " +
                                   "会变成每帧一波。", this);
            }

            // 放行条件是"刷完且清完"：节点上的无限规则会让车永远停在那里（关卡软锁）
            if (isNode && rule.TotalCount <= 0)
                Debug.LogError($"[{nameof(EnemySpawner)}] {at}：节点规则必须填 TotalCount（> 0）—— " +
                               "否则车会永远停在这个节点。", this);
        }
    }

    private static bool HasUsableEntry(SpawnEntry[] entries)
    {
        if (entries == null) return false;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Entity != null && entries[i].Weight > 0f) return true;
        }

        return false;
    }
}
