using UnityEngine;
using UnityEngine.UI;

public class ChooseWeaponPanel : BasePanel
{
    public Image img1;
    public Image img2;
    public Button btn1;
    public Button btn2;

    public override void Init()
    {
        GameLevelManager.Instance.PauseGame();

        // 获取未激活的武器槽（可供选择的武器）
        var inactiveSlots = WeaponManager.Instance.weaponSlots;

        // TODO: 当前 UI 固定支持 2 个选项，后续扩展武器类型时请改为动态生成
        if (inactiveSlots.Count > 0)
        {
            var slot0 = inactiveSlots[0];
            img1.sprite = slot0.weaponSelectSO.displaySprite;
            btn1.onClick.AddListener(() => OnChooseWeapon(slot0));
        }
        if (inactiveSlots.Count > 1)
        {
            var slot1 = inactiveSlots[1];
            img2.sprite = slot1.weaponSelectSO.displaySprite;
            btn2.onClick.AddListener(() => OnChooseWeapon(slot1));
        }
    }

    private void OnChooseWeapon(WeaponSlot slot)
    {
        slot.weaponSelectSO.RaiseSelectEvent();
        UIManager.Instance.HidePanel<ChooseWeaponPanel>();
        GameLevelManager.Instance.ResumeGame();
        BKMusic.Instance.PlaySound(ResourceEnum.ChooseWeapon);
        BKMusic.Instance.audioSource.mute = false;

#if UNITY_ANDROID
        UIManager.Instance.GetPanel<GamePanel>().UpdateJoystickVisibility();
#endif
    }
}
