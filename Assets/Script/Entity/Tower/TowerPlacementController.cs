using UnityEngine;
using System.Collections.Generic;

public class TowerPlacementController : MonoBehaviour
{
    private List<Collider2D> invalidColliders = new List<Collider2D>();
    private bool canPlace = true;
    private TowerEntitySO currentTowerSO;
    private SpriteRenderer spriteRenderer;
    private IInputHandle _inputHandle;
    private int _placementCost;

    [Header("网格设置")]
    public float gridSize = 1f;
    private Vector2 gridOrigin = Vector2.zero;
    private HashSet<Vector2> occupiedCells = new HashSet<Vector2>();

    [Header("攻击范围显示")]
    private float attackRange = 2;
    private TowerRangeVisualizer _rangeVisualizer;
    private Vector3 lastTransformPosition = Vector3.zero;

    private Vector3 _lastTouchWorldPos = Vector3.zero;

    public void Init(TowerEntitySO towerSO, int placementCost, IInputHandle inputHandle = null)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = towerSO.prefab.GetComponent<SpriteRenderer>().sprite;
        currentTowerSO = towerSO;
        _placementCost = placementCost;

        if (towerSO.dataRef is TowerDataSO towerData)
        {
            attackRange = towerData.AttackRange;
        }

        // 支持外部注入输入源（联机模式下服务器可能传入虚拟输入）
        _inputHandle = inputHandle ?? InputHandleFactory.GetInput("local");

        if (_inputHandle == null)
        {
            Debug.LogError("TowerPlacementController: Failed to create IInputHandle!");
        }

        _rangeVisualizer = gameObject.AddComponent<TowerRangeVisualizer>();
        _rangeVisualizer.Show();
        _rangeVisualizer.Refresh(attackRange);

#if UNITY_ANDROID
        UIManager.Service.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(true, this);
#endif
    }

    float timer;  // 0.1s更新频率，减少性能消耗
    void Update()
    {
        // Init 是异步实例化后调用的，防止首帧早于 Init 运行时 _inputHandle 为空
        if (_inputHandle == null) return;

        timer += Time.deltaTime;
        if (timer > 0.1f)
        {
            timer = 0f;
            return;
        }

#if UNITY_ANDROID
        if (_inputHandle.TryGetWorldPointer(out Vector2 screenPos, out bool isDown, out bool isUp))
        {
            Vector3 currentTouchWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0));
            currentTouchWorldPos.z = 0;

            if (_lastTouchWorldPos == Vector3.zero)
            {
                _lastTouchWorldPos = currentTouchWorldPos;
            }

            Vector3 delta = currentTouchWorldPos - _lastTouchWorldPos;
            transform.position = GetGridCenter(transform.position + delta);
            _lastTouchWorldPos = currentTouchWorldPos;
        }
        else
        {
            _lastTouchWorldPos = Vector3.zero;
        }

        spriteRenderer.color = canPlace ? Color.green : Color.red;

#elif UNITY_STANDALONE_WIN
        if (_inputHandle.TryGetWorldPointer(out Vector2 screenPos, out bool isDown, out bool isUp))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            worldPos.z = 0;
            transform.position = GetGridCenter(worldPos);

            spriteRenderer.color = canPlace ? Color.green : Color.red;

            if (isDown && canPlace)
            {
                ConfirmPlacement();
            }
        }

        if (_inputHandle.HasCancelInput)
        {
            CancelPlacement();
        }
#endif

        if (transform.position != lastTransformPosition)
        {
            lastTransformPosition = transform.position;
            _rangeVisualizer.Refresh(attackRange);
        }
    }


    /// <summary>
    /// 放置与取消
    /// </summary>
    public void ConfirmPlacement()
    {
        if (!canPlace) return;

        Instantiate(currentTowerSO.prefab, transform.position, Quaternion.identity);

#if UNITY_ANDROID
        UIManager.Service.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(false);
#endif

        Destroy(gameObject);
    }

    public void CancelPlacement()
    {
        if (_placementCost > 0)
        {
            PlayerManager.Service?.LocalPlayer?.ExperienceController?.AddLevelPoint(_placementCost);
        }

#if UNITY_ANDROID
        UIManager.Service.GetPanel<GamePanel>()?.SetTowerPlacementButtonsActive(false);
#endif

        Destroy(gameObject);
    }


    /// <summary>
    /// 网格对齐
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
    /// 碰撞检测：与玩家、敌人、其他塔重叠时禁止放置
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
