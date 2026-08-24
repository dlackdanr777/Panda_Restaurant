using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GlobalRemainingCookingTimeReductionSkill", menuName = "Scriptable Object/Skill/GlobalRemainingCookingTimeReductionSkill")]
public class GlobalRemainingCookingTimeReductionSkill : SkillBase
{
    [Range(0f, 100f)]
    [SerializeField]
    private float _remainingCookingTimeReductionPercent = 50f;

    public override float FirstValue => _remainingCookingTimeReductionPercent;

    public override float SecondValue => 0;

    public override void Activate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        if (staff == null)
        {
            throw new ArgumentNullException(nameof(staff));
        }

        if (kitchenSystem == null)
        {
            throw new ArgumentNullException(nameof(kitchenSystem));
        }

        if (!staff.CurrentSkillSourceToken.IsValid)
        {
            throw new InvalidOperationException("STAFF_SKILL09 requires a valid current Skill source token.");
        }

        CookingRemainingTimeReductionBatchResult result =
            kitchenSystem.ApplyGlobalRemainingCookingTimeReduction(
                _remainingCookingTimeReductionPercent);
        Debug.Log(
            "[STAFF_SKILL09_REMAINING_TIME] Target Burners: "
            + result.TargetBurnerCount
            + ", Changed Burners: "
            + result.ChangedBurnerCount);
    }

    public override void Deactivate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }

    public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
    }
}
