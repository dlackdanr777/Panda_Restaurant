using System;
using System.Collections.Generic;
using UnityEngine;

public partial class CustomerController
{
    [Header("New natural customer parties")]
    [SerializeField] private CustomerPartySettings _naturalPartySettings;
    // Optional deterministic inputs for isolated verification. Production uses Unity's
    // existing random stream, without reseeding, rewinding or retrying a result.
    [NonSerialized] internal Func<float> NaturalPartyRoll;
    [NonSerialized] internal Func<int, int> NaturalCompanionIndex;
    private bool _naturalSettingsWarning;
    public string LastNaturalPartyDiagnostic { get; private set; }

    internal string[] SelectNaturalPartyMembers(NormalCustomerData representative, IReadOnlyList<NormalCustomerData> appeared)
    {
        var single = new[] { representative.Id };
        if (_naturalPartySettings == null || !_naturalPartySettings.IsValid)
        {
            LastNaturalPartyDiagnostic = "NaturalPartySettingsMissingOrInvalid";
            if (!_naturalSettingsWarning)
            {
                _naturalSettingsWarning = true;
                Debug.LogWarning("[CustomerController] Natural party settings are missing or invalid; new natural visits remain single. Existing and saved parties are unaffected.", this);
            }
            return single;
        }
        _naturalSettingsWarning = false;
        if (!_naturalPartySettings.EnableNaturalParties || UserInfo.IsTutorialStart)
        { LastNaturalPartyDiagnostic = "NaturalPartiesDisabledOrTutorial"; return single; }
        if (!UserInfo.GetCustomerEnableState(representative)
            || UserInfo.GetVisitedCustomerCount(representative.Id) < _naturalPartySettings.MinimumNormalVisits)
        { LastNaturalPartyDiagnostic = "RepresentativeBelowVisitThreshold"; return single; }

        var candidates = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        if (appeared != null)
            foreach (var candidate in appeared)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id)
                    || !CustomerDataManager.Instance.TryGetCustomerData(candidate.Id, out var canonical)
                    || !(canonical is NormalCustomerData normal) || !ReferenceEquals(candidate, normal)
                    || !UserInfo.GetCustomerEnableState(normal)
                    || UserInfo.GetVisitedCustomerCount(normal) < _naturalPartySettings.MinimumNormalVisits
                    || !Enum.IsDefined(typeof(CustomerTendencyType), normal.TendencyType)
                    || !_satisfactionSystem.CheckCustomerTendency(normal.TendencyType) || !unique.Add(normal.Id)) continue;
                candidates.Add(normal.Id);
            }
        if (candidates.Count == 0)
        { LastNaturalPartyDiagnostic = "NoEligibleCompanion"; return single; }

        float roll = NaturalPartyRoll != null ? NaturalPartyRoll() : UnityEngine.Random.value;
        float probability = _naturalPartySettings.CompanionProbability;
        if (!(roll < probability || probability == 1f))
        { LastNaturalPartyDiagnostic = "NaturalPartyRollMiss"; return single; }
        int index = NaturalCompanionIndex != null ? NaturalCompanionIndex(candidates.Count) : UnityEngine.Random.Range(0, candidates.Count);
        LastNaturalPartyDiagnostic = "NaturalCompanionSelected";
        return new[] { representative.Id, candidates[index] };
    }
}
