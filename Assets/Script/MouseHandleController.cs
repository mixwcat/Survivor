using System.Collections.Generic;
using UnityEngine;

public class MouseHandleController : MonoBehaviour
{
    public static MouseHandleController Instance;


    void Awake()
    {
        Instance = this;
    }


    /// <summary>
    /// 处理塔图标显示
    /// </summary>
    /// <param name="towerSO"></param>
    public void HandleSprite(TowerSO towerSO)
    {
        // 获取鼠标位置
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;

        // 生成图标对象
        Instantiate(Resources.Load<GameObject>("Prefabs/SpriteToHandle"), mousePosition, Quaternion.identity).GetComponent<SpriteToHandle>().Init(towerSO);
    }
}
