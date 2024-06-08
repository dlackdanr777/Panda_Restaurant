using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StaffData : ScriptableObject
{
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _name;
    public string Name => _name;

    [SerializeField] private string _description;
    public string Description => _description;

    public abstract float GetActionTime(int level);

    public abstract void AddSlot();

    public abstract void RemoveSlot();

    public abstract void Update();

    public abstract void UseSkill();

    public abstract bool IsUpgradeEnabed();
}
