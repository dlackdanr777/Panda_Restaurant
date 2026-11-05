using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>Six explicit phase definitions. Validation never changes the source catalog or corrects invalid settings.</summary>
[Serializable]
public sealed class PopupStorePhaseProfile
{
    public string ProfileId { get; set; }
    public int Version { get; set; }
    public PopupStorePhaseDefinition[] Phases { get; set; }

    public bool TryCreateSnapshot(PopupStoreMaterialCatalog catalog,
        out PopupStorePhaseProfileSnapshot snapshot, out string error)
    {
        snapshot = null;
        error = null;
        // Copy editable data before validation; only this private copy may become executable data.
        string id = ProfileId;
        int version = Version;
        var source = Phases;
        if (string.IsNullOrWhiteSpace(id)) return Reject("ProfileId must be nonblank.", out error);
        if (version <= 0) return Reject("Version must be positive.", out error);
        if (source == null || source.Length != 6)
            return Reject("Phases must contain exactly six definitions in C/B/A/S/SS/SSS order.", out error);
        var phases = new PopupStorePhaseDefinition[6];
        for (int i = 0; i < phases.Length; i++) phases[i] = source[i]?.Copy();
        if (catalog == null) return Reject("The complete 38-material catalog is required.", out error);
        if (!catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out string catalogError))
            return Reject("The complete catalog is invalid: " + catalogError, out error);
        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plush in plushes) registered.Add(plush.Id);
        HashSet<string> previous = null;
        double previousPatience = double.PositiveInfinity;
        for (int i = 0; i < phases.Length; i++)
        {
            var phase = phases[i];
            string prefix = "Phase " + (i + 1) + ": ";
            if (phase == null) return Reject(prefix + "definition is missing.", out error);
            if (phase.PhaseNumber != i + 1)
                return Reject(prefix + "PhaseNumber must equal its one-based position.", out error);
            if (phase.Grade != (PopupStoreGradeTier)(i + (int)PopupStoreGradeTier.C))
                return Reject(prefix + "Grade must follow C/B/A/S/SS/SSS order.", out error);
            if (!Positive(phase.TimeLimitSeconds)) return Reject(prefix + "TimeLimitSeconds must be finite and positive.", out error);
            if (phase.TargetSuccessfulOrders <= 0) return Reject(prefix + "TargetSuccessfulOrders must be positive.", out error);
            if (!Positive(phase.CustomerPatienceSeconds)) return Reject(prefix + "CustomerPatienceSeconds must be finite and positive.", out error);
            if (!Positive(phase.EntryIntervalSeconds)) return Reject(prefix + "EntryIntervalSeconds must be finite and positive.", out error);
            if (!Finite(phase.StageClearNoticeSeconds) || phase.StageClearNoticeSeconds < 0)
                return Reject(prefix + "StageClearNoticeSeconds must be finite and nonnegative.", out error);
            if (!Positive(phase.CountdownStepSeconds)) return Reject(prefix + "CountdownStepSeconds must be finite and positive.", out error);
            if (phase.CustomerPatienceSeconds > previousPatience)
                return Reject(prefix + "CustomerPatienceSeconds cannot increase from the previous phase.", out error);
            var ids = phase.AllowedPlushIds;
            if (ids == null || ids.Length == 0)
                return Reject(prefix + "AllowedPlushIds must be explicit and nonempty.", out error);
            if (i == 0 && ids.Length != 5)
                return Reject(prefix + "C must allow exactly five plush IDs.", out error);
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string plushId in ids)
            {
                if (plushId == null || !registered.Contains(plushId))
                    return Reject(prefix + "AllowedPlushIds contains an unregistered or non-Plush ID: " + (plushId ?? "<null>"), out error);
                if (!allowed.Add(plushId)) return Reject(prefix + "AllowedPlushIds contains a duplicate ID: " + plushId, out error);
            }
            if (previous != null && !previous.IsSubsetOf(allowed))
                return Reject(prefix + "AllowedPlushIds must include every previously unlocked ID.", out error);
            previous = allowed;
            previousPatience = phase.CustomerPatienceSeconds;
        }
        snapshot = new PopupStorePhaseProfileSnapshot(id, version, phases);
        return true;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Positive(double value) => Finite(value) && value > 0;
    private static bool Reject(string reason, out string error) { error = reason; return false; }
}

/// <summary>One deep-copied profile version. Holds no mutable definition, Unity object or account state.</summary>
public sealed class PopupStorePhaseProfileSnapshot
{
    private readonly ReadOnlyCollection<PopupStorePhaseSnapshot> _phases;
    public string ProfileId { get; }
    public int Version { get; }
    public IReadOnlyList<PopupStorePhaseSnapshot> Phases => _phases;

    internal PopupStorePhaseProfileSnapshot(string profileId, int version, PopupStorePhaseDefinition[] definitions)
    {
        ProfileId = profileId;
        Version = version;
        var phases = new PopupStorePhaseSnapshot[definitions.Length];
        for (int i = 0; i < phases.Length; i++) phases[i] = new PopupStorePhaseSnapshot(definitions[i]);
        _phases = Array.AsReadOnly(phases);
    }
}
