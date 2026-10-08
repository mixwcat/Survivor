using UnityEngine;

/// <summary>
/// 玩家成长进度控制器（按玩家实例化）—— 把「升级」翻译成「发放升级点」。
///
/// <para>
/// 拆出它的理由：<c>ExperienceLevController</c> 只该回答"练到几级了"，
/// 而"升级要发多少点"是**玩法规则**，会随策划调整（现在每级 1 点，将来可能 2 点）。
/// 规则集中在这里，改数值不用碰经验计算。
/// </para>
///
/// <para>
/// <b>升级只发点，不强制选择</b>：旧实现还会排队一次"免费三选一"，
/// 由 <c>LevelUpCoordinator</c> 弹出面板，且**有待选时不允许关闭** ——
/// 玩家必须当场选完才能继续打，把"成长"变成了"打断"。
/// 现在升级只发点；通用升级 / 专属升级都由 HUD 按钮**按需**打开（见 <c>GamePanel</c> 的三个入口），
/// 两者都花升级点，与塔建造/升级共用同一种货币。
/// </para>
///
/// <para>
/// 本组件**不打开面板**：状态变更只发事件/改数据，UI 由订阅者负责（见架构原则「职责拆分」）。
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(ExperienceLevController))]
[RequireComponent(typeof(UpgradePointWallet))]
public class PlayerProgressionController : MonoBehaviour
{
    /// <summary>每升一级发放的升级点。</summary>
    public const int PointsPerLevel = 1;

    private ExperienceLevController _experience;
    private UpgradePointWallet _wallet;

    private void Awake()
    {
        _experience = GetComponent<ExperienceLevController>();
        _wallet = GetComponent<UpgradePointWallet>();
    }

    private void OnEnable()
    {
        if (_experience != null) _experience.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        if (_experience != null) _experience.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp(int newLevel)
    {
        _wallet?.Grant(PointsPerLevel);
    }
}
