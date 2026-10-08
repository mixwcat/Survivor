using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 出行会话服务 —— 唯一持有「一次出行」的配置，跨场景供大厅写、关卡读。
///
/// 数据：<see cref="RunSession"/>（可变，进程内单份）；对外只给 <see cref="RunSessionSnapshot"/>（只读视图）。
/// 流向：LobbyDirector / 选角 / 武器台 / 传送门 → <c>TrySetXxx</c> → PlayerSpawner / StageDirector
/// 生命周期：进程级（挂 [GameBootstrap]，DDOL 跨场景）；一份出行由 <c>BeginNew</c> 重置；不落盘。
/// </summary>
/// <remarks>
/// ⚠️ 它是 <see cref="RunSession"/> 的**唯一写入者**：对外只给只读快照，所有修改都经过
/// <c>TrySetXxx</c> 的锁状态与合法性检查。绕过它直接改状态，会让「界面显示的装备」与
/// 「关卡实际装上的武器」分叉 —— 且只在进关卡后才暴露。
///
/// 为什么不落盘：出行的角色/装备只在一次出行内有效；跨进程要保留的是
/// <see cref="PlayerProfile"/>（金币/解锁/哨站）。
///
/// 与其余全局服务遵循同一套规范：挂在 [GameBootstrap] 上，由组合根注册；
/// 业务代码通过 <see cref="Service"/> 访问（未注册返回 null）。
/// </remarks>
public class RunSessionService : MonoBehaviour, IRunSessionService
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IRunSessionService Service =>
        ServiceLocator.TryGet<IRunSessionService>(out var svc) ? svc : null;

    private readonly RunSession _session = new RunSession();
    private Task _initTask;

    public RunSessionSnapshot Current => _session.Snapshot();

    public bool IsLocked => _session.IsLocked;

    public Task InitializeAsync()
    {
        return _initTask ??= Task.CompletedTask;
    }

    private void OnDestroy()
    {
        if (ServiceLocator.TryGet<IRunSessionService>(out var svc) && ReferenceEquals(svc, this))
            ServiceLocator.Unregister<IRunSessionService>();
    }

    public void BeginNew()
    {
        // 种子取时间戳：单机下够用，且不同次出行的敌人分布不同。
        // 联机时改由服务端下发（各端必须用同一个种子，否则生成的敌人不一致）。
        _session.BeginNew(Environment.TickCount);
    }

    public event Action<CharacterDefinitionSO> CharacterChanged;

    public bool TrySetCharacter(CharacterDefinitionSO definition, out string reason)
    {
        // 先写数据、再发事件：订阅者（大厅里已生成的玩家）据此同步能力位，
        // 反过来会让订阅者在数据还没变时先刷新一次
        if (!_session.TrySetCharacter(definition, out reason)) return false;

        CharacterChanged?.Invoke(definition);
        return true;
    }

    public bool TrySetLoadout(IReadOnlyList<string> weaponIds, out string reason) =>
        _session.TrySetLoadout(weaponIds, out reason);

    public bool TrySetStage(string stageId, out string reason) =>
        _session.TrySetStage(stageId, out reason);

    public bool TryLockForDeparture(out string reason) =>
        _session.TryLockForDeparture(out reason);

    public void Unlock() => _session.Unlock();

    public bool IsReadyToDepart(out string reason) => _session.IsReadyToDepart(out reason);

    public void Clear() => _session.Clear();
}
