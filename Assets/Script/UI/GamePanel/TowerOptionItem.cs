using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 塔升级列表里的一行。
///
/// <para>
/// 它是**纯表现**：只持有控件引用，绑定由 <see cref="TowerLevelUpPanel"/> 负责。
/// 面板按需克隆本组件所在的 GameObject（模板自身隐藏），所以行数不受 prefab 限制 ——
/// 这正是塔升级从"固定三槽随机抽"改成"确定性完整列表"的前提：
/// Teto 有 5 项、Rin/Luo 有 4 项，写死三个槽位必然装不下。
/// </para>
/// </summary>
public class TowerOptionItem : MonoBehaviour
{
    [Tooltip("整行按钮")]
    public Button Button;

    [Tooltip("升级图标（LevelUpSO.levelUpSprite）")]
    public Image Icon;

    [Tooltip("升级名称/描述（LevelUpSO.levelUpText）")]
    public TextMeshProUGUI Title;

    [Tooltip("消耗点数；点数不足时会显示为提示文案")]
    public TextMeshProUGUI Cost;

    /// <summary>当前这一行绑定的选项（未绑定时为 default，IsValid 为 false）。</summary>
    public UpgradeOption Option { get; private set; }

    /// <summary>绑定数据。调用方负责清掉旧的 onClick 监听。</summary>
    public void Bind(in UpgradeOption option)
    {
        Option = option;
    }
}
