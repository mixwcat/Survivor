using System;
using System.Threading.Tasks;

/// <summary>
/// 玩家档案服务 —— **持久数据**（金币 / 解锁 / 哨站 / 音画设置）的唯一入口。
///
/// <para>
/// <b>业务代码不要自己碰 JSON 文件</b>：不要直接 <c>File.WriteAllText</c>，也不要为了
/// "统一一下"再包一层通用的 Json 管理类。档案是玩家唯一会心疼的数据，
/// 它的读写必须同时具备**原子写入、备份恢复、版本迁移、损坏修复、失败可见**五件事 ——
/// 缺任何一件的通用封装都只是把风险藏得更深（本项目曾有一个裸 <c>File.WriteAllText</c>
/// 的 <c>JsonMgr</c>，已删除）。真需要第二个落盘对象时，请复用这里的原子写实现，
/// 而不是新开一条"怎么落盘"的路径。
/// </para>
///
/// <para>
/// 由组合根创建并注册；在 <c>GameBootstrap.Ready</c> 完成前已经加载完毕，
/// 因此任何 <c>await GameBootstrap.Ready</c> 之后的场景脚本都能直接读 <see cref="Profile"/>。
/// </para>
/// </summary>
public interface IPlayerProfileService
{
    /// <summary>加载档案（幂等 + 并发安全，由组合根调用）。</summary>
    Task InitializeAsync();

    /// <summary>当前档案。初始化完成后恒非 null（读失败会回退默认档案）。</summary>
    PlayerProfile Profile { get; }

    /// <summary>
    /// 档案内容变化（金币 / 解锁 / 哨站）。UI 订阅它刷新，**不要轮询** ——
    /// 轮询会让「金币变了但界面没变」这类问题只能靠调刷新频率来掩盖。
    /// </summary>
    event Action Changed;

    /// <summary>扣金币；余额不足或金额非法时返回 false 且不改动。</summary>
    bool TrySpendCoins(int amount);

    /// <summary>发放金币（结算奖励）。</summary>
    void GrantCoins(int amount);

    /// <summary>该武器是否已解锁。</summary>
    bool IsWeaponUnlocked(string weaponId);

    /// <summary>
    /// 购买武器：校验余额 → 扣款 → 记账 → **立即落盘** → 通知 UI。
    /// 价格由调用方传入（来自武器资产），服务本身不认识任何具体武器。
    /// </summary>
    bool TryUnlockWeapon(string weaponId, int price);

    /// <summary>更新当前哨站（通关时 +1）。</summary>
    void SetCurrentOutpost(int outpost);

    /// <summary>把档案里的音画设置应用到对应服务（初始化时与设置变更后调用）。</summary>
    void ApplyAudioSettings();

    /// <summary>立即落盘。只在明确的事务点调用（购买成功、结算成功、设置确认），不要逐帧保存。</summary>
    bool Save();
}
