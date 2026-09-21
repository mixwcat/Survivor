using UnityEngine;
using UnityEngine.Events;


public abstract class BasePanel : MonoBehaviour
{
    // 控制透明度
    private CanvasGroup canvasGroup;
    private float alphaSpeed = 10f;
    public bool isShow = false;
    // 隐藏UI后的回调
    private UnityAction hideCallBack;

    /// <summary>
    /// 是否消费 ESC（HUD 类面板返回 false，弹窗类返回 true）。
    /// UIService 只会把 ESC 分发给显示栈中最上层的「可消费」面板。
    /// </summary>
    public virtual bool CanHandleEscape => false;


    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = true;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        Init();
    }

    protected virtual void Update()
    {
        // 淡入
        if (isShow && canvasGroup.alpha != 1)
        {
            canvasGroup.alpha += alphaSpeed * Time.unscaledDeltaTime;
            if (canvasGroup.alpha > 1)
            {
                canvasGroup.alpha = 1;
            }
        }
        // 淡出
        else if (!isShow && canvasGroup.alpha != 0)
        {
            canvasGroup.alpha -= alphaSpeed * Time.unscaledDeltaTime;
            if (canvasGroup.alpha <= 0)
            {
                canvasGroup.alpha = 0;
                // 淡出结束 执行Action（取一次并清空，避免重复触发）
                UnityAction cb = hideCallBack;
                hideCallBack = null;
                cb?.Invoke();
            }
        }
    }


    /// <summary>
    /// 必须实现的初始化方法
    /// </summary>
    public abstract void Init();

    /// <summary>
    /// ESC 处理逻辑。仅当 <see cref="CanHandleEscape"/> 为 true 时会被 UIService 调用。
    /// </summary>
    public virtual void EscLogic()
    {
    }

    /// <summary>
    /// 显示面板。由 UIService 调用。
    /// </summary>
    public virtual void ShowMe()
    {
        canvasGroup.alpha = 0;
        isShow = true;

        // 后显示的面板置于最上层
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// 隐藏面板。由 UIService 调用。
    /// </summary>
    public virtual void HideMe(UnityAction callBack)
    {
        canvasGroup.alpha = 1;
        isShow = false;

        hideCallBack = callBack;
    }
}
