using UnityEngine;


public abstract class SetData : ScriptableObject
{
    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _name;
    public string Name => _name;

    [TextArea][SerializeField] string _description;
    public string Description => _description;

    [SerializeField] private FoodType _setFoodType;
    public FoodType SetFoodType => _setFoodType;

    public abstract float Value { get; }


    public abstract void Activate();
    public abstract void Deactivate();
    
}
