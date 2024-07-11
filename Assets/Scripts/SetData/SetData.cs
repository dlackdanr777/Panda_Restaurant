using UnityEngine;


public abstract class SetData : ScriptableObject
{
    [SerializeField] private string _id;
    public string Id => _id;

    [TextArea][SerializeField] string _description;
    public string Description => _description;


    public abstract void Activate();
    public abstract void Deactivate();
    
}
