using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 推车上方的**修理进度条**：订阅 <see cref="CartRepairInteractable.ProgressChanged"/>，
/// 进度为 0 时整块隐藏（没在修就不该占屏幕）。
///
/// <para>
/// <b>事件驱动，不轮询</b>：进度只在真的变化时才推过来，这里只写一次
/// <c>Image.fillAmount</c> —— 用填充图而不是 <c>Slider</c>，避免 Slider 的整套
/// handle/fill 布局计算。
/// </para>
///
/// <para>
/// 交互物引用在运行时按父级解析：这个 prefab 是挂在推车下的，
/// 而 prefab 资产里没法存场景对象的引用。
/// </para>
/// </summary>
public class CartRepairProgressView : MonoBehaviour
{
    [Tooltip("要显示的修理交互物；留空则在本对象及父级上找")]
    [SerializeField] private CartRepairInteractable _repair;

    [Tooltip("填充图。**不要**设成 Image.Type = Filled：没有 sprite 时 fillAmount 会被 Unity 忽略，" +
             "进度用 anchorMax 表示（见 Apply）")]
    [SerializeField] private Image _fill;

    [Tooltip("整块可视根（含底槽）。留空则用填充图的父对象")]
    [SerializeField] private GameObject _root;

    private void Awake()
    {
        if (_repair == null) _repair = GetComponentInParent<CartRepairInteractable>();
        if (_repair == null) Debug.LogError("[CartRepairProgressView] 没有找到 CartRepairInteractable，进度条不会更新。", this);
        if (_fill == null) Debug.LogError("[CartRepairProgressView] 未接填充图（_fill）。", this);

        if (_root == null && _fill != null)
            _root = _fill.transform.parent != null ? _fill.transform.parent.gameObject : _fill.gameObject;

        Apply(0f);
    }

    private void OnEnable()
    {
        if (_repair == null) return;

        _repair.ProgressChanged += Apply;
        Apply(_repair.Progress01);
    }

    private void OnDisable()
    {
        if (_repair != null) _repair.ProgressChanged -= Apply;
    }

    private void Apply(float progress01)
    {
        if (_fill != null)
        {
            // 用**锚点**表示进度，而不是 Image.fillAmount：
            // Image 没有 sprite 时 OnPopulateMesh 会退化成"整块矩形"、直接忽略 fillAmount，
            // 表现是"进度条一出现就是满的、按住也不动"。锚点方案不依赖任何 sprite 资源。
            RectTransform rect = _fill.rectTransform;
            Vector2 max = rect.anchorMax;
            if (!Mathf.Approximately(max.x, progress01))
                rect.anchorMax = new Vector2(progress01, max.y);
        }

        if (_root == null) return;

        bool visible = progress01 > 0f;
        if (_root.activeSelf != visible) _root.SetActive(visible);
    }
}
