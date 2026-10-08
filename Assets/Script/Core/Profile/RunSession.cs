using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// 出行配置的**只读快照** —— 业务代码只能看到它，不能改。
///
/// <para>
/// <b>为什么不再直接暴露可变对象：</b>旧实现把 <c>RunSession</c> 整个交出去，
/// 于是"锁"只是写在注释里的约定 —— 任何调用方都能 <c>Current.stageId = ...</c>、
/// 都能往 <c>weaponLoadoutIds</c> 里塞第三把武器。约定式锁定在代码里没有任何着力点，
/// 而它失效的表现（关卡装配出来的装备与大厅界面显示的不一致）只在进关卡后才暴露。
/// </para>
///
/// <para>
/// 列表是 <see cref="ReadOnlyCollection{T}"/> 视图（不是拷贝）：读取零分配，
/// 且无法被强转回 <c>List&lt;string&gt;</c> 改内容。
/// </para>
/// </summary>
public readonly struct RunSessionSnapshot
{
    private static readonly string[] EmptyLoadout = new string[0];

    /// <summary>角色 id（<c>char_gunner</c> / <c>char_engineer</c>）。</summary>
    public readonly string CharacterId;

    /// <summary>关卡 id（<c>stage_01</c>）。</summary>
    public readonly string StageId;

    /// <summary>
    /// 本局随机种子。生成与掉落都用它建 <c>System.Random</c> ——
    /// 不要用 <c>UnityEngine.Random</c>（进程级全局状态，联机时各端不一致，也无法复现）。
    /// </summary>
    public readonly int Seed;

    /// <summary>是否已锁定。锁定后任何写入都会被拒绝。</summary>
    public readonly bool IsLocked;

    /// <summary>出发携带的武器 id（只读视图）。</summary>
    public readonly IReadOnlyList<string> WeaponLoadoutIds;

    public RunSessionSnapshot(string characterId, string stageId, int seed, bool isLocked,
                              IReadOnlyList<string> weaponLoadoutIds)
    {
        CharacterId = characterId;
        StageId = stageId;
        Seed = seed;
        IsLocked = isLocked;
        WeaponLoadoutIds = weaponLoadoutIds ?? EmptyLoadout;
    }

    /// <summary>是否带了至少一把武器。</summary>
    public bool HasLoadout => WeaponLoadoutIds != null && WeaponLoadoutIds.Count > 0;
}

/// <summary>
/// 一次出行的**可变状态**（只由 <see cref="RunSessionService"/> 持有并写入）。
///
/// <para>
/// 它存在的理由是「大厅的选择要能带到战斗场景」：角色、出发装备、关卡 id、随机种子。
/// 不放进 <see cref="PlayerProfile"/>，因为它**一局就作废**；不放进场景对象，因为切场景就没了。
/// </para>
///
/// <para>
/// <b>所有写入都必须走 <c>TrySetXxx</c></b>：每个方法自己检查锁状态与合法性，
/// 失败时**不改动任何字段**（原子）。这样"角色与装备始终互相匹配"是一条不变量，
/// 而不是靠每个调用方记得先检查 <c>IsLocked</c> 再检查 <c>maxWeaponSlots</c>。
/// </para>
/// </summary>
public sealed class RunSession
{
    private readonly List<string> _weaponLoadoutIds = new List<string>();
    private readonly ReadOnlyCollection<string> _loadoutView;

    /// <summary>
    /// 当前角色的定义缓存。只用于校验装备合法性 ——
    /// 快照里对外只有 id（引用无法跨网络同步，也会随资产改名失效）。
    /// </summary>
    private CharacterDefinitionSO _character;

    public RunSession()
    {
        _loadoutView = _weaponLoadoutIds.AsReadOnly();
    }

    /// <summary>角色 id（<c>char_gunner</c> / <c>char_engineer</c>）。</summary>
    public string CharacterId { get; private set; }

    /// <summary>关卡 id（<c>stage_01</c>）。</summary>
    public string StageId { get; private set; }

