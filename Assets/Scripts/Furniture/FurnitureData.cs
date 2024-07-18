using UnityEngine;


[CreateAssetMenu(fileName = "FurnitureData", menuName = "Scriptable Object/FurnitureData/FurnitureData")]
public class FurnitureData : BasicData
{
    [Space]
    [Header("FurnitureData")]

    [SerializeField] private FurnitureType _type;
    public FurnitureType Type => _type;

    [SerializeField] private string _setId;
    public string SetId => _setId;

    [SerializeField] private int _addScore;
    public int AddScore => _addScore;

    [SerializeField] private FurnitureEffectData _effectData;
    public FurnitureEffectData EffectData => _effectData;
}