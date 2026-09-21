using UnityEngine;

/// <summary>
/// 武器选择 SO —— 专用于 ChooseWeaponPanel，仅提供展示信息。
/// 激活逻辑由 <see cref="PlayerWeaponController.SelectWeapon"/> 处理（不再使用全局 SO 事件）。
/// </summary>
[CreateAssetMenu(fileName = "WeaponSelectSO", menuName = "Game/Selection/Weapon Select")]
public class WeaponSelectSO : ScriptableObject
{
    [Header("UI 显示")]
    public string displayName;
    public Sprite displaySprite;
}
