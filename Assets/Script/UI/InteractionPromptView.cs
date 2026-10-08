using TMPro;
using UnityEngine;

/// <summary>
/// 世界空间交互提示 —— 显示在交互物上方（"E 管理防御塔" / 移动端"点击 管理防御塔"），
/// 移动端可直接点它触发交互。
///
/// <para>
/// <b>为什么是世界空间：</b>提示挂在交互物上方，玩家看的是"那个东西"，不是屏幕角落的按钮。
/// 旧实现把提示物（箭头 + "E" 文本）各做一份在塔 prefab 里，平台差异又写在领域脚本里
/// （<c>#if UNITY_STANDALONE_WIN / #elif UNITY_ANDROID</c>）—— 非这两个平台会静默什么都不显示。
/// 现在平台差异只在本文件里，且 <c>#else</c> 有明确默认值（显示按键提示），不会再"静默无提示"。
/// </para>
///
/// <para>
/// <b>谁调用：</b>交互物在 <see cref="IInteractable.OnSelected"/> / <c>OnDeselected</c> 里
/// <see cref="Show"/> / <see cref="Hide"/> —— 选中是**按交互者**的，只有交互物自己知道
/// "这次选中是不是本地玩家"（<see cref="IInteractor.IsLocal"/>）。
/// </para>
///
/// <para>
/// <b>自动接线：</b>文案组件默认在 <see cref="VisualRoot"/> 下按类型查找，
/// 于是新增交互物只要挂上本组件（或挂上带本组件的提示 prefab），不必逐个拖引用。
/// </para>
/// </summary>
public class InteractionPromptView : MonoBehaviour
{
    /// <summary>按键提示文案。非 Android 平台（含编辑器）默认显示它。</summary>
    private const string KeyHint = "E";

    /// <summary>触屏提示文案：移动端没有键盘，"E" 没有意义。</summary>
    private const string TouchHint = "点击";

    [Tooltip("要显隐的世界空间提示物。留空 = 本物体")]
    public GameObject VisualRoot;

    [Tooltip("跟着一起显隐的额外表现物（例如塔上原有的箭头）")]
    public GameObject ExtraVisual;

    [Tooltip("文案组件。留空 = 在 VisualRoot 下按类型自动查找")]
    public TextMeshProUGUI Label;

    private IInteractor _interactor;
    private IInteractable _target;
    private bool _visible;

    private GameObject Root => VisualRoot != null ? VisualRoot : gameObject;

    /// <summary>
    /// 在交互物自身或子物体上找提示视图（找不到返回 null —— 提示是可选的）。
    /// 交互物侧统一用它，避免每个实现各写一遍查找逻辑。
    /// </summary>
    public static InteractionPromptView FindIn(Component owner)
    {
        if (owner == null) return null;

        InteractionPromptView view = owner.GetComponent<InteractionPromptView>();
        return view != null ? view : owner.GetComponentInChildren<InteractionPromptView>(true);
    }

    private void Awake()
    {
        if (Label == null)
        {
            Transform root = Root.transform;
            Label = root.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // 与传感器同物体时必须显式指定 VisualRoot：留空会退化成"关掉自己"，
        // 把交互物（传感器 + 交互实现）一起关掉，而且不报错
        if (VisualRoot == null && GetComponent<InteractionSensor>() != null)
        {
            Debug.LogError($"[InteractionPromptView] {gameObject.name} 与 InteractionSensor 同物体，" +
                           "必须显式指定 VisualRoot，否则会把自己的交互一起关掉。");
        }

        // 强制走一次隐藏：_visible 初值是 false，直接 SetVisible(false) 会被"状态没变"挡掉，
        // 于是 prefab 上那个"E"会一直亮着
        _visible = true;
        SetVisible(false);
    }

    /// <summary>显示提示。非本地交互者、目标不可用、文案为空都直接隐藏。</summary>
    public void Show(IInteractor interactor, IInteractable target)
    {
        if (!InteractorResolver.IsAlive(interactor) || !InteractorResolver.IsAlive(target))
        {
            Hide();
            return;
        }

        // 提示是**本地表现**：远程玩家的交互者同样会选中目标，但不该点亮本地屏幕
        if (!interactor.IsLocal)
        {
            Hide();
            return;
        }

        if (!target.CanInteract(interactor, out _))
        {
            Hide();
            return;
        }

        _interactor = interactor;
        _target = target;

        if (Label != null)
        {
            InteractionPrompt prompt = target.GetPrompt(interactor);
            string action = prompt.ToString();
            Label.text = string.IsNullOrEmpty(action) ? Hint : Hint + " " + action;
        }

        SetVisible(true);
    }

    /// <summary>隐藏提示。重复调用是空操作。</summary>
    public void Hide()
    {
        _interactor = null;
        _target = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible) return;

        _visible = visible;

        GameObject root = Root;
        if (root != null) root.SetActive(visible);

        if (ExtraVisual != null) ExtraVisual.SetActive(visible);
    }

#if UNITY_ANDROID
    /// <summary>平台对应的按键文案。</summary>
    private static string Hint => TouchHint;

    /// <summary>
    /// 移动端点提示即交互。**只在 Android 编译**：PC 上的鼠标点击属于"世界指针"，
    /// 与塔放置的确认点击是同一路输入，两边都响应会让"在已有塔附近放塔"顺手打开塔面板。
    /// </summary>
    private void OnMouseDown()
    {
        if (!_visible) return;

        // 塔放置期间的世界点击归放置流程：提示本身不该抢这次点击
        if (TowerPlacementController.ActiveCount > 0) return;

        IInteractor interactor = _interactor;
        if (!InteractorResolver.IsAlive(interactor)) return;

        // 显示期间可用性可能已经变了（出行被锁定、角色被切换），点下去之前再问一次
        IInteractable target = _target;
        if (!InteractorResolver.IsAlive(target) || !target.CanInteract(interactor, out _)) return;

        // 上报"按住"状态：输入层读不到手指是否还按着屏幕（那是 UI 事件），
        // 而"按住若干秒"的交互（如修车）要能轮询到它
        MobileInputHandle.TouchHeld = true;
        interactor.TryInteract();
    }

    /// <summary>抬起：结束"按住"状态，按住类交互随即中断。</summary>
    private void OnMouseUp()
    {
        MobileInputHandle.TouchHeld = false;
    }

    /// <summary>提示被隐藏/销毁时兜底清掉按住状态，避免残留导致玩家被一直定身。</summary>
    private void OnDisable()
    {
        MobileInputHandle.TouchHeld = false;
    }
#else
    /// <summary>平台对应的按键文案。</summary>
    private static string Hint => KeyHint;
#endif
}
