using UnityEditor;
using UnityEngine;

public class Main : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        UIManager.Instance.ShowPanel<MenuPanel>();
        BKMusic.Instance.Wake();
    }
}
