using UnityEngine;

[CreateAssetMenu(fileName = "Tower", menuName = "Game/Tower")]
public class TowerInfoSO : ScriptableObject
{
    public string towerName;
    public string description;
    public int expConsumption;
    public GameObject towerPrefab;
    public Sprite towerIcon;
}
