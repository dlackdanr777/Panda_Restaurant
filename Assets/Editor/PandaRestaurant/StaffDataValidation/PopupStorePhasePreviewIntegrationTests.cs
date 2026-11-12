#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual prefab, admission, unordered packing, receipt visuals and release; no live account or Scene creation.</summary>
public sealed class PopupStorePhasePreviewIntegrationTests
{
    [Test]
    public void RealPackJustBeforeDeadlineSecuresCAndLaterTimeCannotCancelTheReceipt()
    {
        using (var h = new Host())
        {
            h.Ready();
            h.Clock.AdvanceTime(21.99);
            var receipt = h.Pack();
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None),
                "The new preview commits a cleared grade at the deadline, not when the target is reached.");
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            h.Clock.AdvanceTime(100);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Packing.IsCommittedReceipt(receipt), Is.True);
            h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
            h.AssertNoR1();
        }
    }

    [Test]
    public void RealPackAtDeadlineIsRejectedWithoutReceiptScoreOrCounterConsumption()
    {
        using (var h = new Host())
        {
            h.Ready();
            foreach (var entry in h.Entries(0).Reverse()) Assert.That(h.Counter.TryAdd(entry), Is.True);
            h.Clock.AdvanceTime(22);
            Assert.That(h.Packing.TryPack().Success, Is.False);
            Assert.That(h.Counter.Count, Is.EqualTo(3));
            Assert.That(h.Packing.CommittedReceipts.Count, Is.Zero);
            Assert.That(h.Preview.AcceptedReceiptCount, Is.Zero);
            Assert.That(h.Score.TotalScore, Is.Zero);
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.AssertNoR1();
        }
    }

    [TestCase(PopupStoreGradeTier.C)] [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)] [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)] [TestCase(PopupStoreGradeTier.SSS)]
    public void RealOrderSourceIncludesLastUnlockedPlushAndAllThreeBoxAndRibbonColors(PopupStoreGradeTier grade)
    {
        using (var h = new Host())
        {
            while (h.Preview.Session.CurrentPhase.Grade != grade) { h.ClearPhase(); h.NextPhase(); }
            Assert.That(h.Catalog.TryGetMaterials(PopupStoreMaterialKind.Box, out var boxes, out _), Is.True);
            Assert.That(h.Catalog.TryGetMaterials(PopupStoreMaterialKind.Ribbon, out var ribbons, out _), Is.True);
            for (int color = 0; color < 3; color++)
            {
                int chosenColor = color;
                var source = new PopupStorePhasePreviewOrderSource(h.Catalog,
                    new[] { "CUSTOMER01", "CUSTOMER04", "CUSTOMER11" }, max => max == 3 ? chosenColor : max - 1);
                Assert.That(source.TryCreate(h.Preview.Session, color, out var order), Is.True);
                Assert.That(order.TryResolve(h.Catalog, out var entries, out _), Is.True);
                Assert.That(entries.Last().Id, Is.EqualTo(ribbons[color].Id));
                Assert.That(entries[entries.Length - 2].Id, Is.EqualTo(boxes[color].Id));
                foreach (var entry in entries.Take(entries.Length - 2))
                    Assert.That(entry.Id, Is.EqualTo(h.Preview.Session.CurrentPhase.AllowedPlushIds.Last()));
                Assert.That(source.Owns(h.Preview.Session, order), Is.True);
            }
        }
    }

    [TestCase(PopupStoreGradeTier.C, 100, 0, 0)]
    [TestCase(PopupStoreGradeTier.B, 70, 30, 0)]
    [TestCase(PopupStoreGradeTier.A, 40, 60, 0)]
    [TestCase(PopupStoreGradeTier.S, 0, 70, 30)]
    [TestCase(PopupStoreGradeTier.SS, 0, 50, 50)]
    [TestCase(PopupStoreGradeTier.SSS, 0, 20, 80)]
    public void EveryOrderRollMatchesTheExplicitProposal(PopupStoreGradeTier grade, int one, int two, int three)
    {
        var counts = new int[3];
        for (int i = 0; i < 100; i++) counts[PopupStorePhasePreviewOrderSource.PlushCount(grade, i) - 1]++;
        CollectionAssert.AreEqual(new[] { one, two, three }, counts);
    }

    [TestCase(-1)] [TestCase(100)]
    public void InvalidOrderRollIsNotSilentlyClamped(int roll)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PopupStorePhasePreviewOrderSource.PlushCount(PopupStoreGradeTier.C, roll));

    [Test]
    public void TheAuthoredPrefabDoesNotEnablePreviewOrChangeNinetySecondDefaults()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Host.Prefab);
        Assert.That(prefab.GetComponent<PopupStorePhasePreview>(), Is.Null);
        Assert.That(Read(prefab.GetComponent<PopupStoreBusinessClock>(), "_initialSeconds"), Is.EqualTo(90d));
        Assert.That(Read(prefab.GetComponent<PopupStoreBusinessClock>(), "_entryCutoffSeconds"), Is.EqualTo(10d));
    }

    [Test]
    public void FreshIsolatedApprovalStartsExactlyOneCPhaseAndOneBusinessUse()
    {
        using (var h = new Host())
        {
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1)); Assert.That(h.Admission.BindingCount, Is.EqualTo(1));
            Assert.That(h.Admission.Access.Remaining, Is.Zero);
            Assert.That(h.Preview.Initialize(h.Profile, out _), Is.False);
            Assert.That(h.Preview.TryStart(), Is.False);
            Assert.That(h.Grade.FinalResult, Is.Null); Assert.That(h.Reward.RandomDrawCount, Is.Zero);
            Assert.That(h.PlushCount, Is.EqualTo(5));
        }
    }

    [Test]
    public void FirstRealReverseOrderReceiptClearsCButWaitsForActualDeliveryAndRelease()
    {
        using (var h = new Host())
        {
            h.Ready(); var receipt = h.Pack();
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Preview.AcceptCommitted(receipt), Is.False);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing), "A reached target cannot end the stage early.");
            Assert.That(h.Preview.CanPlay, Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.False);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Packing.TryPack().Success, Is.False);
            Assert.That(h.Counter.TryAdd(h.Material("STAFF01")), Is.False);
            h.Deliver();
            Assert.That(h.Preview.TickCleanup(), Is.False, "Visual completion alone is not customer release.");
            h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Clock.EndedCount, Is.Zero); Assert.That(h.Grade.FinalizationCount, Is.Zero);
            Assert.That(h.Preview.CleanupCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void NoticeAndThreeTwoOneUseSeparateInputsThenBStartsWithFreshTimeAndEightPlushes()
    {
        using (var h = new Host())
        {
            h.ClearPhase();
            double elapsed = h.Clock.ElapsedSeconds;
            h.NextPhase();
            Assert.That(h.Preview.Session.CurrentPhase.Grade, Is.EqualTo(PopupStoreGradeTier.B));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(27)); Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(h.PlushCount, Is.EqualTo(8)); Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Preview.Session.PhaseSuccessfulOrders, Is.Zero);
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void OldReceiptAndForgedReceiptCannotCountInTheNewPhase()
    {
        using (var h = new Host())
        {
            h.ClearPhase(); var receipt = h.Packing.LastReceipt; h.NextPhase();
            Assert.That(h.Preview.AcceptCommitted(receipt), Is.False);
            var forged = new PopupStorePackingReceipt(receipt.OrderId, receipt.SlotIndex, receipt.CustomerId,
                new[] { h.Material("STAFF01"), h.Material("BOX_PINK"), h.Material("RIBBON_PINK") });
            Assert.That(h.Preview.AcceptCommitted(forged), Is.False);
            Assert.That(h.Preview.Session.TotalSuccessfulOrders, Is.EqualTo(1));
        }
    }

    [Test]
    public void DuplicatePlushQuantityAndLeftmostTiePriorityArePreservedInB()
    {
        using (var h = new Host())
        {
            h.ClearPhase(); h.NextPhase(); h.Ready();
            Assert.That(h.Order(0).PlushIds.Count, Is.EqualTo(2));
            var entries = h.Entries(0).Reverse().ToArray();
            foreach (var e in entries.Take(entries.Length - 1)) Assert.That(h.Counter.TryAdd(e), Is.True);
            Assert.That(h.Packing.TryPack().Success, Is.False); Assert.That(h.Counter.Count, Is.EqualTo(3));
            Assert.That(h.Counter.TryAdd(entries.Last()), Is.True);
            var result = h.Packing.TryPack(); Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.Zero);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            Assert.That(h.Preview.Session.PhaseSuccessfulOrders, Is.EqualTo(1));
            Assert.That(h.Score.TotalScore, Is.EqualTo(240));
        }
    }

    [Test]
    public void CTimeoutHasNoSecuredBoxAndNoR1FinalizationOrDraw()
    {
        using (var h = new Host())
        {
            h.Ready(); h.Clock.AdvanceTime(22);
            Assert.That(h.Preview.Session.PendingEndReason, Is.EqualTo(PopupStorePhaseEndReason.TimedOut));
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.ShowResult();
            Assert.That(h.Panel.EditorShownPhaseResult, Is.SameAs(h.Preview.Session.FinalResult));
            Assert.That(h.Root.GetComponent<PopupStoreRewardBox>().BoxImage.enabled, Is.False);
            h.AssertNoR1();
        }
    }

    [Test]
    public void IntermediateTimeoutUsesPreviouslyClearedBoxInsteadOfScoreGrade()
    {
        using (var h = new Host())
        {
            h.ClearPhase(); h.NextPhase(); h.ClearPhase(); h.NextPhase();
            Assert.That(h.Preview.Session.CurrentPhase.Grade, Is.EqualTo(PopupStoreGradeTier.A));
            Assert.That(h.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.C), "Score grade remains an independent statistic.");
            h.Clock.AdvanceTime(30); Assert.That(h.Preview.TickCleanup(), Is.True);
            h.ShowResult();
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.B));
            var summaryBox = h.Root.GetComponent<PopupStoreRewardBox>();
            Assert.That(summaryBox.BoxImage.enabled, Is.True,
                "DEV-GAME-02B-4 previews the secured box without triggering the separate opening flow.");
            Assert.That(summaryBox.BoxImage.sprite, Is.SameAs(h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true)
                .SpriteFor(PopupStoreGradeTier.B)));
            Assert.That(summaryBox.BoxImage.raycastTarget, Is.False);
            Assert.That(summaryBox.AnimationCount, Is.Zero);
            Assert.That(h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true).SpriteFor(h.Preview.SecuredGrade).name,
                Is.EqualTo("PopupRewardBox_B_APPROVED"));
            Assert.That(h.Panel.ShownResult, Is.Null, "No fabricated normal score-grade result.");
            h.AssertNoR1();
        }
    }

    [Test]
    public void AllSixRealPhasesHaveOneApprovalOneFinalResultAndNoSeventhCountdown()
    {
        using (var h = new Host())
        {
            string business = h.Preview.Session.BusinessSessionId;
            foreach (var phase in h.Profile.Phases)
            {
                Assert.That(h.Preview.Session.CurrentPhase.Grade, Is.EqualTo(phase.Grade));
                Assert.That(h.PlushCount, Is.EqualTo(phase.AllowedPlushIds.Count));
                h.ClearPhase();
                if (phase.Grade != PopupStoreGradeTier.SSS) h.NextPhase();
            }
            h.Clock.AdvanceTime(1);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Completed));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(h.Preview.AcceptedReceiptCount, Is.EqualTo(15));
            Assert.That(h.Preview.CleanupCount, Is.EqualTo(6));
            Assert.That(h.Preview.Session.PhaseGeneration, Is.EqualTo(6));
            Assert.That(h.Preview.Session.BusinessSessionId, Is.EqualTo(business));
            Assert.That(h.Admission.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(15));
            Assert.That(h.Entrance.EditorReleasedReceipts.Count, Is.EqualTo(15));
            for (int i = 0; i < 5; i++) h.Clock.AdvanceTime(100);
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            h.ShowResult(); h.AssertNoR1();
        }
    }

    [Test]
    public void PhaseCleanupDoesNotExpireUnfinishedOrdersOrChargeComplaintPenalty()
    {
        using (var h = new Host())
        {
            h.Ready(); var untouched = new[] { h.Order(1), h.Order(2) };
            h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            foreach (var order in untouched) { Assert.That(order.IsExpired, Is.False); Assert.That(order.IsPacked, Is.False); }
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.Zero); Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Packing.CommittedReceipts.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void AbandonWaitsForCommittedDeliveryThenShowsNoSecuredReward()
    {
        using (var h = new Host())
        {
            h.Ready(); h.Pack();
            Assert.That(h.Preview.RequestAbandon(), Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.False);
            h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
            Assert.That(h.Preview.Session.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None),
                "A stage abandoned before its deadline was not cleared merely by reaching its target.");
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Preview.RequestAbandon(), Is.False); h.ShowResult(); h.AssertNoR1();
        }
    }

    [Test]
    public void PhaseInputAndHudRemainFrozenDuringNestedPauseAndCountdown()
    {
        using (var h = new Host())
        {
            h.ClearPhase(); h.Clock.SetApplicationPaused(true); h.Clock.SetApplicationFocused(false);
            h.Clock.AdvanceTime(50); Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.ClearNotice));
            h.Clock.SetApplicationPaused(false); Assert.That(h.Preview.Session.IsPaused, Is.True);
            h.Clock.SetApplicationFocused(true); h.Clock.AdvanceTime(1);
            Assert.That(h.Preview.PresentationCue, Is.EqualTo("Stage"));
            h.Clock.AdvanceTime(100);
            Assert.That(h.Preview.Session.CountdownValue, Is.EqualTo(3));
            h.Preview.RenderHud();
            var hud = h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true);
            Assert.That(hud.DisplayedGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(hud.NextGradeText.text, Is.EqualTo("확보 C상자"));
            Assert.That(hud.EditorPhaseProgressText.text, Is.EqualTo("다음 단계 · 3"));
            Assert.That(h.Counter.TryAdd(h.Material("STAFF01")), Is.False);
        }
    }

    [Test]
    public void OnlyVisibleUnlockedMaterialsCanBeAddedThroughDirectCounterApi()
    {
        using (var h = new Host())
        {
            Assert.That(h.Counter.TryAdd(h.Material("STAFF06")), Is.False);
            Assert.That(h.Counter.TryAdd(h.Material("STAFF05")), Is.True);
            h.Counter.Clear();
            foreach (var kind in new[] { PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon })
            {
                Assert.That(h.Catalog.TryGetMaterials(kind, out var entries, out _), Is.True);
                foreach (var e in entries) { Assert.That(h.Counter.TryAdd(e), Is.True); h.Counter.Clear(); }
            }
        }
    }

    [Test]
    public void RepeatedResultTicksDoNotCreateNormalResultRevealOrAdditionalPhaseFinalization()
    {
        using (var h = new Host())
        {
            h.Clock.AdvanceTime(22); h.Preview.TickCleanup(); h.ShowResult();
            var final = h.Preview.Session.FinalResult;
            for (int i = 0; i < 10; i++) h.Panel.TickResult(1);
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(final));
            Assert.That(h.Panel.PresentationCount, Is.EqualTo(1)); Assert.That(h.Panel.AppearanceCount, Is.EqualTo(1));
            Assert.That(h.Root.GetComponent<PopupStoreRewardBox>().AnimationCount, Is.Zero);
            h.AssertNoR1();
        }
    }

    internal sealed class Host : IDisposable
    {
        internal const string Prefab = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
        internal readonly GameObject Root;
        internal readonly PopupStorePhasePreview Preview;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreScore Score;
        internal readonly PopupStoreGradeSession Grade;
        internal readonly PopupStoreRewardSession Reward;
        internal readonly PopupStoreResultPanel Panel;
        internal readonly PopupStoreMaterialCatalog Catalog;
        internal readonly PopupStoreAdmissionHost Admission;
        internal readonly PopupStorePhaseProfileSnapshot Profile;
        internal int PlushCount => Root.GetComponentsInChildren<PopupStoreMaterialList>(true)
            .Single(x => (PopupStoreMaterialKind)Read(x, "_kind") == PopupStoreMaterialKind.Plush).MaterialCount;
        internal Host(bool completeIntroduction = true, bool requestStart = true,
            PopupStorePhaseProfileSnapshot profile = null, PopupStoreScorePolicySnapshot scoring = null,
            Func<int, int> orderRandom = null)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            try
            {
                Root.name = "phase-preview-integration-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                Clock = Root.GetComponent<PopupStoreBusinessClock>(); Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Packing = Root.GetComponent<PopupStorePacking>(); Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Orders = Root.GetComponent<PopupStoreOrderDisplay>(); Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Score = Root.GetComponent<PopupStoreScore>(); Grade = Root.GetComponent<PopupStoreGradeSession>();
                Reward = Root.GetComponent<PopupStoreRewardSession>(); Panel = Root.GetComponent<PopupStoreResultPanel>();
                Catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
                var view = Root.GetComponent<UIPopupStore>(); view.Init(); view.Show();
                Admission = PopupStoreApprovedTestSession.Attach(Root, 1);
                Assert.That(Counter.Refresh(), Is.True, "counter setup");
                foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true))
                    Assert.That(list.Refresh(), Is.True, "material setup");
                Assert.That(Orders.Refresh(), Is.True, "orders setup");
                Assert.That(Root.GetComponent<PopupStoreOrderProgress>().Refresh(), Is.True, "progress setup");
                Assert.That(Packing.Refresh(), Is.True, "packing setup");
                Assert.That(Score.Refresh(), Is.True, "score setup");
                Assert.That(Grade.Refresh(), Is.True, "grade setup");
                Assert.That(Entrance.BeginVisibleSession(), Is.True, "entrance setup"); Assert.That(Presentation.BeginVisibleSession(), Is.True, "presentation setup");
                string error;
                if (profile == null)
                    Assert.That(PopupStorePhasePreviewSettings.CreateProposalV0().TryCreateSnapshot(Catalog, out Profile, out error), Is.True, error);
                else Profile = profile;
                Preview = Root.AddComponent<PopupStorePhasePreview>();
                Preview.OrderRandomOverride = orderRandom ?? (max => max == 100 ? 99 : 0);
                Assert.That(Preview.Initialize(Profile, scoring ?? PopupStoreScorePolicySnapshot.Ordinary, out error), Is.True, error);
                if (requestStart)
                {
                    Assert.That(Root.GetComponent<PopupStoreStartController>().RequestStart(), Is.True,
                        "start: " + Preview.LastError + " / " + Root.GetComponent<PopupStoreStartController>().LastError);
                    Assert.That(Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
                    if (completeIntroduction) CompleteInitialPresentation(Preview, Clock);
                }
            }
            catch { Dispose(); throw; }
        }
        internal PopupStoreMaterialEntry Material(string id)
        {
            foreach (PopupStoreMaterialKind kind in Enum.GetValues(typeof(PopupStoreMaterialKind)))
                if (Catalog.TryGetMaterials(kind, out var entries, out _)) foreach (var entry in entries) if (entry.Id == id) return entry;
            throw new InvalidOperationException(id);
        }
        internal PopupStoreOrder Order(int slot) { Assert.That(Orders.TryGetEntryBinding(slot, out var order, out _), Is.True); return order; }
        internal PopupStoreMaterialEntry[] Entries(int slot) { Assert.That(Order(slot).TryResolve(Catalog, out var entries, out _), Is.True); return entries; }
        internal void Ready()
        { Entrance.TickEntrance(0); Entrance.TickEntrance(Preview.EntryInterval); Entrance.TickEntrance(Preview.EntryInterval); Entrance.TickEntrance(1.3f); }
        internal PopupStorePackingReceipt Pack()
        {
            int slot = Enumerable.Range(0, 3).First(i => Entrance.IsWaiting(i));
            foreach (var e in Entries(slot).Reverse()) Assert.That(Counter.TryAdd(e), Is.True);
            var result = Packing.TryPack(); Assert.That(result.Success, Is.True, result.Status.ToString()); return result.Receipt;
        }
        internal void Deliver()
        {
            Presentation.TickPresentation(0);
            for (int i = 0; i < 500 && Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Idle; i++) Presentation.TickPresentation(.02f);
            Assert.That(Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Idle));
        }
        internal void ClearPhase()
        {
            Ready(); int target = Preview.Session.CurrentPhase.TargetSuccessfulOrders;
            for (int i = 0; i < target; i++)
            {
                if (!Enumerable.Range(0, 3).Any(Entrance.IsWaiting)) Ready();
                Pack(); Deliver(); Entrance.TickEntrance(2);
            }
            Assert.That(Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing),
                "Every preview stage keeps playing after reaching its goal until the deadline.");
            Clock.AdvanceTime(Clock.RemainingSeconds);
            Assert.That(Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(Preview.TickCleanup(), Is.True, Preview.LastError);
            Assert.That(Preview.PresentationCue, Is.EqualTo("Success"));
        }
        internal void NextPhase() => CompleteNextPresentation(Preview, Clock);
        internal void ShowResult()
        {
            CompleteFailurePresentation(Preview, Clock);
            Panel.TickResult(1); Panel.TickResult(1); Assert.That(Panel.IsVisible, Is.True);
        }
        internal void AssertNoR1()
        { Assert.That(Grade.FinalizationCount, Is.Zero); Assert.That(Grade.FinalResult, Is.Null); Assert.That(Reward.Result, Is.Null); Assert.That(Reward.RandomDrawCount, Is.Zero); Assert.That(Clock.EndedCount, Is.Zero); }
        public void Dispose()
        { if (Root != null) { Entrance?.PauseVisibleSession(); Presentation?.EndVisibleSession(); Object.DestroyImmediate(Root); } }
    }

    // Production adapter gates are driven explicitly by the fixture. These helpers never fake
    // receipts, change core state, skip a visible cue, or spend a following stage's play time.
    internal static void CompleteInitialPresentation(PopupStorePhasePreview preview, PopupStoreBusinessClock clock)
    {
        Assert.That(preview.PresentationCue, Is.EqualTo("Stage"));
        Assert.That(preview.Session.State, Is.EqualTo(PopupStorePhaseState.Ready));
        double elapsed = clock.ElapsedSeconds;
        foreach (var cue in new[] { "3", "2", "1", "GameStart", "" })
        {
            Assert.That(preview.CanPlay, Is.False);
            Assert.That(clock.CanCommitPacking, Is.False);
            clock.AdvanceTime(100);
            Assert.That(preview.PresentationCue, Is.EqualTo(cue));
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(elapsed));
        }
        Assert.That(preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(preview.CanPlay, Is.True);
        Assert.That(clock.RemainingSeconds, Is.EqualTo(preview.Session.CurrentPhase.TimeLimitSeconds));
    }

    internal static void CompleteNextPresentation(PopupStorePhasePreview preview, PopupStoreBusinessClock clock)
    {
        Assert.That(preview.PresentationCue, Is.EqualTo("Success"));
        int phaseNumber = preview.Session.CurrentPhase.PhaseNumber;
        double elapsed = clock.ElapsedSeconds;
        foreach (var cue in new[] { "Stage", "3", "2", "1", "GameStart", "" })
        {
            Assert.That(preview.CanPlay, Is.False);
            Assert.That(clock.CanCommitPacking, Is.False);
            clock.AdvanceTime(100);
            Assert.That(preview.PresentationCue, Is.EqualTo(cue));
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(elapsed));
        }
        Assert.That(preview.Session.CurrentPhase.PhaseNumber, Is.EqualTo(phaseNumber + 1));
        Assert.That(preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
        Assert.That(preview.CanPlay, Is.True);
        Assert.That(clock.RemainingSeconds, Is.EqualTo(preview.Session.CurrentPhase.TimeLimitSeconds));
    }

    internal static void CompleteFailurePresentation(PopupStorePhasePreview preview, PopupStoreBusinessClock clock)
    {
        if (preview.PresentationCue == "GameOver")
        {
            Assert.That(preview.IsResultPresentationReady, Is.False);
            Assert.That(preview.CanPlay, Is.False);
            clock.AdvanceTime(100);
            Assert.That(preview.PresentationCue, Is.Empty);
        }
        Assert.That(preview.IsResultPresentationReady, Is.True);
    }
    private static object Read(object obj, string field) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
}
#endif
