#if UNITY_EDITOR
using System;
using Muks.BackEnd;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class StaffStageMigrationCollectionTests
{
    [TestCase(1, false, false)] [TestCase(2, false, false)]
    [TestCase(7, true, false)] [TestCase(28, false, false)] [TestCase(1, false, true)]
    public void Attendance_ConfirmedResponseCommitsRewardAndProgressOnce(int day, bool doubled, bool inline)
    {
        var fixture = CreateAccountRuntimeFixture(out _);
        var now = new DateTime(2026, 10, 2, 13, 0, 0);
        var state = PrepareAttendance(fixture, now, day);
        if (inline) fixture.Game.OnUpdate = (_, __, reply) => reply(Bro("204", ""));
        try
        {
            float before = AttendanceProgress.Fill(day, false);
            Assert.That(fixture.Manager.TryClaimAttendance(doubled, out var claim, out string error), Is.True, error);
            if (!inline)
            {
                Assert.That(state.Commits, Is.Zero);
                Assert.That(state.Current.Days, Is.EqualTo(day - 1));
                Assert.That(fixture.Manager.TryClaimAttendance(doubled, out _, out _), Is.False);
                fixture.Game.WriteReplies[0](Bro("204", ""));
            }
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(state.Commits, Is.EqualTo(1));
            Assert.That(state.Notifications, Is.EqualTo(1));
            Assert.That(claim.Committed, Is.True);
            Assert.That(state.Current.Days, Is.EqualTo(day));
            Assert.That(state.Current.Money, Is.EqualTo(100 + (doubled ? 6000 : 3000)));
            Assert.That(AttendanceProgress.Fill(day, true), Is.GreaterThanOrEqualTo(before));
            if (day % 7 != 0) Assert.That(AttendanceProgress.Fill(day, true), Is.GreaterThan(before));
            while (!inline && fixture.Manager.CurrentGameDataSaveCoordinator.State != GameDataSaveCoordinatorState.Idle)
                fixture.Game.WriteReplies[fixture.Game.WriteReplies.Count - 1](Bro("204", ""));
            Assert.That(fixture.Manager.CanClaimAttendance(out _, out _), Is.False, "Reopening uses the same committed state");
            Assert.That(AttendanceProgress.Today(state.Current, now, out bool due), Is.EqualTo(day));
            Assert.That(due, Is.False);
            var payload = JObject.Parse(fixture.Game.WriteValues[0].GetJson());
            Assert.That((int)payload["TotalAttendanceDays"], Is.EqualTo(day));
        }
        finally { fixture.Manager.InvalidateGameDataRestore(); }
    }

    [TestCase(false)] [TestCase(true)]
    public void Attendance_UnknownOrInvalidatedResponseNeverAdvancesOrDoubleSends(bool invalidate)
    {
        var fixture = CreateAccountRuntimeFixture(out _);
        var state = PrepareAttendance(fixture, new DateTime(2026, 10, 2, 13, 0, 0), 1);
        try
        {
            Assert.That(fixture.Manager.TryClaimAttendance(false, out var claim, out _), Is.True);
            fixture.Game.WriteReplies[0](Bro("500", ""));
            Assert.That(state.Commits, Is.Zero);
            Assert.That(state.Current.Days, Is.Zero);
            Assert.That(AttendanceProgress.Fill(1, false), Is.Zero);
            Assert.That(fixture.Manager.TryClaimAttendance(false, out _, out _), Is.False);
            Assert.That(fixture.Game.Writes, Is.EqualTo(1));
            if (invalidate) fixture.Manager.InvalidateGameDataRestore();
            fixture.Game.WriteReplies[0](Bro("204", ""));
            Assert.That(state.Commits, Is.EqualTo(invalidate ? 0 : 1));
        }
        finally { fixture.Manager.InvalidateGameDataRestore(); }
    }

    [Test]
    public void Attendance_LastCatalogDayAndNoonBoundaryNeverUseStaleSlotReward()
    {
        var fixture = CreateAccountRuntimeFixture(out _);
        var now = new DateTime(2026, 10, 2, 11, 59, 59);
        var state = PrepareAttendance(fixture, now, 28);
        try
        {
            state.Current = new AttendanceMemory("2026-10-01 13:00:00", 28, 110, 100, 100, 100, 100);
            Assert.That(AttendanceProgress.Today(state.Current, now, out var due), Is.EqualTo(28));
            Assert.That(due, Is.False);
            Assert.That(AttendanceProgress.Today(state.Current, now.AddSeconds(1), out due), Is.EqualTo(29));
            Assert.That(due, Is.True);
            Field(typeof(BackendManager), "_attendanceNowKst").SetValue(fixture.Manager, (Func<DateTime>)(() => now.AddSeconds(1)));
            Assert.That(fixture.Manager.TryClaimAttendance(false, out _, out _), Is.False);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(AttendanceProgress.Fill(7, true), Is.EqualTo(1));
            Assert.That(AttendanceProgress.Fill(8, false), Is.Zero);
        }
        finally { fixture.Manager.InvalidateGameDataRestore(); }
    }

    [Test]
    public void Attendance_UnchangedBindingsSkipUiApplicationsAndReopenInvalidates()
    {
        using (var wallet = new EconomyUserInfoScope())
        using (var host = new EnhancementFairyStage1Host())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            host.PreparePhase3Attendance(fonts, new DateTime(2026, 10, 2, 13, 0, 0), 1);
            var view = host.SceneComponents<UIAttendance>()[0];
            host.BindAndAssertPhase3Attendance(1, true, true);
            string[] counters = { "_editorSlotDataApplications", "_editorSlotStateApplications",
                "_editorButtonApplications", "_editorAdApplications", "_editorGaugeApplications" };
            int[] initial = Array.ConvertAll(counters, name => (int)EnhancementFairyStage1Host.Get(view, name));
            for (int i = 0; i < 10; i++) host.BindAndAssertPhase3Attendance(1, true, true);
            for (int i = 0; i < counters.Length; i++)
                Assert.That(EnhancementFairyStage1Host.Get(view, counters[i]), Is.EqualTo(initial[i]), counters[i]);
            var bar = (UILoadingBar)EnhancementFairyStage1Host.Get(view, "_loadingBar");
            var barRect = (RectTransform)bar.transform;
            barRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barRect.rect.width + 20f);
            host.BindAndAssertPhase3Attendance(1, true, true);
            Assert.That((int)EnhancementFairyStage1Host.Get(view, "_editorGaugeApplications"), Is.EqualTo(initial[4] + 1));
            EnhancementFairyStage1Host.Invoke(view, "InvalidateDisplay");
            host.BindAndAssertPhase3Attendance(1, true, true);
            for (int i = 0; i < counters.Length; i++)
                Assert.That((int)EnhancementFairyStage1Host.Get(view, counters[i]), Is.GreaterThan(initial[i]), counters[i]);
        }
    }

    [Test]
    public void Attendance_RewardChangesAndRecreatedSlotsRebindWithinSameWeek()
    {
        using (var wallet = new EconomyUserInfoScope())
        using (var host = new EnhancementFairyStage1Host())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            host.PreparePhase3Attendance(fonts, new DateTime(2026, 10, 2, 13, 0, 0), 1);
            var view = host.SceneComponents<UIAttendance>()[0];
            var slots = (List<UIAttendanceSlot>)EnhancementFairyStage1Host.Get(view, "_slotList");
            var rewards = AttendanceDataManager.Instance.GetRewardDic();
            host.BindAndAssertPhase3Attendance(1, true, true);
            var replacement = new AttendanceData(MoneyType.Dia, 31);
            rewards[1] = replacement;
            host.BindAndAssertPhase3Attendance(1, true, true);
            Assert.That(EnhancementFairyStage1Host.Get(slots[0], "_data"), Is.SameAs(replacement));
            Assert.That(((TMPro.TextMeshProUGUI)EnhancementFairyStage1Host.Get(slots[0], "_giftCountText")).text,
                Is.EqualTo(Utility.ConvertToMoney(31)));
            EnhancementFairyStage1Host.Set(replacement, "_rewardValue", 52);
            host.BindAndAssertPhase3Attendance(1, true, true);
            Assert.That(((TMPro.TextMeshProUGUI)EnhancementFairyStage1Host.Get(slots[0], "_giftCountText")).text,
                Is.EqualTo(Utility.ConvertToMoney(52)));
            var old = slots[0];
            slots[0] = UnityEngine.Object.Instantiate((UIAttendanceSlot)EnhancementFairyStage1Host.Get(view, "_slotPrefab"),
                (RectTransform)EnhancementFairyStage1Host.Get(view, "_slotParent"));
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            host.BindAndAssertPhase3Attendance(1, true, true);
            Assert.That(EnhancementFairyStage1Host.Get(slots[0], "_data"), Is.SameAs(replacement));
            rewards[8] = replacement;
            host.BindAndAssertPhase3Attendance(8, true, true);
            Assert.That(((TMPro.TextMeshProUGUI)EnhancementFairyStage1Host.Get(slots[0], "_dayText")).text,
                Does.StartWith("8"));
            slots.RemoveAt(6);
            host.BindAndAssertPhase3Attendance(8, true, true);
            Assert.That(((GameObject)EnhancementFairyStage1Host.Get(slots[5], "_specialImages")).activeSelf, Is.True);
        }
    }

    [Test]
    public void Attendance_DisplayTracksConfirmedProgressAdAvailabilityAndLastReward()
    {
        using (var wallet = new EconomyUserInfoScope())
        using (var host = new EnhancementFairyStage1Host())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            host.PreparePhase3Attendance(fonts, new DateTime(2026, 10, 2, 13, 0, 0), 7);
            var view = host.SceneComponents<UIAttendance>()[0];
            var ad = (WatchAdButton)EnhancementFairyStage1Host.Get(view, "_adButton");
            var adButton = (Button)EnhancementFairyStage1Host.Get(ad, "_adButton");
            host.BindAndAssertPhase3Attendance(7, true, true);
            Assert.That(adButton.interactable, Is.True);
            EnhancementFairyStage1Host.Set(view, "_adEarned", true);
            host.BindAndAssertPhase3Attendance(7, true, true);
            Assert.That(adButton.interactable, Is.False);
            EnhancementFairyStage1Host.Set(view, "_adEarned", false);
            host.BindAndAssertPhase3Attendance(7, true, false);
            Assert.That(adButton.interactable, Is.False);
            host.BindAndAssertPhase3Attendance(7, false, false);
            host.BindAndAssertPhase3Attendance(8, true, true);
            EnhancementFairyStage1Host.Invoke(view, "BindConfirmedState", 29, true, true);
            host.AssertPhase3Attendance(28, true);
            Assert.That(((UIButtonAndPressEffect)EnhancementFairyStage1Host.Get(view, "_attendanceButton")).interactable, Is.False);
            Assert.That(adButton.interactable, Is.False);
            AttendanceDataManager.Instance.GetRewardDic()[29] = new AttendanceData(MoneyType.Gold, 10);
            host.BindAndAssertPhase3Attendance(29, true, true);
        }
    }

    private AttendanceTestMemory PrepareAttendance(Fixture fixture, DateTime now, int day)
    {
        var state = new AttendanceTestMemory { Current = new AttendanceMemory(day == 1 ? "" : now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"), day - 1, 110, 100, 100, 100, 100) };
        Field(typeof(BackendManager), "_attendanceState").SetValue(fixture.Manager, state);
        Field(typeof(BackendManager), "_attendanceNowKst").SetValue(fixture.Manager, (Func<DateTime>)(() => now));
        Field(typeof(BackendManager), "_attendanceReward").SetValue(fixture.Manager,
            (Func<int, AttendanceData>)(d => d >= 1 && d <= 28 ? new AttendanceData(MoneyType.Gold, 3000) : null));
        fixture.Game.LatestFactory = () => state.Current.Values();
        return state;
    }

    [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)]
    [TestCase(7, true)] [TestCase(28, false)]
    public void Attendance_ActualUserStateAndNativeUiCommitCsvRewardOnlyAfterConfirmation(int day, bool doubled)
    {
        using (var wallet = new EconomyUserInfoScope())
        using (var host = new EnhancementFairyStage1Host())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            var now = new DateTime(2026, 10, 2, 13, 0, 0);
            var reward = host.PreparePhase3Attendance(fonts, now, day);
            var state = (IAttendanceState)Activator.CreateInstance(typeof(UserInfo).Assembly.GetType("UserAttendanceState"));
            var fixture = CreateAccountRuntimeFixture(out _);
            Field(typeof(BackendManager), "_attendanceState").SetValue(fixture.Manager, state);
            Field(typeof(BackendManager), "_attendanceNowKst").SetValue(fixture.Manager, (Func<DateTime>)(() => now));
            Field(typeof(BackendManager), "_attendanceReward").SetValue(fixture.Manager,
                (Func<int, AttendanceData>)(d => AttendanceDataManager.Instance.GetRewardDic().TryGetValue(d, out var r) ? r : null));
            fixture.Game.LatestFactory = () => state.Read().Values();
            int notifications = 0;
            fixture.Manager.AttendanceChanged += () =>
            {
                if (fixture.Manager.CurrentAttendanceClaim?.Committed == true) notifications++;
                int today = AttendanceProgress.Today(state.Read(), now, out bool due);
                host.BindAndAssertPhase3Attendance(today, due, false);
            };
            try
            {
                host.BindAndAssertPhase3Attendance(day, true, true);
                Assert.That(fixture.Manager.TryClaimAttendance(doubled, out var claim, out string error), Is.True, error);
                fixture.Game.WriteReplies[0](Bro("500", ""));
                Assert.That(state.Read().Days, Is.EqualTo(day - 1));
                Assert.That(notifications, Is.Zero);
                host.AssertPhase3Attendance(day, false);
                Assert.That(fixture.Manager.TryClaimAttendance(doubled, out _, out _), Is.False);
                fixture.Game.WriteReplies[0](Bro("204", ""));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                int amount = reward.RewardValue * (doubled ? 2 : 1);
                var committed = state.Read();
                Assert.That(committed.Days, Is.EqualTo(day));
                Assert.That(committed.Diamonds, Is.EqualTo(110 + (reward.MoneyType == MoneyType.Dia ? amount : 0)));
                Assert.That(committed.Money, Is.EqualTo(100 + (reward.MoneyType == MoneyType.Gold ? amount : 0)));
                Assert.That(committed.Total, Is.EqualTo(100 + (reward.MoneyType == MoneyType.Gold ? amount : 0)));
                Assert.That(committed.Daily, Is.EqualTo(committed.Total));
                Assert.That(committed.Weekly, Is.EqualTo(committed.Total));
                Assert.That(notifications, Is.EqualTo(1));
                host.AssertPhase3Attendance(day, true);
                host.BindAndAssertPhase3Attendance(day, false, false); // Same binding on reentry.
            }
            finally { fixture.Manager.InvalidateGameDataRestore(); }
        }
    }
    private sealed class AttendanceTestMemory : IAttendanceState
    {
        public AttendanceMemory Current;
        public int Commits, Notifications;
        public AttendanceMemory Read() => Current;
        public bool TryCommit(AttendanceClaim claim, out string error)
        {
            error = null;
            if (!claim.Before.SameAttendance(Current) || !Current.CanAdd(claim.Type, claim.Amount)) return false;
            Current = new AttendanceMemory(claim.Time, claim.Day, Current.Diamonds + (claim.Type == MoneyType.Dia ? claim.Amount : 0),
                Current.Money + (claim.Type == MoneyType.Gold ? claim.Amount : 0), Current.Total + (claim.Type == MoneyType.Gold ? claim.Amount : 0),
                Current.Daily + (claim.Type == MoneyType.Gold ? claim.Amount : 0), Current.Weekly + (claim.Type == MoneyType.Gold ? claim.Amount : 0));
            Commits++; return true;
        }
        public void Notify(MoneyType type) => Notifications++;
    }
}
#endif
