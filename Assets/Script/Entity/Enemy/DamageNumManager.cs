using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DamageNumManager : MonoBehaviour
{
    [Header("单例")]
    private static DamageNumManager instance;
    public static DamageNumManager Instance => instance;

    [Header("池")]
    [SerializeField] private List<DamageNumText> damageNumPool = new List<DamageNumText>();
    private DamageNumText damageNumToSpawn;

    private void Start()
    {
        instance = this;
    }


    /// <summary>
    /// 生成伤害数字
    /// </summary>
    /// <param name="position"></param>
    /// <param name="damage"></param>
    public void SpawnDamageNum(Vector3 position, float damage)
    {
        // 从池中取出
        DamageNumText text = GetFromPool();

        text.transform.position = position;
        // 设置数值
        text.GetComponent<DamageNumText>().SetUp((int)damage);
    }


    /// <summary>
    /// 从池中获取一个伤害数字对象，没有则创建
    /// </summary>
    /// <returns></returns>
    public DamageNumText GetFromPool()
    {
        damageNumToSpawn = null;

        if (damageNumPool.Count == 0)
        {
            // 池中没有，创建一个新的
            GameObject dmgNumObj = Instantiate(Resources.Load<GameObject>("UI/DamageNumText"), transform);
            damageNumToSpawn = dmgNumObj.GetComponent<DamageNumText>();
        }
        else
        {
            // 从池中取出一个
            damageNumToSpawn = damageNumPool[0];
            damageNumPool.RemoveAt(0);
            damageNumToSpawn.gameObject.SetActive(true);
        }

        return damageNumToSpawn;
    }

    /// <summary>
    /// 归还伤害数字对象到池中
    /// </summary>
    /// <param name="dmgNum"></param>
    public void ReturnToPool(DamageNumText dmgNum)
    {
        dmgNum.gameObject.SetActive(false);

        damageNumPool.Add(dmgNum);
    }
}
