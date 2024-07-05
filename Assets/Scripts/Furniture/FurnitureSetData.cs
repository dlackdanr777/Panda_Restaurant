using UnityEngine;


public abstract class FurnitureSetData : ScriptableObject
{
    [SerializeField] private string _id;
    public string Id => _id;

    [TextArea][SerializeField] string _effectDescription;
    public string EffectDescription => _effectDescription;


    public abstract void Activate();
    public abstract void Deactivate();
    
}
