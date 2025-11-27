using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeadPanel : BasePanel
{
    public TMPro.TextMeshProUGUI txtSurvivalTime;
    public Button btnMenu;
    public Button btnRestart;

    public override void Init()
    {
        BKMusic.Instance.PlaySound(ResourceEnum.LoseGame);
        BKMusic.Instance.audioSource.mute = true;

        btnMenu.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Menu");
            UIManager.Instance.HidePanel<DeadPanel>();
        });

        btnRestart.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Main");
            UIManager.Instance.HidePanel<DeadPanel>();
        });
    }


    public void SetSurvivalTime(int timeInSeconds)
    {
        int minutes = timeInSeconds / 60;
        int seconds = timeInSeconds % 60;
        txtSurvivalTime.text = $"Survival Time: {minutes:00}:{seconds:00}";
    }
}
