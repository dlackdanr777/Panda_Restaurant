using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GlobalCookingSpeedUpSkill", menuName = "Scriptable Object/Skill/GlobalCookingSpeedUpSkill")]
public class GlobalCookingSpeedUpSkill : SkillBase
{
    [Range(0f, 1000f)]
    [SerializeField]
    private float _globalCookingSpeedUpPercent = 50f;

    public override float FirstValue => _globalCookingSpeedUpPercent;

    public override float SecondValue => 0;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (staff == null)
        {
            throw new ArgumentNullException(nameof(staff));
        }

        StaffSkillEffectRegistry effectRegistry = staff.SkillEffectRegistry;
        if (effectRegistry == null)
        {
            throw new InvalidOperationException("STAFF_SKILL09 requires the Staff cached effect registry.");
        }

        string staffId = staff.StaffData != null ? staff.StaffData.Id : nameof(GlobalCookingSpeedUpSkill);
        effectRegistry.RegisterOrUpdate(
            StaffSkillEffectType.GlobalCookingSpeedPercent,
            staff.CurrentSkillSourceToken,
            _globalCookingSpeedUpPercent,
            "STAFF_SKILL09:" + staffId + ":" + nameof(GlobalCookingSpeedUpSkill));
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (staff == null)
        {
            return;
        }

        StaffSkillEffectRegistry effectRegistry = staff.SkillEffectRegistry;
        StaffSkillSourceToken sourceToken = staff.CurrentSkillSourceToken;
        if (effectRegistry == null || !sourceToken.IsValid)
        {
            return;
        }

        effectRegistry.Remove(
            StaffSkillEffectType.GlobalCookingSpeedPercent,
            sourceToken);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
