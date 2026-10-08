using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池 —— 只负责「存储与复用」，重置逻辑交给 <see cref="IPoolable"/> 实现方。
///
/// <para>
/// <b>刻意不做成全局服务</b>：池化不是横切关注点，而是每种类型各自的生命周期问题
/// （伤害数字要重置文本与计时、经验球要重取玩家引用、敌人要重置血量/击退/波次增强）。
/// 泛型池无法表达这些差异，所以中心化的只该是「规则」，不是「容器」——
/// 每个具体池**自己持有**一个 <c>ObjectPool&lt;T&gt;</c> 字段即可，不要放进 ServiceLocator。
/// </para>
///
/// <para>
/// 本类集中了三件以前在每个池里各写一遍、且**都写错过一次**的事：
/// <list type="number">
/// <item>归还幂等（用 <c>activeSelf</c> 判重）——防重复入池后被两个使用者同时取用；</item>
/// <item>保留上限（<c>maxRetained</c>）——防池随历史峰值无限增长；</item>
/// <item>取出时跳过已被外部销毁的条目（场景卸载）——静态池跨场景存活时的自愈。</item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="T">池化对象类型；须实现 <see cref="IPoolable"/> 以接收取出/归还回调。</typeparam>
public sealed class ObjectPool<T> where T : Component, IPoolable
{
    private readonly Func<T> _create;
    private readonly int _maxRetained;
    private readonly string _name;
    private readonly List<T> _idle = new();

    /// <summary>池中空闲对象数量。</summary>
    public int IdleCount => _idle.Count;

    /// <summary>本池累计创建过的对象数（含已被销毁的）。</summary>
    public int TotalCreated { get; private set; }

    /// <param name="create">创建新实例的工厂；返回 null 表示当前无法创建（如 prefab 尚未加载）。</param>
    /// <param name="maxRetained">池中**保留**的空闲对象上限；超出部分在归还时直接销毁。</param>
    /// <param name="name">用于告警文案。</param>
    public ObjectPool(Func<T> create, int maxRetained, string name)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        _maxRetained = maxRetained > 0 ? maxRetained : 1;
        _name = name;
    }

    /// <summary>
    /// 取出一个对象：优先复用空闲对象，没有则创建。对象返回时已激活且已执行
    /// <see cref="IPoolable.OnGetFromPool"/>。
    /// </summary>
    public T Get()
    {
        // 从尾部取；跳过被外部销毁（如场景卸载）的条目
        while (_idle.Count > 0)
        {
            int last = _idle.Count - 1;
            T reused = _idle[last];
            _idle.RemoveAt(last);

            if (reused == null) continue;

            reused.gameObject.SetActive(true);
            reused.OnGetFromPool();
            return reused;
        }

        T created = _create();
        if (created == null) return null;

        TotalCreated++;
        // 首次创建的对象本就是激活的（SetActive 是空操作），
        // 所以 OnEnable 不会触发 —— 这正是重置逻辑必须走 OnGetFromPool 的原因
        created.gameObject.SetActive(true);
        created.OnGetFromPool();
        return created;
    }

    /// <summary>
    /// 归还对象（幂等）。已在池中（未激活）的对象会被忽略；
    /// 池已达保留上限时直接销毁该对象而不是继续囤积。
    /// </summary>
    public void Release(T item)
    {
        if (item == null) return;

        // 幂等判据：池中对象一律 SetActive(false)，因此"未激活"即"已在池中"
        if (!item.gameObject.activeSelf) return;

        item.OnReturnToPool();
        item.gameObject.SetActive(false);

        if (_idle.Count >= _maxRetained)
        {
            UnityEngine.Object.Destroy(item.gameObject);
            return;
        }

        _idle.Add(item);
    }

    /// <summary>
    /// 清空池并销毁其中的对象。用于跨 Play 会话/场景切换时切断静态引用
    /// （编辑器关闭 Domain Reload 时，静态池会跨会话存活，而其中的 Unity 对象已失效）。
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _idle.Count; i++)
        {
            if (_idle[i] != null)
                UnityEngine.Object.Destroy(_idle[i].gameObject);
        }

        _idle.Clear();
        TotalCreated = 0;
    }

    public override string ToString() => $"{_name}(idle={_idle.Count}, created={TotalCreated})";
}
