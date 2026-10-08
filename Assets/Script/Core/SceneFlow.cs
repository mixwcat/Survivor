using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景流的**唯一入口**。所有切场景都走这里，不要在各面板里直接 <c>SceneManager.LoadScene</c>。
///
/// <para>
/// <b>为什么要有这一层：</b>切场景前必须复位 <c>Time.timeScale</c> ——
/// 它是跨场景存活的全局状态，带着 0 进下一个场景会让整个关卡静止，
/// 而只有 unscaled 的 UI 还在动，现象看起来像"卡死"，实际是暂停没解除。
/// 原先每个面板各自记得复位（`PausePanel` 里还专门写了注释提醒），
/// 但只要新增一个入口就会漏一次。放在这里统一处理，就不必指望调用方记得。
/// </para>
///
/// <para>
/// 场景名常量也集中在这里：散落的字符串字面量在改名时不会有任何编译错误。
/// </para>
/// </summary>
public static class SceneFlow
{
    /// <summary>主菜单（Build Settings index 0，启动场景）。</summary>
    public const string Menu = "Menu";

    /// <summary>大厅：选角色、乱逛、传送门集合。</summary>
    public const string Lobby = "Lobby";

    /// <summary>战斗关卡。当前只有第一关，多哨站时这里会变成按 stageId 解析。</summary>
    public const string Stage = "Level0";

    public static void LoadMenu() => Load(Menu);

    public static void LoadLobby() => Load(Lobby);

    public static void LoadStage() => Load(Stage);

    private static void Load(string sceneName)
    {
        // 见类型注释：必须在这里复位，而不是指望每个调用点
        Time.timeScale = 1f;

        // 异步加载：切场景时不会卡住主线程（大厅/关卡都有 Addressables 加载，同步切会更明显）
        SceneManager.LoadSceneAsync(sceneName);
    }
}
