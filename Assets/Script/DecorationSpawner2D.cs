using System.Collections.Generic;
using UnityEngine;

// 每一类装饰物的配置（不需要 prefab，直接用 Sprite）
[System.Serializable]
public class DecorationType
{
    public string name;          // 方便在 Inspector 里看
    public Sprite sprite;        // 直接拖图片进来
    public float scale = 1f;     // 这一类统一缩放比例
}

public class DecorationSpawner2D : MonoBehaviour
{
    [Header("生成区域（必须是 BoxCollider2D）")]
    public BoxCollider2D areaCollider;

    [Header("装饰物类型配置（每一类有自己的缩放）")]
    public DecorationType[] decorationTypes;

    [Header("数量与密度控制")]
    [Range(0f, 5f)]
    public float density = 1f;        // 每单位面积生成多少个
    public int maxCount = 300;        // 防止生成太多卡死

    [Header("防重叠控制")]
    public float minDistance = 0.5f;  // 装饰物之间的最小距离
    public int maxTryPerObject = 20;  // 每个装饰物最多尝试多少次找位置

    [Header("其它")]
    public bool clearOldOnSpawn = true;   // 每次生成前是否清掉旧的
    public bool spawnOnStart = true;      // 进入场景自动生成

    private readonly List<Transform> spawned = new List<Transform>();

    private void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

    [ContextMenu("Spawn Decorations")]
    public void Spawn()
    {
        if (areaCollider == null)
        {
            Debug.LogWarning("DecorationSpawner2D: 没有指定 areaCollider。");
            return;
        }

        if (decorationTypes == null || decorationTypes.Length == 0)
        {
            Debug.LogWarning("DecorationSpawner2D: 没有装饰物类型配置。");
            return;
        }

        // 清理旧的
        if (clearOldOnSpawn)
        {
            foreach (var t in spawned)
            {
                if (t != null)
                {
#if UNITY_EDITOR
                    DestroyImmediate(t.gameObject);
#else
                    Destroy(t.gameObject);
#endif
                }
            }
            spawned.Clear();
        }

        // 计算区域面积和目标数量
        Bounds b = areaCollider.bounds;
        float area = b.size.x * b.size.y;
        int targetCount = Mathf.Min(maxCount, Mathf.RoundToInt(area * density));

        int created = 0;
        int safety = 0;
        int maxTotalTry = targetCount * maxTryPerObject;

        while (created < targetCount && safety < maxTotalTry)
        {
            safety++;

            // 随机一个点
            float x = Random.Range(b.min.x, b.max.x);
            float y = Random.Range(b.min.y, b.max.y);
            Vector2 pos = new Vector2(x, y);

            // 确保点在 collider 里面
            if (!areaCollider.OverlapPoint(pos))
                continue;

            // 距离检查：防止重叠/太近
            bool tooClose = false;
            foreach (var t in spawned)
            {
                if (t == null) continue;
                if (Vector2.Distance(pos, t.position) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
                continue;

            // 随机选一个装饰物类型
            DecorationType type = decorationTypes[Random.Range(0, decorationTypes.Length)];
            if (type == null || type.sprite == null)
                continue;

            // 动态创建一个物体，并加 SpriteRenderer
            GameObject obj = new GameObject(string.IsNullOrEmpty(type.name) ? "Decoration" : type.name);
            obj.transform.SetParent(transform);
            obj.transform.position = pos;

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = type.sprite;
            // 如果需要指定 Sorting Layer / Order，可以在这里写：
            sr.sortingLayerName = "BK";
            sr.sortingOrder = 1;

            // 统一缩放
            float s = type.scale;
            obj.transform.localScale = new Vector3(s, s, 1f);

            spawned.Add(obj.transform);
            created++;
        }
    }
}
