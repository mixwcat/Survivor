using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 塔台面板 —— 工程师在大厅查看三座防御塔（Rin / Luo / Teto）。
///
/// <para>
/// <b>保留商店语义，解锁逻辑暂缓：</b>槽位仍然是"图标 / 名称 + 状态 / 消耗"的商店形态，
/// 只是当前一切按"已拥有"处理，按钮置灰。接回解锁时把
/// <see cref="Bind"/> 里的按钮接上购买事务即可，UI 结构不用动。
/// </para>
///
/// <para>
/// <b>消耗显示的是关卡内的建塔消耗</b>（<c>BaseTowerDataSO.Cost</c>，花升级点），
/// 不是解锁价格 —— 塔目前没有解锁价格这一说，写死一个数字会变成第二份真相。
/// </para>
///
/// <para>
/// prefab 从 <c>WeaponBenchPanel.prefab</c> 复制：两者的槽位字段（图标 / 描述 / 消耗 / 按钮 ×3）
/// 一一对应，只换脚本 guid，引用零手工重拖。
/// </para>
/// </summary>
public class TowerBenchPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    public Button button1;
    public Button button2;
    public Button button3;

    public Image imgIcon1;
    public Image imgIcon2;
    public Image imgIcon3;

    public TextMeshProUGUI txtDescription1;
    public TextMeshProUGUI txtDescription2;
    public TextMeshProUGUI txtDescription3;

    public TextMeshProUGUI txtConsumption1;
    public TextMeshProUGUI txtConsumption2;
    public TextMeshProUGUI txtConsumption3;

    public Button btnClose;

    private IReadOnlyList<TowerEntitySO> _towers;

    /// <summary>注入要展示的防御塔（Init 之前调用）。</summary>
    public void SetTowers(IReadOnlyList<TowerEntitySO> towers)
    {
        _towers = towers;
    }

    public override void Init()
    {
        Bind(0, imgIcon1, txtDescription1, txtConsumption1, button1);
        Bind(1, imgIcon2, txtDescription2, txtConsumption2, button2);
        Bind(2, imgIcon3, txtDescription3, txtConsumption3, button3);

        if (btnClose != null)
        {
            btnClose.onClick.AddListener(() =>
            {
                UIService.Service?.HidePanel<TowerBenchPanel>();
            });
        }
    }

    private TowerEntitySO GetTower(int index)
    {
        return _towers != null && index >= 0 && index < _towers.Count ? _towers[index] : null;
    }

    private void Bind(int index, Image icon, TextMeshProUGUI description,
                      TextMeshProUGUI consumption, Button button)
    {
        TowerEntitySO tower = GetTower(index);

        // 角色不足/配置不足时把整个槽位藏起来，而不是留一个点了没反应的图标
        bool visible = tower != null;
        if (button != null) button.gameObject.SetActive(visible);
        if (icon != null) icon.gameObject.SetActive(visible);
        if (description != null) description.gameObject.SetActive(visible);
        if (consumption != null) consumption.gameObject.SetActive(visible);

        if (!visible) return;

        if (icon != null) icon.sprite = tower.icon;

        if (description != null)
        {
            string title = string.IsNullOrEmpty(tower.displayName) ? tower.name : tower.displayName;

            // 解锁逻辑暂缓：三座塔默认已拥有。接回解锁后这里改成"未解锁 / 点击解锁"
            string state = "已拥有";
            if (!string.IsNullOrEmpty(tower.description))
                description.text = $"{title}\n{tower.description}\n{state}";
            else
                description.text = $"{title}\n{state}";
        }

        if (consumption != null)
            consumption.text = GetBuildCost(tower).ToString();

        // 已拥有 → 没有可执行的动作，按钮置灰（而不是"点了没反应"）
        if (button != null) button.interactable = false;
    }

    /// <summary>关卡内的建塔消耗（升级点）。数据缺失时给 0，由 ChooseTowerPanel 在关卡里再校验。</summary>
    private static int GetBuildCost(TowerEntitySO tower)
    {
        return tower != null && tower.dataRef is BaseTowerDataSO data ? data.Cost : 0;
    }

    public override void EscLogic()
    {
        UIService.Service?.HidePanel<TowerBenchPanel>();
    }
}
