using UnityEngine;
using UnityEngine.UI;

public class GamePanel : BasePanel
{
    [Header("经验值与等级显示")]
    public Slider sldExp;
    public TMPro.TextMeshProUGUI txtLevel;

    [Header("时间显示")]
    public TMPro.TextMeshProUGUI txtTime;
    public Button btnSetting;

    [Header("购买界面")]
    public TMPro.TextMeshProUGUI txtLevelPoint;
    public Button btnWeaponShop;
    public Button btnTowerShop;

    [Header("摇杆")]
    public Joystick joystick;


    public override void Init()
    {
        UpdateExp(0, 1, 1);
        // 开始游戏时，播放音效
        BKMusic.Instance.audioSource.mute = true;
        BKMusic.Instance.PlaySound(ResourceEnum.StartGame);
        SendJoystickToPlayer(joystick);

        btnWeaponShop.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<LevelUpPanel>();
        });
        btnTowerShop.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<ChooseTowerPanel>();
        });
        btnSetting.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<MusicSettingPanel>();
        });
    }


    /// <summary>
    /// 更新经验值与等级显示
    /// </summary>
    /// <param name="currentExp"></param>
    /// <param name="maxExp"></param>
    /// <param name="Level"></param>
    public void UpdateExp(float currentExp, float maxExp, int Level)
    {
        sldExp.value = currentExp;
        sldExp.maxValue = maxExp;
        txtLevel.text = "Level " + Level.ToString();
    }


    /// <summary>
    /// 更新时间显示
    /// </summary>
    /// <param name="timeInSeconds"></param>
    public void UpdateTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        txtTime.text = "Time: " + minutes.ToString("00") + ":" + seconds.ToString("00");
    }


    /// <summary>
    /// 更新等级点显示
    /// </summary>
    /// <param name="levelPoint"></param>
    public void UpdateLevelPoint(int levelPoint)
    {
        txtLevelPoint.text = "等级点:" + levelPoint.ToString("00");
    }


    public void SendJoystickToPlayer(Joystick js)
    {
        PlayerController player = PlayerManager.Instance.player;
        if (player != null)
        {
            player.GetJoystick(js);
        }
    }
}
