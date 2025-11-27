using UnityEngine;
using UnityEngine.SceneManagement;
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
            // 开始游戏
            SceneManager.LoadSceneAsync("Main");
            UIManager.Instance.HidePanel<MenuPanel>();
        });
        settingsButton.onClick.AddListener(() =>
        {
            // 打开设置面板
            UIManager.Instance.ShowPanel<MusicSettingPanel>();
        });
        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }
}
