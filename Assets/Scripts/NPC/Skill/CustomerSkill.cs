using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public abstract class CustomerSkill : ScriptableObject
{
    [TextArea][SerializeField] private string _description;
    public string Description => _description;

    [Range(0f, 100f)] [SerializeField] private float _skillActivatePercent;
    public float SkillActivatePercent => _skillActivatePercent;

    public abstract float FirstValue { get; }



    public abstract void Activate(Customer customer);
    public abstract void Deactivate(Customer customer);
}
