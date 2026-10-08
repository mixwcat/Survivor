using Mirror;
using UnityEngine;

/// <summary>
/// 越界回收：敌人离开边界触发区即销毁。
///
/// <para>
/// 边界触发区在 Boundary 层，只与 EnemyBody 层配对，能触发回调的只可能是敌人本体 ——
/// 以前靠 <c>CompareTag("Enemy")</c>，而敌人的触发体挂在未打 tag 的子物体上，
/// 与接触伤害踩过同一个坑。
/// </para>
///
/// <para>
/// <b>仍然保留一次身份确认：</b>销毁是不可逆的，而 <c>Default</c> 层刻意没有被隔离
/// （UI / 场景道具还在上面）—— 万一有东西被漏归类，这里必须再挡一次。
/// 用组件而不是 tag，与全项目判据一致。
/// </para>
/// </summary>
public class EnemyBoundary : MonoBehaviour
{
    /// <summary>本端是否该执行越界回收。联机时只有服务端（见 <see cref="NetworkAuthority"/>）。</summary>
    private NetworkAuthority _authority;
    private bool _authorityReady;

    public void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        if (other.GetComponentInParent<EnemyHealthController>() == null) return;

        // 懒解析：越界回调可能在 Awake 之前就到（对象刚生成就被挤出去）
        if (!_authorityReady)
        {
            _authority = new NetworkAuthority(gameObject);
            _authorityReady = true;
        }

        // 联机时只有服务端能回收：客户端自己 Destroy 会绕过 Mirror ——
        // 服务端那边这只怪还活着，而客户端已经看不见它了（且不会自己回来）
        if (!_authority.IsAuthority) return;

        if (NetworkBootstrap.IsActive)
        {
            NetworkServer.Destroy(other.gameObject);
            return;
        }

        Destroy(other.gameObject); // 销毁离开触发器的敌人
    }
}
