using Mirror;
using UnityEngine;

/// <summary>
/// 推车状态的发送与接收（挂在 Cart prefab 上，两端各一份）。
///
/// <para>
/// <b>职责边界：</b>它只做搬运 —— 服务端按固定频率把 <see cref="CartController"/> 的权威状态
/// 广播出去，客户端收到后写回本地的 <see cref="CartController"/>。
/// "推车怎么动、什么时候停"全部留在 <c>CartController</c> / <c>StageDirector</c>，
/// 这里不掺和，否则规则就有了两个来源。
/// </para>
///
/// <para>
/// 单机（没有网络会话）时它什么都不做：两个 <c>if</c> 都不成立，一次消息都不会发。
/// </para>
/// </summary>
public class CartNetworkSync : MonoBehaviour
{
    /// <summary>当前场景里的同步器。静态消息处理器需要一个入口转发到实例（场景切换后实例会换）。</summary>
    public static CartNetworkSync Current { get; private set; }

    /// <summary>
    /// 广播频率。
    /// 15Hz 的台阶是 67ms —— 推车速度约 1.5 单位/秒，每步约 0.1 单位，肉眼看不出来；
    /// 再降就会开始有台阶感。带宽不到 20 字节 × 15 ≈ 300 B/s。
    /// </summary>
    private const float SendInterval = 1f / 15f;

    /// <summary>
    /// 推车与耐久控制器。
    /// 字段是 <c>public</c>（而不是 <c>[SerializeField] private</c>）：这是**prefab 配置组件**，
    /// 而且脚本化接线时直接赋值比走 <c>SerializedObject</c> 可靠（见 CLAUDE.md 的批处理约定）。
    /// </summary>
    [Tooltip("留空则从同物体自动获取")]
    public CartController Cart;

    [Tooltip("留空则从同物体自动获取")]
    public CartHealthController Health;

    private float _sendTimer;

    private void OnEnable()
    {
        if (Current != null && Current != this)
            Debug.LogWarning($"[{nameof(CartNetworkSync)}] 场景中存在多个同步器，以后注册的为准。");

        Current = this;
        EnsureRefs();
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }

    private void EnsureRefs()
    {
        if (Cart == null) Cart = GetComponent<CartController>();
        if (Health == null) Health = GetComponent<CartHealthController>();
    }

    private void Update()
    {
        // 只有服务端广播；单机（没有会话）时 NetworkServer.active 为 false，直接不做事
        if (!NetworkServer.active) return;
        if (Cart == null) return;

        _sendTimer -= Time.deltaTime;
        if (_sendTimer > 0f) return;

        _sendTimer = SendInterval;

        NetworkServer.SendToAll(new CartStateMessage
        {
            Distance = Cart.TravelledDistance,
            HealthNormalized = Cart.HealthNormalized,
            IsMoving = Cart.IsMoving,
            IsDisabled = Cart.IsDisabled,
        });
    }

    /// <summary>
    /// 注册客户端处理器。
    ///
    /// <para>
    /// ⚠️ <b>必须由 <c>SurvivorNetworkManager.OnStartClient</c> 调用，不能放在本组件的 <c>Start</c> 里。</b>
    /// 服务端在关卡加载完就开始广播，而客户端的场景对象要到场景加载完才出现 ——
    /// 在 <c>Start</c> 里注册会漏掉开头那几条（表现是"进关卡后推车停着不动，过一会儿才追上"）。
    /// 而 <c>OnStartClient</c> 在连接建立时就会跑，早于任何场景加载。
    /// </para>
    /// </summary>
    public static void RegisterClientHandler()
    {
        NetworkClient.RegisterHandler<CartStateMessage>(OnClientStateReceived);
    }

    private static void OnClientStateReceived(CartStateMessage message)
    {
        // 场景还没加载完时 Current 为 null —— 丢掉即可，服务端 15Hz 一直在发
        CartNetworkSync sync = Current;
        if (sync == null) return;

        sync.Apply(message);
    }

    private void Apply(CartStateMessage message)
    {
        EnsureRefs();

        Cart?.ApplyNetworkState(message.Distance, message.IsMoving, message.IsDisabled);
        Health?.ApplyNetworkHealth(message.HealthNormalized);
    }
}
