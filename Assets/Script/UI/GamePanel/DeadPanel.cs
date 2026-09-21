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
        AudioService.Service?.PlaySfx(ResourceEnum.LoseGame);
        if (AudioService.Service != null) AudioService.Service.BgmMuted = true;

        btnMenu.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Menu");
            UIManager.Service.HidePanel<DeadPanel>();
        });

        btnRestart.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("Level0");
            UIManager.Service.HidePanel<DeadPanel>();
        });
    }


    public void SetSurvivalTime(int timeInSeconds)
    {
        int minutes = timeInSeconds / 60;
        int seconds = timeInSeconds % 60;
        txtSurvivalTime.text = $"Survival Time: {minutes:00}:{seconds:00}";
    }
}
