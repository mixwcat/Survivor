using System;
using System.Collections.Generic;

/// <summary>
/// 联机会话表 —— 记住**每个连接**在这次会话里的选择（角色 / 装备）。
///
/// <para>
/// <b>为什么必须有它：</b>Mirror 切场景时会把玩家对象销毁重建，挂在玩家对象上的
/// <c>SyncVar</c> 一起消失；而"谁选了什么角色、带了什么武器"必须活过场景切换，
/// 服务端才能在重建玩家时按原样注入。
/// </para>
///
/// <para>
/// <b>为什么不是 <c>RunSessionService</c>：</b>后者是**进程内单份**的行前配置
/// （大厅写、关卡读）。Host 模式下服务端与客户端在同一进程、共用同一个实例，
/// 4 个玩家的选择会互相覆盖 —— 语义上就不成立。
/// </para>
///
/// <para>
/// <b>只服务端写：</b>客户端的选择经 <c>[Command]</c> 上报到服务端后写进本表；
/// 客户端自己不查这张表（它要看的是别的玩家对象上的 <c>SyncVar</c>）。
/// </para>
///
/// <para>
/// ⚠️ <b>它刻意不是 <c>NetworkBehaviour</c>：</b><c>DontDestroyOnLoad</c> 里的对象
/// <c>sceneId == 0</c>，会被 Mirror 当成"动态生成对象"而永远不被 <c>SpawnObjects()</c> 处理，
/// <c>netId</c> 恒为 0 ⇒ <c>[Command]</c>/<c>[SyncVar]</c> 全部静默失效。
/// 跨场景的系统组件一律用纯 C#/MonoBehaviour（见 <c>Docs/Mirror/03-项目落地注意.md</c> §2）。
/// </para>
/// </summary>
public interface INetworkSessionService
{
    /// <summary>读某个连接选的角色 id；没有记录时返回 false。</summary>
    bool TryGetCharacter(int connectionId, out string characterId);

    /// <summary>写某个连接选的角色 id（只应在服务端调用）。</summary>
    void SetCharacter(int connectionId, string characterId);

    /// <summary>读某个连接带的武器 id 列表；没有记录时返回空列表（不是 null）。</summary>
    IReadOnlyList<string> GetLoadout(int connectionId);

    /// <summary>写某个连接带的武器 id 列表（会复制一份，调用方之后改原列表不影响本表）。</summary>
    void SetLoadout(int connectionId, IReadOnlyList<string> weaponIds);

    /// <summary>某个连接断开时清掉它的记录。</summary>
    void Forget(int connectionId);

    /// <summary>整局结束/回主菜单时清空（**不是**每个连接各自调用）。</summary>
    void ClearAll();

    /// <summary>当前有记录的连接数（诊断用）。</summary>
    int Count { get; }

    /// <summary>某个连接的选择发生变化（服务端本地事件，用于刷新房间 UI）。</summary>
    event Action<int> SlotChanged;
}
