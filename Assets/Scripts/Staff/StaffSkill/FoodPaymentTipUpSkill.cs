using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FoodPaymentTipUpSkill", menuName = "Scriptable Object/Skill/FoodPaymentTipUpSkill")]
public class FoodPaymentTipUpSkill : SkillBase
{
    [Range(0f, 1000f)] [SerializeField] private float _foodPaymentTipUpPercent = 50f;

    public override float FirstValue => _foodPaymentTipUpPercent;

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
            throw new InvalidOperationException("STAFF_SKILL06 requires the Staff cached effect registry.");
        }

        string staffId = staff.StaffData != null ? staff.StaffData.Id : nameof(FoodPaymentTipUpSkill);
        effectRegistry.RegisterOrUpdate(
            StaffSkillEffectType.RestaurantTipPayoutPercent,
            staff.CurrentSkillSourceToken,
            _foodPaymentTipUpPercent,
            "STAFF_SKILL06:" + staffId + ":" + nameof(FoodPaymentTipUpSkill));
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
            StaffSkillEffectType.RestaurantTipPayoutPercent,
            sourceToken);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
