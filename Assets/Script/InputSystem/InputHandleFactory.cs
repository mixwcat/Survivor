using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 输入处理工厂类
/// 根据平台创建对应的 IInputHandle 实例
/// 所有平台判断逻辑集中在此处，业务代码无需关心平台差异
///
/// 联机扩展：通过 inputId 区分不同玩家的输入源
///   - "local"     → 本地输入（当前设备）
///   - "network_X" → 远程玩家 X 的网络同步输入（未来实现）
///   - "ai_X"      → AI 玩家输入（未来实现）
///
/// 同一 inputId 返回**同一个**句柄实例，因此采用引用计数：
/// 每个 GetInput 都要有对应的 ReleaseInput，只有最后一个持有者释放时才真正从缓存移除，
/// 避免「玩家销毁顺手释放了输入，导致 UI 的 ESC 订阅 / 其他消费者一起失效」。
/// </summary>
public static class InputHandleFactory
{
    /// <summary>设备级本地输入的 ID（全局 UI/系统输入与本地玩家共用）。</summary>
    public const string LocalId = "local";

    /// <summary>已创建的输入句柄缓存，避免重复创建</summary>
    private static readonly Dictionary<string, IInputHandle> _inputCache = new();

    /// <summary>各句柄的持有者计数（按 inputId）</summary>
    private static readonly Dictionary<string, int> _refCounts = new();

    /// <summary>
    /// 按输入 ID 获取输入处理器（同一 ID 返回同一实例，并累加引用计数）
    /// </summary>
    /// <param name="inputId">输入标识："local" 为本地，其他为远程/AI</param>
    public static IInputHandle GetInput(string inputId)
    {
        if (_inputCache.TryGetValue(inputId, out var cached))
        {
            // 场景切换会销毁句柄依赖的 Unity 对象（Android 摇杆 / PC InputReader 宿主）。
            // 句柄本身不是 null，但会静默返回零输入 —— 必须在这里判死并重建，
            // 否则「重开关卡后玩家无法移动」这种问题不会有任何报错提示。
            if (cached == null || !cached.IsAlive)
            {
                _inputCache.Remove(inputId);
                _refCounts.Remove(inputId);
            }
            else
            {
                _refCounts[inputId] = GetRefCount(inputId) + 1;
                return cached;
            }
        }

        IInputHandle handle = null;

        if (inputId == LocalId)
        {
            handle = CreateLocalInput();
        }
        else if (inputId.StartsWith("network_"))
        {
            // 联机输入尚未实现：给零输入句柄，**不回落本地输入** ——
            // 回落会让远程玩家读到本地设备输入（表现为「远程角色跟着我动」），且没有任何报错。
            // 同一 id 只会创建一次（下面会进缓存），所以这条告警不会刷屏。
            Debug.LogWarning($"[InputHandleFactory] 网络输入 '{inputId}' 尚未实现，该玩家不接受输入。");
            handle = new NullInputHandle();
        }
        else if (inputId.StartsWith("ai_"))
        {
            // 未来：AI 玩家输入
            // handle = new AIInputHandle(inputId);
            Debug.LogWarning($"AI input '{inputId}' not yet implemented.");
        }

        // 创建失败（如 Android 无摇杆场景）不缓存，交由调用方判空
        if (handle != null)
        {
            _inputCache[inputId] = handle;
            _refCounts[inputId] = 1;
        }

        return handle;
    }

    /// <summary>
    /// 创建本地输入处理器（兼容旧代码，内部调用 GetInput("local")）
    /// </summary>
    public static IInputHandle GetLocalInput()
    {
        return GetInput(LocalId);
    }

    /// <summary>
    /// 释放指定输入句柄的一次引用；引用归零时才真正释放并从缓存移除。
    /// </summary>
    public static void ReleaseInput(string inputId)
    {
        if (!_inputCache.TryGetValue(inputId, out var handle)) return;

        int remaining = GetRefCount(inputId) - 1;
        if (remaining > 0)
        {
            _refCounts[inputId] = remaining;
            return;
        }

        _refCounts.Remove(inputId);
        _inputCache.Remove(inputId);
    }

    /// <summary>清空所有输入缓存（忽略引用计数，用于会话整体重建）</summary>
    public static void ClearCache()
    {
        _inputCache.Clear();
        _refCounts.Clear();
    }

    private static int GetRefCount(string inputId)
    {
        return _refCounts.TryGetValue(inputId, out int count) ? count : 0;
    }

    /// <summary>创建本地平台对应的输入实现</summary>
    private static IInputHandle CreateLocalInput()
    {
#if UNITY_STANDALONE_WIN
        // Windows 平台：使用 InputReader（新版 Input System）
        // 场景未配置 InputReaderManager 时按需自建，避免依赖 Inspector/场景接线。
        if (!ServiceLocator.TryGet<IInputReaderManager>(out var readerManager))
        {
            GameObject go = new GameObject("[InputReaderManager]");
            go.AddComponent<InputReaderManager>();
            ServiceLocator.TryGet<IInputReaderManager>(out readerManager);
        }

        if (readerManager == null)
        {
            Debug.LogError("[InputHandleFactory] IInputReaderManager 不可用，PC 输入无法创建。");
            return null;
        }

        return new PCInputHandle(readerManager.InputReader);

#elif UNITY_ANDROID
        // Android 平台：使用 Joystick Pack 插件
        var joysticks = Object.FindObjectsByType<Joystick>(FindObjectsSortMode.None);

        if (joysticks.Length < 2)
        {
            // 菜单等无摇杆场景会走到这里，属正常情况，仅提示
            Debug.LogWarning($"MobileInputHandle 需要场景中至少有 2 个 Joystick，当前 {joysticks.Length} 个，返回 null。");
            return null;
        }

        // 假设第一个是移动摇杆，第二个是攻击摇杆
        // 如果有特定命名规则，可以在这里根据 GameObject.name 筛选
        return new MobileInputHandle(joysticks[0], joysticks[1]);

#else
        Debug.LogError($"Unsupported platform: {Application.platform}. InputHandle not created.");
        return null;
#endif
    }
}
