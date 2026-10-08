using UnityEngine;
using UnityEngine.UI;

public class MenuPanel : BasePanel
{
    public Button startButton;
    public Button settingsButton;
    public Button quitButton;
    public override void Init()
    {
        startButton.onClick.AddListener(() =>
        {
            // 开始游戏：先进大厅（选角色 / 武器台 / 传送门集合），不再直接进关卡
            SceneFlow.LoadLobby();
            UIService.Service.HidePanel<MenuPanel>();
        });
        settingsButton.onClick.AddListener(() =>
        {
            // 打开设置面板
            _ = UIService.Service.ShowPanelAsync<MusicSettingPanel>();
        });
        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }
}
