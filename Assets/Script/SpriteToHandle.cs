using UnityEngine;
using System.Collections.Generic;

public class SpriteToHandle : MonoBehaviour
{
    private List<Collider2D> invalidColliders = new List<Collider2D>();
    private bool canPlace = true;
    private TowerSO currentTowerSO;
    private SpriteRenderer spriteRenderer;

    [Header("网格设置")]
    public float gridSize = 1f; // 网格单元大小
    private Vector2 gridOrigin = Vector2.zero; // 网格原点
    private HashSet<Vector2> occupiedCells = new HashSet<Vector2>(); // 已占用的网格单元

    [Header("攻击范围显示")]
    private float attackRange = 2;
    public int segments = 50; // 圆的分段数
    private LineRenderer lineRenderer;
    private Vector3 lastMousePosition = Vector3.zero;


    /// <summary>
    /// 传递Tower信息
    /// </summary>
    /// <param name="towerSO"></param>
    public void Init(TowerSO towerSO)
    {
        // 获取SpriteRenderer组件
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 鼠标处显示预制体图标
        spriteRenderer.sprite = towerSO.towerPrefab.GetComponent<SpriteRenderer>().sprite;
        currentTowerSO = towerSO;

        // 设置攻击范围
        attackRange = towerSO.towerPrefab.GetComponent<BaseTower>().attackRange;
    }

    private void Start()
    {
        SetUpLineRenderer();
    }

    void Update()
    {
        // 图标跟随鼠标移动
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        transform.position = GetGridCenter(mousePosition);

        // 鼠标移动时绘制圆环
        if (mousePosition != lastMousePosition)
        {
            lastMousePosition = mousePosition;
            DrawCircle();
        }

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

        // 右键取消放置
        if (Input.GetMouseButtonDown(1))
        {
            ExperienceLevController.Instance.AddLevelPoint(currentTowerSO.expConsumption);
            Destroy(gameObject);
        }
    }


    /// <summary>
    /// 使用LineRenderer绘制圆环
    /// 设置LineRenderer属性
    /// </summary>
    void DrawCircle()
    {
        float angle = 0f;
        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Cos(angle) * attackRange + transform.position.x;
            float y = Mathf.Sin(angle) * attackRange + transform.position.y;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
            angle += 2 * Mathf.PI / segments;
        }
    }
    void SetUpLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();

        // 设置 LineRenderer 属性
        lineRenderer.positionCount = segments + 1;
        lineRenderer.loop = true;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;

        // 设置材质
        Material lineMaterial = new Material(Shader.Find("Sprites/Default")); // 使用兼容的材质
        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = Color.white; // 设置起始颜色
        lineRenderer.endColor = Color.white; // 设置结束颜色
    }


    /// <summary>
    /// 计算鼠标所在网格单元的中心点
    /// </summary>
    /// <param name="worldPosition"></param>
    /// <returns></returns>
    private Vector2 GetGridCenter(Vector3 worldPosition)
    {
        float x = Mathf.Floor((worldPosition.x - gridOrigin.x) / gridSize) * gridSize + gridSize / 2 + gridOrigin.x;
        float y = Mathf.Floor((worldPosition.y - gridOrigin.y) / gridSize) * gridSize + gridSize / 2 + gridOrigin.y;
        return new Vector2(x, y);
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
}
