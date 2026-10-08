using UnityEngine;

/// <summary>
/// 哨站难度系数 —— 越往后的哨站，敌人越强。
///
/// <para>
/// <b>它是运行时 modifier，不写回 SO：</b><see cref="EnemyDataSO"/> 之类的资产是常驻只读的，
/// 把系数乘进去会跨局、跨玩家污染 —— 而且第一次玩哨站 1 时基础值就已经被改大了，
/// 之后再也回不去（SO 只在编辑器里能改回来）。
/// </para>
///
/// <para>
/// <b>池化复用不会累计：</b>所有 modifier 都挂在同一个来源标记
/// （<see cref="Source"/>）上，注入前先按来源移除一次。
/// 少了这一步，同一个敌人实例被复用时数值会一层层叠上去，
/// 表现是"玩得越久敌人越离谱"，而单看某一次注入完全正常。
/// </para>
/// </summary>
public static class OutpostDifficulty
{
    /// <summary>每往后一个哨站，敌人强度额外增加的比例（哨站 1 = +0%，哨站 2 = +15%…）。</summary>
    public const float BonusPerOutpost = 0.15f;

    /// <summary>
    /// 系数上限。损坏档案里的天文数字会把敌人放大到不可玩 ——
    /// 而"打不过"和"数值溢出"在表现上很难区分。
    /// </summary>
    public const float MaxBonus = 2f;

    /// <summary>
    /// modifier 的来源标记。用固定对象而不是 <c>this</c>/服务实例：
    /// 来源只用于身份匹配，移除时按它比对；它不持有任何 Unity 引用，
    /// 所以跨 Play 会话存活也无害（关闭 Domain Reload 的工程里这点很重要）。
    /// </summary>
    public static readonly object Source = new object();

    /// <summary>当前哨站对应的强度倍率（哨站 1 = 1.0）。</summary>
    public static float Multiplier
    {
        get
        {
            int outpost = PlayerProfileService.Service?.Profile?.currentOutpost ?? 1;
            float bonus = Mathf.Clamp((outpost - 1) * BonusPerOutpost, 0f, MaxBonus);
            return 1f + bonus;
        }
    }

    /// <summary>
    /// 把当前哨站的强度注入实体数值。**幂等**：重复调用不会叠加。
    ///
    /// <para>
    /// 调用点有两处，都必须覆盖：
    /// ① 池化敌人的取出路径（<c>EnemyController.OnGetFromPool</c>）；
    /// ② 非池化的 Boss（<c>EnemySpawner</c> 直接 Instantiate，永远走不到取出路径）。
    /// 漏掉 ② 的表现是"小怪变强了、Boss 还是老样子"，很容易被当成设计如此。
    /// </para>
    /// </summary>
    public static void Apply(EntityStatModel model)
    {
        if (model == null) return;

        // 先清掉上一次注入的，再加新的 —— 这就是"不会累计"的全部秘密
        model.RemoveModifiersFromSource(Source);

        float bonus = Multiplier - 1f;
        if (bonus <= 0f) return;   // 哨站 1：不加恒等于 1 的乘数，免得数值看起来像被改过

        model.AddModifier(new StatModifier(StatType.MaxHealth, bonus, EModifierType.Multiply, Source));
        model.AddModifier(new StatModifier(StatType.Damage, bonus, EModifierType.Multiply, Source));
    }
}
