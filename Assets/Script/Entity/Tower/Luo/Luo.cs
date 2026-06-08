using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Luo 塔 — 治疗
/// 所有数值属性从 StatModel 读取
/// </summary>
public class Luo : BaseTower
{
    private float _healTimer;
    public Animator anim;
    private List<BaseTower> _towersInRange = new List<BaseTower>();

    protected override void Update()
    {
        base.Update();
        _healTimer += Time.deltaTime;
        float interval = GetStat(StatType.HealInterval);
        if (_healTimer >= interval)
        {
            Heal();
            _healTimer = 0f;
        }
    }

    private void Heal()
    {
        if (_towersInRange.Count == 0) return;

        anim.SetTrigger("Heal");
        DrawCircle();
        BKMusic.Instance.PlaySound(ResourceEnum.Heal);

        float healAmount = GetStat(StatType.HealAmount);
        for (int i = _towersInRange.Count - 1; i >= 0; i--)
        {
            if (_towersInRange[i] != null)
            {
                if (_towersInRange[i] is Luo) continue;
                _towersInRange[i].GetComponent<BaseHealthController>()?.Heal(healAmount);
            }
            else
            {
                _towersInRange.RemoveAt(i);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Tower"))
            _towersInRange.Add(other.GetComponent<BaseTower>());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Tower"))
            _towersInRange.Remove(other.GetComponent<BaseTower>());
    }
}
