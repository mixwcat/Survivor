using System;
using System.Collections.Generic;

/// <summary>
/// 服务定位器：集中管理所有服务接口的注册与获取。
/// 为联机模式预留扩展点——单机注册本地实现，联机注册网络实现。
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    /// <summary>注册服务实例</summary>
    public static void Register<T>(T service)
    {
        _services[typeof(T)] = service;
    }

    /// <summary>获取已注册的服务，未注册时抛出异常</summary>
    public static T Get<T>()
    {
        if (_services.TryGetValue(typeof(T), out var svc))
            return (T)svc;
        throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");
    }

    /// <summary>尝试获取服务，返回是否成功</summary>
    public static bool TryGet<T>(out T service)
    {
        if (_services.TryGetValue(typeof(T), out var svc))
        {
            service = (T)svc;
            return true;
        }
        service = default;
        return false;
    }

    /// <summary>服务是否已注册</summary>
    public static bool IsRegistered<T>()
    {
        return _services.ContainsKey(typeof(T));
    }

    /// <summary>注销服务。返回是否确实移除了一个注册（未注册时返回 false）。</summary>
    public static bool Unregister<T>()
    {
        return _services.Remove(typeof(T));
    }

    /// <summary>
    /// **仅当当前注册的就是 <paramref name="instance"/> 时**才注销。返回是否真的注销了。
    ///
    /// <para>
    /// <b>为什么不要直接调 <see cref="Unregister{T}"/>：</b>场景里出现重复 Manager 时，
    /// 重复实例会被销毁，而它的 <c>OnDestroy</c> 会无条件注销 ——
    /// 结果是主实例还活着，服务入口却空了（输入、玩家、塔、统计突然全部不可用，
    /// 且发生在切场景后一帧，极难定位）。销毁方必须确认"注册的那个确实是我"。
    /// </para>
    /// </summary>
    public static bool UnregisterIfSelf<T>(object instance) where T : class
    {
        if (instance == null) return false;
        if (!TryGet<T>(out T current) || !ReferenceEquals(current, instance)) return false;

        return Unregister<T>();
    }

    /// <summary>
    /// 清空所有服务。用于会话/场景整体重建。
    /// 注意：会同时清除跨场景服务，仅在明确知道需要重新引导时调用。
    /// </summary>
    public static void Clear()
    {
        _services.Clear();
    }
}
