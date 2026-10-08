using Mirror;
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
/// <b>联机时它还要负责"谁有权切场景"：</b>服务端走 <c>NetworkManager.ServerChangeScene</c>
/// （Mirror 会把新场景名广播给所有客户端），**客户端什么都不做** ——
/// 客户端自己 <c>LoadScene</c> 会和 Mirror 的场景同步打架，结果是两端场景不一致、
/// 网络对象在错误的场景里被 spawn。
/// </para>
///
/// <para>
/// 场景名常量也集中在这里：散落的字符串字面量在改名时不会有任何编译错误。
/// Mirror 那边（<c>offlineScene</c> / <c>ServerChangeScene</c>）用**资产路径**口径 ——
/// <c>NetworkManager</c> 内部有一处只认路径的比较（<c>NetworkManager.cs:608,1297</c> 的
/// <c>GetActiveScene().path != offlineScene</c>），两者混用会出现"明明在同一场景却又加载一次"。
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

    // ---- 资产路径（Mirror 的口径）----

    /// <summary>主菜单的资产路径。</summary>
    public const string MenuPath = "Assets/Scenes/Menu.unity";

    /// <summary>大厅的资产路径。它同时是 <c>NetworkManager.offlineScene</c>（见 Docs/MirrorPlan.md §1.3）。</summary>
    public const string LobbyPath = "Assets/Scenes/Lobby.unity";

    /// <summary>战斗关卡的资产路径。</summary>
    public const string StagePath = "Assets/Scenes/Level0.unity";

    public static void LoadMenu() => Load(Menu);

    public static void LoadLobby() => Load(Lobby);

    public static void LoadStage() => Load(Stage);

    /// <summary>按资产路径切场景（联机路径专用；Mirror 需要路径口径）。</summary>
    public static void LoadPath(string scenePath) => Load(scenePath);

    private static void Load(string sceneNameOrPath)
    {
        // 见类型注释：必须在这里复位，而不是指望每个调用点
        Time.timeScale = 1f;

        if (NetworkBootstrap.IsActive)
        {
            // 服务端：由 Mirror 广播场景名，客户端跟着切
            if (NetworkServer.active)
            {
                NetworkBootstrap.ServerChangeScene(ToAssetPath(sceneNameOrPath));
                return;
            }

            // 纯客户端：等服务端的 SceneMessage。
            // 这里刻意**不**打红错 —— 客户端的"我要进关卡"本来就该表达成一次
            // 对服务端的请求（[Command]），由服务端在全员就绪后统一切
            Debug.Log($"[SceneFlow] 客户端不自行切换场景（{sceneNameOrPath}），等待服务端指令。");
            return;
        }

        // 单机：异步加载，切场景时不会卡住主线程（大厅/关卡都有 Addressables 加载，同步切会更明显）
        SceneManager.LoadSceneAsync(sceneNameOrPath);
    }

    /// <summary>把场景名补成资产路径；已经是路径的原样返回。</summary>
    private static string ToAssetPath(string sceneNameOrPath)
    {
        if (string.IsNullOrEmpty(sceneNameOrPath)) return sceneNameOrPath;
        if (sceneNameOrPath.EndsWith(".unity")) return sceneNameOrPath;

        switch (sceneNameOrPath)
        {
            case Menu: return MenuPath;
            case Lobby: return LobbyPath;
            case Stage: return StagePath;
            default: return sceneNameOrPath;
        }
    }
}
