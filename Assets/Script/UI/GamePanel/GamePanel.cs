using System.Threading;
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

    public Button btnTowerLevelUp;
    private BaseTower currentTower;

    [Header("摇杆")]
    public Joystick joystickMove;
    public Joystick joystickWeapon;

    [Header("帧率")]
    private float fpsUpdateInterval = 0.5f;
    private float fpsTimer;
    public TMPro.TextMeshProUGUI txtFPS;


    public override void Init()
    {
        UpdateExp(0, 1, 1);
        // 开始游戏时，播放音效
        BKMusic.Instance.audioSource.mute = true;
        BKMusic.Instance.PlaySound(ResourceEnum.StartGame);

#if UNITY_ANDROID
        // 安卓环境，获取摇杆
        SendJoystickToPlayer(joystick);
#endif

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
            UIManager.Instance.ShowPanel<GameSettingPanel>();
        });
        btnTowerLevelUp.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<TowerLevelUpPanel>().SetTowerType(currentTower);
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


    /// <summary>
    /// 将摇杆发送给玩家
    /// </summary>
    /// <param name="js"></param>
    public void SendJoystickToPlayer(Joystick jsMove, Joystick jsWeapon = null)
    {
        PlayerController player = PlayerManager.Instance.player;

        if (player != null)
        {
            if (WeaponManager.Instance.weapons[0] is GunWeapon)
            {
                joystickMove.gameObject.SetActive(true);
                joystickWeapon.gameObject.SetActive(true);
                player.GetJoystick(jsMove, jsWeapon);
            }
            else
            {
                joystickMove.gameObject.SetActive(true);
                joystickWeapon.gameObject.SetActive(false);
                player.GetJoystick(jsMove);
            }
        }
    }


    public void SetButtonTowerLevelUpActive(bool isActive, BaseTower tower = null)
    {
        if (tower != null)
            currentTower = tower;
        btnTowerLevelUp.gameObject.SetActive(isActive);
    }


    protected override void Update()
    {
        base.Update();

        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer > fpsUpdateInterval)
        {
            // 显示帧率
            txtFPS.text = Mathf.Ceil(1.0f / Time.unscaledDeltaTime).ToString() + " FPS";
            // 重置计时器
            fpsTimer = 0f;
        }
    }
}