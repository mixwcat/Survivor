using System.Collections.Generic;
using UnityEngine;

public class Luo : BaseTower
{
    [Header("属性")]
    [Tooltip("治疗间隔")]
    public float healInterval;
    private float healTimer;
    [Tooltip("治疗量")]
    public int healAmount;
    private List<BaseTower> towersInRange = new List<BaseTower>();

    [Header("组件")]
    public Animator anim;

    [Header("事件")]
    public LevelUpSO luoIncreaseHealAmountSO;
    public LevelUpSO luoHealIntervalSO;
    public LevelUpSO luoHealRangeSO;


    protected override void Start()
    {
        base.Start();
    }


    protected override void Update()
    {
        base.Update();
        healTimer += Time.deltaTime;
        if (healTimer >= healInterval)
        {
            Heal();
            healTimer = 0f;
        }
    }

    private void Heal()
    {
        if (towersInRange.Count == 0) return;

        anim.SetTrigger("Heal");

        DrawCircle();

        BKMusic.Instance.PlaySound(ResourceEnum.Heal);

        for (int i = 0; i < towersInRange.Count; i++)
        {
            if (towersInRange[i] != null)
            {
                if (towersInRange[i] is Luo) continue;
                towersInRange[i].GetComponent<BaseHealthController>().Heal(healAmount);
            }
            else
            {
                towersInRange.RemoveAt(i);
                i--;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Tower"))
        {
            towersInRange.Add(other.GetComponent<BaseTower>());
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Tower"))
        {
            towersInRange.Remove(other.GetComponent<BaseTower>());
        }
    }


    /// <summary>
    /// 订阅升级事件
    /// </summary>
    private void IncreaseHealAmount()
    {
        // 治疗量
        healAmount += 5;
    }
    private void DecreaseHealInterval()
    {
        // 治疗间隔
        healInterval *= 0.9f;
    }
    private void IncreaseHealRange()
    {
        // 治疗范围
        attackRange += 1f;
        detectionCollider.radius = attackRange;
    }


    void OnEnable()
    {
        luoIncreaseHealAmountSO.onLevelUp += IncreaseHealAmount;
        luoHealIntervalSO.onLevelUp += DecreaseHealInterval;
        luoHealRangeSO.onLevelUp += IncreaseHealRange;
        TowerManager.Instance.RegisterTower(this);
    }
    void OnDisable()
    {
        luoIncreaseHealAmountSO.onLevelUp -= IncreaseHealAmount;
        luoHealIntervalSO.onLevelUp -= DecreaseHealInterval;
        luoHealRangeSO.onLevelUp -= IncreaseHealRange;
        TowerManager.Instance.UnregisterTower(this);
    }
}
