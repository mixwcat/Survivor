using UnityEngine;
using System.Collections;
using UnityEngine.UI;
public class TipsPanel : BasePanel
{
    public Image image;
    public TMPro.TextMeshProUGUI text;

    private string _message;

    /// <summary>注入提示文案（必须在 Init 之前调用）。留空则沿用 prefab 上的文字。</summary>
    public void SetMessage(string message)
    {
        _message = message;
    }

    public override void Init()
    {
        if (!string.IsNullOrEmpty(_message) && text != null)
            text.text = _message;

        StartCoroutine(ShowTipsCoroutine());
    }


    /// <summary>
    /// 提示框不断上升，减少alpha，然后消失
    /// </summary>
    /// <returns></returns>
    IEnumerator ShowTipsCoroutine()
    {
        while (image.color.a > 0)
        {
            // 上升效果：UI 用 localPosition（Overlay 画布下 1 单位 = 1 像素）
            transform.localPosition += new Vector3(0, 100 * Time.unscaledDeltaTime, 0);

            // 渐变效果
            Color color = image.color;
            color.a = Mathf.MoveTowards(color.a, 0, 0.5f * Time.unscaledDeltaTime);
            image.color = color;

            color = text.color;
            color.a = Mathf.MoveTowards(color.a, 0, 0.5f * Time.unscaledDeltaTime);
            text.color = color;

            yield return null;
        }

        // 隐藏面板
        UIService.Service.HidePanel<TipsPanel>();
    }
}