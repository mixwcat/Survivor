using UnityEngine;

/// <summary>
/// 推车耐久控制器 —— 与其它实体共用 <see cref="BaseHealthController"/>，
/// 但**重写了死亡行为**。
///
/// <para>
/// 基类的 <c>Die()</c> 是 <c>Destroy(gameObject)</c>，对推车是错的：
/// 车毁即销毁会让"把物资运到下一个哨站"这件事无从继续，结算也拿不到耐久数据。
/// 这里改为"停摆 + 等待恢复"——失败与否由 <c>StageDirector</c> 决定，
/// 而按 P0 定案（D-05）推车耐久耗尽**不是失败条件**，只是压力。
/// </para>
///
/// <para>
/// 血量上限走 <c>StatModel</c>（<c>CartDataSO.MaxHealth</c>），与其它实体一致。
/// </para>
/// </summary>
public class CartHealthController : BaseHealthController
{
    private CartController _cart;

    /// <summary>耐久比例（0..1）。结算与 HUD 只读它，不直接碰 CurrentHealth。</summary>
    public float HealthNormalized
    {
        get
        {
            float max = MaxHealth;
            return max > 0f ? Mathf.Clamp01(CurrentHealth / max) : 0f;
        }
    }

    protected override void Start()
    {
        base.Start();

        _cart = GetComponent<CartController>();
        CurrentHealth = MaxHealth;
    }

    /// <summary>池化/复用时的重置。推车不进池，但保持与基类契约一致。</summary>
    public override void ResetHealth()
    {
        base.ResetHealth();
        _cart = _cart != null ? _cart : GetComponent<CartController>();
    }

    /// <summary>
    /// 恢复到最大耐久的指定比例。下限取 1 点而不是 0 ——
    /// 否则"恢复了"的下一刻会立刻再次判定为耐久耗尽，陷入停摆循环。
    ///
    /// <para>
    /// <b>必须复位 <see cref="BaseHealthController.IsDead"/></b>：耐久归零时基类会把它置位，
    /// 而它是"拒绝后续一切伤害"的判据 —— 不复位的表现是"修好之后车再也打不掉血"，
    /// 顺带 <see cref="BaseHealthController.Heal"/> 也会被拒（它同样先判 IsDead），
    /// 于是"修好了但血量不涨、百分比不动"，看起来像恢复逻辑坏了。
    /// </para>
    ///
    /// <para>
    /// <b>也必须发 <c>HealthChanged</c></b>：订阅方（车顶的耐久百分比）是**事件驱动**的，
    /// 只改字段不发事件的表现是"车已经修好了，UI 还停在 0%"。
    /// </para>
    /// </summary>
    public void RecoverTo(float ratio)
    {
        IsDead = false;
        CurrentHealth = Mathf.Max(1f, MaxHealth * Mathf.Clamp01(ratio));
        RaiseHealthChanged();
    }

    /// <summary>耐久耗尽：**不销毁**，转入停摆。</summary>
    protected override void Die()
    {
        CartController cart = _cart != null ? _cart : GetComponent<CartController>();
        cart?.EnterDisabled();
    }

    /// <summary>
    /// 客户端应用服务端广播的耐久比例（见 <see cref="CartNetworkSync"/>）。
    ///
    /// <para>
    /// <b>只改数据 + 发事件，不走 <c>TakeDamage</c>/<c>Heal</c>：</b>那两个是"结算"，
    /// 在客户端跑就等于两端各结算一次（还会连带触发伤害数字、击退之类的表现）。
    /// </para>
    /// </summary>
    public void ApplyNetworkHealth(float normalized)
    {
        float target = Mathf.Clamp01(normalized) * MaxHealth;

        // 容差：广播是 15Hz、两端浮点路径也不同，逐位相等是奢望。
        // 不设容差会让 HealthChanged 每帧都发一次，而订阅方里有 TMP 百分比文本
        //（每帧赋值会触发整套字形网格重建，见 CLAUDE.md 性能红线）
        if (Mathf.Abs(target - CurrentHealth) < 0.01f) return;

        CurrentHealth = Mathf.Max(target, 0f);
        IsDead = false;          // 停摆不等于死亡，与 RecoverTo 的语义保持一致

        RaiseHealthChanged();
    }
}