    /// <summary>本局随机种子。</summary>
    public int Seed { get; private set; }

    /// <summary>是否已锁定。</summary>
    public bool IsLocked { get; private set; }

    /// <summary>当前快照（读取零分配）。</summary>
    public RunSessionSnapshot Snapshot() =>
        new RunSessionSnapshot(CharacterId, StageId, Seed, IsLocked, _loadoutView);

    /// <summary>开始一次新的出行：清空配置并写入新种子。</summary>
    public void BeginNew(int seed)
    {
        Clear();
        Seed = seed;
    }

    /// <summary>清空为「还没配置」的状态。</summary>
    public void Clear()
    {
        CharacterId = null;
        StageId = null;
        Seed = 0;
        IsLocked = false;
        _character = null;
        _weaponLoadoutIds.Clear();
    }

    /// <summary>
    /// 设置角色，并**原子化规范装备**：清掉该角色不允许的武器、截断到携带上限、
    /// 一件都不剩时补上角色的默认武器。
    ///
    /// <para>
    /// 为什么必须在这里做：Gunner 切 Engineer 时若不换装备，工程师会带着枪出门
    /// （而工程师没有武器升级能力，那把枪永远升不了级）；反过来 Engineer 切 Gunner
    /// 会留下固定火球 —— 两种都只在进关卡后表现为"这角色怎么带着别人的武器"。
    /// </para>
    /// </summary>
    public bool TrySetCharacter(CharacterDefinitionSO definition, out string reason)
    {
        if (IsLocked)
        {
            reason = "出行配置已锁定（出发倒计时已开始）";
            return false;
        }

        if (definition == null || string.IsNullOrEmpty(definition.id))
        {
            reason = "角色定义缺失或 id 为空";
            return false;
        }

        CharacterId = definition.id;
        NormalizeLoadout(definition);

        reason = null;
        return true;
    }

    /// <summary>
    /// 整体替换出发装备。**全有或全无**：任何一项非法都整体拒绝，不写进一半。
    /// </summary>
    public bool TrySetLoadout(IReadOnlyList<string> weaponIds, out string reason)
    {
        if (IsLocked)
        {
            reason = "出行配置已锁定（出发倒计时已开始）";
            return false;
        }

        if (weaponIds == null || weaponIds.Count == 0)
        {
            reason = "至少要携带一把武器";
            return false;
        }

        var staged = new List<string>(weaponIds.Count);
        var seen = new HashSet<string>();

        for (int i = 0; i < weaponIds.Count; i++)
        {
            string id = weaponIds[i];
            if (string.IsNullOrEmpty(id))
            {
                reason = "装备列表里有空 id";
                return false;
            }

            if (!seen.Add(id))
            {
                reason = $"装备列表里有重复的武器 id「{id}」";
                return false;
            }

            staged.Add(id);
        }

        // 角色约束只有拿到角色定义才谈得上；这里用**缓存的定义**（由 TrySetCharacter 记下）
        if (_character != null)
        {
            int maxSlots = MaxSlots(_character);
            if (staged.Count > maxSlots)
            {
                reason = $"角色「{_character.displayName}」最多携带 {maxSlots} 把武器";
                return false;
            }

            for (int i = 0; i < staged.Count; i++)
            {
                if (IsAllowed(_character, staged[i])) continue;

                reason = $"角色「{_character.displayName}」不能携带武器「{staged[i]}」";
                return false;
            }
        }

        _weaponLoadoutIds.Clear();
        _weaponLoadoutIds.AddRange(staged);

        reason = null;
        return true;
    }

    /// <summary>设置关卡 id。</summary>
    public bool TrySetStage(string stageId, out string reason)
    {
        if (IsLocked)
        {
            reason = "出行配置已锁定（出发倒计时已开始）";
            return false;
        }

        if (string.IsNullOrEmpty(stageId))
        {
            reason = "关卡 id 为空";
            return false;
        }

        StageId = stageId;
        reason = null;
        return true;
    }

