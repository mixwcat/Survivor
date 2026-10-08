using TMPro;
using UnityEngine;

/// <summary>
/// 推车上方的耐久百分比文本（世界空间）。
///
/// <para>
/// <b>事件驱动，不轮询</b>：订阅 <see cref="BaseHealthController.HealthChanged"/>，
/// 只在耐久真的变化时改一次文本。TMP 赋值会触发整块字形网格重建，逐帧赋值是性能红线 ——
/// 所以这里没有 <c>Update</c>。
/// </para>
///
/// <para>
/// 订阅的是一般化的 <see cref="BaseHealthController"/>：推车、塔、玩家都能用，
/// 上限变化（升级加耐久上限）也会通过同一个事件到达，不必额外处理。
/// </para>
/// </summary>
public class CartHealthPercentView : MonoBehaviour
{
    [Tooltip("要显示的耐久控制器；留空则在本对象及父级上找（世界空间 UI 通常是它的子物体）")]
    [SerializeField] private BaseHealthController _health;

    [Tooltip("百分比文本，如「85%」")]
    [SerializeField] private TextMeshProUGUI _label;

    private void Awake()
    {
        if (_health == null) _health = GetComponentInParent<BaseHealthController>();
        if (_health == null) Debug.LogError("[CartHealthPercentView] 没有找到 BaseHealthController，百分比不会更新。", this);
        if (_label == null) Debug.LogError("[CartHealthPercentView] 未接百分比文本（_label）。", this);
    }

    private void OnEnable()
    {
        if (_health == null) return;

        _health.HealthChanged += HandleHealthChanged;

        // 订阅时补一次当前值：面板可能是在掉血之后才被激活的
        Apply(_health.CurrentHealth, _health.MaxHealth);
    }

    private void OnDisable()
    {
        if (_health != null) _health.HealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        Apply(current, max);
    }

    private void Apply(float current, float max)
    {
        if (_label == null) return;

        // CeilToInt：还剩一点耐久时显示 1% 而不是 0%（0% 看着像已经毁了）
        int percent = max > 0f ? Mathf.CeilToInt(current / max * 100f) : 0;

        // 百分比没变就不写：TMP 的 text setter 虽然会在内容相同时短路，
        // 但字符串本身每帧都要分配一次（受击约 1 次/秒/只怪，属于可省的白费）
        if (percent == _lastPercent) return;

        _lastPercent = percent;
        _label.text = percent.ToString() + "%";
    }

    /// <summary>上次写进 Label 的百分比（-1 = 还没写过）。</summary>
    private int _lastPercent = -1;
}
