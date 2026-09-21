using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSettingPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    public Toggle togBKM;
    public Toggle togSE;
    public Slider sliderBKM;
    public Slider sliderSE;
    public Button btnClose;
    public Button btnMenu;
    public Button btnRestart;
    public Button btnGoOn;
    public override void Init()
    {
        InitDisplay();

        togBKM.onValueChanged.AddListener((isOn) =>
        {
            AudioService.Service.BgmMuted = !isOn;
        });

        togSE.onValueChanged.AddListener((isOn) =>
        {
            AudioService.Service.SfxEnabled = isOn;
        });

        sliderBKM.onValueChanged.AddListener((value) =>
        {
            AudioService.Service.BgmVolume = value;
        });

        sliderSE.onValueChanged.AddListener((value) =>
        {
            AudioService.Service.SfxVolume = value;
        });
        btnMenu.onClick.AddListener(() =>
        {
            UIManager.Service.HidePanel<GameSettingPanel>();
            UIManager.Service.HidePanel<GamePanel>();
            GameLevelManager.Service.ResumeGame();

            SceneManager.LoadScene("Menu");
        });
        btnRestart.onClick.AddListener(() =>
        {
            UIManager.Service.HidePanel<GameSettingPanel>();
            GameLevelManager.Service.ResumeGame();

            SceneManager.LoadScene("Level0");
        });
        btnGoOn.onClick.AddListener(() =>
        {
            UIManager.Service.HidePanel<GameSettingPanel>();

            GameLevelManager.Service.ResumeGame();
        });
        btnClose.onClick.AddListener(() =>
        {
            UIManager.Service.HidePanel<GameSettingPanel>();

            GameLevelManager.Service.ResumeGame();
        });
    }

    private void InitDisplay()
    {
        IAudioService audio = AudioService.Service;
        if (audio != null)
        {
            togBKM.isOn = !audio.BgmMuted;
            togSE.isOn = audio.SfxEnabled;
            sliderBKM.value = audio.BgmVolume;
            sliderSE.value = audio.SfxVolume;
        }

        GameLevelManager.Service.PauseGame();
    }

    public override void EscLogic()
    {
        UIManager.Service.HidePanel<GameSettingPanel>();
        GameLevelManager.Service?.ResumeGame();
    }
}
