#if UNITY_EDITOR
using System;
using System.Linq;
using Muks.BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class PopupStoreGameDataRecordTests
{
    [Test]
    public void OldAccountMissingFieldIsEmptyWithoutInventingUsageOrReward()
    {
        Assert.That(PopupStoreGameDataRecord.TryRead(null, false, out var record, out var error), Is.True, error);
        Assert.That(record.State, Is.EqualTo(PopupStoreGameDataState.Empty));
        Assert.That(record.UsedCount, Is.Zero); Assert.That(record.Diamonds, Is.Zero);
        RoundTrip(record);
    }
    [TestCase(null)] [TestCase("")] [TestCase("null")] [TestCase("[]")] [TestCase("{}")]
    public void PresentCorruptFieldNeverBecomesUnusedAccount(string json)
    {
        Assert.That(PopupStoreGameDataRecord.TryRead(json, true, out var record, out var error), Is.False);
        Assert.That(record, Is.Null); Assert.That(error, Is.Not.Empty);
    }
    [TestCase("duplicate")] [TestCase("extra")] [TestCase("version")] [TestCase("stringNumber")]
    [TestCase("fraction")] [TestCase("overflow")] [TestCase("trailing")]
    public void StrictJsonRejectsAmbiguousOrUnsupportedSavedState(string kind)
    {
        string json = PopupStoreGameDataTestData.Started().ToJson(); var data = JObject.Parse(json);
        if (kind == "duplicate") json = json.Insert(1, "\"Version\":1,");
        else if (kind == "trailing") json += " {}";
        else
        {
            if (kind == "extra") data["FutureWallet"] = 999;
            if (kind == "version") data["Version"] = 2;
            if (kind == "stringNumber") data["UsedCount"] = "1";
            if (kind == "fraction") data["UsedCount"] = 1.1;
            if (kind == "overflow") data["ProfileVersion"] = long.MaxValue;
            json = data.ToString(Formatting.None);
        }
        Assert.That(PopupStoreGameDataRecord.TryRead(json, true, out _, out _), Is.False);
    }
    [TestCase(DateTimeKind.Local)] [TestCase(DateTimeKind.Unspecified)]
    public void DeviceOrUnspecifiedTimeCannotMintDailyTicket(DateTimeKind kind)
    {
        Assert.That(PopupStoreGameDataRecord.Empty.TryStart(DateTime.SpecifyKind(PopupStoreGameDataTestData.Utc, kind),
            "start", "business", "profile", 1, out _, out _), Is.False);
    }
    [Test]
    public void NoonBoundaryUsesKstAndAbandonNeverRefundsTheSameDay()
    {
        var before = new DateTime(2026, 11, 9, 2, 59, 59, DateTimeKind.Utc);
        Assert.That(PopupStoreGameDataRecord.Empty.TryStart(before, "start", "business", "profile", 1, out var used, out _), Is.True);
        Assert.That(used.DayKey, Is.EqualTo("2026-11-08"));
        Assert.That(used.TryAbandon("business", "abandon", out var abandoned, out _), Is.True);
        Assert.That(abandoned.TryStart(before, "start2", "business2", "profile", 1, out _, out _), Is.False);
        Assert.That(abandoned.TryStart(before.AddSeconds(1), "start2", "business2", "profile", 1, out var next, out _), Is.True);
        Assert.That(next.DayKey, Is.EqualTo("2026-11-09")); Assert.That(next.UsedCount, Is.EqualTo(1));
        RoundTrip(next);
    }
    [Test]
    public void ActualServerStartInstantSurvivesAbandonAndReload()
    {
        DateTime instant = PopupStoreGameDataTestData.Utc.AddMinutes(137).AddTicks(1234);
        Assert.That(PopupStoreGameDataRecord.Empty.TryStart(instant, "start", "business", "profile", 1, out var started, out _), Is.True);
        Assert.That(started.StartedAtUtc, Is.EqualTo(instant)); Assert.That(started.StartedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(started.TryAbandon("business", "exit", out var abandoned, out _), Is.True);
        Assert.That(abandoned.StartedAtUtc, Is.EqualTo(instant)); RoundTrip(abandoned);
    }
    [TestCase("2026-11-09T03:00:00.0000000")]
    [TestCase("2026-11-09T03:00:00.0000000+09:00")]
    [TestCase("2026-11-08T03:00:00.0000000Z")]
    [TestCase("")]
    public void MissingNonUtcOrDifferentDayStartProofIsRejected(string instant)
    {
        var data = JObject.Parse(PopupStoreGameDataTestData.Started().ToJson()); data["StartedAtUtc"] = instant;
        Assert.That(PopupStoreGameDataRecord.TryRead(data.ToString(Formatting.None), true, out _, out _), Is.False);
    }
    [Test]
    public void ActiveOrUnclaimedBusinessCannotDisappearAtTheNextNoon()
    {
        var started = PopupStoreGameDataTestData.Started();
        Assert.That(started.TryStart(PopupStoreGameDataTestData.Utc.AddDays(1), "next", "next-business", "profile", 1, out _, out _), Is.False);
        var finalized = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0);
        Assert.That(finalized.TryStart(PopupStoreGameDataTestData.Utc.AddDays(1), "next", "next-business", "profile", 1, out _, out _), Is.False);
        Assert.That(finalized.TryClaim("claim", out var claimed, out _), Is.True);
        Assert.That(claimed.TryStart(PopupStoreGameDataTestData.Utc.AddDays(1), "next", "next-business", "profile", 1, out var next, out _), Is.True);
        Assert.That(next.Diamonds, Is.Zero); Assert.That(next.FinalScore, Is.Zero); Assert.That(next.DecisionId, Is.Empty);
    }
    [TestCase(PopupStoreGradeTier.C, 0, 1)] [TestCase(PopupStoreGradeTier.B, 900, 8)]
    [TestCase(PopupStoreGradeTier.A, 997, 30)] [TestCase(PopupStoreGradeTier.S, 0, 10)]
    [TestCase(PopupStoreGradeTier.SS, 996, 30)] [TestCase(PopupStoreGradeTier.SSS, 999, 300)]
    public void ApprovedR1FinalizationAndClaimFreezeEveryGrade(PopupStoreGradeTier grade, int roll, int amount)
    {
        var record = PopupStoreGameDataTestData.Finalized(grade, roll); string before = record.ToJson();
        Assert.That(record.HighestGrade, Is.EqualTo(grade)); Assert.That(record.Diamonds, Is.EqualTo(amount));
        Assert.That(record.TryClaim("claim", out var claimed, out var error), Is.True, error);
        Assert.That(claimed.Diamonds, Is.EqualTo(amount)); Assert.That(claimed.DecisionId, Is.EqualTo(record.DecisionId));
        Assert.That(claimed.RewardRoll, Is.EqualTo(roll)); Assert.That(claimed.TryClaim("claim-again", out _, out _), Is.False);
        Assert.That(record.ToJson(), Is.EqualTo(before)); RoundTrip(record); RoundTrip(claimed);
    }
    [TestCase("amount")] [TestCase("kind")] [TestCase("grade")] [TestCase("roll")]
    public void CorruptR1DecisionCannotBeReadThenClaimed(string field)
    {
        var data = JObject.Parse(PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0).ToJson());
        if (field == "amount") data["Diamonds"] = 300;
        if (field == "kind") data["RewardKind"] = 2;
        if (field == "grade") data["HighestGrade"] = (int)PopupStoreGradeTier.SSS;
        if (field == "roll") data["RewardRoll"] = 997;
        Assert.That(PopupStoreGameDataRecord.TryRead(data.ToString(Formatting.None), true, out _, out _), Is.False);
    }
    [Test]
    public void StartedForeignAndNonTerminalCoreCannotFinalize()
    {
        var phase = PopupStoreGameDataTestData.Phase(); var record = PopupStoreGameDataTestData.Started(phase);
        Assert.That(record.TryFinalize(phase, PopupStoreGameDataTestData.Policy(), 0, "final", out _, out _), Is.False);
        var foreign = PopupStoreGameDataTestData.Phase("foreign"); PopupStoreGameDataTestData.Finish(foreign, PopupStoreGradeTier.C);
        Assert.That(record.TryFinalize(foreign, PopupStoreGameDataTestData.Policy(), 0, "final", out _, out _), Is.False);
    }
    [TestCase(false)] [TestCase(true)]
    public void FirstPhaseFailureAndAbandonHaveNoDecisionAndCannotClaim(bool abandon)
    {
        var phase = PopupStoreGameDataTestData.Phase(); var record = PopupStoreGameDataTestData.Started(phase);
        phase.TryStart();
        if (abandon) phase.TryAbandon(); else phase.TryAdvanceTime(phase.RemainingSeconds);
        PopupStoreGameDataTestData.Cleanup(phase);
        Assert.That(record.TryFinalize(phase, null, -1, "final", out var final, out var error), Is.True, error);
        Assert.That(final.Diamonds, Is.Zero); Assert.That(final.DecisionId, Is.Empty);
        Assert.That(final.TryClaim("claim", out _, out _), Is.False); RoundTrip(final);
    }
    [Test]
    public void FinalizedResultCannotBeOverwrittenOrAbandoned()
    {
        var phase = PopupStoreGameDataTestData.Phase(); var record = PopupStoreGameDataTestData.Started(phase);
        PopupStoreGameDataTestData.Finish(phase, PopupStoreGradeTier.C);
        Assert.That(record.TryFinalize(phase, PopupStoreGameDataTestData.Policy(), 0, "final", out var final, out _), Is.True);
        Assert.That(final.TryFinalize(phase, PopupStoreGameDataTestData.Policy(), 999, "second-final", out _, out _), Is.False);
        Assert.That(final.TryAbandon(phase.BusinessSessionId, "abandon", out _, out _), Is.False);
    }
    [Test]
    public void AllRecordPropertiesAreReadOnlyAndRoundTripIsDetached()
    {
        foreach (var property in typeof(PopupStoreGameDataRecord).GetProperties().Where(p => p.Name != "Empty"))
            Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
        var source = PopupStoreGameDataTestData.Started();
        Assert.That(PopupStoreGameDataRecord.TryRead(source.ToJson(), true, out var restored, out _), Is.True);
        Assert.That(restored, Is.Not.SameAs(source)); Assert.That(restored.Matches(source), Is.True);
    }
    private static void RoundTrip(PopupStoreGameDataRecord source)
    {
        Assert.That(PopupStoreGameDataRecord.TryRead(source.ToJson(), true, out var restored, out var error), Is.True, error);
        Assert.That(restored.Matches(source), Is.True);
    }
}

