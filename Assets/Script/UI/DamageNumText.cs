using UnityEngine;
using TMPro;

public class DamageNumText : MonoBehaviour
{
    [Header("浮动数字设置")]
    public TextMeshProUGUI damageText;
    public float floatSpeed = 1f;
    public float lifeTime = 1f;


    // Update is called once per frame
    void Update()
    {
        transform.position += new Vector3(0, floatSpeed * Time.deltaTime, 0);
        damageText.alpha -= Time.deltaTime / lifeTime;
    }


    /// <summary>
    /// 设置伤害数值
    /// </summary>
    /// <param name="damage"></param>
    public void SetUp(int damage)
    {
        damageText.text = damage.ToString();
        damageText.alpha = 1f;
        Invoke(nameof(ReturnToPool), lifeTime);
    }


    /// <summary>
    /// 归还到对象池
    /// </summary>
    private void ReturnToPool()
    {
        DamageNumManager.Instance.ReturnToPool(this);
    }
}
