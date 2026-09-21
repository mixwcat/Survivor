/// <summary>
/// 音频资源 ID。
/// 与 Addressables 地址 <c>Music/&lt;枚举名&gt;</c> 一一对应（见 <see cref="AssetKeys.Music"/>）。
/// 游戏事件不使用枚举，改用强类型 C# 事件 / 接口。
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
