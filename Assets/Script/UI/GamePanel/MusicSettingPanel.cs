using UnityEngine;
using UnityEngine.UI;

public class MusicSettingPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    public Toggle togBKM;
    public Toggle togSE;
    public Slider sliderBKM;
    public Slider sliderSE;
    public Button btnClose;
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
        btnClose.onClick.AddListener(() =>
        {
            UIManager.Service.HidePanel<MusicSettingPanel>();

            if (GameLevelManager.Service != null)
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

        if (GameLevelManager.Service != null)
            GameLevelManager.Service.PauseGame();
    }

    public override void EscLogic()
    {
        UIManager.Service.HidePanel<MusicSettingPanel>();

        if (GameLevelManager.Service != null)
            GameLevelManager.Service.ResumeGame();
    }
}
