/// <summary>
/// 数值修饰符类型
/// 最终值 = (基础值 + Sum(Add)) * (1 + Sum(Multiply))，Override 直接覆盖
/// </summary>
public enum EModifierType
{
    Add,        // 加法
    Multiply,   // 乘法（相对值：0.5 = +50%）
    Override,   // 覆盖
}
