using UnityEngine;

public abstract class KitchenUtensilData : BasicData
{
    [Space]
    [Header("KitchenUtensilData")]
    [SerializeField] private KitchenUtensilType _type;
    public KitchenUtensilType Type => _type;

    [SerializeField] private string _setId;
    public string SetId => _setId;

    [SerializeField] private int _addScore;
    public int AddScore => _addScore;


    [TextArea] [SerializeField] private string _effectDescription;
    public string EffectDescription => _effectDescription;

    public abstract int EffectValue { get; }

    public abstract void AddSlot();
    public abstract void RemoveSlot();
}