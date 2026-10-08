using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大厅编排 —— 进入大厅时开一次新的出行，并在还没选角色时弹出选角面板。
///
/// <para>
/// 它很薄，但有必要：<see cref="RunSession"/> 的生命周期必须有人负责。
/// 放在面板里会导致"玩家不打开选角面板，session 就没被重置"——
/// 上一局的角色与装备会带进这一局，而且看起来完全正常。
/// </para>
/// </summary>
public class LobbyDirector : MonoBehaviour
{
    [Header("可选角色")]
    [Tooltip("按顺序显示在选角面板上。与 PlayerSpawner 的角色列表是同一份数据来源（场景配置）")]
    public List<CharacterDefinitionSO> Characters = new List<CharacterDefinitionSO>();

    private async void Start()
    {
        // 关键服务失败时不再往下走：大厅会立刻用到 Profile（HUD）与 RunSession，
        // 带着一个不可用的服务继续加载只会把 NullReference 抛在更远的地方
        if (!await GameBootstrap.TryWaitReadyAsync()) return;
        if (this == null) return;

        // 开始一次新的出行：清空上一局的角色/装备，并取新的随机种子。
        // 从关卡返回大厅也走这里 —— 那是"这一局结束了"的自然语义。
        RunSessionService.Service?.BeginNew();

        // 常显 HUD（金币 / 哨站）：它订阅 Profile 事件，自己会在数据变化时刷新
        _ = UIService.Service?.ShowPanelAsync<LobbyHudPanel>();

        string characterId = RunSessionService.Service?.Current.CharacterId;
        if (!string.IsNullOrEmpty(characterId)) return;

        if (Characters.Count == 0)
        {
            Debug.LogWarning("[LobbyDirector] 没有配置可选角色，选角面板不会弹出。");
            return;
        }

        _ = UIService.Service?.ShowPanelAsync<CharacterSelectPanel>(panel => panel.SetCharacters(Characters));
    }
}
