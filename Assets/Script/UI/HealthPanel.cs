using UnityEngine;
using UnityEngine.UI;

public class HealthPanel : MonoBehaviour
{
    public Slider healthSlider;
    private PlayerHealthController playerHealthController;

    void Start()
    {
        playerHealthController = GetComponentInParent<PlayerHealthController>();
    }

    public void OnHealthChanged(object obj = null)
    {
        healthSlider.value = playerHealthController.currentHealth / playerHealthController.maxHealth;
    }

    private void OnEnable()
    {
        EventCenter.Subscribe(PlayerEnum.OnHealthChanged, OnHealthChanged);
    }

    private void OnDisable()
    {
        EventCenter.Unsubscribe(PlayerEnum.OnHealthChanged, OnHealthChanged);
    }
}
