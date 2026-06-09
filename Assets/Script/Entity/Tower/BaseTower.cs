using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 防御塔基类
/// 继承 EntityBehaviour，所有数值从 StatModel 读取
/// 监听 AttackRange 变化自动更新碰撞体
/// </summary>
public class BaseTower : EntityBehaviour
{
    protected List<EnemyController> enemyInRange = new List<EnemyController>();

    [Header("攻击范围显示")]
    public int segments = 50;
    private LineRenderer _lineRenderer;
    public CircleCollider2D detectionCollider;

    [Header("高亮材质")]
    [Tooltip("为空则使用 SOManager 中的统一配置")]
    public Material highlightMaterial;
    private SpriteRenderer[] _spriteRenderers;
    private Material[] _originalMaterials;


    protected override void Awake()
    {
        base.Awake();
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = segments + 1;
        _lineRenderer.loop = true;
        _lineRenderer.startWidth = 0.05f;
        _lineRenderer.endWidth = 0.05f;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = Color.white;
        _lineRenderer.endColor = Color.white;

        DrawCircle();

        // 缓存所有 SpriteRenderer 的原始材质
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalMaterials = new Material[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            _originalMaterials[i] = _spriteRenderers[i].material;
        }

        // 如果没有单独配置高亮材质，尝试从 SOManager 获取统一配置
        if (highlightMaterial == null && SOManager.Instance != null)
        {
            highlightMaterial = SOManager.Instance.towerHighlightMaterial;
        }
    }

    protected virtual void Start()
    {
        detectionCollider.radius = GetStat(StatType.TowerAttackRange);

        if (StatModel != null)
            StatModel.OnStatChanged += OnAnyStatChanged;
    }

    void OnEnable()
    {
        TowerManager.Instance.RegisterTower(this);
    }

    void OnDisable()
    {
        TowerManager.Instance.UnregisterTower(this);
    }

    protected virtual void OnDestroy()
    {
        if (StatModel != null)
            StatModel.OnStatChanged -= OnAnyStatChanged;
    }

    /// <summary>
    /// 监听 StatModel 数值变化（子类可 override）
    /// </summary>
    protected virtual void OnAnyStatChanged(StatType type)
    {
        if (type == StatType.TowerAttackRange)
        {
            float newRange = GetStat(StatType.TowerAttackRange);
            detectionCollider.radius = newRange;
            DrawCircle();
        }
    }

    protected virtual void Update()
    {
        if (_lineRenderer.startColor.a > 0f)
        {
            Color color = new Color(1f, 1f, 1f, Mathf.MoveTowards(_lineRenderer.startColor.a, 0f, 2f * Time.deltaTime));
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
        }
    }


    #region 绘制攻击范围
    protected void SetDefaultAlpha()
    {
        _lineRenderer.startColor = new Color(1f, 1f, 1f, 1f);
        _lineRenderer.endColor = new Color(1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 绘制圆环（攻击范围）
    /// </summary>
    protected void DrawCircle()
    {
        SetDefaultAlpha();
        float range = GetStat(StatType.TowerAttackRange);
        float angle = 0f;
        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Cos(angle) * range + transform.position.x;
            float y = Mathf.Sin(angle) * range + transform.position.y;
            _lineRenderer.SetPosition(i, new Vector3(x, y, 0));
            angle += 2 * Mathf.PI / segments;
        }
    }
    #endregion

    #region 索敌逻辑
    /// <summary>
    /// 寻找目标
    /// </summary>
    protected Transform FindTarget()
    {
        if (enemyInRange.Count == 0) return null;
        if (enemyInRange[0] != null)
            return enemyInRange[0].transform;
        else
        {
            enemyInRange.RemoveAt(0);
            return FindTarget();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
            enemyInRange.Add(other.GetComponent<EnemyController>());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
            enemyInRange.Remove(other.GetComponent<EnemyController>());
    }
    #endregion



    #region 高亮显示

    /// <summary>
    /// 设置高亮状态：true 切换为高亮材质，false 恢复原始材质
    /// </summary>
    public void SetHighlight(bool active)
    {
        if (_spriteRenderers == null || _spriteRenderers.Length == 0) return;
        if (active && highlightMaterial == null) return;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] == null) continue;
            _spriteRenderers[i].material = active ? highlightMaterial : _originalMaterials[i];
        }
    }

    #endregion
}