    /// <summary>
    /// 锁定为「可出发」。**先校验再锁**：锁住一份配置不全的出行，
    /// 会让玩家连补救的机会都没有（改不了角色、也出发不了）。
    /// 已经锁定时视为成功（幂等）。
    /// </summary>
    public bool TryLockForDeparture(out string reason)
    {
        if (IsLocked)
        {
            reason = null;
            return true;
        }

        if (!IsReadyToDepart(out reason)) return false;

        IsLocked = true;
        reason = null;
        return true;
    }

    /// <summary>解锁（取消出发倒计时）。让玩家能回到大厅重新配置。</summary>
    public void Unlock()
    {
        IsLocked = false;
    }

    /// <summary>
    /// 是否配置到「可以出发」的程度。**这是出发的唯一判据** ——
    /// 传送门与任何未来入口都消费它，不要各自再写一份检查。
    /// </summary>
    public bool IsReadyToDepart(out string reason)
    {
        if (string.IsNullOrEmpty(CharacterId))
        {
            reason = "还没选角色";
            return false;
        }

        if (string.IsNullOrEmpty(StageId))
        {
            reason = "还没选关卡";
            return false;
        }

        if (_weaponLoadoutIds.Count == 0)
        {
            reason = "还没携带武器（至少一把才能出发）";
            return false;
        }

        reason = null;
        return true;
    }

    // ── 内部 ──

    private void NormalizeLoadout(CharacterDefinitionSO definition)
    {
        _character = definition;

        // 1) 清掉角色不允许的（含空 id）
        for (int i = _weaponLoadoutIds.Count - 1; i >= 0; i--)
        {
            string id = _weaponLoadoutIds[i];
            if (string.IsNullOrEmpty(id) || !IsAllowed(definition, id))
                _weaponLoadoutIds.RemoveAt(i);
        }

        // 2) 去重（保留先出现的）
        var seen = new HashSet<string>();
        for (int i = _weaponLoadoutIds.Count - 1; i >= 0; i--)
        {
            if (!seen.Add(_weaponLoadoutIds[i]))
                _weaponLoadoutIds.RemoveAt(i);
        }

        // 3) 截断到携带上限
        int maxSlots = MaxSlots(definition);
        while (_weaponLoadoutIds.Count > maxSlots)
            _weaponLoadoutIds.RemoveAt(_weaponLoadoutIds.Count - 1);

        // 4) 一件都不剩就补默认武器：**一直补到携带上限**（枪手默认带两把）。
        //    只在"空"的时候补，不覆盖玩家自己挑的配置（比如枪手只带一把枪出门）。
        //    只补白名单内、未重复、非空的项 —— 少补一把的表现是"进关卡只有一把枪"，
        //    而配置里明明写着两把，很难归因到这一步
        if (_weaponLoadoutIds.Count > 0 || definition.defaultWeaponIds == null) return;

        int slots = MaxSlots(definition);
        for (int i = 0; i < definition.defaultWeaponIds.Count && _weaponLoadoutIds.Count < slots; i++)
        {
            string id = definition.defaultWeaponIds[i];
            if (string.IsNullOrEmpty(id)) continue;
            if (!IsAllowed(definition, id)) continue;
            if (_weaponLoadoutIds.Contains(id)) continue;

            _weaponLoadoutIds.Add(id);
        }
    }

    private static int MaxSlots(CharacterDefinitionSO definition) =>
        definition != null ? Mathf.Max(1, definition.maxWeaponSlots) : 1;

    /// <summary>
    /// 白名单为空表示「不做限制」（只按携带上限约束）。
    /// 这样新增武器不必回头改每个角色的资产 —— 而漏改的表现是"买了却装不上"。
    /// </summary>
    private static bool IsAllowed(CharacterDefinitionSO definition, string weaponId)
    {
        List<string> allowed = definition.allowedWeaponIds;
        if (allowed == null || allowed.Count == 0) return true;

        return allowed.Contains(weaponId);
    }
}
