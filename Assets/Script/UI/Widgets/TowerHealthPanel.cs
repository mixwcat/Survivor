using UnityEngine;
using UnityEngine.UI;

public class TowerHealthPanel : MonoBehaviour
{
    public Slider healthSlider;
    public TMPro.TextMeshProUGUI healthText;
    private BaseHealthController tetoHealthController;

    void Awake()
    {
        tetoHealthController = GetComponentInParent<BaseHealthController>();
    }

    public void UpdateHealthUI()
    {
        if (tetoHealthController == null)
            tetoHealthController = GetComponentInParent<BaseHealthController>();
        if (tetoHealthController == null) return;

        healthSlider.value = tetoHealthController.CurrentHealth / tetoHealthController.MaxHealth;
        healthText.text = $"{(int)tetoHealthController.CurrentHealth} / {(int)tetoHealthController.MaxHealth}";
    }
}
