using System;
using System.Collections.Generic;

public sealed class StaffSkillEffectRegistry
{
    private readonly Dictionary<StaffSkillEffectType, Dictionary<StaffSkillSourceToken, Entry>> _entries;

    public int TotalSourceCount
    {
        get
        {
            int count = 0;
            foreach (Dictionary<StaffSkillSourceToken, Entry> effectEntries in _entries.Values)
            {
                count += effectEntries.Count;
            }

            return count;
        }
    }

    public StaffSkillEffectRegistry()
    {
        _entries = new Dictionary<StaffSkillEffectType, Dictionary<StaffSkillSourceToken, Entry>>();
        foreach (StaffSkillEffectType effectType in Enum.GetValues(typeof(StaffSkillEffectType)))
        {
            _entries.Add(effectType, new Dictionary<StaffSkillSourceToken, Entry>());
        }
    }

    public void RegisterOrUpdate(
        StaffSkillEffectType effectType,
        StaffSkillSourceToken sourceToken,
        float bonusPercent,
        string debugLabel)
    {
        ValidateEffectType(effectType);
        ValidateToken(sourceToken);
        ValidatePercent(bonusPercent);

        Dictionary<StaffSkillSourceToken, Entry> effectEntries = _entries[effectType];
        if (bonusPercent == 0f)
        {
            effectEntries.Remove(sourceToken);
            return;
        }

        effectEntries[sourceToken] = new Entry(
            sourceToken,
            debugLabel ?? string.Empty,
            bonusPercent);
    }

    public bool Remove(StaffSkillEffectType effectType, StaffSkillSourceToken sourceToken)
    {
        ValidateEffectType(effectType);
        ValidateToken(sourceToken);
        return _entries[effectType].Remove(sourceToken);
    }

    public int RemoveAllForSource(StaffSkillSourceToken sourceToken)
    {
        ValidateToken(sourceToken);

        int removedCount = 0;
        foreach (Dictionary<StaffSkillSourceToken, Entry> effectEntries in _entries.Values)
        {
            if (effectEntries.Remove(sourceToken))
            {
                removedCount++;
            }
        }

        return removedCount;
    }

    public bool ContainsSource(
        StaffSkillEffectType effectType,
        StaffSkillSourceToken sourceToken)
    {
        ValidateEffectType(effectType);
        ValidateToken(sourceToken);
        return _entries[effectType].ContainsKey(sourceToken);
    }

    public float GetHighestPercent(StaffSkillEffectType effectType)
    {
        ValidateEffectType(effectType);

        float highestPercent = 0f;
        foreach (Entry entry in _entries[effectType].Values)
        {
            if (entry.BonusPercent > highestPercent)
            {
                highestPercent = entry.BonusPercent;
            }
        }

        return highestPercent;
    }

    public float GetMultiplier(StaffSkillEffectType effectType)
    {
        return 1f + GetHighestPercent(effectType) * 0.01f;
    }

    public int GetSourceCount(StaffSkillEffectType effectType)
    {
        ValidateEffectType(effectType);
        return _entries[effectType].Count;
    }

    public void ClearAll()
    {
        foreach (Dictionary<StaffSkillSourceToken, Entry> effectEntries in _entries.Values)
        {
            effectEntries.Clear();
        }
    }

    private static void ValidateEffectType(StaffSkillEffectType effectType)
    {
        if (!Enum.IsDefined(typeof(StaffSkillEffectType), effectType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectType),
                effectType,
                "Undefined staff skill effect type.");
        }
    }

    private static void ValidateToken(StaffSkillSourceToken sourceToken)
    {
        if (!sourceToken.IsValid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceToken),
                sourceToken,
                "Source token must be valid.");
        }
    }

    private static void ValidatePercent(float bonusPercent)
    {
        if (float.IsNaN(bonusPercent)
            || float.IsInfinity(bonusPercent)
            || bonusPercent < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bonusPercent),
                bonusPercent,
                "Bonus percent must be finite and non-negative.");
        }
    }

    private sealed class Entry
    {
        internal StaffSkillSourceToken SourceToken { get; }
        internal string DebugLabel { get; }
        internal float BonusPercent { get; }

        internal Entry(
            StaffSkillSourceToken sourceToken,
            string debugLabel,
            float bonusPercent)
        {
            SourceToken = sourceToken;
            DebugLabel = debugLabel;
            BonusPercent = bonusPercent;
        }
    }
}
