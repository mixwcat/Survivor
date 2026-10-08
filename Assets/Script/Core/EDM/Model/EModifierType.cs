/// <summary>
/// 数值修饰符类型。
/// 最终值 = (基础值 + Sum(Add)) * (1 + Sum(Multiply))，Override 直接覆盖
///
/// <para>
/// ⚠️ <b>成员编号会落盘</b>（<c>LevelUpSO.statModifiers[].ModifierType</c> 是**整数**），
/// 所以一律**显式赋值**、**只在末尾追加**、**删除时保留编号空洞**。
/// 中段插入会让所有既有升级资产的修饰类型静默改变（本该 +5 的变成乘算之类），且不报任何错 ——
/// 与 <c>StatType</c> 是同一类风险。
/// </para>
/// </summary>
public enum EModifierType
{
    Add = 0,        // 加法
    Multiply = 1,   // 乘法（相对值：0.5 = +50%）
    Override = 2,   // 覆盖
}
