using UnityEngine;

[CreateAssetMenu(fileName = "New Tower", menuName = "Tower")]
public class TowerInfoSO : ScriptableObject
{
    public string towerName;
    public string description;
    public int expConsumption;
    public GameObject towerPrefab;
    public Sprite towerIcon;
}
