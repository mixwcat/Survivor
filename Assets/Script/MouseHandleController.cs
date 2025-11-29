using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

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
#if UNITY_STANDALONE_WIN
        HandleSprite_Win(towerSO);
#elif UNITY_ANDROID
        HandleSprite_Android(towerSO);
#endif
    }
    private void HandleSprite_Win(TowerSO towerSO)
    {
        // 获取鼠标位置
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;

        // 生成图标对象
        Instantiate(Resources.Load<GameObject>("Prefabs/SpriteToHandle"), mousePosition, Quaternion.identity).GetComponent<SpriteToHandle>().Init(towerSO);
    }
    private void HandleSprite_Android(TowerSO towerSO)
    {
        // 如果监听到触摸事件，则在触摸位置生成图标对象
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // 只处理右半屏的触摸点
                if (touch.phase == TouchPhase.Began && touch.position.x > Screen.width / 3)
                {
                    Vector3 touchPosition = Camera.main.ScreenToWorldPoint(touch.position);
                    touchPosition.z = 0;

                    // 生成图标对象
                    Instantiate(Resources.Load<GameObject>("Prefabs/SpriteToHandle"), touchPosition, Quaternion.identity).GetComponent<SpriteToHandle>().Init(towerSO);
                    break; // 只处理第一个符合条件的触摸点
                }
            }
        }
    }
}
