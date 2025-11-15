using System.Collections.Generic;
using UnityEngine;

public class GameLevelManager
{
    [Header("单例模式")]
    private static GameLevelManager instance = new GameLevelManager();
    public static GameLevelManager Instance => instance;

    [Header("Enemy管理")]
    private List<EnemyController> enemies = new List<EnemyController>();


    public void RegisterEnemy(EnemyController enemy)
    {
        enemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyController enemy)
    {
        enemies.Remove(enemy);
    }

    public int GetEnemyCount()
    {
        return enemies.Count;
    }
}
