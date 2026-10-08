/// <summary>
/// 玩家经验/等级控制器接口 —— 抽象等级与经验的累积，支持每个玩家持有独立实例。
///
/// <para>
/// <b>它不管升级点</b>：升级点的余额与消费在 <see cref="IUpgradePointWallet"/>（同样按玩家实例化）。
/// 拆开是因为两者的读者不同 —— 经验条看这里，商店 / 塔界面看钱包；
/// 而原先叫「等级点」的那个字段同时被塔建造、塔升级、角色三选一消费，
/// 名字让人以为它跟等级绑定，实际是通用货币。
/// </para>
/// </summary>
public interface IExperienceController
{
    /// <summary>当前等级</summary>
    int CurrentLevel { get; }

    /// <summary>当前累计经验值（当前等级内）</summary>
    int CurrentExp { get; }

    /// <summary>升到下一级所需经验</summary>
    int ExpToNextLevel { get; }

    /// <summary>等级提升事件（参数：新等级）。升级奖励由 <c>PlayerProgressionController</c> 订阅。</summary>
    event System.Action<int> OnLevelUp;

    /// <summary>经验变化事件（参数：当前经验）</summary>
    event System.Action<int> OnExpChanged;

    /// <summary>增加经验值</summary>
    void AddExperience(int amount);
}
