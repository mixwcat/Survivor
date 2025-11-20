using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public abstract class BasePanel : MonoBehaviour
{
    // 控制透明度
    private CanvasGroup canvasGroup;
    private float alphaSpeed = 10f;
    public bool isShow = false;
    // 隐藏UI后的回调
    private UnityAction hideCallBack;


    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
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
                // 淡出结束 执行Action
                hideCallBack?.Invoke();
            }
        }
    }


    /// <summary>
    /// 必须实现的初始化方法
    /// </summary>
    public abstract void Init();

    /// <summary>
    /// 显示面板
    /// </summary>
    public virtual void ShowMe()
    {
        canvasGroup.alpha = 0;
        isShow = true;
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public virtual void HideMe(UnityAction callBack)
    {
        canvasGroup.alpha = 1;
        isShow = false;

        hideCallBack = callBack;
    }
}