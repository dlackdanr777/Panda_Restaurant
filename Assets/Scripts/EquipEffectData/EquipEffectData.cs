using UnityEngine;

public abstract class EquipEffectData : ScriptableObject
{
    public abstract int EffectValue { get; }

    public abstract void AddSlot();
    public abstract void RemoveSlot();
}
