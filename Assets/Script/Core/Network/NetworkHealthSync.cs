using Mirror;
using UnityEngine;

/// <summary>
/// 把实体的权威血量同步给各端。挂在**有 <c>NetworkIdentity</c> 的实体**上
/// （玩家、塔）。放在根节点、与 <see cref="BaseHealthController"/> 同物体。
///
/// <para>
/// <b>为什么是独立组件而不是把血量塞进各自的网络状态类：</b>
/// 血量是**每种可受损实体都要的**东西，而"网络状态类"是每种实体各不相同的
///（玩家有角色/装备/等级，塔有归属与升级）。
/// 把它抽出来之后，新增一种可受损实体只要挂上本组件 + 让它的
/// <see cref="BaseHealthController"/> 声明 <c>IsHealthSynced</c> 即可。
/// </para>
///
/// <para>
/// <b>为什么必须同步：</b>伤害是**服务端结算**的（见 <see cref="DamageRouter"/>），
/// 客户端副本的血量自己永远不会变 —— 不同步的话客户端看到的是"血条一直是满的、
/// 也永远不会死"，而且完全静默。
/// </para>
///
/// <para>
/// ⚠️ <b>被同步的实体必须让 <c>BaseHealthController.IsHealthSynced</c> 返回 true</b>：
/// SyncVar 的 hook 跑在 <c>Awake</c> 之后、<c>Start</c> <b>之前</b>，
/// 而 <c>Start</c> 里那句 <c>CurrentHealth = MaxHealth</c> 会把刚同步下来的值抹掉。
/// </para>
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkHealthSync : NetworkBehaviour
{
    /// <summary>
    /// 权威血量。<c>-1</c> 是"还没写过"的哨兵值：它随初始 <c>SpawnMessage</c> 下发时，
    /// 客户端的 hook 会忽略它，等 <c>Start</c> 里那次 <c>RaiseHealthChanged</c> 把真值推过来。
    /// </summary>
    [SyncVar(hook = nameof(OnHealthChanged))]
    [SerializeField] private float _health = -1f;

    /// <summary>权威血量（同步值；-1 表示尚未初始化）。</summary>
    public float SyncedHealth => _health;

    private BaseHealthController _target;

    public override void OnStartServer()
    {
        if (!TryGetComponent(out _target)) return;

        // 订阅 HealthChanged 而不是在每个扣血点写 SyncVar ——
        // 扣血路径有好几条（接触伤害、投射物、将来的毒圈），漏一条就是
        //"某种伤害客户端看不见"，而且完全静默
        _health = _target.CurrentHealth;
        _target.HealthChanged += OnServerHealthChanged;
    }

    public override void OnStopServer()
    {
        if (_target != null) _target.HealthChanged -= OnServerHealthChanged;
    }

    private void OnServerHealthChanged(float current, float max) => _health = current;

    private void OnHealthChanged(float oldValue, float newValue)
    {
        // 服务端自己那份已经是权威值，跳过（否则会绕一圈回到 ApplyNetworkHealth）
        if (isServer) return;

        if (_target == null) TryGetComponent(out _target);

        _target?.ApplyNetworkHealth(newValue);
    }
}
