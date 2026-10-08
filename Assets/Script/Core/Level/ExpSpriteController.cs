using UnityEngine;

public class ExpSpriteController : MonoBehaviour, IPoolable
{
    /// <summary>经验精灵的存在时长（秒），超时自动回收。</summary>
    private const float LifeTime = 20f;

    private PlayerController player;

    /// <summary>
    /// 玩家 Transform 的缓存。玩家不会换 Transform，所以取池时缓存一次即可；
    /// **拾取半径不缓存** —— 它是可以升级的数值，缓存下来会让升级后已掉落的经验球继续用旧半径。
    /// </summary>
    private Transform _playerTransform;

    public float moveSpeed = 5f;

    /// <summary>
    /// 本次掉落的经验值，由 <see cref="ExperienceLevController"/> 的池在取池后立即设置。
    /// 默认 1 只作为「漏设」的兜底；每次取池都会在 <see cref="OnGetFromPool"/> 里复位，
    /// 避免复用的实例带着上一个敌人的经验值出场。
    /// </summary>
    private int _expAmount = 1;

    /// <summary>设置本次掉落的经验值。</summary>
    public void SetExpAmount(int amount)
    {
        _expAmount = amount;
    }

    /// <summary>
    /// 每次从池中取出都重新取玩家引用并重新计时。
    ///
    /// 重置逻辑必须走这个显式钩子而不是 <c>OnEnable</c>/<c>Start</c>：
    /// <list type="bullet">
    /// <item><c>Start</c> 对池化对象**只执行第一次** —— 原实现在 Start 里 Invoke，
    /// 归还后那次计时只是被挂起而非取消，复用时会带着残留计时被强制回收，
    /// 同一对象于是被重复归还进池，随后可能被两个敌人同时取用；</item>
    /// <item><c>OnEnable</c> 在**首次创建**时不会触发（实例本来就是激活的，
    /// <c>SetActive(true)</c> 是空操作），挂在它上面的初始化会漏掉第一次。</item>
    /// </list>
    /// </summary>
    public void OnGetFromPool()
    {
        player = PlayerManager.Service?.LocalPlayer;
        _playerTransform = player != null ? player.transform : null;
        _expAmount = 1;
        Invoke(nameof(DestorySelf), LifeTime);
    }

    public void OnReturnToPool()
    {
        CancelInvoke(nameof(DestorySelf));
        // 置空强制下次取出时重新取引用：玩家可能已换人，跨场景后旧引用也会变伪 null
        player = null;
        _playerTransform = null;
    }

    void Update()
    {
        MoveTowardsPlayer();
    }

    private void MoveTowardsPlayer()
    {
        if (player == null || _playerTransform == null)
        {
            return;
        }

        // 用 sqrMagnitude 而不是 Vector2.Distance：后者每帧每个经验球一次开方，
        // 而活动经验球数量**不受 maxRetained 限制**（那是"空闲保留上限"），
        // 高击杀场景里可能同时存在上百个
        Vector2 delta = (Vector2)_playerTransform.position - (Vector2)transform.position;
        float pickRange = player.GetStat(StatType.PlayerPickRange);

        if (delta.sqrMagnitude >= pickRange * pickRange) return;

        transform.position = Vector2.MoveTowards(transform.position, _playerTransform.position,
                                                 moveSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 拾取区在 Pickup 层，只与 PlayerBody 层配对 —— 能触发回调的只可能是玩家本体，
        // 所以不必再判 tag（玩家的武器/召唤物在别的层上）
        var player = other.GetComponent<PlayerController>();
        if (player == null || player.ExperienceController == null) return;

        // 给碰撞到的玩家自身加经验（按玩家实例化）；数量来自掉落它的敌人配置
        player.ExperienceController.AddExperience(_expAmount);

        ExpSpritePool.Instance.ReturnToPool(this);
        AudioService.Service?.PlaySfx(ResourceEnum.PickExp);
    }

    private void DestorySelf()
    {
        ExpSpritePool.Instance.ReturnToPool(this);
    }
}
