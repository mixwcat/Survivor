using System;
using UnityEngine;

/// <summary>
/// 推车修理 —— 站在车旁**按住交互键 5 秒**，恢复 10% 耐久；期间对外发布进度，供车上方画进度条。
///
/// <para>
/// <b>它是推车唯一的主动恢复手段</b>：耐久归零后 <c>CartController.EnterDisabled</c> 会让车停摆，
/// 修满一次即调 <see cref="CartController.Repair"/> 退出停摆 —— 是否立刻重新开动由
/// <c>StageDirector</c> 按当前阶段决定（充能点/终点的停车是设计的一部分）。
/// </para>
///
/// <para>
/// <b>为什么按住状态由交互物自己轮询：</b>交互键的"按下"是一次性事件
/// （<see cref="IInteractor.TryInteract"/>），表达不了"还按着"。这里每帧读
/// <see cref="IInteractor.IsInteractHeld"/> 推进进度，松开 / 离开范围（<see cref="OnDeselected"/>）
/// 即清零 —— 不需要给交互框架加"长按"这个新概念。
/// </para>
///
/// <para>
/// <b>提示必须显示</b>（<see cref="OnSelected"/> 里 <c>Show</c>）：它不只是"给玩家看" ——
/// 触屏的按住状态由提示 UI 上报（<c>MobileInputHandle.TouchHeld</c>），
/// 提示不显示则触屏永远推进不了进度；PC 上也一样需要它才看得到"按 E 修车"。
/// </para>
///
/// <para>
/// <b>进度是事件而非轮询，且**量化**后发布：</b>UI 订阅 <see cref="ProgressChanged"/>，
/// 进度为 0 时整块隐藏。量化到 1/64 步长，一次 5 秒修理最多 64 次 UI 写入
/// （不量化则是每帧一次 ≈ 300 次，全是白费的 RectTransform 标脏）。
/// </para>
/// </summary>
public class CartRepairInteractable : MonoBehaviour, IInteractable
{
    /// <summary>进度发布粒度：一次修理最多发这么多次事件（见类型注释）。</summary>
    private const int ProgressSteps = 64;

    [Header("接线")]
    [Tooltip("要修的推车。字段是 public：场景组件的引用本就该在 Inspector 里配，" +
             "脚本化接线时直接赋值比走 SerializedObject 可靠。留空 = 从父级自动解析")]
    public CartController Cart;

    [Header("修理规则")]
    [Tooltip("按住多少秒修一次")]
    [SerializeField] private float _holdSeconds = 5f;

    [Tooltip("每次修理恢复的耐久比例（0.1 = 10%）")]
    [Range(0.01f, 1f)]
    [SerializeField] private float _repairRatio = 0.1f;

    private IInteractor _interactor;
    private InteractionPromptView _prompt;
    private float _held;
    private float _published = -1f;

    /// <summary>当前持有移动锁的玩家（修理期间定身）。按它释放，而不是按"当前交互者" —— 走出范围后交互者已变成 null。</summary>
    private PlayerController _lockedPlayer;

    /// <summary>修理进度 0..1（没在修时为 0）。</summary>
    public float Progress01 => _holdSeconds > 0f ? Mathf.Clamp01(_held / _holdSeconds) : 0f;

    /// <summary>进度变化（0..1，已量化）。只在数值变化时触发。</summary>
    public event Action<float> ProgressChanged;

    private void Awake()
    {
        // 与 Hurtbox 同一套写法：默认自动解析，Inspector 可覆盖。
        // 推车上有三个组件各用一套引用解析策略（手工接线 / GetComponentInParent / 传感器显式指定），
        // 于是"漏接一个引用"有三种不同的失败表现
        if (Cart == null) Cart = GetComponentInParent<CartController>();

        _prompt = InteractionPromptView.FindIn(this);
    }

    // ── IInteractable ──

    /// <inheritdoc />
    public Transform Anchor => transform;

    /// <summary>
    /// 修车是**救命**交互，优先级与塔/工作台同级（它们都是 10）。
    ///
    /// <para>
    /// 曾经是 0：车与塔同范围时永远先选塔，玩家必须先走开才能修车 ——
    /// "为什么按 E 打开了塔面板"是最难查的一类问题。
    /// </para>
    /// </summary>
    public int Priority => 10;

