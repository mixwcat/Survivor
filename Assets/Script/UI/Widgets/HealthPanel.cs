using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血量条控件 —— 显示"当前 / 上限"与比例，由 <see cref="PlayerHudBinder"/> 绑定到本地玩家。
///
/// <para>
/// <b>它只订阅、不轮询</b>：数据来自 <see cref="BaseHealthController.HealthChanged"/>，
/// 掉血/治疗/加血上限都会立刻反映出来。旧实现是玩家控制器在 <c>TakeDamage</c> 里
/// 直接推头顶那条血条的 UI —— 领域代码认识具体 UI，而且只有受伤才会刷新
/// （治疗、加血上限都不动，血条会停在旧比例）。
/// </para>
///
/// <para>
/// <b>绑定可以重复调用</b>（HUD 面板跨场景复用，每次显示都重新绑），
/// 所以 <see cref="Bind"/> 第一步必须退订上一个玩家 —— 否则血条会一直挂在一个已销毁的
/// 对象上，表现为"第二局血条永远不动"，且不报错。
/// </para>
/// </summary>
public class HealthPanel : MonoBehaviour
{
    public Slider healthSlider;
    public TextMeshProUGUI healthText;

    private BaseHealthController _health;

    /// <summary>绑定血量来源（传 null 表示没有玩家，血条归零）。</summary>
    public void Bind(BaseHealthController health)
    {
        Unbind();

        _health = health;

        if (_health != null)
        {
            _health.HealthChanged += HandleHealthChanged;
            Apply(_health.CurrentHealth, _health.MaxHealth);
            return;
        }

        Apply(0f, 0f);
    }

    /// <summary>退订。重复调用是空操作。</summary>
    public void Unbind()
    {
        if (_health == null) return;

        _health.HealthChanged -= HandleHealthChanged;
        _health = null;
    }

    private void OnDisable()
    {
        // 面板被销毁/隐藏时退订：留着订阅就是"已销毁对象上的回调"
        Unbind();
    }

    private void HandleHealthChanged(float current, float max)
    {
        Apply(current, max);
    }

    private void Apply(float current, float max)
    {
        if (healthSlider != null)
            healthSlider.value = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // 向上取整：活着的时候不该显示成 "0 / 100"
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }
}
