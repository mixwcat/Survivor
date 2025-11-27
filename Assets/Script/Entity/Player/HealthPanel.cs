using UnityEngine;
using UnityEngine.UI;

public class HealthPanel : MonoBehaviour
{
    public Slider healthSlider;
    public TMPro.TextMeshProUGUI healthText;
    private PlayerHealthController playerHealthController;

    void Start()
    {
        playerHealthController = GetComponentInParent<PlayerHealthController>();
        // 初始化血量面板
        UpdateHealthUI();
    }

    public void UpdateHealthUI()
    {
        healthSlider.value = playerHealthController.currentHealth / playerHealthController.maxHealth;
        healthText.text = $"{playerHealthController.currentHealth} / {playerHealthController.maxHealth}";
    }
}
