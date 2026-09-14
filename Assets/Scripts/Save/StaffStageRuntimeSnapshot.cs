using System;
using System.Collections.Generic;

/// <summary>
/// A detached, immutable observation of one StageInfo's staff dictionary, not a save payload or migration approval.
/// Opaque tokens detect ordinary changes/reapplication; value comparison also detects changes through existing aliases.
/// </summary>
public sealed class StaffStageRuntimeSnapshot
{
    internal object OwnerToken { get; }
    internal object ChangeToken { get; }
    public bool IsApplying { get; }
    public IReadOnlyList<StaffStageRuntimeRecord> Staff { get; }

    internal StaffStageRuntimeSnapshot(object ownerToken, object changeToken, bool isApplying,
        IReadOnlyList<StaffStageRuntimeRecord> staff)
    {
        OwnerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
        ChangeToken = changeToken ?? throw new ArgumentNullException(nameof(changeToken));
        IsApplying = isApplying;
        if (staff == null) throw new ArgumentNullException(nameof(staff));
        var records = new StaffStageRuntimeRecord[staff.Count];
        for (int index = 0; index < records.Length; index++)
        {
            StaffStageRuntimeRecord record = staff[index];
            records[index] = record == null ? null : new StaffStageRuntimeRecord(
                record.DictionaryId, record.Id, record.Level, record.SkinId);
        }
        Staff = Array.AsReadOnly(records);
    }

    /// <summary>Exact identity/change/value comparison; dictionary enumeration order has no meaning.</summary>
    public bool HasSameState(StaffStageRuntimeSnapshot other)
    {
        if (other == null || !ReferenceEquals(OwnerToken, other.OwnerToken)
            || !ReferenceEquals(ChangeToken, other.ChangeToken) || IsApplying != other.IsApplying
            || Staff.Count != other.Staff.Count)
            return false;
        var matched = new bool[other.Staff.Count];
        foreach (StaffStageRuntimeRecord record in Staff)
        {
            if (record == null) return false;
            StaffStageRuntimeRecord matching = null;
            int matchingIndex = -1;
            for (int index = 0; index < other.Staff.Count; index++)
            {
                StaffStageRuntimeRecord candidate = other.Staff[index];
                if (candidate == null || !string.Equals(record.DictionaryId, candidate.DictionaryId, StringComparison.Ordinal))
                    continue;
                if (matching != null) return false; // Never merge or choose duplicate dictionary keys.
                matching = candidate;
                matchingIndex = index;
            }
            if (matching == null || matched[matchingIndex] || !string.Equals(record.Id, matching.Id, StringComparison.Ordinal)
                || record.Level != matching.Level || !string.Equals(record.SkinId, matching.SkinId, StringComparison.Ordinal))
                return false;
            matched[matchingIndex] = true;
        }
        return true;
    }
}

/// <summary>Raw copied values; a null saved record has null Id/Level/SkinId while its dictionary key remains visible.</summary>
public sealed class StaffStageRuntimeRecord
{
    public string DictionaryId { get; }
    public string Id { get; }
    public int? Level { get; }
    public string SkinId { get; }

    internal StaffStageRuntimeRecord(string dictionaryId, string id, int? level, string skinId)
    {
        DictionaryId = dictionaryId;
        Id = id;
        Level = level;
        SkinId = skinId;
    }
}
