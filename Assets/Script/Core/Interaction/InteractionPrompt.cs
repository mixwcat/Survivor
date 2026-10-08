/// <summary>
/// 一次交互的提示内容 —— 交互物给**事实**（做什么、对谁做），提示层决定怎么显示。
///
/// <para>
/// 只读结构体：选中目标变化时才构造一次（不是逐帧路径），零 GC 压力。
/// </para>
///
/// <para>
/// <b>这里没有按键名：</b>"E" 还是"点击"由提示层按平台决定 ——
/// 领域脚本里出现 <c>#if UNITY_STANDALONE_WIN / #elif UNITY_ANDROID</c> 时，
/// 非这两个平台会**静默什么都不显示**（塔的旧提示就是这么坏的）。
/// </para>
/// </summary>
public readonly struct InteractionPrompt
{
    /// <summary>动作（"管理" / "挑选武器" / "切换角色"）。</summary>
    public readonly string Action;

    /// <summary>作用对象（"防御塔" / "武器台"），可为空。</summary>
    public readonly string Target;

    public InteractionPrompt(string action, string target = null)
    {
        Action = action;
        Target = target;
    }

    /// <summary>没有任何文案（提示层只显示按键）。</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Action) && string.IsNullOrEmpty(Target);

    /// <summary>拼成一行文案。只在目标变化时调用，不在逐帧路径上。</summary>
    public override string ToString()
    {
        if (string.IsNullOrEmpty(Target)) return Action;
        if (string.IsNullOrEmpty(Action)) return Target;

        return Action + Target;
    }
}
