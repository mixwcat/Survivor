using UnityEngine;
using System.Collections.Generic;

public class BaseTower : MonoBehaviour
{
    protected List<EnemyController> enemyInRange = new List<EnemyController>();
    [Header("攻击范围")]
    public float attackRange = 2;
    public int segments = 50; // 圆的分段数
    private LineRenderer lineRenderer;
    public CircleCollider2D detectionCollider;


    protected virtual void Start()
    {
        detectionCollider.radius = attackRange;
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

        DrawCircle();
    }

    protected virtual void Update()
    {
        if (lineRenderer.startColor.a > 0f)
        {
            Color color = new Color(1f, 1f, 1f, Mathf.MoveTowards(lineRenderer.startColor.a, 0f, 2f * Time.deltaTime));
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
    }


    protected void SetDefaultAlpha()
    {
        lineRenderer.startColor = new Color(1f, 1f, 1f, 1f);
        lineRenderer.endColor = new Color(1f, 1f, 1f, 1f);
    }


    /// <summary>
    /// 绘制圆环
    /// </summary>
    protected void DrawCircle()
    {
        SetDefaultAlpha();

        float angle = 0f;
        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Cos(angle) * attackRange + transform.position.x;
            float y = Mathf.Sin(angle) * attackRange + transform.position.y;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
            angle += 2 * Mathf.PI / segments;
        }
    }

    /// <summary>
    /// 寻找目标
    /// </summary>
    /// <returns></returns>
    protected Transform FindTarget()
    {
        if (enemyInRange.Count == 0) return null;

        if (enemyInRange[0] != null)
        {
            Transform target = enemyInRange[0].transform;
            return target;
        }
        else
        {
            enemyInRange.RemoveAt(0);
            return FindTarget();
        }
    }


    /// <summary>
    /// 碰撞检测，记录进入范围的敌人
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            enemyInRange.Add(other.GetComponent<EnemyController>());
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            enemyInRange.Remove(other.GetComponent<EnemyController>());
        }
    }


    void OnEnable()
    {
        TowerManager.Instance.RegisterTower(this);
    }
    void OnDisable()
    {
        TowerManager.Instance.UnregisterTower(this);
    }
}
