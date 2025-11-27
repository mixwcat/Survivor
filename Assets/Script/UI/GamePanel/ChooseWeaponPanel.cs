using UnityEngine;
using UnityEngine.UI;

public class ChooseWeaponPanel : BasePanel
{
    public Image img1;
    public Image img2;
    public Button btn1;
    public Button btn2;
    public LevelUpSO fireBallSO;
    public LevelUpSO shootGunSO;
    public override void Init()
    {
        GameLevelManager.Instance.PauseGame();
        SetupPanel();


        btn1.onClick.AddListener(() =>
        {
            fireBallSO.RaiseEvent();
            UIManager.Instance.HidePanel<ChooseWeaponPanel>();
            GameLevelManager.Instance.ResumeGame();
            BKMusic.Instance.PlaySound(ResourceEnum.ChooseWeapon);
            BKMusic.Instance.audioSource.mute = false;
        });
        btn2.onClick.AddListener(() =>
        {
            shootGunSO.RaiseEvent();
            UIManager.Instance.HidePanel<ChooseWeaponPanel>();
            GameLevelManager.Instance.ResumeGame();
            BKMusic.Instance.PlaySound(ResourceEnum.ChooseWeapon);
            BKMusic.Instance.audioSource.mute = false;
        });
    }

    private void SetupPanel()
    {
        img1.sprite = fireBallSO.levelUpSprite;
        img2.sprite = shootGunSO.levelUpSprite;
    }
}
