using Mirror;
using UnityEngine;

/// <summary>
/// 组件级的「本实例是不是**本机玩家**」判据。
///
/// <para>
/// <b>为什么需要它：</b>联机时同一个玩家 prefab 会在每个端都有一份副本 ——
/// 只有被本机拥有的那一份该读输入，其余副本（服务端上的远程玩家、客户端上的别人）
/// 必须完全不响应本地设备。项目里"是不是本地玩家"原本靠 <c>_inputHandleId == "local"</c> 判，
/// 而那是**序列化字段**、每个副本都是 <c>"local"</c> —— 判据在联机下直接失效
/// （表现是"我按 E，所有玩家的副本一起去交互"，且没有任何报错）。
/// </para>
///
/// <para>
/// <b>用法：</b><c>Awake</c> 里 <c>_guard = new LocalPlayerGuard(gameObject);</c>，
/// 之后随时读 <see cref="IsLocal"/>。它在构造时查一次 <c>NetworkIdentity</c>，
/// 逐帧读 <c>IsLocal</c> 不会触发 <c>GetComponent</c>（性能红线）。
/// </para>
///
/// <para>
/// ⚠️ <b>它只在网络对象被 spawn 之后才准确</b>：<c>isLocalPlayer</c> 是
/// <c>NetworkClient.ApplySpawnPayload</c> 在 <c>SetActive(true)</c> **之后**才赋值的
/// （<c>NetworkClient.cs:1151</c> vs <c>:1169</c>），而 <c>Awake</c>/<c>OnEnable</c> 落在两者之间。
/// 单机对象（没有 <c>NetworkIdentity</c>）恒为 true。
/// </para>
///
/// <para>
/// 用 <c>GetComponentInParent</c> 而不是 <c>GetComponent</c>：武器实例挂在玩家根节点**下面**，
/// <c>NetworkIdentity</c> 只在根上 —— 子物体上的组件（如 <c>GunWeapon</c>）也要能判对。
/// </para>
/// </summary>
public readonly struct LocalPlayerGuard
{
    private readonly NetworkIdentity _identity;

    public LocalPlayerGuard(GameObject go)
    {
        _identity = go != null ? go.GetComponentInParent<NetworkIdentity>() : null;
    }

    /// <summary>
    /// 本实例是否由本机玩家控制。
    /// 没有 <c>NetworkIdentity</c>（单机生成的玩家/武器）时恒为 true，与改造前的行为一致。
    /// </summary>
    public bool IsLocal => _identity == null || _identity.isLocalPlayer;

    /// <summary>本实例是不是网络对象（用于区分"单机"与"联机但还没 spawn 完"）。</summary>
    public bool IsNetworked => _identity != null;
}
