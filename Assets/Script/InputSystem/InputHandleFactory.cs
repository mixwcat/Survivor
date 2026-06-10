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
/// </summary>
public static class InputHandleFactory
{
    /// <summary>已创建的输入句柄缓存，避免重复创建</summary>
    private static readonly Dictionary<string, IInputHandle> _inputCache = new();

    /// <summary>
    /// 按输入 ID 获取输入处理器
    /// </summary>
    /// <param name="inputId">输入标识："local" 为本地，其他为远程/AI</param>
    public static IInputHandle GetInput(string inputId)
    {
        if (_inputCache.TryGetValue(inputId, out var cached))
            return cached;

        IInputHandle handle = null;

        if (inputId == "local")
        {
            handle = CreateLocalInput();
        }
        else if (inputId.StartsWith("network_"))
        {
            // 未来：联机模式下创建 NetworkInputHandle
            // handle = new NetworkInputHandle(inputId);
            Debug.LogWarning($"Network input '{inputId}' not yet implemented. Falling back to local.");
            handle = CreateLocalInput();
        }
        else if (inputId.StartsWith("ai_"))
        {
            // 未来：AI 玩家输入
            // handle = new AIInputHandle(inputId);
            Debug.LogWarning($"AI input '{inputId}' not yet implemented.");
        }

        if (handle != null)
            _inputCache[inputId] = handle;

        return handle;
    }

    /// <summary>
    /// 创建本地输入处理器（兼容旧代码，内部调用 GetInput("local")）
    /// </summary>
    public static IInputHandle GetLocalInput()
    {
        return GetInput("local");
    }

    /// <summary>释放指定输入句柄的缓存</summary>
    public static void ReleaseInput(string inputId)
    {
        _inputCache.Remove(inputId);
    }

    /// <summary>清空所有输入缓存</summary>
    public static void ClearCache()
    {
        _inputCache.Clear();
    }

    /// <summary>创建本地平台对应的输入实现</summary>
    private static IInputHandle CreateLocalInput()
    {
#if UNITY_STANDALONE_WIN
        // Windows 平台：使用 InputReader（新版 Input System）
        if (InputReaderManager.Instance == null)
        {
            Debug.LogError("InputReaderManager.Instance is null! Make sure InputReaderManager exists in scene.");
            return null;
        }

        return new PCInputHandle(InputReaderManager.Instance.inputReader);

#elif UNITY_ANDROID
        // Android 平台：使用 Joystick Pack 插件
        var joysticks = Object.FindObjectsByType<Joystick>(FindObjectsSortMode.None);

        if (joysticks.Length < 2)
        {
            Debug.LogError($"MobileInputHandle requires 2 Joystick components in scene, but found {joysticks.Length}. " +
            "Make sure you have a move joystick and an attack joystick in the scene.");
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
