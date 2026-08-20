using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalCustomerMoveSpeedUpSkill", menuName = "Scriptable Object/Skill/NormalCustomerMoveSpeedUpSkill")]
public class NormalCustomerMoveSpeedUpSkill : SkillBase
{
    [Range(0f, 1000f)]
    [SerializeField]
    private float _normalCustomerMoveSpeedUpPercent = 100f;

    public override float FirstValue => _normalCustomerMoveSpeedUpPercent;

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
            throw new InvalidOperationException("STAFF_SKILL08 requires the Staff cached effect registry.");
        }

        string staffId = staff.StaffData != null ? staff.StaffData.Id : nameof(NormalCustomerMoveSpeedUpSkill);
        effectRegistry.RegisterOrUpdate(
            StaffSkillEffectType.NormalCustomerMovePercent,
            staff.CurrentSkillSourceToken,
            _normalCustomerMoveSpeedUpPercent,
            "STAFF_SKILL08:" + staffId + ":" + nameof(NormalCustomerMoveSpeedUpSkill));
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
            StaffSkillEffectType.NormalCustomerMovePercent,
            sourceToken);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
