using UnityEngine;


[CreateAssetMenu(fileName = "FurnitureData", menuName = "Scriptable Object/FurnitureData/FurnitureData")]
public class FurnitureData : ShopData
{
    [Space]
    [Header("FurnitureData")]

    [SerializeField] private FurnitureType _type;
    public FurnitureType Type => _type;

    [SerializeField] private string _setId;
    public string SetId => _setId;

    [SerializeField] private int _addScore;
    public int AddScore => _addScore;

    [Space]
    [Header("EquipData")]
    [SerializeField] private EquipEffectType _equipEffectType;
    public EquipEffectType EquipEffectType => _equipEffectType;

    [Range(0, 1000000)][SerializeField] private int _effectValue;
    public int EffectValue => _effectValue;
}