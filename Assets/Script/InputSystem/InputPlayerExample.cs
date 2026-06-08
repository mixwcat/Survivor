using UnityEngine;

// 本脚本演示怎么读取移动、E交互、dash等
public class PlayerExample : MonoBehaviour
{
    [Header("Input Channel")]
    private IInputHandle _inputHandle;
    private Vector2 inputVector;
    private IInteractable currentInteractable;

    // ... 其他代码

    protected void Awake()
    {
        _inputHandle = InputHandleFactory.GetLocalInput();

        if (_inputHandle == null)
        {
            Debug.LogError("Player (InputPlayer): Failed to create IInputHandle!");
        }
    }

    protected void Update()
    {
        if (_inputHandle == null) return;

        inputVector = _inputHandle.MoveInput;
        Move();
        Debug.Log($"Current Move Input: {inputVector}");
    }

    private void Move()
    {
        transform.Translate(new Vector3(inputVector.x, 0, inputVector.y) * Time.deltaTime * 5f);
    }

    #region 处理交互
    // 实现原理：当玩家进入一个可交互物体的触发器时，记录该物体的 IInteractable 接口引用。
    void OnTriggerEnter(Collider other)
    {
        var interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
        {
            currentInteractable = interactable;
        }
    }

    void OnEnable()
    {
        if (_inputHandle != null)
        {
            _inputHandle.OnInteract += HandleEPress;
        }
    }

    void OnDisable()
    {
        if (_inputHandle != null)
        {
            _inputHandle.OnInteract -= HandleEPress;
        }
    }

    private void HandleEPress()
    {
        if (currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }
    #endregion


    // 在PlayerOnGround等脚本中订阅Dash和Jump等事件
}