using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSettingPanel : BasePanel
{
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
        btnMenu.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<GameSettingPanel>();
            UIManager.Instance.HidePanel<GamePanel>();
            GameLevelManager.Instance.ResumeGame();

            SceneManager.LoadScene("Menu");
        });
        btnRestart.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<GameSettingPanel>();
            GameLevelManager.Instance.ResumeGame();

            SceneManager.LoadScene("Main");
        });
        btnGoOn.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<GameSettingPanel>();

            GameLevelManager.Instance.ResumeGame();
        });
        btnClose.onClick.AddListener(() =>
        {
            UIManager.Instance.HidePanel<GameSettingPanel>();

            GameLevelManager.Instance.ResumeGame();
        });
    }

    private void InitDisplay()
    {
        togBKM.isOn = !BKMusic.Instance.audioSource.mute;
        togSE.isOn = BKMusic.Instance.soundOpen;
        sliderBKM.value = BKMusic.Instance.audioSource.volume;
        sliderSE.value = BKMusic.Instance.soundValue;

        GameLevelManager.Instance.PauseGame();
    }
}
