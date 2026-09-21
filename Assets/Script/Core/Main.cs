using UnityEngine;

/// <summary>
/// 菜单场景入口。全局服务由 GameBootstrap 统一初始化，
/// 这里只负责在服务就绪后展示菜单面板。
/// </summary>
public class Main : MonoBehaviour
{
    async void Start()
    {
        await GameBootstrap.Ready;
        UIManager.Service.ShowPanel<MenuPanel>();
    }
}