internal static class PopupStoreGameDataTestData
{
    internal static readonly DateTime Utc = new DateTime(2026, 11, 9, 3, 0, 0, DateTimeKind.Utc);
    internal static PopupStoreRewardPolicySnapshot Policy()
    {
        var table = AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>("Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R1.asset");
        Assert.That(table.TryCreateSnapshot(out var policy, out var error), Is.True, error); return policy;
    }
    internal static PopupStorePhaseSession Phase(string id = "TEST_ONLY-GameData-business")
    {
        var definition = PopupStorePhaseTestProfiles.CreateDefinitionProfile();
        definition.GoalMetric = PopupStorePhaseGoalMetric.CumulativeScore;
        int[] targets = { 200, 650, 1200, 1900, 2800, 3900 };
        for (int i = 0; i < 6; i++) { definition.Phases[i].TargetScore = targets[i]; definition.Phases[i].TargetSuccessfulOrders = 0; }
        Assert.That(definition.TryCreateSnapshot(AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(PopupStorePhaseTestProfiles.CatalogPath),
            out var snapshot, out var error), Is.True, error);
        Assert.That(PopupStorePhaseSession.TryCreate(id, snapshot, out var phase, out error), Is.True, error); return phase;
    }
    internal static PopupStoreGameDataRecord Started(PopupStorePhaseSession phase = null)
    {
        phase = phase ?? Phase();
        Assert.That(PopupStoreGameDataRecord.Empty.TryStart(Utc, "TEST_ONLY-start", phase.BusinessSessionId,
            phase.Profile.ProfileId, phase.Profile.Version, out var started, out var error), Is.True, error); return started;
    }
    internal static PopupStoreGameDataRecord Finalized(PopupStoreGradeTier grade, int roll)
    {
        var phase = Phase(); var source = Started(phase); Finish(phase, grade);
        Assert.That(source.TryFinalize(phase, Policy(), roll, "TEST_ONLY-final", out var record, out var error), Is.True, error); return record;
    }
    internal static void Finish(PopupStorePhaseSession phase, PopupStoreGradeTier highest)
    {
        Assert.That(phase.TryStart(), Is.True); int receipt = 0;
        while (phase.CurrentPhase.Grade <= highest)
        {
            while (phase.TotalScore < phase.CurrentPhase.TargetScore)
            {
                string id = "test-" + (++receipt);
                Assert.That(phase.TryRecordPacking(new PopupStorePhasePackingEvidence(phase.BusinessSessionId,
                    phase.PhaseRunId, "order-" + id, "receipt-" + id, 100)), Is.True);
            }
            phase.TryAdvanceTime(phase.RemainingSeconds); Cleanup(phase);
            if (phase.CurrentPhase.Grade == PopupStoreGradeTier.SSS) { phase.TryAdvanceTime(double.MaxValue); return; }
            phase.TryAdvanceTime(double.MaxValue);
            for (int i = 0; i < 3; i++) phase.TryAdvanceTime(double.MaxValue);
        }
        phase.TryAdvanceTime(phase.RemainingSeconds); Cleanup(phase);
        Assert.That(phase.IsTerminal, Is.True);
    }
    internal static void Cleanup(PopupStorePhaseSession phase) => Assert.That(phase.TryCompleteCleanup(
        new PopupStorePhaseCleanupSignal(phase.BusinessSessionId, phase.PhaseRunId, PopupStorePhaseCleanupStatus.Completed)), Is.True);
}
#endif