    /// <inheritdoc />
    public bool CanInteract(IInteractor interactor, out string reason)
    {
        if (Cart == null)
        {
            reason = "推车未接线";
            return false;
        }

        // 满耐久时不给提示：留一个"按了没用"的交互物只会让人以为坏了
        if (Cart.HealthNormalized >= 1f)
        {
            reason = "耐久已满";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public InteractionPrompt GetPrompt(IInteractor interactor)
    {
        // 按键名不写在这里：PC 是 E、触屏是"点击"，由提示层按平台决定 ——
        // 领域脚本里出现平台分支时，非目标平台会静默什么都不显示
        return new InteractionPrompt("按住修理", $"推车 +{Mathf.RoundToInt(_repairRatio * 100f)}%");
    }

    /// <summary>
    /// 按下交互键。**实际推进在 <see cref="Update"/>**：按住状态只能轮询，这里只记录交互者。
    /// </summary>
    public void Interact(IInteractor interactor)
    {
        _interactor = interactor;
    }

    /// <inheritdoc />
    public void OnSelected(IInteractor interactor)
    {
        _interactor = interactor;

        // 提示是 IInteractable.OnSelected 的契约之一（显示提示 / 高亮），
        // 同时也是触屏按住状态的唯一来源 —— 不显示就两边都用不了
        _prompt?.Show(interactor, this);
    }

    /// <inheritdoc />
    public void OnDeselected(IInteractor interactor)
    {
        // 只认"当前那个交互者"的取消，避免别人离开时打断我正在进行的修理。
        //
        // ⚠️ 已知限制：`_interactor` 是**单槽**，第二个玩家进入范围并成为当前目标时会接管它，
        // 于是他离开就会清掉进度与移动锁。当前规则是「同时只有一个人能修」，
        // 联机前需要明确：修理进度是否按玩家分别记账、以及是否需要服务端权威。
        if (!ReferenceEquals(_interactor, interactor)) return;

        _interactor = null;
        _prompt?.Hide();
        ResetProgress();
    }

    private void OnDisable()
    {
        _interactor = null;
        _prompt?.Hide();
        ResetProgress();
    }

    private void Update()
    {
        if (_interactor == null || !InteractorResolver.IsAlive(_interactor))
        {
            ResetProgress();
            return;
        }

        if (Cart == null || Cart.HealthNormalized >= 1f)
        {
            ResetProgress();
            return;
        }

        if (!_interactor.IsInteractHeld)
        {
            ResetProgress();
            return;
        }

        // 修理期间定身：站着修，不能一边跑一边修（松开/走开/修完都会在 ResetProgress 里解锁）
        AcquireMoveLock();

        _held += Time.deltaTime;

        if (_held < _holdSeconds)
        {
            Publish(Progress01);
            return;
        }

        CompleteRepair();
        ResetProgress();
    }

    private void CompleteRepair()
    {
        // 走 CartController.Repair：停摆/非停摆的分支与耐久写入都在那边，
        // 交互物不再自己 GetComponent<CartHealthController>()
        Cart.Repair(_repairRatio);

        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
    }

    private void ResetProgress()
    {
        _held = 0f;
        ReleaseMoveLock();
        Publish(0f);
    }

    /// <summary>定身：给玩家上移动锁（令牌就是本组件）。</summary>
    private void AcquireMoveLock()
    {
        PlayerController player = _interactor != null ? _interactor.Player : null;
        if (player == null || ReferenceEquals(_lockedPlayer, player)) return;

        ReleaseMoveLock();      // 交互者换了人（多人同站）：先把旧玩家的锁放掉
        _lockedPlayer = player;
        player.AcquireMoveLock(this);
    }

    /// <summary>解锁。按记录下来的玩家释放 —— 交互者可能已经变成 null（走出范围 / 玩家离场）。</summary>
    private void ReleaseMoveLock()
    {
        if (_lockedPlayer == null) return;

        _lockedPlayer.ReleaseMoveLock(this);
        _lockedPlayer = null;
    }

    /// <summary>
    /// 量化后只在数值变化时发事件：进度条每次写 <c>anchorMax</c> 都会标脏 canvas，
    /// 逐帧发等于一次修理白做约 300 次 UI 写入。
    /// </summary>
    private void Publish(float progress01)
    {
        float quantized = Mathf.Round(progress01 * ProgressSteps) / ProgressSteps;
        if (Mathf.Approximately(quantized, _published)) return;

        _published = quantized;
        ProgressChanged?.Invoke(quantized);
    }
}
