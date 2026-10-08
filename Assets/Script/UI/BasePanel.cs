using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;


/// <summary>
/// 面板基类。
/// 生命周期由 <see cref="UIService"/> 驱动，顺序固定：
/// 实例化 → <see cref="Initialize"/>（一次性接线）→ <see cref="ShowMe"/>（淡入）。
/// 因此 <c>ShowPanelAsync&lt;T&gt;(p =&gt; p.Configure(...))</c> 注入的数据一定先于 <see cref="Init"/> 生效。
///
/// 动画统一交给 DOTween（unscaled 时间，暂停时仍可播放），面板自身不再需要每帧 Update 轮询。
/// </summary>
public abstract class BasePanel : MonoBehaviour
{
    [Header("动画")]
    [Tooltip("淡入时长（秒，unscaled）")]
    [SerializeField] private float _fadeInDuration = 0.15f;
    [Tooltip("淡出时长（秒，unscaled）")]
    [SerializeField] private float _fadeOutDuration = 0.12f;
    [Tooltip("显示时是否附加轻微缩放弹出（弹窗类面板适用）")]
    [SerializeField] private bool _useScalePop = false;
    [Tooltip("缩放弹出的起始比例")]
    [SerializeField] private float _scalePopFrom = 0.9f;

    private CanvasGroup _canvasGroup;
    private Sequence _sequence;
    private UnityAction _hideCallback;
    private Vector3 _initialScale = Vector3.one;

    /// <summary>是否已执行过 <see cref="Init"/>。</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>是否处于显示状态（含淡入过程中）。</summary>
    public bool IsShown { get; private set; }

    /// <summary>
    /// 是否消费 ESC（HUD 类面板返回 false，弹窗类返回 true）。
    /// UIService 只会把 ESC 分发给显示栈中最上层的「可消费」面板。
    /// </summary>
    public virtual bool CanHandleEscape => false;

    /// <summary>
    /// 本面板是否是**模态**的：显示期间要求暂停游戏。
    ///
    /// <para>
    /// 面板不再自己调 <c>PauseGame</c>/<c>ResumeGame</c> —— 那是"谁都能恢复全局时间"的写法，
    /// 两个模态面板（升级三选一 + 塔管理）重叠时，先关的那个会把时间恢复成 1，
    /// 另一个还显示着的面板就失去了暂停保护。
    /// 改成在这里声明意图，由 <c>UIService</c> 在显示/销毁时申请与释放**本面板自己的令牌**，
    /// 于是"还有几个面板要暂停"由 <c>GameLevelManager</c> 的持有者集合回答。
    /// </para>
    /// </summary>
    public virtual bool WantsPause => false;

    /// <summary>本面板持有的暂停令牌（未申请时为 null）。令牌是普通对象，不涉及 Unity 生命周期。</summary>
    private object _pauseToken;

    /// <summary>由 UIService 在面板显示后调用。非模态面板是空操作。</summary>
    public void AcquirePauseIfNeeded()
    {
        if (!WantsPause || _pauseToken != null) return;

        _pauseToken = new object();
        GameLevelManager.Service?.AcquirePause(_pauseToken);
    }

    /// <summary>由 UIService 在面板销毁/强制关闭时调用。重复调用是空操作。</summary>
    public void ReleasePauseIfHeld()
    {
        if (_pauseToken == null) return;

        // 关卡可能已经销毁（切场景）：这时令牌随本面板一起作废，不需要也不该再找服务
        GameLevelManager.Service?.ReleasePause(_pauseToken);
        _pauseToken = null;
    }


    protected virtual void Awake()
    {
        _canvasGroup = EnsureCanvasGroup();
        _initialScale = transform.localScale;
    }

    protected virtual void OnDestroy()
    {
        KillSequence();
    }


    /// <summary>
    /// 必须实现的初始化方法：只做一次性接线（按钮回调等）。
    /// 由 UIService 在实例化后、首次显示前同步调用一次，不要再依赖 Start/Awake 时序。
    /// </summary>
    public abstract void Init();

    /// <summary>幂等初始化入口（由 UIService 调用）。</summary>
    public void Initialize()
    {
        if (IsInitialized) return;

        IsInitialized = true;
        Init();
    }


    /// <summary>
    /// 显示面板：淡入，并复位交互状态。由 UIService 调用。
    /// </summary>
    public virtual void ShowMe()
    {
        IsShown = true;
        KillSequence();

        CanvasGroup group = EnsureCanvasGroup();
        group.blocksRaycasts = true;
        group.interactable = true;

        float duration = Mathf.Max(0f, _fadeInDuration);
        group.alpha = 0f;

        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _sequence.Append(group.DOFade(1f, duration).SetEase(Ease.OutQuad));

        if (_useScalePop)
        {
            transform.localScale = _initialScale * _scalePopFrom;
            _sequence.Join(transform.DOScale(_initialScale, duration).SetEase(Ease.OutBack));
        }
    }


    /// <summary>
    /// 隐藏面板：淡出结束后回调。由 UIService 调用。
    /// 不在这里销毁自身——销毁时机由 UIService 统一掌握。
    /// </summary>
    public virtual void HideMe(UnityAction callBack)
    {
        IsShown = false;
        KillSequence();

        CanvasGroup group = EnsureCanvasGroup();
        // 淡出期间立刻放弃射线，避免正在消失的面板继续吃掉点击
        group.blocksRaycasts = false;
        group.interactable = false;

        _hideCallback = callBack;

        float duration = Mathf.Max(0f, _fadeOutDuration);
        if (duration <= 0f)
        {
            group.alpha = 0f;
            InvokeHideCallback();
            return;
        }

        // 从当前 alpha 平滑淡出，不会像旧实现那样先跳到 1 再变暗
        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _sequence.Append(group.DOFade(0f, duration).SetEase(Ease.InQuad));

        if (_useScalePop)
            _sequence.Join(transform.DOScale(_initialScale * _scalePopFrom, duration).SetEase(Ease.InQuad));

        _sequence.OnComplete(InvokeHideCallback);
    }


    /// <summary>
    /// 立即终止当前动画。被强制关闭时调用，用来确保淡出完成回调不再触发。
    /// </summary>
    public void KillSequence()
    {
        if (_sequence != null && _sequence.IsActive())
            _sequence.Kill();

        _sequence = null;
    }


    /// <summary>
    /// ESC 处理逻辑。仅当 <see cref="CanHandleEscape"/> 为 true 时会被 UIService 调用。
    /// </summary>
    public virtual void EscLogic()
    {
    }


    /// <summary>取 CanvasGroup，缺失时按需补一个（子类重写 Awake 不调 base 也不会 NRE）。</summary>
    protected CanvasGroup EnsureCanvasGroup()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        return _canvasGroup;
    }


    private void InvokeHideCallback()
    {
        // 取一次并清空，避免重复触发
        UnityAction cb = _hideCallback;
        _hideCallback = null;
        cb?.Invoke();
    }
}
