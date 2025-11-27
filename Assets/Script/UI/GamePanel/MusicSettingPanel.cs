using UnityEngine;
using UnityEngine.UI;

public class MusicSettingPanel : BasePanel
{
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
            BKMusic.Instance.audioSource.mute = !isOn;
        });

        togSE.onValueChanged.AddListener((isOn) =>
        {
            BKMusic.Instance.soundOpen = isOn;
        });

        sliderBKM.onValueChanged.AddListener((value) =>
        {
            BKMusic.Instance.audioSource.volume = value;
        });

        sliderSE.onValueChanged.AddListener((value) =>
        {
            BKMusic.Instance.soundValue = value;
        });
        btnClose.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<MusicSettingPanel>();

            if (GameLevelManager.Instance != null)
                GameLevelManager.Instance.ResumeGame();
        });
    }

    private void InitDisplay()
    {
        togBKM.isOn = !BKMusic.Instance.audioSource.mute;
        togSE.isOn = BKMusic.Instance.soundOpen;
        sliderBKM.value = BKMusic.Instance.audioSource.volume;
        sliderSE.value = BKMusic.Instance.soundValue;

        if (GameLevelManager.Instance != null)
            GameLevelManager.Instance.PauseGame();
    }
}
