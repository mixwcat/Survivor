using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 出行会话服务 —— 承载「大厅配置 → 战斗场景消费 → 返回大厅清空」这条链路。
///
/// <para>
/// 生命周期是**一次出行**，不是一局游戏进程，也不是一次场景：
/// <list type="bullet">
/// <item><see cref="BeginNew"/>：进入大厅时调用，清空并按当前时间取种子；</item>
/// <item><see cref="TrySetCharacter"/> / <see cref="TrySetLoadout"/> / <see cref="TrySetStage"/>：大厅里的受控写入；</item>
/// <item><see cref="TryLockForDeparture"/> / <see cref="Unlock"/>：出发倒计时开始 / 取消；</item>
/// <item><see cref="Clear"/>：回到大厅时调用（或再次 <see cref="BeginNew"/>）。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>只暴露只读快照 <see cref="Current"/>，不暴露可变对象</b>：
/// 写入一律走 <c>TrySetXxx</c>，由服务自己检查锁状态与合法性。
/// 旧的 <c>Current.xxx = …</c> 写法让"锁定"只是注释里的约定 ——
/// 任何调用方都能在倒计时期间改角色，而后果（关卡里装配的武器与界面显示不一致）
/// 只在进关卡后才暴露。
/// </para>
///
/// <para>
/// 单机下它就是一份普通对象；联机时**只有服务端**能写角色与装备，
/// 种子也由服务端下发（否则各端生成的敌人不一致）。
/// </para>
/// </summary>
public interface IRunSessionService
{
    /// <summary>初始化（由组合根调用，幂等）。</summary>
    Task InitializeAsync();

    /// <summary>当前出行配置的只读快照，恒非 null（未配置时字段为空）。</summary>
    RunSessionSnapshot Current { get; }

    /// <summary>配置是否已锁定（出发倒计时已开始）。</summary>
    bool IsLocked { get; }

    /// <summary>开始一次新的出行：清空配置并取新种子。</summary>
    void BeginNew();

    /// <summary>
    /// 设置角色，并原子化规范装备（清掉该角色不允许的、截断到上限、空了就补默认武器）。
    /// </summary>
    bool TrySetCharacter(CharacterDefinitionSO definition, out string reason);

    /// <summary>
    /// 角色**成功写入之后**触发，参数是新的角色定义。
    ///
    /// <para>
    /// <b>为什么需要它：</b>大厅里的玩家实例在选角**之前**就已经生成（<c>PlayerSpawner</c> 按场景配置生成），
    /// 选角只写 RunSession。不通知的话，玩家身上的 <c>PlayerRoleController</c> 会一直停在
    /// 生成时的回退角色上 —— 切到工程师后武器台仍按枪手放行、塔台提示不出现，
    /// 而且不报任何错（能力位是"看起来正常"的错值）。
    /// </para>
    /// </summary>
    event Action<CharacterDefinitionSO> CharacterChanged;

    /// <summary>整体替换出发装备；任何一项非法都整体拒绝。</summary>
    bool TrySetLoadout(IReadOnlyList<string> weaponIds, out string reason);

    /// <summary>设置关卡 id。</summary>
    bool TrySetStage(string stageId, out string reason);

    /// <summary>锁定为「可出发」；配置不全时拒绝并给出原因。</summary>
    bool TryLockForDeparture(out string reason);

    /// <summary>解锁（取消出发倒计时），让玩家能回到大厅重新配置。</summary>
    void Unlock();

    /// <summary>是否配置到可以出发的程度。<paramref name="reason"/> 为失败原因。</summary>
    bool IsReadyToDepart(out string reason);

    /// <summary>清空（回到大厅）。</summary>
    void Clear();
}
