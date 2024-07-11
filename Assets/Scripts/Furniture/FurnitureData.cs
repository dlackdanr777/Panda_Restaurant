using UnityEngine;

public abstract class FurnitureData : BasicData
{
    [Space]
    [Header("FurnitureData")]

    [SerializeField] private FurnitureType _type;
    public FurnitureType Type => _type;

    [SerializeField] private string _setId;
    public string SetId => _setId;

    [SerializeField] private int _addScore;
    public int AddScore => _addScore;

    public abstract int EffectValue { get; }

    public abstract void AddSlot();
    public abstract void RemoveSlot();
}