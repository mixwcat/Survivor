/// <summary>
/// 玩家经验/等级控制器接口
/// 抽象等级、经验值、技能点的获取与消耗，支持每个玩家持有独立实例。
/// </summary>
public interface IExperienceController
{
    /// <summary>当前等级</summary>
    int CurrentLevel { get; }

    /// <summary>当前累计经验值（当前等级内）</summary>
    int CurrentExp { get; }

    /// <summary>当前可用技能点</summary>
    int AvailablePoints { get; }

    /// <summary>升到下一级所需经验</summary>
    int ExpToNextLevel { get; }

    /// <summary>等级提升事件（参数：新等级）</summary>
    event System.Action<int> OnLevelUp;

    /// <summary>经验变化事件（参数：当前经验）</summary>
    event System.Action<int> OnExpChanged;

    /// <summary>技能点变化事件（参数：当前技能点）</summary>
    event System.Action<int> OnPointsChanged;

    /// <summary>增加经验值</summary>
    void AddExperience(int amount);

    /// <summary>
    /// 尝试消耗指定数量的技能点。
    /// 点数不足时返回 false，并弹出提示面板。
    /// </summary>
    bool CanUseLevelPoint(int amount);

    /// <summary>增加技能点</summary>
    void AddLevelPoint(int amount);
}
