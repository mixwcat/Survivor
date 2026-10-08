using Mirror;
using UnityEngine;

/// <summary>
/// 组件级的「本实例该不该跑权威逻辑」判据（AI / 伤害结算 / 移动 / 生成）。
///
/// <para>
/// <b>为什么需要它：</b>联机时每个网络对象在每个端都有一份副本。敌人的副本如果两端都跑
/// <c>FixedUpdate</c> 的寻敌与移动，就会出现两套各自演化的位置（客户端的怪和队友看到的怪不在同一处），
/// 而伤害更是会在两端各结算一次 —— 血量各扣各的、经验各掉各的。这类问题**不会报错**，
/// 只表现为"打着打着两边不一样了"。
/// </para>
///
/// <para>
/// <b>用法：</b><c>Awake</c> 里 <c>_authority = new NetworkAuthority(gameObject);</c>，
/// 逐帧路径上读 <see cref="IsAuthority"/> 不会触发 <c>GetComponent</c>。
/// </para>
///
/// <para>
/// 单机对象（没有 <c>NetworkIdentity</c>）恒为 true —— 与改造前的行为完全一致，
/// 所以单机流程不需要任何分支。
/// </para>
///
/// <para>
/// 与 <see cref="LocalPlayerGuard"/> 的分工：那个回答"是不是**我**的角色"（输入相关），
/// 这个回答"该不该由**这一端**结算"（权威相关）。玩家对象上两个判据都要用，
/// 且含义相反 —— 玩家移动是**客户端权威**（本地副本跑），敌人是**服务端权威**（服务端副本跑）。
/// </para>
/// </summary>
public readonly struct NetworkAuthority
{
    private readonly NetworkIdentity _identity;

    public NetworkAuthority(GameObject go)
    {
        // 用 GetComponentInParent：武器的实体是玩家/塔的子节点，NetworkIdentity 只在根上
        _identity = go != null ? go.GetComponentInParent<NetworkIdentity>() : null;
    }

    /// <summary>
    /// 本实例是否由**本端**权威驱动。
    /// 没有 <c>NetworkIdentity</c>（单机对象）时恒为 true。
    /// </summary>
    public bool IsAuthority => _identity == null || _identity.isServer;

    /// <summary>本实例是不是网络对象（用于区分"单机"与"联机但还没 spawn 完"）。</summary>
    public bool IsNetworked => _identity != null;

    /// <summary>本实例是不是网络对象**且不是**服务端（纯客户端副本）。</summary>
    public bool IsRemoteCopy => _identity != null && !_identity.isServer;
}
