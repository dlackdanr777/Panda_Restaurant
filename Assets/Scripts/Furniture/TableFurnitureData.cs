using UnityEngine;

[CreateAssetMenu(fileName = "TableFurnitureData", menuName = "Scriptable Object/FurnitureData/TableFurnitureData")]
public class TableFurnitureData : FurnitureData
{
    [Space]
    [Header("TableFurnitureData")]
    [Range(0.5f, 2f)] [SerializeField] private float _scale = 1;
    public float Scale => _scale;

    [SerializeField] private Sprite _chairSprite;
    public Sprite ChairSprite => _chairSprite;

    [Tooltip("우측 의자 스프라이트(없으면 좌측 스프라이트로 대체함)")]
    [SerializeField] private Sprite _rightChairSprite;
    public Sprite RightChairSprite => _rightChairSprite;
}
