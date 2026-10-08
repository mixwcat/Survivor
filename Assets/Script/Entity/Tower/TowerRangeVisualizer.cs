using UnityEngine;

/// <summary>
/// 范围可视化组件 —— 绘制圆环、管理显示/渐隐/隐藏状态。
///
/// <para>
/// <b>两种用法：</b>
/// <list type="bullet">
/// <item><b>绑定武器</b>（<see cref="Bind"/>）：半径从武器的 <c>StatType.AttackRange</c> 实时取，
/// 武器升级射程会自动重绘 —— 宿主（塔）因此不必知道射程是哪个数值、也不必订阅武器的 StatModel。
/// 这是"选中塔时显示它的攻击范围"那条路径；</item>
/// <item><b>不绑定</b>：由调用方 <see cref="Refresh"/> 显式给半径。放置预览走这条
/// （那时还没有武器实例，半径来自武器 DataSO 的字段）。</item>
/// </list>
/// </para>
///
/// <para>
/// 组件留在**宿主**上而不是武器上：圆环回答的是"这个宿主覆盖多大范围"，
/// 显示时机由宿主的选中状态决定（<c>TowerInteractable</c> → <c>BaseTower.OnSelected</c>），
/// 而同一个渲染器也被放置幽灵复用（<c>TowerPlacementController</c>）—— 那里根本没有武器实例。
/// </para>
/// </summary>
public class TowerRangeVisualizer : MonoBehaviour
{
    private const int SEGMENTS = 50;
    private const float LINE_WIDTH = 0.05f;
    private const float FADE_SPEED = 2f;

    private LineRenderer _lineRenderer;
    private RangeState _state;
    private float _alpha;

    /// <summary>半径来源（绑定的武器）。见 <see cref="Bind"/>。</summary>
    private BaseWeapon _boundWeapon;
    private EntityStatModel _boundStats;

    /// <summary>
    /// 全局共享的范围线材质。
    /// 原实现在 Awake 里 <c>new Material(...)</c>，而本组件每次放置塔都会被 AddComponent 一次，
    /// 于是**每次放塔泄漏一份材质**；改为全局共享一份，并用 HideAndDontSave 避免被场景卸载销毁。
    /// 静态字段跨 Play 会话会失效（编辑器关闭了 Domain Reload），Unity 的伪 null 判定会自动重建。
    /// </summary>
    private static Material _sharedLineMaterial;

    private static Material SharedLineMaterial
    {
        get
        {
            if (_sharedLineMaterial != null) return _sharedLineMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[TowerRangeVisualizer] 找不到 Sprites/Default 着色器，攻击范围线无法绘制。");
                return null;
            }

            _sharedLineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _sharedLineMaterial;
        }
    }

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
        // 共享材质要用 sharedMaterial 赋值，避免被当成实例材质处理
        _lineRenderer.sharedMaterial = SharedLineMaterial;
        _lineRenderer.useWorldSpace = true;

        _state = RangeState.Hidden;
        _alpha = 0f;
        ApplyAlpha();
    }

    /// <summary>
    /// 持续显示范围线（alpha=1），不会自动渐隐。
    /// 已绑定武器时按武器射程绘制；未绑定时等调用方 <see cref="Refresh"/>。
    /// </summary>
    public void Show()
    {
        _state = RangeState.Visible;
        _alpha = 1f;

        float range = BoundRange;
        if (range > 0f) DrawCircle(range);

        ApplyAlpha();
    }

    /// <summary>
    /// 绑定"半径从哪来"。传 null 解绑（解绑后回到 <see cref="Refresh"/> 显式传值的用法）。
    ///
    /// <para>
    /// 绑定后本组件自己订阅武器的 <c>StatModel.OnStatChanged</c>：射程升级时圆环自动跟着变，
    /// 宿主不必替它盯着武器。换武器时重新 <see cref="Bind"/> 即可（内部会先退订旧的）。
    /// </para>
    /// </summary>
    public void Bind(BaseWeapon weapon)
    {
        if (ReferenceEquals(_boundWeapon, weapon)) return;

        Unsubscribe();
        _boundWeapon = weapon;
        if (weapon == null) return;

        _boundStats = weapon.StatModel;
        if (_boundStats != null) _boundStats.OnStatChanged += HandleStatChanged;
    }

    /// <summary>已绑定武器的射程；未绑定或数值缺失时为 0。</summary>
    private float BoundRange => _boundWeapon != null ? _boundWeapon.GetStat(StatType.AttackRange) : 0f;

    private void HandleStatChanged(StatType type)
    {
        if (type != StatType.AttackRange) return;
        if (_state == RangeState.Hidden) return;   // 没显示就不用重绘

        float range = BoundRange;
        if (range > 0f) DrawCircle(range);

        ApplyAlpha();
    }

    private void Unsubscribe()
    {
        if (_boundStats != null) _boundStats.OnStatChanged -= HandleStatChanged;

        _boundStats = null;
        _boundWeapon = null;
    }

    private void OnDestroy()
    {
        Unsubscribe();
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
