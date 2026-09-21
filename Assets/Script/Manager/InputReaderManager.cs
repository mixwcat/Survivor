using UnityEngine;

[DefaultExecutionOrder(-150)]
public class InputReaderManager : ManagerSingleton<InputReaderManager>
{
    protected override bool PersistAcrossScenes => true;

    public InputReader inputReader;

    protected override void OnSingletonAwake()
    {
        // InputReader 自身在 OnEnable 中创建并启用 InputSystem_Actions，
        // 无需 Inspector 拖拽；未配置时运行时自建，保证 PC 输入可用。
        if (inputReader == null)
            inputReader = ScriptableObject.CreateInstance<InputReader>();
    }
}
