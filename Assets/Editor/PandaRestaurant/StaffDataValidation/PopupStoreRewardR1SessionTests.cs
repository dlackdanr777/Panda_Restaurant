#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Muks.BackEnd;
using Muks.MobileUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>R1 decisions from approved starts and real local receipts, never synthetic final scores or grants.</summary>
public sealed class PopupStoreRewardR1SessionTests
{
    private const string R1Path = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R1.asset";
    private const string R0Path = "Assets/Resources/UI/PopupStore/PopupStoreRewardTable_R0.asset";
    private const string Notice = "미지급 · 실제 계정에 반영되지 않음";
    private string _account;
    private object _manager, _score;
    private BackendManager _backend;
    private bool _sdk;
    private UnityEngine.Random.State _random;

    [SetUp]
    public void SnapshotRealAccountAndUnityRandomWithoutUsingThem()
    {
        _account = AccountState(); _manager = ReadStatic(typeof(GameManager), "_instance");
        _score = ReadStatic(typeof(UserInfo), "_score"); _backend = BackendManager.ExistingInstance;
        _sdk = BackEnd.Backend.IsInitialized; _random = UnityEngine.Random.state;
    }

    [TearDown]
    public void NoActualAccountCurrencyDailyStateBackendManagerOrGlobalRandomChanged()
    {
        Assert.That(AccountState(), Is.EqualTo(_account)); Assert.That(ReadStatic(typeof(GameManager), "_instance"), Is.SameAs(_manager));
        Assert.That(ReadStatic(typeof(UserInfo), "_score"), Is.EqualTo(_score)); Assert.That(BackendManager.ExistingInstance, Is.SameAs(_backend));
        Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_sdk)); Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [TestCase(PopupStoreGradeTier.C, 1, 3, 5)]
    [TestCase(PopupStoreGradeTier.B, 3, 8, 15)]
    [TestCase(PopupStoreGradeTier.A, 5, 15, 30)]
    [TestCase(PopupStoreGradeTier.S, 10, 20, 100)]
    [TestCase(PopupStoreGradeTier.SS, 20, 30, 200)]
    [TestCase(PopupStoreGradeTier.SSS, 30, 50, 300)]
    public void RealSalesForEveryFinalGradeUseOnePermilleDrawForEachApprovedAlternative(
        PopupStoreGradeTier grade, int basic, int bonus, int jackpot)
    {
        int[] rolls = { 0, 900, 997 }, amounts = { basic, bonus, jackpot };
        for (int i = 0; i < rolls.Length; i++)
            using (var h = new Host(new Source(rolls[i])))
            {
                Assert.That(h.Reward.HasPolicySnapshot, Is.False); Assert.That(h.Source.Calls, Is.Zero);
                h.Finish(grade);
                var result = h.Reward.Result;
                Assert.That(result.Grade, Is.EqualTo(grade)); Assert.That(result.Kind, Is.EqualTo((PopupStoreRewardKind)i));
                Assert.That(result.Diamonds, Is.EqualTo(amounts[i])); Assert.That(result.PolicyId, Is.EqualTo(PopupStoreRewardTable.R1PolicyId));
                Assert.That(result.SessionId, Is.EqualTo(h.Grade.FinalResult.SessionId)); Assert.That(result.DecisionId, Is.Not.Empty);
                Assert.That(h.Grade.FinalResult.SuccessfulOrders, Is.EqualTo(h.Score.SuccessfulOrderCount));
                Assert.That(h.Packing.CommittedReceipts.Count, Is.EqualTo(h.Score.SuccessfulOrderCount));
                AssertOne(h); h.Show(); h.Confirm();
            }
    }

    [TestCase(PopupStoreBusinessState.NotStarted)]
    [TestCase(PopupStoreBusinessState.Running)]
    [TestCase(PopupStoreBusinessState.Paused)]
    public void UnapprovedOrUnfinishedBusinessDoesNotDrawOrCreateAReward(PopupStoreBusinessState state)
    {
        using (var h = new Host(new Source(997)))
        {
            if (state != PopupStoreBusinessState.NotStarted) h.Start();
            if (state == PopupStoreBusinessState.Paused) h.Clock.SetManualPaused(true);
            Assert.That(h.Reward.HasPolicySnapshot, Is.EqualTo(state != PopupStoreBusinessState.NotStarted));
            Error(); Assert.That(h.Reward.TryDecide(), Is.False);
            Assert.That(h.Grade.FinalResult, Is.Null); Assert.That(h.Reward.Result, Is.Null); Assert.That(h.Source.Calls, Is.Zero);
            Assert.That(h.Clock.State, Is.EqualTo(state)); Assert.That(h.Reward.DecisionCount, Is.Zero);
        }
    }

    [Test]
    public void ApprovedStartCapturesPolicyWithoutDrawingOrFinalizing()
    {
        using (var h = new Host(new Source(997)))
        {
            h.Start();
            Assert.That(h.Reward.HasPolicySnapshot, Is.True); Assert.That(h.Reward.PolicySnapshot.PolicyId, Is.EqualTo(PopupStoreRewardTable.R1PolicyId));
            Assert.That(h.Reward.PolicySnapshot.Denominator, Is.EqualTo(1000)); Assert.That(h.Reward.PolicyError, Is.Null);
            Assert.That(h.Source.Calls, Is.Zero); Assert.That(h.Reward.Result, Is.Null); Assert.That(h.Grade.FinalResult, Is.Null);
            var controller = h.Root.GetComponent<PopupStoreStartController>();
            Assert.That(controller.IsApproved, Is.True); Assert.That(controller.Host.LocalStartCount, Is.EqualTo(1));
            Assert.That(controller.Host.BindingCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ApprovedButFocusDelayedStartPinsR1BeforeAnAssetSwapAndConsumesOnlyOnce()
    {
        using (var h = new Host(new Source(997)))
        {
            var store = new PopupStoreBusinessStartFakeStore(() => PopupStoreApprovedTestSession.AuthorityTime,
                new PopupStoreBusinessAccessPolicy("r1-focus-delay", 1));
            const string account = "r1-focus-delay-isolated";
            store.RegisterAccount(account); var accountSession = store.OpenSession(account);
            var repository = new PopupStoreBusinessStartFakeRepository(store);
            using (var authority = new PopupStoreAdmissionHost(repository, repository, () => accountSession, editorReview: true))
            {
                var controller = h.Root.GetComponent<PopupStoreStartController>();
                Assert.That(controller.BindHost(authority), Is.True);
                repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer;
                Assert.That(controller.RequestStart(), Is.True); h.Clock.SetApplicationFocused(false); repository.DeliverNext();
                Assert.That(authority.Transaction.LocalCompletionPending, Is.True);
                Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
                Assert.That(h.Reward.HasPolicySnapshot, Is.True); var policy = h.Reward.PolicySnapshot;
                Write(h.Reward, "_table", AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R0Path));
                h.Clock.SetApplicationFocused(true); Assert.That(authority.RetryLocalStart(controller), Is.True);
                Assert.That(h.Reward.PolicySnapshot, Is.SameAs(policy)); Assert.That(h.Reward.PolicySnapshot.Denominator, Is.EqualTo(1000));
                Assert.That(authority.LocalStartCount, Is.EqualTo(1)); Assert.That(repository.SubmitCalls, Is.EqualTo(1));
                Assert.That(store.Read(account, "2026-11-03").Usage, Is.EqualTo(1));
                h.Call("SellUntil", PopupStoreGradeTier.C); h.Call("End");
                Assert.That(h.Reward.Result.Diamonds, Is.EqualTo(5)); AssertOne(h);
            }
        }
    }

    [Test]
    public void ZeroSuccessfulOrdersResolvesNoneWithoutAnAmountOrRandomCall()
    {
        using (var h = new Host(new Source(997)))
        {
            h.Finish(PopupStoreGradeTier.None); h.Show();
            Assert.That(h.Reward.IsResolved, Is.True); Assert.That(h.Reward.Result, Is.Null);
            Assert.That(h.Source.Calls, Is.Zero); Assert.That(h.Reward.DecisionCount, Is.Zero);
            Assert.That(h.Box.IsVisible, Is.False); Assert.That(h.Preview.TryReveal(), Is.False); h.Confirm();
        }
    }

    [Test]
    public void DirectExitAfterSuccessfulSaleNeverCreatesFinalResultOrR1Decision()
    {
        using (var h = new Host(new Source(997)))
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C); var receipts = h.Packing.CommittedReceipts.ToArray();
            Assert.That(h.Root.GetComponent<UIPopupStore>().TryRequestExit(), Is.True);
            Assert.That(h.Root.GetComponent<PopupStoreExitConfirmation>().TryConfirmAbandon(), Is.True);
            Assert.That(h.Clock.EndReason, Is.EqualTo(PopupStoreBusinessEndReason.DirectExit));
            Assert.That(h.Grade.FinalResult, Is.Null); Assert.That(h.Reward.Result, Is.Null);
            Assert.That(h.Source.Calls, Is.Zero); Assert.That(h.Reward.DecisionCount, Is.Zero);
            Assert.That(h.Score.TotalScore, Is.EqualTo(100)); CollectionAssert.AreEqual(receipts, h.Packing.CommittedReceipts.ToArray());
            Error(); Assert.That(h.Reward.TryDecide(), Is.False);
        }
    }

    [TestCase("amounts")]
    [TestCase("reference")]
    [TestCase("null-reference")]
    public void ApprovedStartSnapshotSurvivesAssetOrReferenceChangesBeforeFinalization(string mutation)
    {
        var memory = Object.Instantiate(Authored());
        try
        {
            using (var h = new Host(new Source(997)))
            {
                Write(h.Reward, "_table", memory); h.Start(); var snapshot = h.Reward.PolicySnapshot;
                if (mutation == "amounts")
                {
                    var entries = (PopupStoreRewardEntry[])Read(memory, "_entries");
                    entries[0] = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 9, 90, 900, 900, 97, 3);
                    Assert.That(memory.TryGetEntries(out _, out _), Is.False, "The edited R1 row is not the approved table.");
                }
                else Write(h.Reward, "_table", mutation == "reference" ? AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R0Path) : null);
                h.Call("SellUntil", PopupStoreGradeTier.C); h.Call("End");
                Assert.That(h.Reward.PolicySnapshot, Is.SameAs(snapshot)); Assert.That(h.Reward.Result.Diamonds, Is.EqualTo(5));
                Assert.That(h.Reward.Result.PolicyId, Is.EqualTo(PopupStoreRewardTable.R1PolicyId)); AssertOne(h);
            }
        }
        finally { Object.DestroyImmediate(memory); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailedApprovedStartSnapshotCannotBeRepairedIntoALateReward(bool missing)
    {
        var memory = Object.Instantiate(Authored());
        try
        {
            var rows = (PopupStoreRewardEntry[])Read(memory, "_entries");
            rows[0] = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 1, 3, 5, 900, 96, 3);
            using (var h = new Host(new Source(997)))
            {
                Write(h.Reward, "_table", missing ? null : memory); h.Start();
                Assert.That(h.Reward.HasPolicySnapshot, Is.False); Assert.That(h.Reward.PolicyError, Is.Not.Null.And.Not.Empty);
                Write(h.Reward, "_table", Authored()); h.Call("SellUntil", PopupStoreGradeTier.C);
                Error(); h.Call("End");
                for (int i = 0; i < 3; i++) Assert.That(h.Reward.TryDecide(), Is.False);
                Assert.That(h.Reward.Result, Is.Null); Assert.That(h.Source.Calls, Is.Zero);
                h.Show(false); Assert.That(h.Panel.IsVisible, Is.True); Assert.That(h.Preview.TryReveal(), Is.False);
                Assert.That(h.Value.text, Is.EqualTo("보상 정보 오류 · 미결정")); Assert.That(h.Value.gameObject.activeInHierarchy, Is.True);
                Assert.That(h.Notice.text, Is.EqualTo(Notice)); Assert.That(h.Notice.gameObject.activeInHierarchy, Is.True);
                h.Confirm(); Assert.That(h.Navigation.FirstView, Is.SameAs(h.Floor));
            }
        }
        finally { Object.DestroyImmediate(memory); }
    }

    [Test]
    public void SnapshotIsImmutableAndReturnedEntriesCannotAlterItsPolicy()
    {
        using (var h = new Host(new Source(997)))
        {
            h.Start(); var snapshot = h.Reward.PolicySnapshot;
            Assert.That(snapshot.GetType().IsSealed, Is.True);
            foreach (var property in snapshot.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            Assert.That(snapshot.TryGetEntries(out var entries, out _), Is.True);
            entries[0] = new PopupStoreRewardEntry(PopupStoreGradeTier.C, 99, 999, 9999, 1, 1, 998);
            Assert.That(snapshot.TryGetReward(PopupStoreGradeTier.C, out var row, out _), Is.True);
            Assert.That(row.JackpotDiamonds, Is.EqualTo(5));
            h.Call("SellUntil", PopupStoreGradeTier.C); h.Call("End"); Assert.That(h.Reward.Result.Diamonds, Is.EqualTo(5)); AssertOne(h);
        }
    }

    [TestCase(-1)]
    [TestCase(1000)]
    [TestCase(int.MinValue)]
    [TestCase(int.MaxValue)]
    public void InvalidPermilleProviderResultIsRejectedWithoutClampFallbackOrAnotherDraw(int roll)
    {
        using (var h = new Host(new Source(roll)))
        {
            Error(); h.Finish(PopupStoreGradeTier.C); AssertFailedOneAttempt(h);
            h.Show(false); h.Hide(); h.Reopen(false); AssertFailedOneAttempt(h); h.Confirm();
        }
    }

    [Test]
    public void ThrowingPermilleProviderCannotBeRetriedAfterFinalizedRefreshOrReentry()
    {
        using (var h = new Host(new Source(() => throw new InvalidOperationException("R1 controlled failure"))))
        {
            Error(); h.Finish(PopupStoreGradeTier.C); AssertFailedOneAttempt(h);
            h.Show(false); h.Hide(); h.Reopen(false); AssertFailedOneAttempt(h); h.Confirm();
        }
    }

    [Test]
    public void ProviderReentryCannotCreateASecondR1DecisionOrReplaceSource()
    {
        Host active = null; bool nested = true, replaced = true;
        using (var h = new Host(new Source(() =>
        { nested = active.Reward.TryDecide(); replaced = active.Reward.TrySetRandomSource(new Source(0)); return 997; })))
        {
            active = h; h.Finish(PopupStoreGradeTier.C);
            Assert.That(nested, Is.False); Assert.That(replaced, Is.False); Assert.That(h.Reward.Result.Diamonds, Is.EqualTo(5)); AssertOne(h);
        }
    }

    [TestCase(PopupStoreGradeTier.S, 100)]
    [TestCase(PopupStoreGradeTier.SS, 200)]
    [TestCase(PopupStoreGradeTier.SSS, 300)]
    public void ThreeDigitR1PreviewFitsExistingLabelsAndPreservesUnpaidNoticeAndConfirm(PopupStoreGradeTier tier, int amount)
    {
        using (var h = new Host(new Source(997)))
        {
            h.Finish(tier); h.Show(); Assert.That(h.Preview.TryReveal(), Is.True); h.Preview.TickPreview(1);
            Assert.That(h.Value.text, Is.EqualTo("R1 검수 보상 +" + amount + "D")); Assert.That(h.Notice.text, Is.EqualTo(Notice));
            h.Value.ForceMeshUpdate(true); h.Notice.ForceMeshUpdate(true);
            string metrics = "value=" + h.Value.rectTransform.rect + " preferred=" + h.Value.preferredWidth + "x" + h.Value.preferredHeight
                + " font=" + h.Value.fontSize + " maxVisible=" + h.Value.maxVisibleCharacters + " overflowMode=" + h.Value.overflowMode
                + " autosize=" + h.Value.enableAutoSizing + " notice=" + h.Notice.rectTransform.rect + " preferred=" + h.Notice.preferredWidth + "x" + h.Notice.preferredHeight
                + " font=" + h.Notice.fontSize + " valueOverflow=" + h.Value.isTextOverflowing + " noticeOverflow=" + h.Notice.isTextOverflowing;
            Assert.That(h.Value.isTextOverflowing, Is.False, metrics); Assert.That(h.Notice.isTextOverflowing, Is.False, metrics);
            Assert.That(h.Value.preferredWidth, Is.LessThanOrEqualTo(h.Value.rectTransform.rect.width + 1));
            Assert.That(h.Box.DisplayedSprite.name, Is.EqualTo("PopupRewardBox_" + tier + "_APPROVED"));
            Assert.That((float)Read(h.Preview, "_duration"), Is.EqualTo(.45f));
            Assert.That(h.Value.raycastTarget || h.Notice.raycastTarget, Is.False);
            AssertOne(h); h.Confirm(); Assert.That(h.Panel.ConfirmationCount, Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InterruptedOrFinishedSssRevealReentersWithSame300AndNoNewDecisionOrAnimation(bool finished)
    {
        using (var h = new Host(new Source(997)))
        {
            h.Finish(PopupStoreGradeTier.SSS); h.Show(); var decision = h.Reward.Result; var final = h.Grade.FinalResult;
            Assert.That(h.Preview.TryReveal(), Is.True); h.Preview.TickPreview(finished ? 1f : .1f);
            h.Hide(); h.Reopen();
            for (int i = 0; i < 10; i++)
            {
                h.PublishFinalized(); h.Reward.Refresh(); h.Preview.Refresh();
                Assert.That(h.Preview.TryReveal(), Is.False);
            }
            Assert.That(h.Reward.Result, Is.SameAs(decision)); Assert.That(h.Grade.FinalResult, Is.SameAs(final));
            Assert.That(h.Preview.IsAnimating, Is.False); Assert.That(h.Preview.RevealCount, Is.EqualTo(1));
            Assert.That(h.Value.text, Is.EqualTo("R1 검수 보상 +300D")); Assert.That(h.Notice.text, Is.EqualTo(Notice));
            Assert.That(h.Grade.FinalizationCount, Is.EqualTo(1)); AssertOne(h); h.Confirm();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalReplacementKeepsR1ButCannotBypassOneUseAdmission(bool abandon)
    {
        using (var h = new Host(new Source(0)))
        {
            var authority = PopupStoreApprovedTestSession.Attach(h.Root, 1); h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C);
            string oldId = h.Grade.SessionId;
            if (abandon)
            {
                Assert.That(h.Root.GetComponent<UIPopupStore>().TryRequestExit(), Is.True);
                Assert.That(h.Root.GetComponent<PopupStoreExitConfirmation>().TryConfirmAbandon(), Is.True);
            }
            else { h.Call("End"); h.Show(); Assert.That(h.Preview.TryReveal(), Is.True); h.Preview.TickPreview(1); h.Confirm(); }
            h.Entry.onClick.Invoke(); var next = (UIPopupStore)h.Navigation.FirstView;
            Assert.That(next.gameObject, Is.Not.SameAs(h.Root));
            var nextReward = next.GetComponent<PopupStoreRewardSession>(); var nextStart = next.GetComponent<PopupStoreStartController>();
            Assert.That(Read(nextReward, "_table"), Is.SameAs(Authored())); Assert.That(nextStart.Host, Is.SameAs(authority));
            Assert.That(nextStart.RequestStart(), Is.False); Assert.That(next.GetComponent<PopupStoreBusinessClock>().State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(nextStart.Host.Access.Remaining, Is.Zero); Assert.That(authority.LocalStartCount, Is.EqualTo(1));
            Assert.That(nextReward.Result, Is.Null); Assert.That(nextReward.HasPolicySnapshot, Is.False);
            Assert.That(next.GetComponent<PopupStoreGradeSession>().SessionId, Is.Not.EqualTo(oldId));
            Assert.That(next.GetComponent<PopupStoreRewardRevealPreview>().IsRevealed, Is.False);
        }
    }

    [Test]
    public void ForeignFinalResultAndOldCallbacksCannotContaminateANewBusinessDecision()
    {
        using (var first = new Host(new Source(997)))
        using (var next = new Host(new Source(0)))
        {
            first.Finish(PopupStoreGradeTier.C); next.Finish(PopupStoreGradeTier.S);
            var original = next.Reward.Result;
            Error(); Assert.That(next.Reward.TryDecide(first.Grade.FinalResult), Is.False);
            first.PublishFinalized(); first.Reward.Refresh();
            Assert.That(next.Reward.Result, Is.SameAs(original)); Assert.That(original.Diamonds, Is.EqualTo(10));
            Assert.That(original.SessionId, Is.Not.EqualTo(first.Reward.Result.SessionId));
            Assert.That(original.DecisionId, Is.Not.EqualTo(first.Reward.Result.DecisionId));
            Assert.That(next.Reward.TryDecide(), Is.True); AssertOne(first); AssertOne(next);
        }
    }

    [Test]
    public void RuntimePreviewPaymentAndPolicyBoundaryHasNoFixedReviewRollOrActualPaymentCalls()
    {
        string folder = "Assets/Scripts/UI/Floor3UI/";
        foreach (string name in new[] { "PopupStoreRewardTable", "PopupStoreRewardDecision", "PopupStoreRewardSession", "PopupStoreRewardRevealPreview" })
        {
            string source = File.ReadAllText(folder + name + ".cs");
            foreach (string forbidden in new[] { "Backend.", "UserInfo.", "PlayerPrefs.", "UnityEngine.Random.Range(", "UnityEngine.Random.value", "new PopupStoreRewardTransaction", "new PopupStoreBusinessStartFakeRepository" })
                Assert.That(source, Does.Not.Contain(forbidden), name + " must not invoke " + forbidden);
        }
        string preview = File.ReadAllText(folder + "PopupStoreRewardRevealPreview.cs");
        int playerStart = preview.LastIndexOf("#else", StringComparison.Ordinal);
        string player = preview.Substring(playerStart);
        Assert.That(player, Does.Contain("TryReveal() { DisablePreviewForPlayer(); return false; }"));
        Assert.That(player, Does.Not.Contain("R1 검수 보상").And.Not.Contain("NextInt").And.Not.Contain("TryDecide"));
    }

    private static void AssertOne(Host h)
    {
        Assert.That(h.Source.Calls, Is.EqualTo(1)); Assert.That(h.Source.UpperBound, Is.EqualTo(1000));
        Assert.That(h.Reward.RandomDrawCount, Is.EqualTo(1)); Assert.That(h.Reward.DecisionCount, Is.EqualTo(1));
        Assert.That(h.Reward.IsResolved, Is.True); Assert.That(h.Reward.LastError, Is.Null);
    }
    private static void AssertFailedOneAttempt(Host h)
    {
        for (int i = 0; i < 3; i++) { h.PublishFinalized(); Assert.That(h.Reward.TryDecide(), Is.False); Assert.That(h.Reward.Refresh(), Is.False); }
        Assert.That(h.Reward.Result, Is.Null); Assert.That(h.Reward.DecisionCount, Is.Zero);
        Assert.That(h.Source.Calls, Is.EqualTo(1)); Assert.That(h.Source.UpperBound, Is.EqualTo(1000));
        Assert.That(h.Reward.RandomDrawCount, Is.EqualTo(1)); Assert.That(h.Reward.TrySetRandomSource(new Source(0)), Is.False);
    }
    private sealed class Source : IPopupStoreRewardRandomSource
    {
        private readonly Func<int> _next;
        internal int Calls, UpperBound;
        internal Source(int roll) : this(() => roll) { }
        internal Source(Func<int> next) { _next = next; }
        public int NextInt(int exclusiveUpperBound) { Calls++; UpperBound = exclusiveUpperBound; return _next(); }
    }
    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        internal readonly Source Source;
        internal Host(Source source)
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
            Source = source;
            try { Assert.That(Read(Reward, "_table"), Is.SameAs(Authored())); Assert.That(Reward.TrySetRandomSource(source), Is.True); Assert.That(Reward.Refresh(), Is.True); }
            catch { Dispose(); throw; }
        }
        internal GameObject Root => (GameObject)Read(_inner, "Root");
        internal PopupStoreRewardSession Reward => Root.GetComponent<PopupStoreRewardSession>();
        internal PopupStoreRewardRevealPreview Preview => Root.GetComponent<PopupStoreRewardRevealPreview>();
        internal PopupStoreRewardBox Box => Root.GetComponent<PopupStoreRewardBox>();
        internal PopupStoreGradeSession Grade => Root.GetComponent<PopupStoreGradeSession>();
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal PopupStoreScore Score => Root.GetComponent<PopupStoreScore>();
        internal PopupStorePacking Packing => Root.GetComponent<PopupStorePacking>();
        internal PopupStoreResultPanel Panel => Root.GetComponent<PopupStoreResultPanel>();
        internal MobileUINavigation Navigation => (MobileUINavigation)Read(_inner, "Navigation");
        internal UIFloor3 Floor => (UIFloor3)Read(_inner, "Floor");
        internal Button Entry => (Button)Read(_inner, "Entry");
        internal TMP_Text Value => (TMP_Text)Read(Preview, "_valueText");
        internal TMP_Text Notice => (TMP_Text)Read(Preview, "_noticeText");
        internal object Call(string method, params object[] args) => Invoke(_inner, method, args);
        internal void Start() => Call("Start", 90d);
        internal void Finish(PopupStoreGradeTier tier)
        { Start(); if (tier != PopupStoreGradeTier.None) Call("SellUntil", tier); Call("End"); Assert.That(Grade.FinalResult.Grade, Is.EqualTo(tier)); }
        internal void Show(bool rewardValid = true)
        { Call("ShowResult"); Panel.TickResult(1); if (Grade.FinalResult.Grade != PopupStoreGradeTier.None) Assert.That(Preview.Refresh(), Is.EqualTo(rewardValid)); }
        internal void Hide() { Call("Close"); Invoke(Preview, "OnDisable"); }
        internal void Reopen(bool rewardValid = true)
        { Call("Reopen"); Assert.That(Reward.Refresh(), Is.EqualTo(rewardValid)); Panel.TickResult(0); Preview.Refresh(); }
        internal void Confirm()
        { Assert.That(Panel.TryConfirm(), Is.True); Call("DisableObservers"); Invoke(Preview, "OnDisable"); Assert.That(Navigation.FirstView, Is.SameAs(Floor)); }
        internal void PublishFinalized() => ((Action<PopupStoreGradeResult>)Read(Grade, "Finalized"))?.Invoke(Grade.FinalResult);
        public void Dispose()
        { if (Root != null && Reward != null) Invoke(Reward, "OnDestroy"); ((IDisposable)_inner).Dispose(); }
    }
    private static PopupStoreRewardTable Authored() => AssetDatabase.LoadAssetAtPath<PopupStoreRewardTable>(R1Path);
    private static void Error() => LogAssert.Expect(LogType.Error, new Regex("^PopupStoreRewardSession: local preview not decided\\."));
    private static string AccountState() => string.Join("|", new object[] { UserInfo.Dia, UserInfo.Money, UserInfo.TotalCumulativeCustomerCount,
        UserInfo.CustomerEncounterContextGeneration, UserInfo.DailyAddMoney, UserInfo.DailyCookCount, UserInfo.DailyCumulativeCustomerCount,
        UserInfo.DailyAdvertisingViewCount, UserInfo.DailyCleanCount, UserInfo.DailyAdGoldRewardCount, UserInfo.DailyAdDiaRewardCount });
    private static object ReadStatic(Type type, string name) => Field(type, name).GetValue(null);
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        { var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; }
        throw new MissingFieldException(type.FullName, name);
    }
    private static object Invoke(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
}
#endif
