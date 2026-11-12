#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real navigation and authored controls around an isolated phase run; no Scene or live account.</summary>
public sealed class PopupStorePhasePreviewNavigationTests
{
    [Test]
    public void RepeatedCloseAndContinueKeepOneModalTheSamePhaseOrdersAndCounter()
    {
        using (var h = new Harness())
        {
            h.Ready();
            var first = h.Order(0);
            Assert.That(h.Counter.TryAdd(h.Entries(0)[0]), Is.True);
            double remaining = h.Clock.RemainingSeconds;
            string phase = h.Preview.Session.PhaseRunId;
            Assert.That(h.View.TryRequestExit(), Is.True);
            Assert.That(h.View.TryRequestExit(), Is.True);
            Assert.That(h.Exit.ShowCount, Is.EqualTo(1));
            Assert.That(h.Preview.Session.IsPaused, Is.True);
            h.Clock.AdvanceTime(20);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(h.Counter.TryAdd(h.Entries(0)[1]), Is.False);
            Assert.That(h.Packing.TryPack().Success, Is.False);
            Assert.That(h.Exit.TryContinue(), Is.True);
            Assert.That(h.Exit.TryContinue(), Is.False);
            Assert.That(h.Exit.ContinueCount, Is.EqualTo(1));
            Assert.That(h.Preview.Session.IsPaused, Is.False);
            Assert.That(h.Preview.Session.PhaseRunId, Is.EqualTo(phase));
            Assert.That(h.Order(0), Is.SameAs(first));
            Assert.That(h.Counter.Count, Is.EqualTo(1));
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void ContinueClearsOnlyTheModalPauseAndPreservesFocusLoss()
    {
        using (var h = new Harness())
        {
            Assert.That(h.View.TryRequestExit(), Is.True);
            h.Clock.SetApplicationFocused(false);
            Assert.That(h.Exit.TryContinue(), Is.True);
            Assert.That(h.Preview.Session.IsPaused, Is.True);
            h.Clock.AdvanceTime(100);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(22));
            h.Clock.SetApplicationFocused(true);
            Assert.That(h.Preview.Session.IsPaused, Is.False);
            h.Clock.AdvanceTime(1);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(21));
            Assert.That(h.Exit.IsVisible, Is.False);
        }
    }

