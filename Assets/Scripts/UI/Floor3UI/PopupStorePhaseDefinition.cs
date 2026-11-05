using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>Editable configuration data only. No balance defaults and no live scene bindings.</summary>
[Serializable]
public sealed class PopupStorePhaseDefinition
{
    public int PhaseNumber { get; set; }
    public PopupStoreGradeTier Grade { get; set; }
    public double TimeLimitSeconds { get; set; }
    public int TargetSuccessfulOrders { get; set; }
    public string[] AllowedPlushIds { get; set; }
    public double CustomerPatienceSeconds { get; set; }
    public double EntryIntervalSeconds { get; set; }
    public double StageClearNoticeSeconds { get; set; }
    public double CountdownStepSeconds { get; set; }

    internal PopupStorePhaseDefinition Copy()
        => new PopupStorePhaseDefinition
        {
            PhaseNumber = PhaseNumber,
            Grade = Grade,
            TimeLimitSeconds = TimeLimitSeconds,
            TargetSuccessfulOrders = TargetSuccessfulOrders,
            AllowedPlushIds = AllowedPlushIds == null ? null : (string[])AllowedPlushIds.Clone(),
            CustomerPatienceSeconds = CustomerPatienceSeconds,
            EntryIntervalSeconds = EntryIntervalSeconds,
            StageClearNoticeSeconds = StageClearNoticeSeconds,
            CountdownStepSeconds = CountdownStepSeconds
        };
}

/// <summary>Validated immutable phase input shared by future material lists and order suppliers.</summary>
public sealed class PopupStorePhaseSnapshot
{
    private readonly ReadOnlyCollection<string> _allowedPlushIds;
    private readonly HashSet<string> _allowed;
    public int PhaseNumber { get; }
    public PopupStoreGradeTier Grade { get; }
    public double TimeLimitSeconds { get; }
    public int TargetSuccessfulOrders { get; }
    public IReadOnlyList<string> AllowedPlushIds => _allowedPlushIds;
    public double CustomerPatienceSeconds { get; }
    public double EntryIntervalSeconds { get; }
    public double StageClearNoticeSeconds { get; }
    public double CountdownStepSeconds { get; }

    internal PopupStorePhaseSnapshot(PopupStorePhaseDefinition definition)
    {
        PhaseNumber = definition.PhaseNumber;
        Grade = definition.Grade;
        TimeLimitSeconds = definition.TimeLimitSeconds;
        TargetSuccessfulOrders = definition.TargetSuccessfulOrders;
        CustomerPatienceSeconds = definition.CustomerPatienceSeconds;
        EntryIntervalSeconds = definition.EntryIntervalSeconds;
        StageClearNoticeSeconds = definition.StageClearNoticeSeconds;
        CountdownStepSeconds = definition.CountdownStepSeconds;
        // Neither the mutable definition nor its array is retained by a running model.
        var ids = (string[])definition.AllowedPlushIds.Clone();
        _allowedPlushIds = Array.AsReadOnly(ids);
        _allowed = new HashSet<string>(ids, StringComparer.Ordinal);
    }

    public bool IsPlushAllowed(string id) => id != null && _allowed.Contains(id);
}
