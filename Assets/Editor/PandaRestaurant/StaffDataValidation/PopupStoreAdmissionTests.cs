#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual authored popup plus an explicitly injected isolated admission authority.</summary>
public sealed class PopupStoreAdmissionTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string Day = "2026-11-03";

    [Test]
    public void AuthoredStartPanelUsesLocalControlsApprovedArtAndFailClosedInitialState()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var panel = prefab.GetComponent<PopupStoreStartPanel>();
        Assert.That(panel, Is.Not.Null);
        Assert.That(Read(prefab.GetComponent<PopupStoreStartController>(), "_panel"), Is.SameAs(panel));
        var overlay = (GameObject)Read(panel, "_overlay");
        Assert.That(overlay.activeSelf, Is.False, "Ordinary entry starts on the latest tutorial; only an explicit 90-second review may display this panel.");
        Assert.That(overlay.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(overlay.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);
        Assert.That(((Button)Read(panel, "_startButton")).interactable, Is.False);
        Assert.That(((Button)Read(panel, "_closeButton")).onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(Read(prefab.GetComponent<PopupStoreBusinessClock>(), "_autoStartForEditorReview"), Is.False);
        foreach (var text in overlay.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(AssetDatabase.GetAssetPath(text.font), Does.EndWith("영덕대게체.asset"));
        Assert.That(overlay.GetComponentsInChildren<Image>(true).Count(i => i.sprite != null), Is.EqualTo(3));
        Assert.That(((Button)Read(prefab.GetComponent<PopupStorePauseController>(), "_pauseButton")).gameObject.activeSelf, Is.False);
    }

    [Test]
    public void MissingInjectedHostBlocksDirectClockAndActualGameplayApis()
    {
        using (var h = new Harness(bind: false))
        {
            Assert.That(h.Controller.Host, Is.Null);
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.Clock.TryStartSession(), Is.False);
            h.AssertNotStartedAndUntouched();
            Assert.That(h.StartButton.interactable, Is.False);
            Assert.That(h.Panel.IsVisible, Is.False);
            var tutorial = h.Root.GetComponentsInChildren<PopupStorePhasePreviewView>(true).Single(x => x.IsTutorialVisible);
            Assert.That(tutorial.PageCount, Is.EqualTo(5));
            while (tutorial.TryNext()) { }
            Assert.That(tutorial.TryPlay(), Is.False, "No authority is created by showing the approved tutorial.");
        }
    }

    [Test]
    public void OpeningClosingAndReopeningBeforeStartOnlyQueriesTheSameDailyUsage()
    {
        using (var h = new Harness())
        {
            string local = h.Controller.LocalSessionId;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(h.Panel.Remaining, Is.EqualTo(1));
                Assert.That(h.View.TryClose(), Is.True);
                h.DisableObservers();
                Assert.That(h.Navigation.FirstView, Is.SameAs(h.Map));
                h.OpenCurrent();
                Assert.That(h.Controller.LocalSessionId, Is.EqualTo(local));
            }
            h.AssertUsage(0, 0);
            Assert.That(h.Repository.SubmitCalls, Is.Zero);
            Assert.That(h.Host.Transaction, Is.Null);
            h.AssertNotStartedAndUntouched();
        }
    }

    [Test]
    public void CallingHostDirectlyForAHiddenViewCannotConsumeAnAdmission()
    {
        using (var h = new Harness())
        {
            Assert.That(h.View.TryClose(), Is.True); h.DisableObservers();
            Assert.That(h.Root.activeInHierarchy, Is.False);
            Assert.That(h.Host.RequestStart(h.Controller), Is.False);
            Assert.That(h.Host.Transaction, Is.Null);
            Assert.That(h.Host.Context.GetPendingRequest(h.Session.AccountId), Is.Null);
            Assert.That(h.Repository.SubmitCalls, Is.Zero);
            h.AssertUsage(0, 0); h.AssertNotStartedAndUntouched();
        }
    }

    [Test]
    public void OneApprovedStartBindsDistinctAuthorityAndLocalIdsAndConsumesExactlyOnce()
    {
        using (var h = new Harness())
        {
            Assert.That(h.Controller.RequestStart(), Is.True);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(90));
            Assert.That(h.Host.BusinessSessionId, Is.Not.EqualTo(h.Controller.LocalSessionId));
            Assert.That(h.Host.LocalSessionId, Is.EqualTo(h.Controller.LocalSessionId));
            Assert.That(h.Host.BindingCount, Is.EqualTo(1));
            Assert.That(h.Host.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Panel.IsVisible, Is.False);
            Assert.That(h.Panel.Remaining, Is.Zero);
            Assert.That(((TMP_Text)Read(h.Panel, "_usageHudText")).text, Is.EqualTo("영업권 0/1"));
            h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void RepeatedPanelBindingAndDoubleStartClickCannotMultiplyApproval()
    {
        using (var h = new Harness())
        {
            for (int i = 0; i < 8; i++) { h.Controller.OnViewShown(); Assert.That(h.Panel.Refresh(), Is.True); }
            h.StartButton.onClick.Invoke(); h.StartButton.onClick.Invoke();
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1));
            Assert.That(h.Host.Transaction.ConfirmationCount, Is.EqualTo(1));
            h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void BeforeApprovalMaterialSelectionClearPackingSupplyAndTimersRemainBlocked()
    {
        using (var h = new Harness())
        {
            Assert.That(h.Counter.TryAdd(h.FirstMaterial), Is.False);
            h.Counter.Clear();
            Assert.That(h.Packing.TryPack().Success, Is.False);
            h.Entrance.TickEntrance(20);
            Assert.That(h.Clock.AdvanceTime(20), Is.False);
            h.Root.GetComponent<PopupStoreResultPanel>().TickResult(1);
            h.AssertNotStartedAndUntouched();
        }
    }

    [Test]
    public void StartRestoresBackgroundInputEvenWhenClockObserversRefreshThePanel()
    {
        using (var h = new Harness())
        {
            Assert.That(h.Input.interactable, Is.False);
            Assert.That(h.Input.blocksRaycasts, Is.False);
            Action observer = () => h.Panel.Refresh();
            h.Clock.StateChanged += observer;
            Assert.That(h.Controller.RequestStart(), Is.True);
            h.Clock.StateChanged -= observer;
            Assert.That(h.Input.interactable, Is.True);
            Assert.That(h.Input.blocksRaycasts, Is.True);
            Assert.That(h.Counter.TryAdd(h.FirstMaterial), Is.True);
            h.Counter.Clear();
            Assert.That(h.Counter.Count, Is.Zero);
        }
    }

    [TestCase(PopupStoreFakeStartFault.CommitThenIndeterminate)]
    [TestCase(PopupStoreFakeStartFault.CommitThenDrop)]
    [TestCase(PopupStoreFakeStartFault.ThrowAfterCommit)]
    public void UncertainCommittedStartReconcilesTheOriginalRequestWithoutAnotherCharge(PopupStoreFakeStartFault fault)
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = fault;
            Assert.That(h.Controller.RequestStart(), Is.True);
            var tx = h.Host.Transaction;
            string request = tx.Request.StartRequestId;
            h.Host.ObserveElapsed(11);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Controller.RequestStart(), Is.True);
            Assert.That(h.Host.Transaction, Is.SameAs(tx));
            Assert.That(tx.Request.StartRequestId, Is.EqualTo(request));
            Assert.That(tx.State, Is.EqualTo(PopupStoreBusinessStartState.Started));
            Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1));
            Assert.That(h.Repository.QueryCalls, Is.EqualTo(1));
            Assert.That(h.Host.LocalStartCount, Is.EqualTo(1));
            h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void PendingSubmitDisablesRepeatedStartUntilTimeoutReconciliation()
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDrop;
            Assert.That(h.Controller.RequestStart(), Is.True);
            for (int i = 0; i < 6; i++) Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.StartButton.interactable, Is.False);
            Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1));
            h.Host.ObserveElapsed(11);
            Assert.That(h.StartButton.interactable, Is.True);
            h.StartButton.onClick.Invoke();
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void UnknownWithoutCommitAndNotFoundNeverReplaceTheOriginalRequest()
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = PopupStoreFakeStartFault.UnknownWithoutCommit;
            h.Controller.RequestStart(); var tx = h.Host.Transaction;
            for (int i = 0; i < 3; i++) Assert.That(h.Controller.RequestStart(), Is.True);
            Assert.That(h.Host.Transaction, Is.SameAs(tx));
            Assert.That(h.Host.Context.GetPendingRequest(h.Session.AccountId), Is.SameAs(tx.Request));
            Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1));
            Assert.That(h.Repository.QueryCalls, Is.EqualTo(3));
            h.AssertUsage(0, 0); h.AssertNotStartedAndUntouched();
        }
    }

    [Test]
    public void DefiniteNoEffectReplyAllowsOneNewRequestAfterReconciliation()
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = PopupStoreFakeStartFault.UnknownWithoutCommit;
            h.Controller.RequestStart(); var first = h.Host.Transaction.Request;
            h.Repository.NextQueryFault = PopupStoreFakeStartQueryFault.ConfirmNotApplied;
            Assert.That(h.Controller.RequestStart(), Is.True);
            Assert.That(h.Host.Transaction.State, Is.EqualTo(PopupStoreBusinessStartState.NotAppliedConfirmed));
            h.AssertUsage(0, 0);
            Assert.That(h.Controller.RequestStart(), Is.False, "A definitive rejection first refreshes current access.");
            Assert.That(h.Host.Transaction, Is.Null);
            Assert.That(h.Controller.RequestStart(), Is.True);
            Assert.That(h.Host.Transaction.Request.StartRequestId, Is.Not.EqualTo(first.StartRequestId));
            h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void DeferredOldAccountApprovalCannotStartTheNewAccountView()
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer;
            h.Controller.RequestStart(); var old = h.Session;
            h.Store.RegisterAccount("admission-other"); h.Session = h.Store.OpenSession("admission-other");
            h.Repository.DeliverNext();
            Assert.That(h.Host.Transaction.State, Is.EqualTo(PopupStoreBusinessStartState.Invalidated));
            Assert.That(h.Controller.IsApproved, Is.False);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Store.Read(old.AccountId, Day).Usage, Is.EqualTo(1));
            Assert.That(h.Store.Read(h.Session.AccountId, Day).Usage, Is.Zero);
            Assert.That(h.Controller.RequestStart(), Is.False);
        }
    }

    [Test]
    public void DeferredReplyAfterHostDisposalCannotActivateThePopup()
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer;
            h.Controller.RequestStart(); h.Host.Dispose(); h.Repository.DeliverNext();
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Controller.IsApproved, Is.False);
            Assert.That(h.Host.BindingCount, Is.Zero);
            h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void FocusFailureAfterApprovalPreservesOneStartAndResumesWithoutASecondUse()
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer;
            Assert.That(h.Controller.RequestStart(), Is.True);
            h.Clock.SetApplicationFocused(false);
            h.Repository.DeliverNext();
            Assert.That(h.Host.Transaction.LocalCompletionPending, Is.True);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Host.RefreshAccess(), Is.True);
            Assert.That(h.Panel.Remaining, Is.Zero);
            Assert.That(h.StartButton.interactable, Is.True, "The same approval remains recoverable at 0/1.");
            h.Clock.SetApplicationFocused(true);
            Assert.That(h.Host.RetryLocalStart(h.Controller), Is.True);
            Assert.That(h.Host.Transaction.LocalCompletionPending, Is.False);
            Assert.That(h.Host.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void TimeSourceCallbackLosingFocusAtLocalCommitPreservesApprovalWithoutStarting()
    {
        using (var h = new Harness())
        {
            bool interrupt = true;
            Write(h.Clock, "_timeSource", new Func<double>(() =>
            {
                if (interrupt) { interrupt = false; h.Clock.SetApplicationFocused(false); }
                return 10d;
            }));
            Assert.That(h.Controller.RequestStart(), Is.True);
            Assert.That(interrupt, Is.False, "The interruption ran inside the actual local start time read.");
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Host.Transaction.State, Is.EqualTo(PopupStoreBusinessStartState.Started));
            Assert.That(h.Host.Transaction.LocalCompletionPending, Is.True);
            Assert.That(h.Host.LocalStartCount, Is.Zero); h.AssertUsage(1, 1);
            var approved = h.Host.Transaction.Record;
            h.Clock.SetApplicationFocused(true);
            Assert.That(h.Host.RetryLocalStart(h.Controller), Is.True);
            Assert.That(h.Host.Transaction.Record, Is.SameAs(approved));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(h.Host.LocalStartCount, Is.EqualTo(1));
            Assert.That(h.Repository.SubmitCalls, Is.EqualTo(1)); h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void ClosingDuringPendingStartRetainsContextAndReentryFinishesTheSameApproval()
    {
        using (var h = new Harness())
        {
            h.Repository.NextSubmitFault = PopupStoreFakeStartFault.CommitThenDefer;
            h.Controller.RequestStart(); var tx = h.Host.Transaction;
            Assert.That(h.View.TryClose(), Is.True); h.DisableObservers();
            h.Repository.DeliverNext();
            Assert.That(tx.LocalCompletionPending, Is.True);
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            h.OpenCurrent();
            Assert.That(h.Host.Transaction, Is.SameAs(tx));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(tx.LocalCompletionPending, Is.False);
            Assert.That(h.Host.LocalStartCount, Is.EqualTo(1)); h.AssertUsage(1, 1);
        }
    }

    [TestCase(PopupStoreFakeAccessFault.Fail)]
    [TestCase(PopupStoreFakeAccessFault.Throw)]
    [TestCase(PopupStoreFakeAccessFault.Drop)]
    public void FailedAccessInformationNeverConsumesOrStarts(PopupStoreFakeAccessFault fault)
    {
        using (var h = new Harness(bind: false))
        {
            h.Repository.NextAccessFault = fault; Assert.That(h.Controller.BindHost(h.Host), Is.True);
            if (fault == PopupStoreFakeAccessFault.Drop)
            {
                Assert.That(h.Host.IsQuerying, Is.True);
                Assert.That(h.StartButton.interactable, Is.False);
                h.Host.ObserveElapsed(11);
            }
            Assert.That(h.Host.Access, Is.Null); h.AssertUsage(0, 0);
            Assert.That(h.Repository.SubmitCalls, Is.Zero);
            h.AssertNotStartedAndUntouched();
        }
    }

    [Test]
    public void TimedOutAccessResponseCannotOverwriteANewerValidSnapshot()
    {
        using (var h = new Harness(bind: false))
        {
            h.Repository.NextAccessFault = PopupStoreFakeAccessFault.Defer;
            h.Controller.BindHost(h.Host); h.Host.ObserveElapsed(11);
            Assert.That(h.Host.Access, Is.Null);
            Assert.That(h.Host.RefreshAccess(), Is.True); var valid = h.Host.Access;
            h.Repository.DeliverNext();
            Assert.That(h.Host.Access, Is.SameAs(valid));
            Assert.That(h.StartButton.interactable, Is.True); h.AssertUsage(0, 0);
        }
    }

    [Test]
    public void DuplicateTerminalAccessCallbackCannotRepublishOrReplaceTheAcceptedSnapshot()
    {
        using (var h = new Harness(bind: false))
        {
            h.Repository.NextAccessFault = PopupStoreFakeAccessFault.Defer;
            Assert.That(h.Controller.BindHost(h.Host), Is.True);
            Assert.That(h.Host.IsQuerying, Is.True);
            h.Repository.DeliverNext(); var accepted = h.Host.Access;
            Assert.That(accepted, Is.Not.Null);
            int changes = 0;
            Action observed = () => changes++;
            h.Host.Changed += observed;
            for (int i = 0; i < 4; i++) h.Repository.ReplayLastAccess();
            h.Host.Changed -= observed;
            Assert.That(h.Host.Access, Is.SameAs(accepted));
            Assert.That(h.Host.IsQuerying, Is.False);
            Assert.That(changes, Is.Zero, "One terminal information reply owns this query; duplicates must be ignored.");
            Assert.That(h.Repository.AccessQueryCalls, Is.EqualTo(1)); h.AssertUsage(0, 0);
        }
    }

    [Test]
    public void OneLiveViewCannotSwitchToAnotherInjectedHost()
    {
        using (var h = new Harness())
        using (var another = new PopupStoreAdmissionHost(h.Repository, h.Repository, () => h.Session))
        {
            Assert.That(h.Controller.BindHost(another), Is.False);
            Assert.That(h.Controller.Host, Is.SameAs(h.Host));
            h.AssertUsage(0, 0);
        }
    }

    [Test]
    public void AnApprovedLiveGameCannotShareItsApprovalWithASecondLocalView()
    {
        using (var h = new Harness())
        {
            h.Controller.RequestStart();
            var second = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), h.Scope.transform, false);
            var controller = second.GetComponent<PopupStoreStartController>();
            Assert.That(controller.LocalSessionId, Is.Not.EqualTo(h.Controller.LocalSessionId));
            Assert.That(controller.BindHost(h.Host), Is.False);
            Assert.That(controller.IsApproved, Is.False);
            Assert.That(second.GetComponent<PopupStoreBusinessClock>().State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Host.BindingCount, Is.EqualTo(1)); h.AssertUsage(1, 1);
        }
    }

    [Test]
    public void AbandonAndRealPrefabReplacementKeepConsumedUsageAndShowZeroWithoutStarting()
    {
        using (var h = new Harness())
        {
            h.Controller.RequestStart(); string old = h.Controller.LocalSessionId;
            h.AbandonAndReplace();
            Assert.That(h.Controller.LocalSessionId, Is.Not.EqualTo(old));
            Assert.That(h.Controller.Host, Is.SameAs(h.Host));
            Assert.That(h.Host.Access.Remaining, Is.Zero);
            Assert.That(h.Panel.Remaining, Is.Zero);
            Assert.That(h.StartButton.interactable, Is.False);
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.Host.LocalStartCount, Is.EqualTo(1));
            h.AssertUsage(1, 1); h.AssertNotStartedAndUntouched();
        }
    }

    [Test]
    public void CompletedResultAcknowledgementCreatesUnstartedViewWithoutReusingOldApproval()
    {
        using (var h = new Harness())
        {
            h.Controller.RequestStart(); Assert.That(h.Clock.AdvanceTime(90), Is.True);
            var oldResult = h.Root.GetComponent<PopupStoreGradeSession>().FinalResult;
            Assert.That(oldResult, Is.Not.Null);
            Assert.That(h.View.TryClose(), Is.True); h.DisableObservers(); h.ReplaceAndOpen();
            Assert.That(h.Root.GetComponent<PopupStoreGradeSession>().FinalResult, Is.Null);
            Assert.That(h.Host.Transaction, Is.Null); Assert.That(h.Controller.IsApproved, Is.False);
            Assert.That(h.Host.Access.Remaining, Is.Zero);
            h.AssertUsage(1, 1); h.AssertNotStartedAndUntouched();
        }
    }

    [TestCase(2)]
    [TestCase(3)]
    public void InjectedTwoOrThreeUsePolicySurvivesViewReplacementAndStopsAtItsLimit(int limit)
    {
        using (var h = new Harness(limit))
        {
            for (int used = 1; used <= limit; used++)
            {
                Assert.That(h.Controller.RequestStart(), Is.True);
                h.AssertUsage(used, used);
                h.AbandonAndReplace();
                Assert.That(h.Host.Access.Remaining, Is.EqualTo(limit - used));
            }
            Assert.That(h.Controller.RequestStart(), Is.False);
            Assert.That(h.Host.LocalStartCount, Is.EqualTo(limit));
            Assert.That(h.Repository.SubmitCalls, Is.EqualTo(limit)); h.AssertUsage(limit, limit);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReviewNoticeIsDrivenOnlyByTheExplicitHostFlag(bool editorReview)
    {
        using (var h = new Harness(editorReview: editorReview))
        {
            Assert.That(((GameObject)Read(h.Panel, "_reviewNotice")).activeSelf, Is.EqualTo(editorReview));
            Assert.That(h.Panel.Limit, Is.EqualTo(1));
            Assert.That(new PopupStoreBusinessAccessPolicy().RewardedDailyLimit, Is.EqualTo(2), "The legacy C2 policy remains separate.");
        }
    }

    [Test]
    public void StartPanelAllowsControllerOwnedRecoveryAtZeroOrUnknownCount()
    {
        using (var h = new Harness(bind: false))
        {
            foreach (int count in new[] { 0, -1 })
            {
                h.Panel.Render(true, true, true, count, 1, "같은 승인 확인", true);
                Assert.That(h.StartButton.interactable, Is.True);
            }
            h.Panel.Render(true, false, true, 0, 1, "오늘의 영업권을 모두 사용했습니다.", true);
            Assert.That(h.StartButton.interactable, Is.False);
        }
    }

    [Test]
    public void AdmissionMemoryValidationRestoresSceneUndoSelectionAndManagers()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        using (var h = new Harness()) { h.Controller.RequestStart(); h.AbandonAndReplace(); }
        witness.AssertUnchanged("popup store admission memory validation");
    }

    private sealed class Harness : IDisposable
    {
        internal readonly GameObject Scope;
        internal readonly MobileUINavigation Navigation;
        internal readonly PopupStoreAdmissionMapView Map;
        internal readonly PopupStoreBusinessStartFakeStore Store;
        internal readonly PopupStoreBusinessStartFakeRepository Repository;
        internal readonly PopupStoreAdmissionHost Host;
        internal PopupStoreRewardAccountSession Session;
        internal UIPopupStore View;
        internal GameObject Root => View.gameObject;
        internal PopupStoreStartController Controller => Root.GetComponent<PopupStoreStartController>();
        internal PopupStoreStartPanel Panel => Root.GetComponent<PopupStoreStartPanel>();
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal PopupStoreCustomerEntrance Entrance => Root.GetComponent<PopupStoreCustomerEntrance>();
        internal PopupStoreSalesCounter Counter => Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
        internal PopupStorePacking Packing => Root.GetComponent<PopupStorePacking>();
        internal CanvasGroup Input => Root.GetComponent<CanvasGroup>();
        internal Button StartButton => (Button)Read(Panel, "_startButton");
        internal PopupStoreMaterialEntry FirstMaterial
        {
            get
            {
                var catalog = (PopupStoreMaterialCatalog)Read(Counter, "_catalog");
                Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var entries, out var error), Is.True, error);
                return entries[0];
            }
        }

        internal Harness(int limit = 1, bool bind = true, bool editorReview = true)
        {
            Scope = new GameObject("popup-store-admission-isolated-memory"); Scope.SetActive(false);
            try
            {
                Assert.That(PopupStoreBusinessAccessSchedule.TryCreate(new PopupStoreBusinessAccessSetting(limit, "admission-test-v1", Day),
                    null, out var schedule, out var error), Is.True, error);
                Store = new PopupStoreBusinessStartFakeStore(schedule, () => new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero));
                Store.RegisterAccount("admission-isolated"); Session = Store.OpenSession("admission-isolated");
                Repository = new PopupStoreBusinessStartFakeRepository(Store);
                Host = new PopupStoreAdmissionHost(Repository, Repository, () => Session, editorReview: editorReview);
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Scope.transform, false);
                View = root.GetComponent<UIPopupStore>(); ConfigureCanvas();
                var navObject = new GameObject("admission-navigation", typeof(RectTransform)); navObject.transform.SetParent(Scope.transform, false);
                Navigation = navObject.AddComponent<MobileUINavigation>();
                var mapObject = new GameObject("admission-map", typeof(RectTransform), typeof(Canvas)); mapObject.transform.SetParent(Scope.transform, false);
                Map = mapObject.AddComponent<PopupStoreAdmissionMapView>();
                Write(Navigation, "_uiViews", new[] {
                    new MobileViewDicStruct { Name = "AdmissionMap", UIView = Map },
                    new MobileViewDicStruct { Name = UIPopupStore.ViewName, UIView = View } });
                Invoke(Navigation, "Init"); Scope.SetActive(true); Navigation.PushNoAnime("AdmissionMap");
                OpenCurrent();
                if (bind) Assert.That(Controller.BindHost(Host), Is.True);
            }
            catch { Dispose(); throw; }
        }
        private void ConfigureCanvas()
        {
            Root.GetComponent<CanvasScaler>().enabled = false;
            Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
        }
        internal void OpenCurrent()
        {
            Navigation.Push(UIPopupStore.ViewName);
            Assert.That(Navigation.FirstView, Is.SameAs(View));
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreOrderDisplay>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreOrderProgress>().Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True); Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Root.GetComponent<PopupStorePackingPresentation>().BeginVisibleSession(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreCustomerPatience>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreScore>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreGradeSession>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreRewardSession>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreResultPanel>().Refresh(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreExitConfirmation>().Refresh(), Is.True);
            Controller.OnViewShown();
        }
        internal void AbandonAndReplace()
        {
            Assert.That(View.TryRequestExit(), Is.True);
            Assert.That(Root.GetComponent<PopupStoreExitConfirmation>().TryConfirmAbandon(), Is.True);
            DisableObservers(); ReplaceAndOpen();
        }
        internal void ReplaceAndOpen()
        {
            Assert.That(Navigation.FirstView, Is.SameAs(Map));
            var life = Root.GetComponent<PopupStoreSessionLifetime>();
            Assert.That(life.TryPrepareForEntry(out var next), Is.True, life.LastError);
            View = next; ConfigureCanvas(); OpenCurrent();
        }
        internal void DisableObservers()
        {
            Invoke(Panel, "OnDisable");
            Entrance.PauseVisibleSession(); Root.GetComponent<PopupStorePackingPresentation>().EndVisibleSession();
        }
        internal void AssertUsage(int usage, int starts)
        {
            var snapshot = Store.Read("admission-isolated", Day);
            Assert.That(snapshot.Usage, Is.EqualTo(usage)); Assert.That(snapshot.Starts, Is.EqualTo(starts));
        }
        internal void AssertNotStartedAndUntouched()
        {
            Assert.That(Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(Clock.ElapsedSeconds, Is.Zero); Assert.That(Entrance.SuppliedOrderCount, Is.Zero);
            Assert.That(Entrance.StartedEntranceCount, Is.Zero); Assert.That(Counter.Count, Is.Zero);
            Assert.That(Packing.CommittedReceipts, Is.Empty);
            Assert.That(Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreGradeSession>().FinalizationCount, Is.Zero);
            Assert.That(Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
        public void Dispose()
        {
            Host?.Dispose();
            if (Scope != null) Object.DestroyImmediate(Scope);
        }
    }

    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static object Invoke(object target, string name)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, null);
}

public sealed class PopupStoreAdmissionMapView : MobileUIView
{
    public override void Init() { base.OnValidate(); VisibleState = VisibleState.Disappeared; gameObject.SetActive(false); }
    public override void Show() { gameObject.SetActive(true); VisibleState = VisibleState.Appeared; }
    public override void Hide() { VisibleState = VisibleState.Disappeared; gameObject.SetActive(false); }
}
#endif
