using UnityEngine;

// 本脚本演示怎么控制游戏暂停和继续（不依赖Player的输入）
public class GameSceneManager : MonoBehaviour
{
    private IInputHandle _inputHandle;
    private bool isGameActive = true;

    void Awake()
    {
        _inputHandle = InputHandleFactory.GetInput("local");

        if (_inputHandle == null)
        {
            Debug.LogError("GameSceneManager: Failed to create IInputHandle!");
        }
    }

    void OnEnable()
    {
        if (_inputHandle != null)
        {
            _inputHandle.OnInteract += ShowPauseUI;
        }
    }

    void OnDisable()
    {
        if (_inputHandle != null)
        {
            _inputHandle.OnInteract -= ShowPauseUI;
        }
    }


    private void ShowPauseUI()
    {
        if (isGameActive)
        {
            ResumeGame();
        }
    }
    public void PauseGame()
    {
        if (!isGameActive)
        {
            Time.timeScale = 0f; // 暂停游戏
            isGameActive = true;
        }
    }

    public void ResumeGame()
    {
        if (isGameActive)
        {
            Time.timeScale = 1f; // 恢复游戏
            isGameActive = false;
        }
    }
}