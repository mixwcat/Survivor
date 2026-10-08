/// <summary>
/// 音频资源 ID。
/// 与 Addressables 地址 <c>Music/&lt;枚举名&gt;</c> 一一对应（见 <see cref="AssetKeys.Music"/>）。
/// 游戏事件不使用枚举，改用强类型 C# 事件 / 接口。
///
/// <para>
/// <b>本枚举的「值」不承载语义</b>：地址拼的是**成员名**，所以中段插入成员**不会**像
/// <c>StatType</c> / <c>EModifierType</c> 那样让既有资产错位（那两个的值会被读，本枚举不会）——
/// 这也是这里刻意不写显式赋值的原因，别照抄成「所有枚举都要显式赋值」。
/// 代价在另一头：<b>重命名成员必须同步重命名对应的音频资产</b>，否则地址拼不出来（加载失败，有日志）。
/// </para>
/// </summary>
public enum ResourceEnum
{
    ChooseWeapon,
    Heal,
    LoseGame,
    OnMouseClickUI,
    PickExp,
    PlayerAttackEnemy,
    PlayerGetHurt,
    PlayerLevelUP,
    PlayerShoot,
    RinAttack,
    StartGame,
    PlayerMove,
    WinGame,
    bgm
}
