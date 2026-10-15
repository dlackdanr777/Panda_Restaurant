using UnityEngine;

[CreateAssetMenu(menuName = "Panda Restaurant/Customer party settings")]
public sealed class CustomerPartySettings : ScriptableObject
{
    [Tooltip("Applies only to NEW natural general-customer visits. Saved and existing parties are unchanged.")]
    public bool EnableNaturalParties = false;
    [Min(0)] [Tooltip("Completed normal visits already recorded for the representative before this spawn.")]
    public int MinimumNormalVisits = 25;
    [Range(0f, 1f)] [Tooltip("Probability from 0 to 1. 0.20 = 20%. One draw when eligible and a valid companion exists. Maximum party size is fixed at two.")]
    public float CompanionProbability = .20f;

    internal bool IsValid => MinimumNormalVisits >= 0 && !float.IsNaN(CompanionProbability)
        && !float.IsInfinity(CompanionProbability) && CompanionProbability >= 0 && CompanionProbability <= 1;
}
