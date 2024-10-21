using UnityEngine;

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

    [SerializeField] private EquipEffectData _effectData;
    public EquipEffectData EffectData => _effectData;
}