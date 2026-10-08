using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 把 Cinemachine 的跟随目标绑到**本地玩家**上。
///
/// <para>
/// <b>为什么需要它：</b>场景里的 <c>CinemachineCamera</c> 的 Follow 是空的 ——
/// 而 Main Camera 上有 <c>CinemachineBrain</c>，它会**每帧接管相机 transform**。
/// 于是相机被钉在 CinemachineCamera 自己的位置上不动，而旁边那个
/// <c>CameraController</c> 写的 transform 会被 Brain 覆盖掉（两个驱动者互相打架）。
/// 玩家是运行时生成的，Follow 没法在 Inspector 里预接，所以由这个组件在运行时绑。
/// </para>
///
/// <para>
/// <b>懒绑定 + 事件重绑：</b><c>Start</c> 时玩家可能还没生成（<c>PlayerSpawner</c> 是异步的），
/// 所以每帧兜一次查找；联机下本地玩家会切换，靠 <see cref="IPlayerManager.LocalPlayerChanged"/>
/// 重绑，而不是每帧轮询。
/// </para>
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CinemachinePlayerFollow : MonoBehaviour
{
    [Tooltip("绑定失败时每帧重试的间隔（秒）。玩家生成是异步的，0.2s 足够跟手又不浪费")]
    [SerializeField] private float _retryInterval = 0.2f;

    private CinemachineCamera _camera;
    private IPlayerManager _players;
    private float _nextRetryTime;

    private void Awake()
    {
        _camera = GetComponent<CinemachineCamera>();
    }

    private void OnEnable()
    {
        _players = PlayerManager.Service;
        if (_players != null) _players.LocalPlayerChanged += HandleLocalPlayerChanged;

        Bind(_players != null ? _players.LocalPlayer : null);
    }

    private void OnDisable()
    {
        if (_players != null) _players.LocalPlayerChanged -= HandleLocalPlayerChanged;
        _players = null;
    }

    private void Update()
    {
        // 已经绑上就不做任何事（联机换人由事件处理）
        if (_camera.Follow != null) return;

        // 服务在 OnEnable 时可能还没注册（初始化时序防御：不依赖 Awake/Start 顺序）
        if (_players == null) _players = PlayerManager.Service;

        if (Time.unscaledTime < _nextRetryTime) return;

        _nextRetryTime = Time.unscaledTime + _retryInterval;
        Bind(_players != null ? _players.LocalPlayer : null);
    }

    private void HandleLocalPlayerChanged(PlayerController player)
    {
        Bind(player);
    }

    private void Bind(PlayerController player)
    {
        _camera.Follow = player != null ? player.transform : null;
    }
}
