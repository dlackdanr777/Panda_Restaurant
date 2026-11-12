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

    // Daily quota expansion: these tests are pure/local and never call the backend SDK.
    [TestCase(1)] [TestCase(3)] [TestCase(200)]
    public void Q200_ConfiguredLimitAllowsExactlyThatManyStarts(int limit)
    {
        var record = PopupStoreGameDataRecord.Empty;
        for (int i = 1; i <= limit; i++)
        {
            Assert.That(record.TryStart(PopupStoreGameDataTestData.Utc, "q-start-" + i,
                "q-session-" + i, "q-profile", 1, limit, out var started, out var error), Is.True, error);
            Assert.That(started.UsedCount, Is.EqualTo(i));
            Assert.That(started.Revision, Is.EqualTo(record.Revision + 1));
            Assert.That(started.TryAbandon(started.SessionId, "q-end-" + i, out var ended, out error), Is.True, error);
            Assert.That(PopupStoreGameDataRecord.TryRead(ended.ToJson(), true, out record, out error), Is.True, error);
        }
        string original = record.ToJson();
        Assert.That(record.TryStart(PopupStoreGameDataTestData.Utc, "q-over-limit", "q-extra-session",
            "q-profile", 1, limit, out var rejected, out _), Is.False);
        Assert.That(rejected, Is.Null);
        Assert.That(record.ToJson(), Is.EqualTo(original));
    }

    [Test]
    public void Q200_ExistingClaimedRewardCanStartAndClaimASecondRunWithoutChangingFirstDecision()
    {
        var first = PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0);
        Assert.That(first.TryClaim("q-first-claim", out var claimed, out var error), Is.True, error);
        string prior = claimed.ToJson();
        var phase = PopupStoreGameDataTestData.Phase("q-second-business");
        Assert.That(claimed.TryStart(PopupStoreGameDataTestData.Utc, "q-second-start", phase.BusinessSessionId,
            phase.Profile.ProfileId, phase.Profile.Version, 200, out var second, out error), Is.True, error);
        Assert.That(second.UsedCount, Is.EqualTo(2));
        Assert.That(second.DecisionId, Is.Empty);
        Assert.That(second.Diamonds, Is.Zero);
        PopupStoreGameDataTestData.Finish(phase, PopupStoreGradeTier.C);
        Assert.That(second.TryFinalize(phase, PopupStoreGameDataTestData.Policy(), 0, "q-second-final",
            out var final, out error), Is.True, error);
        Assert.That(final.TryClaim("q-second-claim", out var secondClaim, out error), Is.True, error);
        Assert.That(secondClaim.UsedCount, Is.EqualTo(2));
        Assert.That(secondClaim.Diamonds, Is.EqualTo(1));
        Assert.That(secondClaim.TryClaim("q-double-claim", out _, out _), Is.False);
        Assert.That(claimed.ToJson(), Is.EqualTo(prior));
        RoundTrip(secondClaim);
    }

    [TestCase(1)] [TestCase(3)]
    public void Q200_ReducingPolicyDoesNotCorruptPreviousUsage(int lowerLimit)
    {
        var record = Q200_CompleteRuns(5, PopupStoreGameDataTestData.Utc);
        Assert.That(PopupStoreGameDataRecord.TryRead(record.ToJson(), true, out var restored, out var error), Is.True, error);
        Assert.That(restored.UsedCount, Is.EqualTo(5));
        Assert.That(restored.TryStart(PopupStoreGameDataTestData.Utc, "q-reduced-start", "q-reduced-session",
            "q-profile", 1, lowerLimit, out _, out _), Is.False);
        Assert.That(restored.TryStart(PopupStoreGameDataTestData.Utc.AddDays(1), "q-tomorrow-start", "q-tomorrow-session",
            "q-profile", 1, lowerLimit, out var tomorrow, out error), Is.True, error);
        Assert.That(tomorrow.UsedCount, Is.EqualTo(1));
        Assert.That(tomorrow.Revision, Is.EqualTo(restored.Revision + 1));
        RoundTrip(tomorrow);
    }

    [Test]
    public void Q200_NoonResetsOnlyTheNextDayCounterAndKeepsHistoryImmutable()
    {
        var before = new DateTime(2026, 11, 9, 2, 59, 59, DateTimeKind.Utc);
        var record = Q200_CompleteRuns(3, before);
        string old = record.ToJson();
        Assert.That(record.DayKey, Is.EqualTo("2026-11-08"));
        Assert.That(record.TryStart(before.AddSeconds(1), "q-noon-start", "q-noon-session", "q-profile", 1,
            200, out var next, out var error), Is.True, error);
        Assert.That(next.DayKey, Is.EqualTo("2026-11-09"));
        Assert.That(next.UsedCount, Is.EqualTo(1));
        Assert.That(record.ToJson(), Is.EqualTo(old));
        RoundTrip(next);
    }

    [TestCase(false)] [TestCase(true)]
    public void Q200_ExpandedQuotaCannotSkipActiveOrUnclaimedRun(bool unclaimed)
    {
        var record = unclaimed ? PopupStoreGameDataTestData.Finalized(PopupStoreGradeTier.C, 0)
            : PopupStoreGameDataTestData.Started();
        foreach (var time in new[] { PopupStoreGameDataTestData.Utc, PopupStoreGameDataTestData.Utc.AddDays(1) })
            Assert.That(record.TryStart(time, "q-skip-start", "q-skip-session", "q-profile", 1,
                200, out _, out _), Is.False);
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(201)] [TestCase(int.MaxValue)]
    public void Q200_InvalidConfiguredLimitsFailBeforeStarting(int invalidLimit)
    {
        var record = PopupStoreGameDataRecord.Empty;
        Assert.That(record.TryStart(PopupStoreGameDataTestData.Utc, "q-invalid", "q-invalid-session",
            "q-profile", 1, invalidLimit, out var next, out _), Is.False);
        Assert.That(next, Is.Null);
        Assert.That(record.UsedCount, Is.Zero);
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(201)] [TestCase(int.MaxValue)]
    public void Q200_OutOfRangeStoredUsageIsStillRejected(int used)
    {
        var json = Q200_ReadFixtureJsonWithoutDateConversion(PopupStoreGameDataTestData.Started().ToJson());
        json["UsedCount"] = used;
        Assert.That(PopupStoreGameDataRecord.TryRead(json.ToString(Formatting.None), true, out _, out _), Is.False);
    }

    [Test]
    public void Q200_DefaultOverloadStillEnforcesOneUse()
    {
        var record = Q200_CompleteRuns(1, PopupStoreGameDataTestData.Utc);
        Assert.That(record.TryStart(PopupStoreGameDataTestData.Utc, "q-default", "q-default-session",
            "q-profile", 1, out _, out _), Is.False);
        Assert.That(record.TryStart(PopupStoreGameDataTestData.Utc, "q-expanded", "q-expanded-session",
            "q-profile", 1, 200, out var next, out _), Is.True);
        Assert.That(next.UsedCount, Is.EqualTo(2));
    }

    [TestCase(1)] [TestCase(3)]
    public void Q200_SameDaySuccessorCannotResetOrSkipCounter(int invalidNextCount)
    {
        var old = Q200_CompleteRuns(1, PopupStoreGameDataTestData.Utc);
        Assert.That(old.TryStart(PopupStoreGameDataTestData.Utc, "q-next", "q-next-session", "q-profile", 1,
            200, out var next, out var error), Is.True, error);
        var data = Q200_ReadFixtureJsonWithoutDateConversion(next.ToJson());
        data["UsedCount"] = invalidNextCount;
        Assert.That(PopupStoreGameDataRecord.TryRead(data.ToString(Formatting.None), true, out var changed, out error), Is.True, error);
        Assert.That(changed.UsedCount, Is.EqualTo(invalidNextCount));
        Assert.That(changed.StartedAtUtc, Is.EqualTo(next.StartedAtUtc));
        Assert.That(old.IsValidSuccessor(changed), Is.False);
        Assert.That(old.IsValidSuccessor(next), Is.True);
    }

    [Test]
    public void Q200_NextDaySuccessorMustStartAtOne()
    {
        var old = Q200_CompleteRuns(3, PopupStoreGameDataTestData.Utc);
        Assert.That(old.TryStart(PopupStoreGameDataTestData.Utc.AddDays(1), "q-next-day", "q-next-day-session",
            "q-profile", 1, 200, out var next, out _), Is.True);
        var data = Q200_ReadFixtureJsonWithoutDateConversion(next.ToJson());
        data["UsedCount"] = 4;
        Assert.That(PopupStoreGameDataRecord.TryRead(data.ToString(Formatting.None), true, out var changed, out var error), Is.True, error);
        Assert.That(changed.UsedCount, Is.EqualTo(4));
        Assert.That(changed.StartedAtUtc, Is.EqualTo(next.StartedAtUtc));
        Assert.That(old.IsValidSuccessor(changed), Is.False);
        Assert.That(old.IsValidSuccessor(next), Is.True);
    }

    [Test]
    public void Q200_BackwardDayAndDuplicateIdentitiesRemainBlocked()
    {
        var old = Q200_CompleteRuns(1, PopupStoreGameDataTestData.Utc);
        Assert.That(old.TryStart(PopupStoreGameDataTestData.Utc.AddDays(-1), "q-back", "q-back-session",
            "q-profile", 1, 200, out _, out _), Is.False);
        Assert.That(old.TryStart(PopupStoreGameDataTestData.Utc, old.StartRequestId, "q-duplicate-request",
            "q-profile", 1, 200, out _, out _), Is.False);
        Assert.That(old.TryStart(PopupStoreGameDataTestData.Utc, "q-duplicate-session", old.SessionId,
            "q-profile", 1, 200, out _, out _), Is.False);
    }

    [TestCase(3, 200, 0, false)]
    [TestCase(200, 200, 0, true)]
    [TestCase(200, 200, 199, true)]
    [TestCase(3, 3, 5, true)]
    public void Q200_AccessHostRequiresExplicitQuotaCap(int hostCap, int limit, int used, bool expectedAccepted)
    {
        var session = new PopupStoreRewardAccountSession("TEST_ONLY-q200-account");
        using (var host = new PopupStoreAdmissionHost(new Q200_NoWrites(), new Q200_AccessInfo(limit, used),
            () => session, maximumDailyLimit: hostCap))
        {
            // Pure adapter fixture: no MonoBehaviour, SDK authentication, Play, or backend.
            var field = typeof(PopupStoreAdmissionHost).GetField("_capturedSession",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(host, session);
            Assert.That(host.RefreshAccess(), Is.True);
            Assert.That(host.Access != null, Is.EqualTo(expectedAccepted));
            if (expectedAccepted)
                Assert.That(host.Access.Remaining, Is.EqualTo(Math.Max(0, limit - used)));
        }
    }

    [Test]
    public void Q200_InspectorDailyLimitIsSerializedAndDefaultsToOne()
    {
        var config = UnityEngine.ScriptableObject.CreateInstance<PopupStoreAccountPhaseConfiguration>();
        try
        {
            Assert.That(config.DailyBusinessLimit, Is.EqualTo(1));
            var so = new SerializedObject(config);
            var field = so.FindProperty("_dailyBusinessLimit");
            Assert.That(field, Is.Not.Null);
            field.intValue = 200; so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(config.DailyBusinessLimit, Is.EqualTo(200));
            field.intValue = 3; so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(config.DailyBusinessLimit, Is.EqualTo(3));
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    // Test-data editing must preserve the production record's exact UTC string.
    // JObject.Parse otherwise promotes the string to a date token and serializes
    // it with a different precision, failing before the counter rule is reached.
    private static JObject Q200_ReadFixtureJsonWithoutDateConversion(string json)
    {
        using (var text = new System.IO.StringReader(json))
        using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
        {
            var data = JObject.Load(reader);
            Assert.That(data["StartedAtUtc"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(PopupStoreGameDataRecord.TryRead(data.ToString(Formatting.None), true,
                out var unchanged, out var error), Is.True, error);
            Assert.That(unchanged.ToJson(), Is.EqualTo(json), "Fixture preparation must not change the source record.");
            return data;
        }
    }

    private static PopupStoreGameDataRecord Q200_CompleteRuns(int count, DateTime time)
    {
        var record = PopupStoreGameDataRecord.Empty;
        for (int i = 1; i <= count; i++)
        {
            Assert.That(record.TryStart(time, "q-helper-start-" + i, "q-helper-session-" + i,
                "q-profile", 1, 200, out var started, out var error), Is.True, error);
            Assert.That(started.TryAbandon(started.SessionId, "q-helper-end-" + i, out record, out error), Is.True, error);
        }
        return record;
    }

    private sealed class Q200_NoWrites : IPopupStoreBusinessStartPersistence
    {
        public void Submit(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
            Action<PopupStoreBusinessStartResponse> completed) => throw new InvalidOperationException("This fixture never submits.");
        public void Query(PopupStoreRewardAccountSession session, PopupStoreBusinessStartRequest request,
            Action<PopupStoreBusinessStartResponse> completed) => throw new InvalidOperationException("This fixture never reconciles.");
    }
    private sealed class Q200_AccessInfo : IPopupStoreBusinessAccessInformation
    {
        private readonly int _limit, _used;
        public Q200_AccessInfo(int limit, int used) { _limit = limit; _used = used; }
        public void QueryAccess(PopupStoreRewardAccountSession session, Action<PopupStoreBusinessAccessSnapshot> completed)
            => completed(new PopupStoreBusinessAccessSnapshot(session, "2026-11-09", "q200-local-test",
                _limit, _used, new DateTimeOffset(PopupStoreGameDataTestData.Utc)));
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
