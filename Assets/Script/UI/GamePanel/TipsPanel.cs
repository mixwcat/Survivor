using UnityEngine;
using System.Collections;
using UnityEngine.UI;
public class TipsPanel : BasePanel
{
    public Image image;
    public TMPro.TextMeshProUGUI text;

    public override void Init()
    {
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
            // 上升效果
            transform.position += new Vector3(0, 100 * Time.unscaledDeltaTime, 0);

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
        UIManager.Instance.HidePanel<TipsPanel>();
    }
}