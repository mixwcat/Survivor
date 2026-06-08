using UnityEngine;
using UnityEngine.UI;

public class TowerHealthPanel : MonoBehaviour
{
    public Slider healthSlider;
    public TMPro.TextMeshProUGUI healthText;
    private BaseHealthController tetoHealthController;

    void Start()
    {
        tetoHealthController = GetComponentInParent<BaseHealthController>();
    }

    public void UpdateHealthUI()
    {
        healthSlider.value = tetoHealthController.CurrentHealth / tetoHealthController.MaxHealth;
        healthText.text = $"{(int)tetoHealthController.CurrentHealth} / {(int)tetoHealthController.MaxHealth}";
    }
}
