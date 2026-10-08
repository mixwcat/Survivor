using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「选一」升级面板基类：统一持有 3 个选项槽位的控件引用与绑定逻辑。
/// 子类只负责「选项从哪来」和「点了之后做什么」。
///
/// 字段刻意保持平铺（btn1/txt1/img1/… 而不是嵌套列表或数组）：
/// 面板预制体是按**字段名**序列化的，平铺才能把原有引用原样继承下来，零手工重拖。
/// </summary>
public abstract class BaseOptionPanel : BasePanel
{
    /// <summary>
    /// 本面板的选项槽位数（<c>btn1..btn3</c>）。
    ///
    /// <para>
    /// 平铺字段决定了它就是 3 —— 加槽位要同时改 prefab 与 <see cref="RefreshOptionUI"/> 的绑定。
    /// 之所以把它写出来：<see cref="SetOptions"/> 收到**多于 3 项**时，
    /// 超出的部分既不会显示也不会报错（三选一面板不会遇到，但"商店"类面板会，见
    /// <c>WeaponUpgradePanel</c> 的告警）。
    /// </para>
    /// </summary>
    public const int SlotCount = 3;

    [Header("选项 1")]
    public Button btn1;
    public Image img1;
    public TextMeshProUGUI txt1;
    public TextMeshProUGUI txtConsumption1;

    [Header("选项 2")]
    public Button btn2;
    public Image img2;
    public TextMeshProUGUI txt2;
    public TextMeshProUGUI txtConsumption2;

    [Header("选项 3")]
    public Button btn3;
    public Image img3;
    public TextMeshProUGUI txt3;
    public TextMeshProUGUI txtConsumption3;

    private UpgradeOption[] _options;

    /// <summary>当前展示的选项；索引越界或该槽位为空时返回无效项（<see cref="UpgradeOption.IsValid"/> 为 false）。</summary>
    protected UpgradeOption GetOption(int index)
    {
        return _options != null && index >= 0 && index < _options.Length ? _options[index] : default;
    }

    /// <summary>更新选项数据并刷新 UI。</summary>
    protected void SetOptions(UpgradeOption[] options)
    {
        _options = options;
        RefreshOptionUI();
    }

    /// <summary>把当前选项写入 3 个槽位；空槽位清空显示并置灰按钮。</summary>
    protected void RefreshOptionUI()
    {
        BindOption(0, btn1, img1, txt1, txtConsumption1);
        BindOption(1, btn2, img2, txt2, txtConsumption2);
        BindOption(2, btn3, img3, txt3, txtConsumption3);
    }

    private void BindOption(int index, Button button, Image icon, TextMeshProUGUI title, TextMeshProUGUI consumption)
    {
        UpgradeOption option = GetOption(index);
        LevelUpSO so = option.So;

        if (icon != null) icon.sprite = so != null ? so.levelUpSprite : null;
        if (title != null) title.text = BuildTitle(so, option);
        if (consumption != null) consumption.text = so != null ? so.cost.ToString() : "";
        // 不可购买就置灰：空槽位，或「有 SO 但缺目标实体」（点了不会有任何反应）都算
        if (button != null) button.interactable = option.IsValid;
    }

    /// <summary>
    /// 标题 = SO 上的文案 +（有上限时）「Lv 已应用/上限」。
    ///
    /// <para>
    /// 等级按**目标实例**计数（<see cref="IUpgradeStateHolder"/>）：武器的升级记在那一把武器上，
    /// 所以同一项在两把武器上各显示各自的等级。上限为 0（不限次）时不显示等级 ——
    /// 显示「Lv 3/∞」对玩家没有意义。
    /// </para>
    /// </summary>
    private static string BuildTitle(LevelUpSO so, UpgradeOption option)
    {
        if (so == null) return "";

        if (so.MaxLevel <= 0 || option.Target == null) return so.levelUpText;

        return $"{so.levelUpText}（Lv {so.GetAppliedLevel(option.Target)}/{so.MaxLevel}）";
    }
}
