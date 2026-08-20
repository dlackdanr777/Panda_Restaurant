using System;
using UnityEngine;

[CreateAssetMenu(fileName = "AllStaffMoveSpeedUpSkill", menuName = "Scriptable Object/Skill/AllStaffMoveSpeedUpSkill")]
public class AllStaffMoveSpeedUpSkill : SkillBase
{
    [Range(0f, 1000f)]
    [SerializeField]
    private float _allStaffMoveSpeedUpPercent = 50f;

    public override float FirstValue => _allStaffMoveSpeedUpPercent;

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
            throw new InvalidOperationException("STAFF_SKILL10 requires the Staff cached effect registry.");
        }

        string staffId = staff.StaffData != null ? staff.StaffData.Id : nameof(AllStaffMoveSpeedUpSkill);
        effectRegistry.RegisterOrUpdate(
            StaffSkillEffectType.AllStaffMovePercent,
            staff.CurrentSkillSourceToken,
            _allStaffMoveSpeedUpPercent,
            "STAFF_SKILL10:" + staffId + ":" + nameof(AllStaffMoveSpeedUpSkill));
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
            StaffSkillEffectType.AllStaffMovePercent,
            sourceToken);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
