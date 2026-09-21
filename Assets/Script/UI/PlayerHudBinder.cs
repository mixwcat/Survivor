using System.Collections;
using UnityEngine;

/// <summary>
/// 本地玩家 HUD 绑定器。
/// 订阅本地玩家 <see cref="IExperienceController"/> 的强类型事件刷新 <see cref="GamePanel"/>，
/// 使经验/等级显示与核心状态解耦（核心只发事件，UI 只做表现）。
/// 联机模式下每个客户端只绑定自己的 <c>LocalPlayer</c>，天然隔离。
/// </summary>
public class PlayerHudBinder : MonoBehaviour
{
    private GamePanel _panel;
    private IExperienceController _exp;
    private Coroutine _routine;

    /// <summary>由面板在初始化时调用，绑定目标并开始监听。</summary>
    public void Bind(GamePanel panel)
    {
        _panel = panel;
        if (isActiveAndEnabled && _routine == null)
            _routine = StartCoroutine(BindRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        Unbind();
    }

    private IEnumerator BindRoutine()
    {
        while (_exp == null)
        {
            PlayerController local = PlayerManager.Service?.LocalPlayer;
            if (local != null)
                _exp = local.ExperienceController;

            if (_exp != null) break;
            yield return null;
        }

        _exp.OnExpChanged += HandleExpChanged;
        _exp.OnLevelUp += HandleLevelUp;
        _exp.OnPointsChanged += HandlePointsChanged;

        RefreshExp();
        _panel?.UpdateLevelPoint(_exp.AvailablePoints);
    }

    private void Unbind()
    {
        if (_exp == null) return;

        _exp.OnExpChanged -= HandleExpChanged;
        _exp.OnLevelUp -= HandleLevelUp;
        _exp.OnPointsChanged -= HandlePointsChanged;
        _exp = null;
    }

    private void HandleExpChanged(int _) => RefreshExp();
    private void HandleLevelUp(int _) => RefreshExp();
    private void HandlePointsChanged(int points) => _panel?.UpdateLevelPoint(points);

    private void RefreshExp()
    {
        if (_exp == null || _panel == null) return;

        int max = _exp.ExpToNextLevel;
        if (max <= 0 || max == int.MaxValue) max = 1;

        _panel.UpdateExp(_exp.CurrentExp, max, _exp.CurrentLevel);
    }
}
