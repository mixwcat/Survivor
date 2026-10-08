using UnityEngine;
using UnityEngine.UI;

public class MusicSettingPanel : BasePanel
{
    public override bool CanHandleEscape => true;

    /// <summary>
    /// 模态面板。它在菜单场景里打开（那里没有关卡管理器，暂停是空操作），
    /// 但万一从关卡里打开也不该让玩法继续跑。
    /// </summary>
    public override bool WantsPause => true;

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
            // 只隐藏自己：时间由暂停令牌集合恢复（不在关卡里时本来就没有暂停）
            UIService.Service.HidePanel<MusicSettingPanel>();
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
    }

    public override void EscLogic()
    {
        UIService.Service.HidePanel<MusicSettingPanel>();
    }
}
