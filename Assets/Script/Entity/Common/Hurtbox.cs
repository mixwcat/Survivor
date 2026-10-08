using UnityEngine;

/// <summary>
/// 本体受击标记 —— 挂在**实体本体碰撞体所在的那个 GameObject** 上，
/// 回答"碰到这个碰撞体算不算打到了这个实体"。
///
/// <para>
/// <b>为什么需要它：</b>一个实体往往有多个碰撞体 —— 本体（根节点）、
/// 触发体（子物体，如敌人的 <c>ColliderTrigger</c>、塔武器的检测圈）、
/// 武器与召唤物的判定体（玩家的环绕火球）。于是"碰到了碰撞体"和"打到了实体"不是一回事，
/// 而每个攻击系统都各自写了一套判据（tag + 自己身上 / tag + 父级 / <c>useTriggers = false</c> …），
/// 累计出四次线上问题：怪群互相掉血、玩家被火球"代打"掉血、怪隔空打塔、溅射对同一敌人结算两次 ——
/// 全是同一个问题的不同表现。
/// </para>
///
/// <para>
/// <b>契约：</b>只有本体碰撞体所在对象带本组件；武器判定体、检测范围、召唤物一律**不带**。
/// 所有伤害路径统一走 <see cref="DamageTargetResolver"/>，不再各自判断"碰撞体挂在哪一层"。
/// </para>
///
/// <para>
/// <b>它不回答"该不该打"</b>：敌我区分、攻击方式的目标类型仍由调用方按 tag 判断 ——
/// tag 表达身份（玩法规则），本标记表达"这是本体"（几何语义）。
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class Hurtbox : MonoBehaviour
{
    [Tooltip("本体的血量控制器。留空 = 同物体或父级自动解析（本体碰撞体通常在根节点上）")]
    [SerializeField] private BaseHealthController _health;

    private bool _resolved;

    /// <summary>本体血量控制器（解析失败为 null）。</summary>
    public BaseHealthController Health
    {
        get
        {
            if (_resolved) return _health;

            _resolved = true;
            if (_health == null)
                _health = GetComponent<BaseHealthController>() ?? GetComponentInParent<BaseHealthController>();

            return _health;
        }
    }
}
