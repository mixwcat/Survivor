public interface IInteractable
{
    // 定义交互行为
    void Interact();

    // 成为当前交互目标时调用
    void OnSelected();

    // 不再是当前交互目标时调用
    void OnDeselected();
}