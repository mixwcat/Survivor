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

    /// <summary>注销服务</summary>
    public static void Unregister<T>()
    {
        _services.Remove(typeof(T));
    }
}
