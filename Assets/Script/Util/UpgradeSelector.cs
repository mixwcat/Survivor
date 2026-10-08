using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 升级选项随机抽取器
/// 纯逻辑类（静态、无状态），无 MonoBehaviour 依赖，可独立测试。
///
/// 只做一件事：从给定升级池里**不重复**地抽 count 个。
/// 池子不足时尾部槽位留空（由面板把该槽位置灰），**不做静默兜底**——
/// 「升级池为空」属于配置缺失，应当报警，而不是拿一个占位升级把问题盖住。
/// </summary>
public static class UpgradeSelector
{
    /// <summary>
    /// 抽取用的随机源。
    ///
    /// <para>
    /// 刻意**不用** <c>UnityEngine.Random</c>：它是进程级全局状态，无法注入、无法复现，
    /// 而且联机时各端各抽各的会让同一个玩家的升级选项在两端不一致
    /// （正确做法是权威端抽完广播，或各端用同一个种子）。
    /// </para>
    /// </summary>
    private static System.Random _rng = new System.Random();

    /// <summary>
    /// 注入随机源：联机时由权威端传同一个种子，测试时可传固定种子复现。
    /// 传 null 等价于恢复默认随机源。
    /// </summary>
    public static void SetRandomSource(System.Random rng)
    {
        _rng = rng ?? new System.Random();
    }

    /// <summary>
    /// 恢复默认随机源。由 <see cref="GameBootstrap.ResetStatics"/> 在每次进入 Play 前调用
    /// （关闭 Domain Reload 时静态字段跨会话存活）。
    /// </summary>
    public static void Reset()
    {
        _rng = new System.Random();
    }

    /// <summary>
    /// 从 <paramref name="pool"/> 中不重复地抽取 count 个升级。
    /// 返回数组长度恒为 count；池子不足或为空时，尾部元素为 <c>default</c>（<see cref="UpgradeOption.IsValid"/> 为 false）。
    /// </summary>
    /// <param name="context">仅用于告警文案（如「玩家及其武器」「塔 Tower_Teto」）。</param>
    public static UpgradeOption[] PickDistinct(IReadOnlyList<UpgradeOption> pool, int count, string context)
    {
        var result = new UpgradeOption[count];
        if (count <= 0) return result;

        // 先滤掉无效项再抽：缺 SO 或缺目标实体的选项不应占用一个槽位
        var remaining = new List<UpgradeOption>();
        if (pool != null)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                UpgradeOption option = pool[i];
                if (option.IsValid)
                {
                    remaining.Add(option);
                }
                else
                {
                    // 不静默跳过：缺目标实体意味着「点了不会有任何反应」，
                    // 那比池子为空更难排查，必须留下线索（抽取由玩家操作触发，不会刷屏）
                    Debug.LogWarning($"[UpgradeSelector] {context} 的升级池里有一项缺少 LevelUpSO 或目标实体，" +
                                     "已跳过该选项。请检查对应 EntitySO 的 upgrades 与目标实体的 StatModel 是否就绪。");
                }
            }
        }

        if (remaining.Count == 0)
        {
            Debug.LogWarning($"[UpgradeSelector] {context} 的升级池为空，这些槽位将没有可选项。" +
                             "请检查对应 EntitySO 的 upgrades 配置。");
            return result;
        }

        int filled = remaining.Count < count ? remaining.Count : count;
        for (int i = 0; i < filled; i++)
        {
            int index = _rng.Next(remaining.Count);
            result[i] = remaining[index];
            remaining.RemoveAt(index);
        }

        return result;
    }
}
