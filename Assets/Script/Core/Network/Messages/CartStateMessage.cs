using Mirror;

/// <summary>
/// 推车状态（服务端 → 全体客户端）。
///
/// <para>
/// <b>为什么推车不走 <c>NetworkIdentity</c> + <c>NetworkTransform</c>：</b>
/// 推车是**摆在场景里**的 prefab 实例，给它挂 <c>NetworkIdentity</c> 会让它变成
/// Mirror 的"场景对象" —— <c>NetworkScenePostProcess</c> 会在进 Play 时
/// <b>强制 <c>SetActive(false)</c></b>（<c>Assets/Mirror/Editor/NetworkScenePostProcess.cs:103</c>），
/// 只有 <c>NetworkServer.SpawnObjects()</c> 才会把它激活。
/// 而本项目**单机模式仍然要能玩**，单机没有服务端 ⇒ 推车永远不会被激活 ⇒ 整个关卡瘫掉。
/// </para>
///
/// <para>
/// 所以推车两端各留一份**本地实例**（场景里那份，不动任何接线），
/// 只把"权威进度"从服务端广播过来：服务端跑 <c>CartController.Update</c> 推进弧长，
/// 客户端只做"把车摆到服务端说的位置"。
/// </para>
///
/// <para>
/// 带宽：结构体不到 20 字节，15Hz 广播 ≈ 300 B/s —— 相对敌人的位置同步可以忽略。
/// </para>
/// </summary>
public struct CartStateMessage : NetworkMessage
{
    /// <summary>沿路径已行驶的弧长（<c>CartController.TravelledDistance</c>）。</summary>
    public float Distance;

    /// <summary>耐久比例（0..1）。</summary>
    public float HealthNormalized;

    /// <summary>是否正在行驶。</summary>
    public bool IsMoving;

    /// <summary>是否处于停摆（耐久耗尽）。</summary>
    public bool IsDisabled;
}
