using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuPanel : BasePanel
{
    public Button startButton;
    public Button quitButton;
    public override void Init()
    {
        startButton.onClick.AddListener(() =>
        {
            // 开始游戏

            SceneManager.LoadSceneAsync("Main");
            UIManager.Instance.HidePanel<MenuPanel>();
        });

        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });

    }
}
