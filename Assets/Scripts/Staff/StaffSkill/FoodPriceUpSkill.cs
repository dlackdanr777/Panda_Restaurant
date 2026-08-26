using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FoodPriceUpSkill", menuName = "Scriptable Object/Skill/FoodPriceUpSkill")]
public class FoodPriceUpSkill : SkillBase
{
    [Range(0f, 1000f)] [SerializeField] private float _foodPriceUpPercent = 50f;

    public override float FirstValue => _foodPriceUpPercent;

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
            throw new InvalidOperationException("STAFF_SKILL05 requires the Staff cached effect registry.");
        }

        string staffId = staff.StaffData != null ? staff.StaffData.Id : nameof(FoodPriceUpSkill);
        effectRegistry.RegisterOrUpdate(
            StaffSkillEffectType.FoodPricePercent,
            staff.CurrentSkillSourceToken,
            _foodPriceUpPercent,
            "STAFF_SKILL05:" + staffId + ":" + nameof(FoodPriceUpSkill));
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
            StaffSkillEffectType.FoodPricePercent,
            sourceToken);
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
