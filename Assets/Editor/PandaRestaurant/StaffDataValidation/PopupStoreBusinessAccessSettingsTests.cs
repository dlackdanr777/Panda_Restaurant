#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Muks.BackEnd;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Authored review access configuration and the existing C2 authority only. No Play, live account or SDK operations.</summary>
public sealed class PopupStoreBusinessAccessSettingsTests
{
    private const string AssetPath = "Assets/Resources/PopupStore/PopupStoreBusinessAccessSettings.asset";
    private const string Day = "2026-11-03";
    private string _account, _settings;
    private BackendManager _backend;
    private bool _sdk;
    private UnityEngine.Random.State _random;

    [SetUp]
    public void Before()
    {
        _account = Account(); _backend = BackendManager.ExistingInstance; _sdk = BackEnd.Backend.IsInitialized;
        _random = UnityEngine.Random.state; _settings = File.ReadAllText(AssetPath);
    }
    [TearDown]
    public void After()
    {
        Assert.That(Account(), Is.EqualTo(_account)); Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk)); Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
        Assert.That(File.ReadAllText(AssetPath), Is.EqualTo(_settings));
    }

    [Test]
    public void AuthoredOneUseAssetAndLegacyTwoUseModelRemainDistinct()
    {
        var settings = AssetDatabase.LoadAssetAtPath<PopupStoreBusinessAccessSettings>(AssetPath);
        Assert.That(settings, Is.Not.Null); Assert.That(settings.TryCreateSchedule(out var schedule, out string error), Is.True, error);
        Assert.That(settings.Current.DailyFreeLimit, Is.EqualTo(1)); Assert.That(settings.Current.EffectiveGameDay, Is.EqualTo(Day));
        Assert.That(settings.ResetBasis, Is.EqualTo(PopupStoreBusinessDayReset.KstNoon)); Assert.That(settings.HasFuturePolicy, Is.False);
        Assert.That(schedule.TryResolve(Day, out var policy, out error), Is.True, error);
        Assert.That(policy.RewardedDailyLimit, Is.EqualTo(1)); Assert.That(new PopupStoreBusinessAccessPolicy().RewardedDailyLimit, Is.EqualTo(2));
        Assert.That(Resources.Load<PopupStoreBusinessAccessSettings>("PopupStore/PopupStoreBusinessAccessSettings"), Is.SameAs(settings));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ConfiguredLimitAndInformationUseExactlyTheSameApprovedRecords(int limit)
    {
        var h = new Harness(limit);
        for (int i = 0; i < limit; i++)
        {
            var before = h.Info(); Assert.That(before.Remaining, Is.EqualTo(limit - i));
            Assert.That(h.Start("request-" + i).State, Is.EqualTo(PopupStoreBusinessStartState.Started));
            Assert.That(h.Info().Used, Is.EqualTo(i + 1));
        }
        Assert.That(h.Start("over-limit").State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        Assert.That(h.Info().Remaining, Is.Zero); Assert.That(h.Store.IssuedBusinessCount, Is.EqualTo(limit));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(int.MaxValue)]
    public void OutOfRangeAuthoredLimitIsAnErrorAndNeverAQuietFallback(int limit)
    {
        Assert.That(PopupStoreBusinessAccessSchedule.TryCreate(new PopupStoreBusinessAccessSetting(limit, "test-v1", Day), null,
            out var schedule, out string error), Is.False); Assert.That(schedule, Is.Null); Assert.That(error, Is.Not.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("version 1")]
    [TestCase("검수버전")]
    [TestCase("12345678901234567890123456789012345678901234567890123456789012345")]
    public void InvalidVersionRejectsTheAuthoredSchedule(string version)
    {
        Assert.That(PopupStoreBusinessAccessSchedule.TryCreate(new PopupStoreBusinessAccessSetting(1, version, Day), null,
            out var schedule, out _), Is.False); Assert.That(schedule, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("2026-2-03")]
    [TestCase("2026-02-30")]
    [TestCase("today")]
    public void InvalidEffectiveDateRejectsTheAuthoredSchedule(string day)
    {
        Assert.That(PopupStoreBusinessAccessSchedule.TryCreate(new PopupStoreBusinessAccessSetting(1, "v1", day), null,
            out var schedule, out _), Is.False); Assert.That(schedule, Is.Null);
    }

    [TestCase("2026-11-02")]
    [TestCase("2026-11-03")]
    public void FutureSettingMustFollowCurrentEffectiveDate(string future)
    {
        Assert.That(PopupStoreBusinessAccessSchedule.TryCreate(new PopupStoreBusinessAccessSetting(1, "v1", Day),
            new PopupStoreBusinessAccessSetting(2, "v2", future), out _, out _), Is.False);
    }

    [Test]
    public void DistinctEffectiveDatesCannotReuseOnePolicyVersion()
    {
        Assert.That(PopupStoreBusinessAccessSchedule.TryCreate(new PopupStoreBusinessAccessSetting(1, "same", Day),
            new PopupStoreBusinessAccessSetting(3, "same", "2026-11-04"), out _, out _), Is.False);
    }

    [Test]
    public void InvalidResetEnumIsNotSilentlyConvertedToKstNoon()
    {
        var settings = ScriptableObject.CreateInstance<PopupStoreBusinessAccessSettings>();
        try
        {
            Set(settings, "_resetBasis", (PopupStoreBusinessDayReset)99);
            Assert.That(settings.TryCreateSchedule(out var schedule, out string error), Is.False);
            Assert.That(schedule, Is.Null); Assert.That(error, Does.Contain("12:00 KST"));
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    [Test]
    public void EditingAnAssetDoesNotMutateAnAlreadyInjectedSchedule()
    {
        var settings = ScriptableObject.CreateInstance<PopupStoreBusinessAccessSettings>();
        try
        {
            Assert.That(settings.TryCreateSchedule(out var original, out _), Is.True);
            Set(settings, "_current", new PopupStoreBusinessAccessSetting(3, "isolated-v2", Day));
            Assert.That(settings.TryCreateSchedule(out var changed, out _), Is.True);
            original.TryResolve(Day, out var oldPolicy, out _); changed.TryResolve(Day, out var newPolicy, out _);
            Assert.That(oldPolicy.RewardedDailyLimit, Is.EqualTo(1)); Assert.That(newPolicy.RewardedDailyLimit, Is.EqualTo(3));
            Assert.That(original.Current.Version, Is.Not.EqualTo(changed.Current.Version));
        }
        finally { UnityEngine.Object.DestroyImmediate(settings); }
    }

    [Test]
    public void NoPolicyBeforeItsEffectiveGameDateMeansNoQuotaOrStart()
    {
        var h = new Harness(1, Day); h.Now = Instant("2026-11-03T02:59:59Z");
        var info = h.Info(); Assert.That(info.Success, Is.False); Assert.That(info.Remaining, Is.Zero);
        Assert.That(info.Error, Does.Contain("No access policy applies before"));
        Assert.That(h.Start("before-effective").State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        Assert.That(h.Store.IssuedBusinessCount, Is.Zero);
    }

    [Test]
    public void ExplicitFuturePolicyChangesOnlyAtItsKstNoonBoundary()
    {
        var schedule = Schedule(1, "2026-11-01", new PopupStoreBusinessAccessSetting(3, "v2", "2026-11-04"));
        var h = new Harness(schedule); h.Now = Instant("2026-11-04T02:59:59Z");
        Assert.That(h.Info().DailyLimit, Is.EqualTo(1)); Assert.That(h.Info().PolicyVersion, Is.EqualTo("v1"));
        h.Now = Instant("2026-11-04T03:00:00Z");
        Assert.That(h.Info().DailyLimit, Is.EqualTo(3)); Assert.That(h.Info().PolicyVersion, Is.EqualTo("v2"));
    }

    [TestCase("2026-11-02")]
    [TestCase("2026-11-03")]
    public void OperationalConfigurationChangeCannotIncreaseTodaysAllowance(string effective)
    {
        var h = new Harness(); h.Start("used");
        Assert.That(h.Store.TryScheduleFuture(h.Session, new PopupStoreBusinessAccessSetting(3, "v2", effective), out string error), Is.False);
        Assert.That(error, Does.Contain("next game date")); Assert.That(h.Info().Used, Is.EqualTo(1)); Assert.That(h.Info().Remaining, Is.Zero);
    }

    [Test]
    public void FutureConfigurationDoesNotEraseTodayOrHistoricalUsage()
    {
        var h = new Harness(); var original = h.Start("first").Record;
        Assert.That(h.Store.TryScheduleFuture(h.Session, new PopupStoreBusinessAccessSetting(2, "v2", "2026-11-04"), out string error), Is.True, error);
        Assert.That(h.Info().Used, Is.EqualTo(1)); Assert.That(h.Info().DailyLimit, Is.EqualTo(1));
        h.Now = Instant("2026-11-04T03:00:00Z"); Assert.That(h.Info().Remaining, Is.EqualTo(2));
        Assert.That(h.Start("tomorrow", "v2").State, Is.EqualTo(PopupStoreBusinessStartState.Started));
        Assert.That(h.Store.Read("A", Day).Usage, Is.EqualTo(1)); Assert.That(h.Store.Read("A", "2026-11-04").Usage, Is.EqualTo(1));
        Assert.That(h.Store.Query(h.Session, original.Request).Record, Is.SameAs(original)); Assert.That(original.PolicyVersion, Is.EqualTo("v1"));
    }

    [Test]
    public void AuthoredScheduleCannotBeChangedThroughLegacyImmediatePolicySetter()
    {
        var h = new Harness(); h.Start("used");
        Assert.Throws<InvalidOperationException>(() => h.Store.UsePolicy(new PopupStoreBusinessAccessPolicy("bypass", 3)));
        Assert.That(h.Info().Remaining, Is.Zero);
    }

    [Test]
    public void RepeatedInformationQueriesConsumeNoUsageOrRequestIdentity()
    {
        var h = new Harness();
        for (int i = 0; i < 5; i++)
        {
            var info = h.Info(); Assert.That(info.Success, Is.True); Assert.That(info.Session, Is.SameAs(h.Session));
            Assert.That(info.GameDay, Is.EqualTo(Day)); Assert.That(info.ObservedAtUtc, Is.EqualTo(h.Now)); Assert.That(info.Remaining, Is.EqualTo(1));
        }
        Assert.That(h.Repository.AccessQueryCalls, Is.EqualTo(5)); Assert.That(h.Repository.SubmitCalls, Is.Zero);
        Assert.That(h.Context.GetPendingRequest("A"), Is.Null); Assert.That(h.Store.Read("A", Day).Starts, Is.Zero);
    }

    [TestCase("missing")]
    [TestCase("throw")]
    [TestCase("underflow")]
    public void UnusableAuthorityTimeBlocksInformationAndApprovalWithoutMintingAUse(string fault)
    {
        var h = new Harness(); h.Store.Clock = () => fault == "throw" ? throw new Exception("clock")
            : fault == "underflow" ? DateTimeOffset.MinValue : (DateTimeOffset?)null;
        var info = h.Info(); Assert.That(info.Success, Is.False); Assert.That(info.Remaining, Is.Zero);
        Assert.That(h.Start("clock-failed").State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        Assert.That(h.Store.IssuedBusinessCount, Is.Zero); Assert.That(h.Store.Read("A", Day).Usage, Is.Zero);
    }

    [TestCase("2026-11-03T02:59:59Z")]
    [TestCase("2026-11-03T03:00:09Z")]
    public void TimeRollbackCannotRestoreQuotaEvenWithinTheSameGameDay(string earlier)
    {
        var h = new Harness(); h.Now = Instant("2026-11-03T03:00:10Z"); var record = h.Start("accepted").Record;
        h.Now = Instant(earlier); Assert.That(h.Info().Success, Is.False);
        Assert.That(h.Start("rewound").State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend));
        Assert.That(h.Store.Query(h.Session, record.Request).Record, Is.SameAs(record)); Assert.That(h.Store.IssuedBusinessCount, Is.EqualTo(1));
        h.Now = Instant("2026-11-03T03:00:10Z"); Assert.That(h.Info().Success, Is.True); Assert.That(h.Info().Remaining, Is.Zero);
    }

    [Test]
    public void UsageAndTimeWatermarksBelongToTheFakeAccountNotThePopupOrLoginToken()
    {
        var h = new Harness(); h.Start("used"); h.Store.RegisterAccount("B"); var b = h.Store.OpenSession("B");
        Assert.That(h.Store.ReadAccess(b).Remaining, Is.EqualTo(1));
        var otherToken = h.Store.OpenSession("A"); Assert.That(h.Store.ReadAccess(otherToken).Remaining, Is.Zero);
        h.Store.Revoke(h.Session); Assert.That(h.Info().Success, Is.False); Assert.That(h.Store.ReadAccess(otherToken).Remaining, Is.Zero);
    }

    [Test]
    public void DeferredInformationEchoesTheOriginalSessionAndCannotBecomeANewLoginResponse()
    {
        var h = new Harness(); var original = h.Session; PopupStoreBusinessAccessSnapshot delivered = null;
        h.Repository.NextAccessFault = PopupStoreFakeAccessFault.Defer;
        h.Repository.QueryAccess(original, value => delivered = value); Assert.That(delivered, Is.Null);
        h.Store.Revoke(original); h.Session = h.Store.OpenSession("A"); h.Repository.DeliverNext();
        Assert.That(delivered.Session, Is.SameAs(original)); Assert.That(delivered.Session, Is.Not.SameAs(h.Session));
        Assert.That(h.Store.Read("A", Day).Usage, Is.Zero);
    }

    [TestCase("revoke")]
    [TestCase("reenter")]
    public void UntrustedClockReentrancyDoesNotExposeUnvalidatedAccess(string action)
    {
        var h = new Harness(); PopupStoreBusinessAccessSnapshot nested = null;
        h.Store.Clock = () =>
        {
            if (action == "revoke") h.Store.Revoke(h.Session); else nested = h.Store.ReadAccess(h.Session);
            return h.Now;
        };
        var outer = h.Info();
        if (action == "revoke") Assert.That(outer.Success, Is.False);
        else { Assert.That(nested.Success, Is.False); Assert.That(outer.Success, Is.True); }
        Assert.That(h.Store.IssuedBusinessCount, Is.Zero);
    }

    [Test]
    public void ApprovalUsesAuthorityDateAndRejectsAnObsoleteRequestedVersionWithoutConsumption()
    {
        var h = new Harness(Schedule(1, "2026-11-01", new PopupStoreBusinessAccessSetting(2, "v2", "2026-11-04")));
        var oldRequest = h.Transaction("prepared-before-noon"); h.Now = Instant("2026-11-04T03:00:00Z"); oldRequest.TrySubmit();
        Assert.That(oldRequest.State, Is.EqualTo(PopupStoreBusinessStartState.RejectedBeforeSend)); Assert.That(h.Info().Used, Is.Zero);
        var current = h.Start("prepared-after-info", "v2").Record;
        Assert.That(current.StartDayKey, Is.EqualTo("2026-11-04")); Assert.That(current.PolicyVersion, Is.EqualTo("v2"));
        Assert.That(h.Store.Read("A", Day).Usage, Is.Zero); Assert.That(h.Info().Used, Is.EqualTo(1));
    }

    [Test]
    public void OldApprovalReplayPrecedesCurrentPolicyAndTimeFailure()
    {
        var h = new Harness(); var record = h.Start("accepted").Record;
        Assert.That(h.Store.TryScheduleFuture(h.Session, new PopupStoreBusinessAccessSetting(3, "v2", "2026-11-04"), out _), Is.True);
        h.Now = Instant("2026-11-07T03:00:00Z"); Assert.That(h.Info().Used, Is.Zero);
        var replay = h.Store.Query(h.Session, record.Request); Assert.That(replay.Record, Is.SameAs(record));
        h.Now = null; Assert.That(h.Store.Query(h.Session, record.Request).Record, Is.SameAs(record));
        Assert.That(record.StartDayKey, Is.EqualTo(Day)); Assert.That(h.Store.Read("A", "2026-11-07").Usage, Is.Zero);
    }

    [Test]
    public void MissedDaysDoNotCarryOverUnusedFreeStarts()
    {
        var h = new Harness(3); Assert.That(h.Info().Remaining, Is.EqualTo(3));
        h.Now = Instant("2026-12-01T03:00:00Z"); Assert.That(h.Info().Remaining, Is.EqualTo(3));
        Assert.That(h.Info().Used, Is.Zero); Assert.That(h.Store.IssuedBusinessCount, Is.Zero);
    }

    [Test]
    public void LastOneUseIsAtomicForIndependentContextsUsingTheAuthoredPolicy()
    {
        var h = new Harness(); PopupStoreBusinessStartResponse a = null, b = null;
        var first = new Thread(() => a = h.Store.Submit(h.Session, h.Request("device-a")));
        var second = new Thread(() => b = h.Store.Submit(h.Session, h.Request("device-b")));
        first.Start(); second.Start(); first.Join(); second.Join();
        Assert.That(new[] { a, b }.Count(x => x.Kind == PopupStoreBusinessStartResponseKind.Started), Is.EqualTo(1));
        Assert.That(h.Info().Used, Is.EqualTo(1)); Assert.That(h.Info().Remaining, Is.Zero); Assert.That(h.Store.IssuedBusinessCount, Is.EqualTo(1));
    }

    [Test]
    public void LostApprovalAndTemporaryNotFoundKeepTheSameRequestAcrossInfoRefresh()
    {
        var h = new Harness(); h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenIndeterminate;
        var tx = h.Start("lost"); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Indeterminate));
        Assert.That(h.Info().Remaining, Is.Zero); h.Repository.NextQueryFault = PopupStoreFakeStartQueryFault.NotFound; tx.TryReconcile();
        Assert.That(h.Context.GetPendingRequest("A"), Is.SameAs(tx.Request));
        var bypass = h.Transaction("new-id"); Assert.That(bypass.TrySubmit(), Is.False);
        tx.TryReconcile(); Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started));
        Assert.That(tx.ConfirmationCount, Is.EqualTo(1)); Assert.That(h.Store.IssuedBusinessCount, Is.EqualTo(1));
    }

    [Test]
    public void ExistingLegacyCtorStillReportsItsExplicitTwoUsePolicy()
    {
        var store = new PopupStoreBusinessStartFakeStore(() => Instant("2026-11-03T03:00:00Z"));
        store.RegisterAccount("legacy"); var session = store.OpenSession("legacy"); var info = store.ReadAccess(session);
        Assert.That(info.Success, Is.True); Assert.That(info.DailyLimit, Is.EqualTo(2)); Assert.That(info.PolicyVersion, Is.EqualTo("v1.3-test"));
    }

    [TestCase("2026-11-03T02:59:59Z", "2026-11-02")]
    [TestCase("2026-11-03T03:00:00Z", "2026-11-03")]
    public void QueryDayExactlyMatchesAttendanceWithoutReadingAttendanceState(string utc, string expected)
    {
        var h = new Harness(); h.Now = Instant(utc); var info = h.Info();
        DateTime kst = h.Now.Value.UtcDateTime.AddHours(9);
        Assert.That(info.GameDay, Is.EqualTo(expected));
        Assert.That(info.GameDay, Is.EqualTo(AttendanceProgress.GameDay(kst).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    [Test]
    public void InvalidInformationCannotAdvertiseAnUnlimitedOrRestoredAllowance()
    {
        var session = new PopupStoreRewardAccountSession("fake"); var utc = Instant("2026-11-03T03:00:00Z");
        var invalid = new[] {
            new PopupStoreBusinessAccessSnapshot(null, Day, "v1", 1, 0, utc),
            new PopupStoreBusinessAccessSnapshot(session, "wrong", "v1", 1, 0, utc),
            new PopupStoreBusinessAccessSnapshot(session, Day, "", 1, 0, utc),
            new PopupStoreBusinessAccessSnapshot(session, Day, "v1", -1, 0, utc),
            new PopupStoreBusinessAccessSnapshot(session, Day, "v1", 1, -1, utc),
            new PopupStoreBusinessAccessSnapshot(session, Day, "v1", 1, 0, null),
            new PopupStoreBusinessAccessSnapshot(session, Day, "v1", 1, 0, utc.ToOffset(TimeSpan.FromHours(9))),
            new PopupStoreBusinessAccessSnapshot(session, "2026-11-02", "v1", 1, 0, utc)
        };
        foreach (var value in invalid) { Assert.That(value.Success, Is.False); Assert.That(value.Remaining, Is.Zero); }
    }

    [Test]
    public void RuntimeSettingsAndInformationContractsContainNoLiveAccountOrBackendAdapter()
    {
        string source = File.ReadAllText("Assets/Scripts/UI/Floor3UI/PopupStoreBusinessAccessSettings.cs");
        foreach (string forbidden in new[] { "UserInfo.", "Backend.", "PlayerPrefs.", "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now",
            "DateTimeOffset.UtcNow", "UnityEngine.Random.", "File.Write", "new PopupStoreRewardTransaction" })
            Assert.That(source, Does.Not.Contain(forbidden));
        // DEV-SERVER-01E adds only the explicit existing-GameData bridge. Pure settings stay
        // account-free; the separate account bootstrap is disabled in its authored configuration.
        CollectionAssert.AreEquivalent(new[] { typeof(PopupStoreBackendPersistence), typeof(PopupStoreGameDataAccountBridge) },
            typeof(IPopupStoreBusinessAccessInformation).Assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface
                && typeof(IPopupStoreBusinessAccessInformation).IsAssignableFrom(t)));
        Assert.That(typeof(PopupStoreBackendPersistence).IsSealed, Is.True);
        Assert.That(typeof(PopupStoreBackendPersistence).GetConstructor(Type.EmptyTypes), Is.Null);
        Assert.That(typeof(PopupStoreBackendPersistence).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
        Assert.That(typeof(PopupStoreGameDataAccountBridge).IsSealed, Is.True);
        Assert.That(typeof(PopupStoreGameDataAccountBridge).GetConstructor(Type.EmptyTypes), Is.Null);
        Assert.That(typeof(PopupStoreGameDataAccountBridge).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
        Assert.That(Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource)
            .EnableAccountMode, Is.False);
    }

    private sealed class Harness
    {
        internal DateTimeOffset? Now = Instant("2026-11-03T03:00:00Z");
        internal readonly PopupStoreBusinessStartFakeStore Store;
        internal readonly PopupStoreBusinessStartFakeRepository Repository;
        internal readonly PopupStoreBusinessStartContext Context = new PopupStoreBusinessStartContext();
        internal PopupStoreRewardAccountSession Session;
        internal Harness(int limit = 1, string effective = "2026-11-01") : this(Schedule(limit, effective)) { }
        internal Harness(PopupStoreBusinessAccessSchedule schedule)
        {
            Store = new PopupStoreBusinessStartFakeStore(schedule, () => Now); Store.RegisterAccount("A");
            Session = Store.OpenSession("A"); Repository = new PopupStoreBusinessStartFakeRepository(Store);
        }
        internal PopupStoreBusinessAccessSnapshot Info()
        { PopupStoreBusinessAccessSnapshot result = null; Repository.QueryAccess(Session, value => result = value); return result; }
        internal PopupStoreBusinessStartRequest Request(string id, string version = "v1")
            => new PopupStoreBusinessStartRequest("A", id, PopupStoreBusinessMode.Rewarded, version);
        internal PopupStoreBusinessStartTransaction Transaction(string id, string version = "v1")
            => new PopupStoreBusinessStartTransaction(Request(id, version), Repository, () => Session, Context);
        internal PopupStoreBusinessStartTransaction Start(string id, string version = "v1")
        { var tx = Transaction(id, version); tx.TrySubmit(); return tx; }
    }
    private static PopupStoreBusinessAccessSchedule Schedule(int limit = 1, string effective = "2026-11-01", PopupStoreBusinessAccessSetting? future = null)
    {
        Assert.That(PopupStoreBusinessAccessSchedule.TryCreate(new PopupStoreBusinessAccessSetting(limit, "v1", effective), future,
            out var schedule, out string error), Is.True, error); return schedule;
    }
    private static DateTimeOffset Instant(string input) => DateTimeOffset.Parse(input, CultureInfo.InvariantCulture, DateTimeStyles.None);
    private static void Set(object instance, string field, object value)
        => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    private static string Account() => string.Join("|", new object[] { UserInfo.Dia, UserInfo.Money, UserInfo.TotalCumulativeCustomerCount,
        UserInfo.CustomerEncounterContextGeneration, UserInfo.DailyAddMoney, UserInfo.DailyCookCount, UserInfo.DailyCumulativeCustomerCount,
        UserInfo.DailyAdvertisingViewCount, UserInfo.DailyCleanCount, UserInfo.DailyAdGoldRewardCount, UserInfo.DailyAdDiaRewardCount });
}
#endif
