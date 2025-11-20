using System;
using System.Collections.Generic;

public static class EventCenter
{
    private static Dictionary<Enum, Action<object>> eventDictionary = new Dictionary<Enum, Action<object>>();

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventEnum"></param>
    /// <param name="listener"></param>
    public static void Subscribe(Enum eventEnum, Action<object> listener)
    {
        if (!eventDictionary.ContainsKey(eventEnum))
        {
            eventDictionary[eventEnum] = null;
        }
        eventDictionary[eventEnum] += listener;
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventEnum"></param>
    /// <param name="listener"></param>
    public static void Unsubscribe(Enum eventEnum, Action<object> listener)
    {
        if (eventDictionary.ContainsKey(eventEnum))
        {
            eventDictionary[eventEnum] -= listener;
        }
    }

    /// <summary>
    /// 触发事件
    /// </summary>
    /// <param name="eventEnum"></param>
    /// <param name="parameter"></param>
    public static void Trigger(Enum eventEnum, object parameter = null)
    {
        if (eventDictionary.ContainsKey(eventEnum) && eventDictionary[eventEnum] != null)
        {
            eventDictionary[eventEnum]?.Invoke(parameter);
        }
    }


    /// <summary>
    /// 取消订阅某个事件的所有监听器
    /// </summary>
    /// <param name="eventEnum"></param>
    public static void UnsubscribeSpecificEvent(Enum eventEnum)
    {
        if (eventDictionary.ContainsKey(eventEnum))
        {
            eventDictionary[eventEnum] = null;
        }
    }


    /// <summary>
    /// 清除所有事件和监听器
    /// </summary>
    public static void ClearAll()
    {
        eventDictionary.Clear();
    }


    /// <summary>
    /// 获取某个事件的订阅者数量
    /// </summary>
    /// <param name="eventEnum"></param>
    /// <returns></returns>
    public static int GetSubscriberCount(Enum eventEnum)
    {
        if (eventDictionary.ContainsKey(eventEnum) && eventDictionary[eventEnum] != null)
        {
            return eventDictionary[eventEnum].GetInvocationList().Length;
        }
        return 0;
    }
}