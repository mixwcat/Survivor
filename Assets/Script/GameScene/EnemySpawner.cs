using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("生成设置")]
    public int maxEnemies = 20;  // 最大敌人数量

    [Header("波次信息")]
    public List<WaveInfo> waves; // 波次信息列表
    private int currentWaveIndex = 0;  // 当前波次索引
    private bool waveGoingOn = true;  // 当前波是否正在进行
    private float timer;  // 当前波的持续时间计时器


    void Start()
    {
        StartCoroutine(SpawnEnemyCoroutine());
        timer = waves[currentWaveIndex].waveLength;
    }


    /// <summary>
    /// 玩家消失前持续生成敌人
    /// </summary>
    /// <returns></returns>
    private IEnumerator SpawnEnemyCoroutine()
    {
        while (PlayerManager.Instance.player != null)
        {
            while (waveGoingOn)
            {
                // 生成敌人并等待
                SpawnEnemy();
                yield return new WaitForSeconds(waves[currentWaveIndex].spawnCooldown);

                // 计时器递减
                timer -= waves[currentWaveIndex].spawnCooldown;
                if (timer <= 0 && waveGoingOn)
                {
                    waveGoingOn = false;
                }
            }

            // 等待所有敌人被消灭
            while (GameLevelManager.Instance.GetEnemyCount() != 0)
            {
                yield return new WaitForSeconds(1f);
            }

            // 波次结束，等待下一波开始
            yield return new WaitForSeconds(waves[currentWaveIndex].timeBetweenSpawns);

            // 重置信息
            currentWaveIndex++;
            waveGoingOn = true;
            if (currentWaveIndex < waves.Count)
            {
                timer = waves[currentWaveIndex].waveLength;
            }
            else
            {
                yield break; // 结束协程
            }
        }
    }


    /// <summary>
    /// 生成敌人的逻辑
    /// </summary>
    private void SpawnEnemy()
    {
        // 控制最大敌人数量
        if (GameLevelManager.Instance.GetEnemyCount() >= maxEnemies)
            return;

        // 生成敌人
        Instantiate(waves[currentWaveIndex].enemyPrefab, SelectSpawnPoint(), Quaternion.identity);
    }


    /// <summary>
    /// 随机生成敌人出生点
    /// </summary>
    /// <returns></returns>
    private Vector3 SelectSpawnPoint()
    {
        Vector3 selectedPoint = PlayerManager.Instance.player.transform.position;
        if (Random.Range(0, 2) == 0)
        {
            // 上下生成
            selectedPoint += new Vector3(Random.Range(-16f, 16f), Random.Range(0, 2) == 0 ? -9f : 9f, 0);
        }
        else
        {
            // 左右生成
            selectedPoint += new Vector3(Random.Range(0, 2) == 0 ? -16f : 16f, Random.Range(-8f, 8f), 0);
        }
        return selectedPoint;
    }
}


[System.Serializable]
public class WaveInfo
{
    public GameObject enemyPrefab;
    public int waveLength;  // 波次持续时间，单位秒
    public float timeBetweenSpawns;  // 两波次之间的间隔时间，单位秒
    public float spawnCooldown; // 每个敌人生成的间隔时间
}