using TMPro;
using UnityEngine;

/// <summary>
/// 传送门状态文本（世界空间，挂在门上方）：倒计时秒数 / 等待所有玩家 / 还不能出发的原因。
///
/// <para>
/// <b>为什么是轮询而不是事件：</b>它显示的是一条**每秒都在变**的倒计时，
/// 天然是"读状态"而不是"响应变化"。轮询本身不贵 —— 贵的是每帧重建字符串，
/// 所以这里只在**秒数变化**或**文案变化**时才给 TMP 赋值（TMP 赋值会触发整块字形网格重建）。
/// </para>
///
/// <para>
/// 文案优先级：已出发（隐藏）→ 倒计时 → 人不齐（"等待所有玩家"）→ 配置不全（原因）→ "准备出发…"。
/// 配置不全的原因直接取 <see cref="IRunSessionService.IsReadyToDepart"/> —— 出发判据只有那一份，
/// 这里不自己检查角色/关卡/装备。
/// </para>
/// </summary>
public class PortalStatusView : MonoBehaviour
{
    [Tooltip("要显示状态的传送门；留空则在本对象及父级上找")]
    [SerializeField] private PortalController _portal;

    [Tooltip("状态文本")]
    [SerializeField] private TextMeshProUGUI _label;

    [Tooltip("整块可视根（含底板）。留空则只开关 _label 所在对象")]
    [SerializeField] private GameObject _visualRoot;

    private int _lastSecond = int.MinValue;
    private string _lastText;

    private void Awake()
    {
        if (_portal == null) _portal = GetComponentInParent<PortalController>();
        if (_portal == null) Debug.LogError("[PortalStatusView] 没有找到 PortalController，状态文本不会更新。", this);
        if (_label == null) Debug.LogError("[PortalStatusView] 未接状态文本（_label）。", this);

        SetVisible(false);
    }

    private void Update()
    {
        if (_portal == null || _label == null) return;

        // 已出发：场景正在切换，继续显示倒计时没有意义
        if (_portal.IsDeparting)
        {
            SetVisible(false);
            return;
        }

        if (_portal.IsCountingDown)
        {
            int second = Mathf.CeilToInt(_portal.CountdownRemaining);
            if (second == _lastSecond) return;   // 同一秒内不重建字符串（零逐帧分配）

            _lastSecond = second;
            Apply(second.ToString());
            return;
        }

        _lastSecond = int.MinValue;
        Apply(_portal.AreAllPlayersInside ? ReadyText() : "等待所有玩家");
    }

    /// <summary>人都到齐了：能出发就提示准备，不能就说清缺什么。</summary>
    private static string ReadyText()
    {
        IRunSessionService session = RunSessionService.Service;
        if (session == null) return "还不能出发";

        return session.IsReadyToDepart(out string reason) ? "准备出发…" : reason;
    }

    /// <summary>文案没变就不碰 TMP（赋值会触发字形网格重建）。</summary>
    private void Apply(string text)
    {
        if (text == _lastText)
        {
            SetVisible(true);
            return;
        }

        _lastText = text;
        _label.text = text;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        GameObject target = _visualRoot != null ? _visualRoot : _label.gameObject;
        if (target.activeSelf != visible) target.SetActive(visible);
    }
}
