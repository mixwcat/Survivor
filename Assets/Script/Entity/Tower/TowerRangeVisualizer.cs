using UnityEngine;

/// <summary>
/// 塔攻击/治疗范围可视化组件
/// 职责：绘制圆环、管理显示/渐隐/隐藏状态
/// 与 BaseTower 解耦：范围值由外部传入，组件只负责渲染
/// </summary>
public class TowerRangeVisualizer : MonoBehaviour
{
    private const int SEGMENTS = 50;
    private const float LINE_WIDTH = 0.05f;
    private const float FADE_SPEED = 2f;

    private LineRenderer _lineRenderer;
    private RangeState _state;
    private float _alpha;

    private enum RangeState
    {
        Hidden,
        Visible,
        Fading
    }

    void Awake()
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = SEGMENTS + 1;
        _lineRenderer.loop = true;
        _lineRenderer.startWidth = LINE_WIDTH;
        _lineRenderer.endWidth = LINE_WIDTH;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.useWorldSpace = true;

        _state = RangeState.Hidden;
        _alpha = 0f;
        ApplyAlpha();
    }

    /// <summary>
    /// 持续显示范围线（alpha=1），不会自动渐隐
    /// </summary>
    public void Show()
    {
        _state = RangeState.Visible;
        _alpha = 1f;
        ApplyAlpha();
    }

    /// <summary>
    /// 开始渐隐范围线
    /// </summary>
    public void FadeOut()
    {
        _state = RangeState.Fading;
    }

    /// <summary>
    /// 立刻隐藏范围线
    /// </summary>
    public void Hide()
    {
        _state = RangeState.Hidden;
        _alpha = 0f;
        ApplyAlpha();
    }

    /// <summary>
    /// 重绘圆环。隐藏状态下不绘制。
    /// </summary>
    public void Refresh(float range)
    {
        if (_state == RangeState.Hidden) return;
        DrawCircle(range);
        ApplyAlpha();
    }

    void Update()
    {
        if (_state != RangeState.Fading) return;

        _alpha = Mathf.MoveTowards(_alpha, 0f, FADE_SPEED * Time.deltaTime);
        ApplyAlpha();
        if (_alpha <= 0f)
            _state = RangeState.Hidden;
    }

    private void DrawCircle(float range)
    {
        Vector3 center = transform.position;
        float angle = 0f;
        float step = 2f * Mathf.PI / SEGMENTS;

        for (int i = 0; i <= SEGMENTS; i++)
        {
            float x = Mathf.Cos(angle) * range + center.x;
            float y = Mathf.Sin(angle) * range + center.y;
            _lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
            angle += step;
        }
    }

    private void ApplyAlpha()
    {
        Color color = new Color(1f, 1f, 1f, _alpha);
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
    }
}
