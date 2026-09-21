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

    [Header("索敌/治疗范围碰撞体")]
    [Tooltip("为空时自动在子对象中查找 isTrigger 的 CircleCollider2D")]
    [SerializeField] private CircleCollider2D _detectionCollider;

    [Header("高亮材质")]
    [Tooltip("为空则使用 SOManager 中的统一配置")]
    [SerializeField] private Material _highlightMaterial;
    private SpriteRenderer[] _spriteRenderers;
    private Material[] _originalMaterials;

    private TowerRangeVisualizer _rangeVisualizer;
    private float _operateTimer;
    private float _cleanupTimer = 0.5f;


    protected override void Awake()
    {
        base.Awake();

        _rangeVisualizer = GetComponent<TowerRangeVisualizer>();
        if (_rangeVisualizer == null)
            _rangeVisualizer = gameObject.AddComponent<TowerRangeVisualizer>();

        // 缓存所有 SpriteRenderer 的原始材质
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalMaterials = new Material[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            _originalMaterials[i] = _spriteRenderers[i].material;
        }

        // 如果没有单独配置高亮材质，尝试从 SOManager 获取统一配置
        if (_highlightMaterial == null && SOManager.Service != null)
        {
            _highlightMaterial = SOManager.Service.TowerHighlightMaterial;
        }
    }

    protected virtual void Start()
    {
        // 自动查找子对象中的 search range trigger
        if (_detectionCollider == null)
        {
            CircleCollider2D[] circles = GetComponentsInChildren<CircleCollider2D>();
            foreach (var c in circles)
            {
                if (c.isTrigger)
                {
                    _detectionCollider = c;
                    break;
                }
            }
        }

        if (_detectionCollider != null)
            _detectionCollider.radius = GetStat(StatType.TowerAttackRange);
        else
            Debug.LogWarning($"[{nameof(BaseTower)}] 找不到 search range trigger: {gameObject.name}");

        if (StatModel != null)
            StatModel.OnStatChanged += OnAnyStatChanged;
    }

    void OnEnable()
    {
        TowerManager.Service?.RegisterTower(this);
    }

    void OnDisable()
    {
        TowerManager.Service?.UnregisterTower(this);
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
            if (_detectionCollider != null)
                _detectionCollider.radius = newRange;
            _rangeVisualizer?.Refresh(newRange);
        }
    }

    protected virtual void Update()
    {
        _cleanupTimer -= Time.deltaTime;
        if (_cleanupTimer <= 0f)
        {
            RemoveNullTargets(enemyInRange);
            CleanupCustomTargets();
            _cleanupTimer = 0.5f;
        }

        if (TryOperate())
            OnOperate();
    }

    #region 周期行为骨架（模板方法模式）

    /// <summary>
    /// 子类返回自己的操作间隔。Teto/Rin 默认 AttackInterval，Luo 可覆盖为 HealInterval。
    /// </summary>
    protected virtual float GetOperateInterval()
    {
        return GetStat(StatType.AttackInterval);
    }

    /// <summary>
    /// 按间隔累计计时器；返回 true 表示该进行一次操作。
    /// </summary>
    protected bool TryOperate()
    {
        float interval = GetOperateInterval();
        if (interval <= 0f)
        {
            Debug.LogWarning($"[{nameof(BaseTower)}] operate interval <= 0 on {GetType().Name}");
            return false;
        }

        _operateTimer -= Time.deltaTime;
        if (_operateTimer <= 0f)
        {
            _operateTimer = interval;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 子类重写具体行为（攻击/治疗等）
    /// </summary>
    protected virtual void OnOperate() { }

    /// <summary>
    /// 每 0.5s 清理时调用；子类可重写以清理自己的自定义目标列表。
    /// </summary>
    protected virtual void CleanupCustomTargets() { }

    #endregion

    #region 索敌逻辑

    /// <summary>
    /// 范围内是否有敌人
    /// </summary>
    protected bool HasEnemyInRange => enemyInRange.Count > 0;

    /// <summary>
    /// 寻找目标（默认取最先进入范围的敌人）
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
        TryAddTarget(other, "Enemy", enemyInRange);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        TryRemoveTarget(other, "Enemy", enemyInRange);
    }

    #endregion

    #region 目标列表工具

    protected void TryAddTarget<T>(Collider2D other, string tag, List<T> list) where T : Component
    {
        if (other.CompareTag(tag))
            list.Add(other.GetComponent<T>());
    }

    protected void TryRemoveTarget<T>(Collider2D other, string tag, List<T> list) where T : Component
    {
        if (other.CompareTag(tag))
            list.Remove(other.GetComponent<T>());
    }

    protected void RemoveNullTargets<T>(List<T> list) where T : Component
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null)
                list.RemoveAt(i);
        }
    }

    protected void ForEachValidTarget<T>(List<T> targets, System.Action<T> action) where T : Component
    {
        if (targets.Count == 0) return;

        // 快照遍历：action 可能因目标死亡触发 OnTriggerExit2D 修改 targets，
        // 直接按索引遍历会越界。
        T[] snapshot = targets.ToArray();
        foreach (T target in snapshot)
        {
            if (target == null)
            {
                targets.Remove(target);
                continue;
            }
            action(target);
        }
    }

    #endregion

    #region 选中与高亮

    /// <summary>
    /// 玩家选中该塔（由 DetectPlayer 调用）
    /// </summary>
    public void OnSelected()
    {
        SetHighlight(true);
        if (_rangeVisualizer != null)
        {
            _rangeVisualizer.Show();
            _rangeVisualizer.Refresh(GetStat(StatType.TowerAttackRange));
        }
    }

    /// <summary>
    /// 玩家取消选中该塔（由 DetectPlayer 调用）
    /// </summary>
    public void OnDeselected()
    {
        SetHighlight(false);
        _rangeVisualizer?.FadeOut();
    }

    /// <summary>
    /// 设置高亮状态：true 切换为高亮材质，false 恢复原始材质
    /// </summary>
    protected void SetHighlight(bool active)
    {
        if (_spriteRenderers == null || _spriteRenderers.Length == 0) return;
        if (active && _highlightMaterial == null) return;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] == null) continue;
            _spriteRenderers[i].material = active ? _highlightMaterial : _originalMaterials[i];
        }
    }

    #endregion
}
