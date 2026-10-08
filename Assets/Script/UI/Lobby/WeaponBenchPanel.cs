using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 武器台面板 —— 在大厅里解锁武器并挑选出发装备。
///
/// <para>
/// <b>解锁与装备是两件事，但放同一个界面</b>：玩家走到武器台前的意图就是"弄装备"，
/// 分成两个面板会让"先解锁再换到另一个面板装上"变成两步无意义的操作。
/// 点击行为按状态决定：未解锁 → 花金币买；已解锁 → 加入/移出出发装备。
/// </para>
///
/// <para>
/// <b>购买是明确事务点</b>：扣款 → 记账 → **立即落盘** → 刷新 UI（由
/// <see cref="IPlayerProfileService.TryUnlockWeapon"/> 内部完成，见那里的注释）。
/// 装备选择只写 <see cref="RunSession"/>（跨场景临时数据，不落盘）。
/// </para>
///
/// <para>
/// <b>解锁逻辑暂缓</b>：可用性统一问 <see cref="UnlockPolicy"/>（当前"默认全都拥有"），
/// 购买分支保留在下面 —— 接回解锁时只改策略常量，这里一行都不用动。
/// </para>
///
/// <para>
/// prefab 从 <c>ChooseTowerPanel.prefab</c> 复制：它的槽位字段（图标 / 名称 / 消耗 / 按钮 ×3）
/// 与这里需要的一一对应，所以只换脚本 guid，引用零手工重拖。
/// </para>
/// </summary>
public class WeaponBenchPanel : BasePanel
{
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

    private IReadOnlyList<WeaponEntitySO> _weapons;

    /// <summary>注入可选武器（Init 之前调用）。</summary>
    public void SetWeapons(IReadOnlyList<WeaponEntitySO> weapons)
    {
        _weapons = weapons;
    }

    public override void Init()
    {
        Bind(0, imgIcon1, txtDescription1, txtConsumption1, button1);
        Bind(1, imgIcon2, txtDescription2, txtConsumption2, button2);
        Bind(2, imgIcon3, txtDescription3, txtConsumption3, button3);

        btnClose.onClick.AddListener(() =>
        {
            UIService.Service?.HidePanel<WeaponBenchPanel>();
        });
    }

    private WeaponEntitySO GetWeapon(int index)
    {
        return _weapons != null && index >= 0 && index < _weapons.Count ? _weapons[index] : null;
    }

    private void Bind(int index, Image icon, TextMeshProUGUI description,
                      TextMeshProUGUI consumption, Button button)
    {
        WeaponEntitySO weapon = GetWeapon(index);

        bool visible = weapon != null;
        if (button != null) button.gameObject.SetActive(visible);
        if (icon != null) icon.gameObject.SetActive(visible);
        if (description != null) description.gameObject.SetActive(visible);
        if (consumption != null) consumption.gameObject.SetActive(visible);

        if (!visible) return;

        IPlayerProfileService profile = PlayerProfileService.Service;
        IRunSessionService session = RunSessionService.Service;

        bool unlocked = UnlockPolicy.IsWeaponAvailable(profile, weapon.id);
        bool equipped = session != null && ContainsWeapon(session.Current.WeaponLoadoutIds, weapon.id);
        bool allowed = IsAllowedForCharacter(weapon.id);

        if (icon != null) icon.sprite = weapon.displaySprite;

        if (description != null)
        {
            string state = !unlocked ? "未解锁" : !allowed ? "本角色不可携带" : equipped ? "已装备" : "点击装备";
            description.text = $"{weapon.displayName}\n{state}";
        }

        if (consumption != null)
            consumption.text = unlocked ? "-" : weapon.unlockPrice.ToString();

        if (button != null)
        {
            // 已锁定的 session 不能改装备；未解锁的仍然可以买（买不改变出行配置）。
            // 角色不允许携带的直接置灰 —— 点了被领域层拒绝只会表现成"没反应"。
            bool canInteract = unlocked && allowed && !(session != null && session.IsLocked);
            button.interactable = canInteract;

            // 必须先清空：Bind 既在 Init 里跑，也在每次点击后的 RefreshAll 里跑，
            // 直接 AddListener 会让监听器逐次累加 —— 点一下触发两次（装/卸互相抵消，
            // 表现为"点了没反应"），而且每点一次再多一个
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClick(weapon));
        }
    }

    /// <summary>
    /// 当前角色是否允许携带这把武器。角色未就绪（还在选角）时按"允许"处理 ——
    /// 那时玩家本来就要先选角色，提前置灰会让武器台看起来坏了。
    /// </summary>
    private static bool IsAllowedForCharacter(string weaponId)
    {
        CharacterDefinitionSO definition = PlayerManager.Service?.LocalPlayer?.Role?.Definition;
        if (definition == null) return true;

        List<string> allowed = definition.allowedWeaponIds;
        if (allowed == null || allowed.Count == 0) return true;

        return allowed.Contains(weaponId);
    }

    /// <summary>装备列表是只读快照（<c>IReadOnlyList</c>），没有 <c>Contains</c> 扩展。</summary>
    private static bool ContainsWeapon(IReadOnlyList<string> loadout, string weaponId)
    {
        if (loadout == null) return false;

        for (int i = 0; i < loadout.Count; i++)
        {
            if (loadout[i] == weaponId) return true;
        }

        return false;
    }

    private void OnClick(WeaponEntitySO weapon)
    {
        IPlayerProfileService profile = PlayerProfileService.Service;
        IRunSessionService session = RunSessionService.Service;
        if (weapon == null || profile == null || session == null) return;

        if (!UnlockPolicy.IsWeaponAvailable(profile, weapon.id))
        {
            if (profile.TryUnlockWeapon(weapon.id, weapon.unlockPrice))
            {
                AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
                RefreshAll();
                return;
            }

            // 余额不足与"保存失败已回滚"都会走到这里。
            // **失败时不刷新 UI**：内存态已被回滚，界面看起来"没变化"才是对的；
            // 刷新反而会让玩家以为买到了（这正是"伪成功"的形态）。
            Debug.LogWarning($"[WeaponBenchPanel] 购买武器「{weapon.id}」失败（余额不足，或档案保存失败已回滚）。");
            _ = UIService.Service?.ShowPanelAsync<TipsPanel>();
            return;
        }

        // 先算出目标装备列表，再整体交给 session 校验：
        // 「至少一把」「携带上限」「角色白名单」三条规则都在那里，
        // 面板自己再算一遍迟早会与领域规则漂移。
        var staged = new List<string>(session.Current.WeaponLoadoutIds);
        if (!staged.Remove(weapon.id)) staged.Add(weapon.id);

        if (!session.TrySetLoadout(staged, out string reason))
        {
            Debug.Log($"[WeaponBenchPanel] 装备变更被拒绝：{reason}");
            return;
        }

        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
        RefreshAll();
    }

    private void RefreshAll()
    {
        Bind(0, imgIcon1, txtDescription1, txtConsumption1, button1);
        Bind(1, imgIcon2, txtDescription2, txtConsumption2, button2);
        Bind(2, imgIcon3, txtDescription3, txtConsumption3, button3);
    }

    public override void EscLogic()
    {
        UIService.Service?.HidePanel<WeaponBenchPanel>();
    }
}
