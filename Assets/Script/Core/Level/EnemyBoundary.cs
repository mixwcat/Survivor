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
    public void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        if (other.GetComponentInParent<EnemyHealthController>() == null) return;

        Destroy(other.gameObject); // 销毁离开触发器的敌人
    }
}
