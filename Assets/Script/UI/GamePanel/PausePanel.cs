using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class PausePanel : BasePanel
{
    public override bool CanHandleEscape => true;

    /// <summary>模态面板：显示期间暂停游戏（令牌由 UIService 按本面板生命周期管理）。</summary>
    public override bool WantsPause => true;

    public TextMeshProUGUI gameTimeText;
    public Button menuButton;
    public Button resumeButton;
    public Button restartButton;
    public override void Init()
    {
        GetGameTime();

        menuButton.onClick.AddListener(() =>
        {
            UIService.Service.HidePanel<PausePanel>();
            UIService.Service.HidePanel<GamePanel>();
            // timeScale 的复位由 SceneFlow 统一负责（它必须在任何切场景前发生）
            SceneFlow.LoadMenu();
        });
        resumeButton.onClick.AddListener(() =>
        {
            // 只隐藏自己：时间由暂停令牌集合恢复（还有别的模态面板时不会恢复）
            UIService.Service.HidePanel<PausePanel>();
        });
        restartButton.onClick.AddListener(() =>
        {
            UIService.Service.HidePanel<GamePanel>(false);
            UIService.Service.HidePanel<PausePanel>();
            // 重开 = 回大厅重新出发（与 RunResultPanel 的语义保持一致）
            SceneFlow.LoadLobby();
        });
    }

    private void GetGameTime()
    {
        float time = GameLevelManager.Service.LevelTime;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        gameTimeText.text = string.Format("存活时间为: {0:00}:{1:00}", minutes, seconds);
    }

    public override void EscLogic()
    {
        UIService.Service.HidePanel<PausePanel>();
    }
}
