using UnityEngine;

[CreateAssetMenu(fileName = "TableFurnitureData", menuName = "Scriptable Object/FurnitureData/TableFurnitureData")]
public class TableFurnitureData : FurnitureData
{
    [Space]
    [Header("TableFurnitureData")]
    [SerializeField] private Sprite _chairSprite;
    public Sprite ChairSprite => _chairSprite;
}
