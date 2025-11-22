using UnityEngine;
using System.Collections.Generic;

public class SpriteToHandle : MonoBehaviour
{
    private List<Collider2D> invalidColliders = new List<Collider2D>();
    private bool canPlace = true;
    private TowerSO currentTowerSO;
    private SpriteRenderer spriteRenderer;


    public void Init(TowerSO towerSO)
    {
        // 获取SpriteRenderer组件
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 鼠标处显示预制体图标
        spriteRenderer.sprite = towerSO.towerIcon;
        currentTowerSO = towerSO;
    }

    void Update()
    {
        // 图标跟随鼠标移动
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        transform.position = mousePosition;

        // 更新图标颜色
        if (canPlace)
        {
            GetComponent<SpriteRenderer>().color = Color.green;
        }
        else
        {
            GetComponent<SpriteRenderer>().color = Color.red;
        }

        // 放置塔逻辑
        if (Input.GetMouseButtonDown(0) && canPlace)
        {
            PlaceObject(transform);
        }
    }


    /// <summary>
    /// 碰撞检测，判断是否可放置塔
    /// </summary>
    /// <param name="other"></param>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Enemy") || other.CompareTag("Tower"))
        {
            invalidColliders.Add(other);
            canPlace = false;
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Enemy") || other.CompareTag("Tower"))
        {
            invalidColliders.Remove(other);
            if (invalidColliders.Count == 0)
            {
                canPlace = true;
            }
        }
    }

    /// <summary>
    /// 在指定位置放置塔
    /// </summary>
    /// <param name="positionToPlace"></param>
    public void PlaceObject(Transform positionToPlace)
    {
        // 在指定位置实例化当前处理的塔预制体
        Instantiate(currentTowerSO.towerPrefab, positionToPlace.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
