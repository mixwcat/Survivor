using UnityEngine;
using TMPro;

/// <summary>
/// 伤害数字（对象池复用）。
///
/// 性能说明：淡出刻意走 <see cref="CanvasGroup"/> 而不是 <c>TextMeshProUGUI.alpha</c>——
/// 改 TMP 的 alpha 会让文本每帧 <c>SetVerticesDirty</c>、重建整套字形网格；
/// 改 CanvasGroup 只影响画布合批用的颜色，不重建网格。伤害数字同时存在数量多，这个差别很明显。
/// </summary>
public class DamageNumText : MonoBehaviour, IPoolable
{
    [Header("浮动数字设置")]
    public TextMeshProUGUI damageText;
    public float floatSpeed = 1f;
    public float lifeTime = 1f;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 从池中取出时重置表现状态。
    /// 走显式钩子而不是 <c>OnEnable</c>：首次创建时实例本就是激活的，
    /// <c>SetActive(true)</c> 是空操作，<c>OnEnable</c> 不会触发。
    /// </summary>
    public void OnGetFromPool()
    {
        transform.localScale = Vector3.one;
        _canvasGroup.alpha = 1f;
    }

    public void OnReturnToPool()
    {
        // 取消回收计时：残留的 Invoke 会在对象被复用时把它从使用者手里抢回池里
        CancelInvoke(nameof(ReturnToPool));
    }

    void Update()
    {
        transform.position += new Vector3(0, floatSpeed * Time.deltaTime, 0);
        _canvasGroup.alpha -= Time.deltaTime / lifeTime;
    }


    /// <summary>
    /// 设置伤害数值
    /// </summary>
    /// <param name="damage"></param>
    public void SetUp(int damage, DamageNumType type = DamageNumType.white)
    {
        damageText.text = damage.ToString();
        _canvasGroup.alpha = 1f;

        // 先取消上一次的回收计时：池化对象复用时，残留的 Invoke 会在使用中途把对象抢回池里
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), lifeTime);

        // 根据类型设置颜色
        switch (type)
        {
            case DamageNumType.Red:
                damageText.color = Color.red;
                break;
            case DamageNumType.green:
                damageText.color = Color.green;
                break;
            case DamageNumType.white:
                damageText.color = Color.white;
                break;  
        }
    }


    /// <summary>
    /// 归还到对象池
    /// </summary>
    private void ReturnToPool()
    {
        DamageNumService.Service?.ReturnToPool(this);
    }
}

public enum DamageNumType
{
    Red,
    white,
    green
}
