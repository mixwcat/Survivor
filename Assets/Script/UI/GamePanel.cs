using UnityEngine;
using UnityEngine.UI;

public class GamePanel : BasePanel
{
    public Slider sldExp;
    public TMPro.TextMeshProUGUI txtLevel;
    public override void Init()
    {
        UpdateExp(0, 1, 1);
    }

    public void UpdateExp(float currentExp, float maxExp, int Level)
    {
        sldExp.value = currentExp;
        sldExp.maxValue = maxExp;
        txtLevel.text = "Level " + Level.ToString();
    }
}
