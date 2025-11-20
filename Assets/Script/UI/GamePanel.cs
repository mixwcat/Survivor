using UnityEngine;
using UnityEngine.UI;

public class GamePanel : BasePanel
{
    [Header("经验值与等级显示")]
    public Slider sldExp;
    public TMPro.TextMeshProUGUI txtLevel;

    [Header("时间显示")]
    public TMPro.TextMeshProUGUI txtTime;

    [Header("购买界面")]
    public TMPro.TextMeshProUGUI txtLevelPoint;
    public Button btnWeaponShop;


    public override void Init()
    {
        UpdateExp(0, 1, 1);

        btnWeaponShop.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowPanel<LevelUpPanel>();
        });
    }

    public void UpdateExp(float currentExp, float maxExp, int Level)
    {
        sldExp.value = currentExp;
        sldExp.maxValue = maxExp;
        txtLevel.text = "Level " + Level.ToString();
    }

    public void UpdateTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        txtTime.text = "Time: " + minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    public void UpdateLevelPoint(int levelPoint)
    {
        txtLevelPoint.text = "等级点:" + levelPoint.ToString("00");
    }
}
