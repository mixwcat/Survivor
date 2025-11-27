using UnityEngine;
using System.Collections.Generic;

public class TowerManager : MonoBehaviour
{
    [Header("单例")]
    public static TowerManager Instance;
    void Awake()
    {
        Instance = this;
    }

    [Header("塔列表")]
    public List<BaseTower> towers = new List<BaseTower>();

    public void RegisterTower(BaseTower tower)
    {
        towers.Add(tower);
    }
    public void UnregisterTower(BaseTower tower)
    {
        towers.Remove(tower);
    }
}
