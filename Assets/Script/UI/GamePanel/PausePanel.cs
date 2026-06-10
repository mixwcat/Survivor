using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class PausePanel : BasePanel
{
    public TextMeshProUGUI gameTimeText;
    public Button menuButton;
    public Button resumeButton;
    public Button restartButton;
    public override void Init()
    {
        GetGameTime();
        GameLevelManager.Service.PauseGame();

        menuButton.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<PausePanel>();
            UIManager.Instance.HidePanel<GamePanel>();
            SceneManager.LoadScene("Menu");
        });
        resumeButton.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<PausePanel>();
            GameLevelManager.Service.ResumeGame();
        });
        restartButton.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<GamePanel>(false);
            UIManager.Instance.HidePanel<PausePanel>();
            SceneManager.LoadScene("Main");
        });
    }

    private void GetGameTime()
    {
        float time = GameLevelManager.Service.LevelTime;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        gameTimeText.text = string.Format("存活时间为: {0:00}:{1:00}", minutes, seconds);
    }

    public override void EscLogic()
    {
        UIManager.Instance.HidePanel<PausePanel>();
        GameLevelManager.Service.ResumeGame();
    }
}
