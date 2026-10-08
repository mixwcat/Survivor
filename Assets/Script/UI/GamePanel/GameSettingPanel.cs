using UnityEngine;
using UnityEngine.UI;

public class GameSettingPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    /// <summary>模态面板：显示期间暂停游戏（令牌由 UIService 按本面板生命周期管理）。</summary>
    public override bool WantsPause => true;

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

        // 音画设置写进**档案**（内存），并立即应用到服务 —— 拖动时能实时听到效果。
        // 落盘时机是"关闭面板"这个明确事务点，不是每次拖动（见 SaveAudioSettings）。
        togBKM.onValueChanged.AddListener((isOn) =>
        {
            PlayerProfile profile = PlayerProfileService.Service?.Profile;
            if (profile != null) profile.audio.bgmMuted = !isOn;

            if (AudioService.Service != null) AudioService.Service.BgmMuted = !isOn;
        });

        togSE.onValueChanged.AddListener((isOn) =>
        {
            PlayerProfile profile = PlayerProfileService.Service?.Profile;
            if (profile != null) profile.audio.sfxEnabled = isOn;

            if (AudioService.Service != null) AudioService.Service.SfxEnabled = isOn;
        });

        sliderBKM.onValueChanged.AddListener((value) =>
        {
            PlayerProfile profile = PlayerProfileService.Service?.Profile;
            if (profile != null) profile.audio.bgmVolume = value;

            if (AudioService.Service != null) AudioService.Service.BgmVolume = value;
        });

        sliderSE.onValueChanged.AddListener((value) =>
        {
            PlayerProfile profile = PlayerProfileService.Service?.Profile;
            if (profile != null) profile.audio.sfxVolume = value;

            if (AudioService.Service != null) AudioService.Service.SfxVolume = value;
        });
        btnMenu.onClick.AddListener(() =>
        {
            UIService.Service.HidePanel<GameSettingPanel>();
            UIService.Service.HidePanel<GamePanel>();
            SaveAudioSettings();

            SceneFlow.LoadMenu();
        });
        btnRestart.onClick.AddListener(() =>
        {
            UIService.Service.HidePanel<GameSettingPanel>();
            // 关卡 HUD 也要收掉：它挂在 DontDestroyOnLoad 的画布上，
            // 不隐藏会跟着玩家进大厅继续显示上一局的等级与摇杆
            UIService.Service.HidePanel<GamePanel>();
            SaveAudioSettings();

            // 重开 = 回大厅重新出发（与 PausePanel / RunResultPanel 保持一致）
            SceneFlow.LoadLobby();
        });
        btnGoOn.onClick.AddListener(() =>
        {
            UIService.Service.HidePanel<GameSettingPanel>();
            SaveAudioSettings();
        });
        btnClose.onClick.AddListener(() =>
        {
            UIService.Service.HidePanel<GameSettingPanel>();
            SaveAudioSettings();
        });
    }

    /// <summary>
    /// 设置面板的「确认」事务点：把内存里的音画设置落到档案。
    /// 只在关闭路径调用 —— 拖动滑块时逐次写盘会让存档 IO 变得又频繁又不可预测。
    /// </summary>
    private static void SaveAudioSettings()
    {
        PlayerProfileService.Service?.Save();
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
    }

    public override void EscLogic()
    {
        UIService.Service.HidePanel<GameSettingPanel>();
        SaveAudioSettings();
    }
}
