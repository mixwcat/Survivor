using System.Collections.Generic;
using UnityEngine;

public class UIManager
{
    private static UIManager instance = new UIManager();
    public static UIManager Instance => instance;
    private Dictionary<string, BasePanel> panelDict = new Dictionary<string, BasePanel>();  // 正在显示的面板存入字典
    private Transform canvasTrans;  // panel的父物体Canvas


    private UIManager()
    {
        // 创建Canvas
        GameObject canvasObj = GameObject.Instantiate(Resources.Load<GameObject>("UI/Canvas"));
        canvasTrans = canvasObj.transform;

        // 不随场景切换而销毁
        GameObject.DontDestroyOnLoad(canvasObj);
    }


    /// <summary>
    /// 完全依靠代码控制面板的显示
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T ShowPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (panelDict.ContainsKey(panelName))
        {
            return panelDict[panelName] as T;
        }

        // 从Resources加载预制Panel实例化
        GameObject panelObj = GameObject.Instantiate(Resources.Load<GameObject>($"UI/{typeof(T).Name}"));
        panelObj.transform.SetParent(canvasTrans, false);

        // 获取面板脚本，调用方法
        T panel = panelObj.GetComponent<T>();
        panelDict.Add(panelName, panel);
        panel.ShowMe();

        return panel;
    }


    public void HidePanel<T>(bool isFade = true) where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (panelDict.ContainsKey(panelName))
        {
            if (isFade)
            {
                // 通过匿名函数 传递Action
                panelDict[panelName].HideMe(() =>
                {
                    GameObject.Destroy(panelDict[panelName].gameObject);
                    panelDict.Remove(panelName);
                });
            }
            else
            {
                GameObject.Destroy(panelDict[panelName].gameObject);
                panelDict.Remove(panelName);
            }
        }
    }

    public T GetPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;

        if (panelDict.ContainsKey(panelName))
        {
            return panelDict[panelName] as T;
        }
        else
        {
            return null;
        }
    }
}