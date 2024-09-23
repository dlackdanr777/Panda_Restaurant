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

    [Tooltip("���� ���� ��������Ʈ(������ ���� ��������Ʈ�� ��ü��)")]
    [SerializeField] private Sprite _rightChairSprite;
    public Sprite RightChairSprite => _rightChairSprite;
}
