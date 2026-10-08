using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="INetworkSessionService"/> 的实现：一张按 <c>connectionId</c> 索引的内存表。
///
/// <para>
/// 由组合根 <see cref="GameBootstrap"/> 创建并注册（与其余全局服务同一套约定）；
/// 业务代码走 <see cref="Service"/>（未注册时返回 null）。
/// </para>
///
/// <para>
/// <b>它没有跨端同步</b>：表里的内容只在**服务端**有意义。客户端要显示"队友选了什么"，
/// 读的是那个玩家对象上的 <c>SyncVar</c>，不是这张表。
/// </para>
/// </summary>
public class NetworkSessionService : MonoBehaviour, INetworkSessionService
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static INetworkSessionService Service =>
        ServiceLocator.TryGet<INetworkSessionService>(out var svc) ? svc : null;

    /// <summary>一个连接的选择。</summary>
    private sealed class Slot
    {
        public string CharacterId;
        public readonly List<string> Loadout = new List<string>();
    }

    private readonly Dictionary<int, Slot> _slots = new Dictionary<int, Slot>();

    /// <summary>空列表常量：没有记录时返回它，避免调用方每次判 null。</summary>
    private static readonly string[] EmptyLoadout = Array.Empty<string>();

    public event Action<int> SlotChanged;

    public int Count => _slots.Count;

    private void Awake()
    {
        ServiceLocator.Register<INetworkSessionService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.UnregisterIfSelf<INetworkSessionService>(this);
    }

    public bool TryGetCharacter(int connectionId, out string characterId)
    {
        characterId = null;

        if (!_slots.TryGetValue(connectionId, out Slot slot)) return false;
        if (string.IsNullOrEmpty(slot.CharacterId)) return false;

        characterId = slot.CharacterId;
        return true;
    }

    public void SetCharacter(int connectionId, string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return;

        Slot slot = GetOrCreate(connectionId);
        if (slot.CharacterId == characterId) return;

        slot.CharacterId = characterId;
        SlotChanged?.Invoke(connectionId);
    }

    public IReadOnlyList<string> GetLoadout(int connectionId)
    {
        if (!_slots.TryGetValue(connectionId, out Slot slot)) return EmptyLoadout;
        return slot.Loadout;
    }

    public void SetLoadout(int connectionId, IReadOnlyList<string> weaponIds)
    {
        Slot slot = GetOrCreate(connectionId);
        slot.Loadout.Clear();

        if (weaponIds != null)
        {
            for (int i = 0; i < weaponIds.Count; i++)
            {
                string id = weaponIds[i];
                if (!string.IsNullOrEmpty(id)) slot.Loadout.Add(id);
            }
        }

        SlotChanged?.Invoke(connectionId);
    }

    public void Forget(int connectionId)
    {
        if (_slots.Remove(connectionId)) SlotChanged?.Invoke(connectionId);
    }

    public void ClearAll()
    {
        if (_slots.Count == 0) return;

        _slots.Clear();
        SlotChanged?.Invoke(-1);
    }

    private Slot GetOrCreate(int connectionId)
    {
        if (_slots.TryGetValue(connectionId, out Slot slot)) return slot;

        slot = new Slot();
        _slots[connectionId] = slot;
        return slot;
    }
}
