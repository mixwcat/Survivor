using Mirror;
using UnityEngine;

/// <summary>
/// 伤害的路由：单机/服务端直接结算，客户端把命中上报给服务端。
///
/// <para>
/// <b>为什么要有这一层：</b>伤害的调用点有六处（子弹 ×2、环绕物、范围伤害、光束、敌人接触伤害），
/// 挨个加联机分支既容易漏，也会让"谁能造成伤害"散落在六个地方。
/// 所以路由挂在 <see cref="BaseHealthController.TakeDamage"/> 这个**唯一入口**上，
/// 调用点一行都不用改。
/// </para>
///
/// <para>
/// <b>方向性（重要）：</b>服务端 → 客户端的伤害**不走这里**（那靠状态同步）。
/// 这里只处理**客户端本地产生**的命中，也就是"我这一枪打中了"。
/// </para>
///
/// <para>
/// ⚠️ <b>这是客户端上报、服务端结算 —— 不是服务端权威的命中判定。</b>
/// <c>Docs/MirrorPlan.md</c> 的 4.4 首选方案是"服务端生成投射物 + 服务端判定命中"，
/// 那是完整做法；这里先用上报方案，理由是它**不动投射物与武器那一整套**，
/// 能先把"客户端打得动怪"这条链路打通。服务端会做形状校验（目标存在、不是自己、
/// 数值有限），但**校验不了"这一枪是否真的打中了"**。
/// 代价写在这里，将来要收紧就替换本类的 <see cref="ForwardToServer"/>。
/// </para>
/// </summary>
public static class DamageRouter
{
    /// <summary>
    /// 上报数值的上限兜底。**这是防呆，不是反作弊** ——
    /// 目的是挡住 NaN / 无穷 / 明显离谱的数值把血条打穿成负几百万。
    /// </summary>
    private const float MaxReportedDamage = 100000f;
    private const float MaxReportedHitForce = 1000f;

    private static bool _warnedNoSender;

    /// <summary>
    /// 本端是否该把伤害**上报**而不是本地结算。
    ///
    /// <para>
    /// 判据是"本进程不是服务端"：服务端**永远不会**在客户端的副本上调用 <c>TakeDamage</c>
    /// （它只在自己的那份上结算），所以客户端上出现的每一次 <c>TakeDamage</c> 都必然是本地产生的。
    /// </para>
    /// </summary>
    public static bool ShouldForwardToServer(GameObject target)
    {
        if (!NetworkBootstrap.IsActive) return false;   // 单机：与改造前完全一致
        if (NetworkServer.active) return false;         // 本进程就是服务端
        if (target == null) return false;

        // 只有**网络对象**才有可上报的身份。场景里的本地对象（放置幽灵之类）没有 netId，
        // 它们本来也不该被当成伤害目标
        return target.TryGetComponent(out NetworkIdentity identity) && identity.netId != 0;
    }

    /// <summary>把一次本地命中上报给服务端（由本机的玩家对象发出）。</summary>
    public static void ForwardToServer(GameObject target, in DamageInfo info)
    {
        NetworkPlayerState sender = NetworkPlayerState.LocalSender;
        if (sender == null)
        {
            // 逐帧路径之外（每次命中一次），但仍按"只提示一次"处理，避免刷屏
            if (!_warnedNoSender)
            {
                _warnedNoSender = true;
                Debug.LogWarning($"[{nameof(DamageRouter)}] 客户端上还没有本地玩家，无法上报命中 —— " +
                                 "这些伤害会被丢弃。若刚进场景就开火，属于正常竞态；持续出现则是 bug。");
            }

            return;
        }

        sender.CmdApplyDamage(
            ResolveNetId(target),
            info.Amount,
            info.HitForce,
            (byte)info.Source,
            info.Attacker != null ? ResolveNetId(info.Attacker.gameObject) : 0u);
    }

    /// <summary>解析一个对象的网络 id（0 表示不是网络对象 / 尚未 spawn）。</summary>
    public static uint ResolveNetId(GameObject go)
    {
        if (go == null) return 0u;

        // GetComponentInParent：敌人的 EntityBehaviour 与 NetworkIdentity 同物体，
        // 但武器/塔的实体可能是子节点 —— 统一往上找，别让调用点各写一份
        NetworkIdentity identity = go.GetComponentInParent<NetworkIdentity>();
        return identity != null ? identity.netId : 0u;
    }

    /// <summary>服务端侧：把上报来的 id 还原成场景里的对象（解析不出返回 null）。</summary>
    public static bool TryResolveSpawned<T>(uint netId, out T component) where T : Component
    {
        component = null;
        if (netId == 0u) return false;
        if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity)) return false;

        return identity.TryGetComponent(out component);
    }

    /// <summary>服务端侧的数值校验。返回 false 表示这次上报应当被丢弃。</summary>
    public static bool IsPlausible(float amount, float hitForce, out float safeHitForce)
    {
        safeHitForce = 0f;

        if (float.IsNaN(amount) || float.IsInfinity(amount)) return false;
        if (amount <= 0f) return false;
        if (amount > MaxReportedDamage) return false;

        safeHitForce = float.IsNaN(hitForce) || float.IsInfinity(hitForce)
            ? 0f
            : Mathf.Clamp(hitForce, 0f, MaxReportedHitForce);

        return true;
    }
}
