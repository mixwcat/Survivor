using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "LevelUpSO", menuName = "LevelUpSO", order = 0)]
public class LevelUpSO : ScriptableObject 
{
    public String levelUpText;
    public Sprite levelUpSprite;
    public UnityAction onLevelUp;
    public void RaiseEvent()
    {
        onLevelUp?.Invoke();
    }
}
