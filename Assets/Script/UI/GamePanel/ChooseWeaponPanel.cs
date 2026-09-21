using System.Collections.Generic;
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
        GameLevelManager.Service.PauseGame();

        // 获取本地玩家未激活的武器槽（按玩家实例化）
        IReadOnlyList<WeaponSlot> inactiveSlots = PlayerManager.Service?.LocalPlayer?.Weapons?.WeaponSlots;

        // TODO: 当前 UI 固定支持 2 个选项，后续扩展武器类型时请改为动态生成
        if (inactiveSlots != null && inactiveSlots.Count > 0)
        {
            var slot0 = inactiveSlots[0];
            img1.sprite = slot0.weaponSelectSO.displaySprite;
            btn1.onClick.AddListener(() => OnChooseWeapon(slot0));
        }
        if (inactiveSlots != null && inactiveSlots.Count > 1)
        {
            var slot1 = inactiveSlots[1];
            img2.sprite = slot1.weaponSelectSO.displaySprite;
            btn2.onClick.AddListener(() => OnChooseWeapon(slot1));
        }
    }

    private void OnChooseWeapon(WeaponSlot slot)
    {
        PlayerManager.Service?.LocalPlayer?.Weapons?.SelectWeapon(slot);
        UIManager.Service.HidePanel<ChooseWeaponPanel>();
        GameLevelManager.Service.ResumeGame();
        AudioService.Service?.PlaySfx(ResourceEnum.ChooseWeapon);
        AudioService.Service.BgmMuted = false;

#if UNITY_ANDROID
        UIManager.Service.GetPanel<GamePanel>().UpdateJoystickVisibility();
#endif
    }
}
