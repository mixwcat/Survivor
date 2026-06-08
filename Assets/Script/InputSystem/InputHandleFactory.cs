using UnityEngine;

/// <summary>
/// 输入处理工厂类
/// 根据平台创建对应的 IInputHandle 实例
/// 所有平台判断逻辑集中在此处，业务代码无需关心平台差异
/// </summary>
public static class InputHandleFactory
{
    /// <summary>
    /// 创建本地输入处理器（根据编译平台自动选择）
    /// </summary>
    /// <returns>平台对应的 IInputHandle 实例</returns>
    public static IInputHandle GetLocalInput()
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

    // 未来扩展点：联机模式输入（暂不实现）
    // public static IInputHandle GetNetworkInput(NetworkConnection connection)
    // {
    //     return new NetworkInputHandle(connection);
    // }

    // 未来扩展点：AI 输入（暂不实现）
    // public static IInputHandle GetAIInput(AIController aiController)
    // {
    //     return new AIInputHandle(aiController);
    // }

    // 未来扩展点：回放输入（暂不实现）
    // public static IInputHandle GetReplayInput(ReplayData replayData)
    // {
    //     return new ReplayInputHandle(replayData);
    // }
}
