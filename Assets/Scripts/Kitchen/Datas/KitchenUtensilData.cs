using UnityEngine;


public enum EquipEffectType
{
    AddScore,
    AddTipPerMinute,
    AddCookSpeed,
    AddMaxTip,
    None,
}


[CreateAssetMenu(fileName = "KitchenUtensilData", menuName = "Scriptable Object/KitchenUtensilData/KitchenUtensilData")]
public class KitchenUtensilData : ShopData
{
    [Space]
    [Header("KitchenUtensilData")]

    [SerializeField] private KitchenUtensilType _type;
    public KitchenUtensilType Type => _type;

    [SerializeField] private string _setId;
    public string SetId => _setId;

    [SerializeField] private int _addScore;
    public int AddScore => _addScore;

    [SerializeField] private float _sizeMul = 1;
    public float SizeMul => _sizeMul;
    

    [Space]
    [Header("EquipData")]
    [SerializeField] private EquipEffectType _equipEffectType;
    public EquipEffectType EquipEffectType => _equipEffectType;

    [Range(0, 1000000)] [SerializeField] private int _effectValue;
    public int EffectValue => _effectValue;
}