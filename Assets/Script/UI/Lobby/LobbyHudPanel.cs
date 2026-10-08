using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 大厅 HUD —— 常显金币与当前哨站。
///
/// <para>
/// <b>数据来自 Profile 事件，不是轮询</b>：轮询会让"金币变了但界面没变"这类问题
/// 只能靠调刷新频率来掩盖；而订阅 <see cref="IPlayerProfileService.Changed"/> 之后，
/// 任何金币变化（购买、结算）都会立刻反映出来，且不消耗逐帧开销。
/// </para>
///
/// <para>
/// 它只读 Profile，不写 —— 显示层不该有副作用。
/// </para>
/// </summary>
public class LobbyHudPanel : BasePanel
{
    public TextMeshProUGUI txtCoins;
    public TextMeshProUGUI txtOutpost;

    private IPlayerProfileService _profile;

    public override void Init()
    {
        _profile = PlayerProfileService.Service;
        if (_profile != null) _profile.Changed += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (_profile != null) _profile.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (_profile == null || _profile.Profile == null) return;

        if (txtCoins != null)
            txtCoins.text = $"金币 {_profile.Profile.coins}";

        if (txtOutpost != null)
            txtOutpost.text = $"哨站 {_profile.Profile.currentOutpost}";
    }
}
