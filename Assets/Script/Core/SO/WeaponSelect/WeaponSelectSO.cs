using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器选择 SO —— 专用于 ChooseWeaponPanel，职责单一：选择武器并激活
/// 与 LevelUpSO（数值升级）彻底解耦
/// </summary>
[CreateAssetMenu(fileName = "WeaponSelectSO", menuName = "Game/Selection/Weapon Select")]
public class WeaponSelectSO : ScriptableObject
{
    [Header("UI 显示")]
    public string displayName;
    public Sprite displaySprite;

    /// <summary>
    /// 选择事件 —— WeaponManager 订阅此事件来激活对应武器
    /// </summary>
    public event Action OnSelect;

    /// <summary>
    /// 触发选择事件
    /// </summary>
    public void RaiseSelectEvent() => OnSelect?.Invoke();
}