    [Test]
    public void AuthoredAbandonButtonWaitsForCleanupThenItsResultConfirmsBackToTheMap()
    {
        using (var h = new Harness())
        {
            h.Ready();
            Assert.That(h.View.TryRequestExit(), Is.True);
            Assert.That(h.Exit.TryConfirmAbandon(), Is.True);
            Assert.That(h.Exit.TryConfirmAbandon(), Is.False);
            Assert.That(h.Exit.IsVisible, Is.False);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Clearing));
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View), "Abandon cannot pop before phase cleanup.");
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
            h.ShowResult();
            Assert.That(h.Root.GetComponent<PopupStoreRewardBox>().BoxImage.enabled, Is.False);
            h.ConfirmToMap();
            Assert.That(h.Preview.ConfirmationCount, Is.EqualTo(1));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void AbandonFromTheModalPreservesCommittedReceiptAndWaitsForRealRelease()
    {
        using (var h = new Harness())
        {
            h.Ready(); var receipt = h.Pack();
            Assert.That(h.View.TryRequestExit(), Is.True);
            Assert.That(h.Exit.TryConfirmAbandon(), Is.True);
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Panel.TryConfirm(), Is.False);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.View));
            h.Deliver();
            Assert.That(h.Preview.TickCleanup(), Is.False, "Visual completion still waits for actual departure.");
            h.Entrance.TickEntrance(2);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Abandoned));
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
            h.ShowResult();
            Assert.That(h.Preview.Session.FinalResult.HighestClearedGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(h.Root.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Single(text => text.name == "Result Grade").gameObject.activeSelf, Is.False);
            Assert.That(h.Root.GetComponent<PopupStoreRewardBox>().BoxImage.enabled, Is.False);
            h.ConfirmToMap(); h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void TimeoutResultReentryRetainsOneTerminalInstanceAndCannotStartAnotherBusiness()
    {
        using (var h = new Harness())
        {
            h.Clock.AdvanceTime(22); Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            var final = h.Preview.Session.FinalResult;
            string session = h.Preview.Session.BusinessSessionId;
            h.ConfirmToMap();
            Assert.That(h.Panel.TryConfirm(), Is.False);
            Assert.That(h.Preview.TryConfirmResult(), Is.False);
            Assert.That(h.Preview.ConfirmationCount, Is.EqualTo(1));
            h.Reenter(); h.ShowResult();
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(final));
            Assert.That(h.Preview.Session.BusinessSessionId, Is.EqualTo(session));
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.Preview.TryStart(), Is.False);
            Assert.That(h.Clock.RemainingSeconds, Is.Zero);
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Counter.TryAdd(h.Entries(0)[0]), Is.False);
            Assert.That(h.Panel.AppearanceCount, Is.EqualTo(1));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            h.ConfirmToMap(); h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void SecuredCResultReentryKeepsApprovedBoxScoreAndReceiptsWithoutRevealOrReplay()
    {
        using (var h = new Harness())
        {
            h.Ready(); var receipt = h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            h.NextPhase();
            Assert.That(h.Preview.Session.CurrentPhase.Grade, Is.EqualTo(PopupStoreGradeTier.B));
            h.Clock.AdvanceTime(27); Assert.That(h.Preview.TickCleanup(), Is.True); h.ShowResult();
            var final = h.Preview.Session.FinalResult;
            var box = h.Root.GetComponent<PopupStoreRewardBox>(); var sprite = box.BoxImage.sprite;
            // DEV-GAME-02B-4: the summary previews the secured box, while opening still belongs to Receive.
            Assert.That(sprite, Is.SameAs(h.Root.GetComponentInChildren<PopupStoreCurrentGradeHud>(true)
                .SpriteFor(PopupStoreGradeTier.C)));
            Assert.That(box.BoxImage.enabled, Is.True);
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.C));
            Assert.That(h.Root.GetComponent<PopupStoreRewardRevealPreview>().TryReveal(), Is.False);
            h.ConfirmToMap(); h.Reenter(); h.ShowResult();
            Assert.That(h.Panel.EditorShownPhaseResult, Is.SameAs(final));
            Assert.That(box.BoxImage.sprite, Is.SameAs(sprite));
            Assert.That(box.BoxImage.raycastTarget, Is.False);
            Assert.That(h.Panel.AppearanceCount, Is.EqualTo(1));
            Assert.That(box.AnimationCount, Is.Zero);
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Root.GetComponent<PopupStoreRewardRevealPreview>().RevealCount, Is.Zero);
            h.ConfirmToMap(); h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void TerminalCloseUsesResultConfirmationWithoutShowingAnAbandonModal()
    {
        using (var h = new Harness())
        {
            h.Clock.AdvanceTime(22); h.Preview.TickCleanup(); h.ShowResult();
            Assert.That(h.View.TryRequestExit(), Is.True);
            Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
            Assert.That(h.Exit.ShowCount, Is.Zero);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.TimedOut));
            Assert.That(h.Preview.ConfirmationCount, Is.EqualTo(1));
            Assert.That(h.View.TryRequestExit(), Is.False);
        }
    }

    [Test]
    public void CancelledCommittedVisualFaultsInsteadOfPretendingCleanupCompleted()
    {
        using (var h = new Harness())
        {
            h.Ready(); h.Pack(); h.Presentation.TickPresentation(0);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            h.Presentation.EndVisibleSession();
            LogAssert.Expect(LogType.Error, new Regex("^POPUP_STORE_PHASE_PREVIEW_BLOCKED"));
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Faulted));
            Assert.That(h.Preview.CleanupCount, Is.Zero);
            Assert.That(h.Entrance.EditorReleasedReceipts, Is.Empty);
            Assert.That(h.Preview.CanPlay, Is.False);
            Assert.That(h.Preview.SecuredGrade, Is.EqualTo(PopupStoreGradeTier.None));
            h.AssertSingleUseAndNoR1();
        }
    }

    [Test]
    public void ForeignUnpackedOrderDuringCleanupFaultsInsteadOfWaitingForever()
    {
        using (var h = new Harness())
        {
            h.Ready(); h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Write(h.Order(1), "_runtimeId", "different-phase-order");
            LogAssert.Expect(LogType.Error, new Regex("^POPUP_STORE_PHASE_PREVIEW_BLOCKED"));
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Entrance.EditorPhaseSupplyFaulted, Is.True);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Faulted));
            Assert.That(h.Preview.CleanupCount, Is.Zero);
            Assert.That(h.Preview.LastError, Is.Not.Null.And.Not.Empty);
            Assert.That(h.Counter.TryAdd(h.Entries(0)[0]), Is.False);
        }
    }

    [Test]
    public void CancelledEntryIdentityBeforeCleanupFaultsWithoutAdvancingToAnotherPhase()
    {
        using (var h = new Harness())
        {
            h.Ready(); h.Pack(); h.Deliver();
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            string phase = h.Preview.Session.PhaseRunId;
            Write(h.Order(1), "_runtimeId", "cancelled-old-phase-identity");
            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_ENTRY_CANCELLED slot=1"));
            h.Entrance.TickEntrance(2);
            Assert.That(h.Entrance.EditorPhaseSupplyFaulted, Is.True);
            LogAssert.Expect(LogType.Error, new Regex("^POPUP_STORE_PHASE_PREVIEW_BLOCKED"));
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Faulted));
            Assert.That(h.Preview.Session.PhaseRunId, Is.EqualTo(phase));
            Assert.That(h.Preview.Session.PhaseGeneration, Is.EqualTo(1));
            Assert.That(h.Preview.CleanupCount, Is.Zero);
            Assert.That(h.Preview.CanPlay, Is.False);
        }
    }

    [Test]
    public void TamperedTargetOfARealCurrentReceiptFaultsInsteadOfWaitingForReleaseForever()
    {
        using (var h = new Harness())
        {
            h.Ready(); var receipt = h.Pack();
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Invoke(h.Entrance, "UnbindPresentation");
            h.Deliver();
            var outcome = h.Presentation.VisualOutcomes.Single();
            Assert.That(outcome.Receipt, Is.SameAs(receipt));
            Assert.That(outcome.Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(h.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.True);
            Assert.That(h.Orders.TryGetEntryBinding(1, out _, out var otherCustomer), Is.True);
            Write(outcome, "<Target>k__BackingField", otherCustomer.rectTransform);
            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DEPARTURE_REJECTED"));
            Assert.That(h.Entrance.BeginVisibleSession(), Is.True);
            Assert.That(h.Entrance.EditorPhaseSupplyFaulted, Is.True);
            LogAssert.Expect(LogType.Error, new Regex("^POPUP_STORE_PHASE_PREVIEW_BLOCKED"));
            Assert.That(h.Preview.TickCleanup(), Is.False);
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Faulted));
            Assert.That(h.Entrance.EditorReleasedReceipts, Is.Empty);
            Assert.That(h.Preview.Session.PhaseGeneration, Is.EqualTo(1));
            Assert.That(h.Preview.CleanupCount, Is.Zero);
            Assert.That(h.Score.TotalScore, Is.EqualTo(100), "A failed visual proof does not refund a committed transaction.");
        }
    }

    [Test]
    public void PriorPhaseDeliveredOutcomesCannotFaultOrMoveTheNextPhaseCustomers()
    {
        using (var h = new Harness())
        {
            h.Ready(); var receipt = h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            h.NextPhase();
            string phase = h.Preview.Session.PhaseRunId;
            var first = h.Order(0);
            int departures = h.Entrance.StartedDepartureCount;
            h.Entrance.PauseVisibleSession();
            Assert.That(h.Entrance.BeginVisibleSession(), Is.True);
            Assert.That(h.Entrance.EditorPhaseSupplyFaulted, Is.False);
            Assert.That(h.Preview.LastError, Is.Null);
            Assert.That(h.Preview.Session.PhaseRunId, Is.EqualTo(phase));
            Assert.That(h.Preview.Session.State, Is.EqualTo(PopupStorePhaseState.Playing));
            Assert.That(h.Order(0), Is.SameAs(first));
            Assert.That(first.IsPacked, Is.False);
            Assert.That(h.Entrance.StartedDepartureCount, Is.EqualTo(departures));
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
        }
    }

    [Test]
    public void NavigationMemoryValidationPreservesSceneUndoSelectionAndManagers()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        using (var h = new Harness())
        {
            h.View.TryRequestExit(); h.Exit.TryContinue();
            h.Clock.AdvanceTime(22); h.Preview.TickCleanup(); h.ShowResult(); h.ConfirmToMap();
            h.Reenter(); h.ShowResult(); h.ConfirmToMap();
        }
        witness.AssertUnchanged("phase preview navigation memory validation");
    }

    internal sealed class Harness : IDisposable
    {
        internal readonly GameObject Scope;
        internal readonly UIPopupStore View;
        internal readonly MobileUINavigation Navigation;
        internal readonly PopupStoreAdmissionMapView Map;
        internal readonly PopupStoreAdmissionHost Admission;
        internal readonly PopupStorePhasePreview Preview;
        internal GameObject Root => View.gameObject;
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal PopupStoreCustomerEntrance Entrance => Root.GetComponent<PopupStoreCustomerEntrance>();
        internal PopupStorePacking Packing => Root.GetComponent<PopupStorePacking>();
        internal PopupStorePackingPresentation Presentation => Root.GetComponent<PopupStorePackingPresentation>();
        internal PopupStoreSalesCounter Counter => Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
        internal PopupStoreOrderDisplay Orders => Root.GetComponent<PopupStoreOrderDisplay>();
        internal PopupStoreScore Score => Root.GetComponent<PopupStoreScore>();
        internal PopupStoreResultPanel Panel => Root.GetComponent<PopupStoreResultPanel>();
        internal PopupStoreExitConfirmation Exit => Root.GetComponent<PopupStoreExitConfirmation>();
        internal PopupStoreStartController Controller => Root.GetComponent<PopupStoreStartController>();
        private PopupStoreMaterialCatalog Catalog => Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");

        internal Harness(bool requestStart = true, bool completeIntroduction = true,
            PopupStorePhaseProfileSnapshot profile = null, PopupStoreScorePolicySnapshot scoring = null,
            Func<int, int> orderRandom = null)
        {
            Scope = new GameObject("phase-preview-navigation-isolated-memory"); Scope.SetActive(false);
            try
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/UI/PopupStore/UIPopupStore.prefab"), Scope.transform, false);
                View = root.GetComponent<UIPopupStore>();
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                var navObject = new GameObject("phase-preview-navigation", typeof(RectTransform));
                navObject.transform.SetParent(Scope.transform, false);
                Navigation = navObject.AddComponent<MobileUINavigation>();
                var mapObject = new GameObject("phase-preview-map", typeof(RectTransform), typeof(Canvas));
                mapObject.transform.SetParent(Scope.transform, false);
                Map = mapObject.AddComponent<PopupStoreAdmissionMapView>();
                Write(Navigation, "_uiViews", new[] {
                    new MobileViewDicStruct { Name = "AdmissionMap", UIView = Map },
                    new MobileViewDicStruct { Name = UIPopupStore.ViewName, UIView = View } });
                Invoke(Navigation, "Init"); Scope.SetActive(true); Navigation.PushNoAnime("AdmissionMap");
                Open();
                Admission = PopupStoreApprovedTestSession.Attach(Root, 1);
                string error = null;
                if (profile == null)
                    Assert.That(PopupStorePhasePreviewSettings.CreateProposalV0().TryCreateSnapshot(Catalog,
                        out profile, out error), Is.True, error);
                Preview = Root.AddComponent<PopupStorePhasePreview>();
                Preview.OrderRandomOverride = orderRandom ?? (max => max == 100 ? 99 : 0);
                Assert.That(Preview.Initialize(profile, scoring ?? PopupStoreScorePolicySnapshot.Ordinary, out error), Is.True, error);
                if (requestStart)
                {
                    Assert.That(Controller.RequestStart(), Is.True, Controller.LastError);
                    if (completeIntroduction) PopupStorePhasePreviewIntegrationTests.CompleteInitialPresentation(Preview, Clock);
                }
            }
            catch { Dispose(); throw; }
        }

        private void Open()
        {
            Navigation.Push(UIPopupStore.ViewName);
            Assert.That(Navigation.FirstView, Is.SameAs(View));
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreOrderProgress>().Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True);
            Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Presentation.BeginVisibleSession(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreCustomerPatience>().Refresh(), Is.True);
            Assert.That(Score.Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreGradeSession>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreRewardSession>().Refresh(), Is.True);
            Assert.That(Panel.Refresh(), Is.True); Assert.That(Exit.Refresh(), Is.True);
            Controller.OnViewShown();
        }
        internal void Reenter()
        {
            Assert.That(Navigation.FirstView, Is.SameAs(Map));
            var life = Root.GetComponent<PopupStoreSessionLifetime>();
            Assert.That(life.IsRetired, Is.False);
            Assert.That(life.TryPrepareForEntry(out var same), Is.True, life.LastError);
            Assert.That(same, Is.SameAs(View), "A phase preview is restarted only by a fresh isolated Editor run.");
            Open();
        }
        internal PopupStoreOrder Order(int slot)
        { Assert.That(Orders.TryGetEntryBinding(slot, out var order, out _), Is.True); return order; }
        internal PopupStoreMaterialEntry[] Entries(int slot)
        { Assert.That(Order(slot).TryResolve(Catalog, out var entries, out var error), Is.True, error); return entries; }
        internal void Ready()
        { Entrance.TickEntrance(0); Entrance.TickEntrance(Preview.EntryInterval); Entrance.TickEntrance(Preview.EntryInterval); Entrance.TickEntrance(1.3f); }
        internal PopupStorePackingReceipt Pack()
        {
            int slot = Enumerable.Range(0, 3).First(Entrance.IsWaiting);
            foreach (var entry in Entries(slot).Reverse()) Assert.That(Counter.TryAdd(entry), Is.True);
            var result = Packing.TryPack(); Assert.That(result.Success, Is.True, result.Status.ToString()); return result.Receipt;
        }
        internal void Deliver()
        {
            Presentation.TickPresentation(0);
            for (int i = 0; i < 500 && Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Idle; i++)
                Presentation.TickPresentation(.02f);
            Assert.That(Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Idle));
        }
        internal void ShowResult()
        {
            PopupStorePhasePreviewIntegrationTests.CompleteFailurePresentation(Preview, Clock);
            Panel.TickResult(1); Panel.TickResult(1); Assert.That(Panel.IsVisible, Is.True); Assert.That(Panel.IsAppearing, Is.False);
        }
        internal void NextPhase() => PopupStorePhasePreviewIntegrationTests.CompleteNextPresentation(Preview, Clock);
        internal void ConfirmToMap()
        { Assert.That(Panel.TryConfirm(), Is.True); Assert.That(Navigation.FirstView, Is.SameAs(Map)); Assert.That(Root.activeSelf, Is.False); }
        internal void AssertSingleUseAndNoR1()
        {
            Assert.That(Admission.LocalStartCount, Is.EqualTo(1)); Assert.That(Admission.Access.Remaining, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreGradeSession>().FinalizationCount, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreGradeSession>().FinalResult, Is.Null);
            Assert.That(Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreRewardSession>().Result, Is.Null);
            Assert.That(Clock.EndedCount, Is.Zero); Assert.That(Clock.AbandonCount, Is.Zero);
        }
        public void Dispose()
        {
            Admission?.Dispose();
            if (Scope != null) Object.DestroyImmediate(Scope);
        }
    }

    private static void Write(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static void Invoke(object target, string name)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, null);
}
#endif
