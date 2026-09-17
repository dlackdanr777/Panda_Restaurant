#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using Muks.Tween;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Reuses the existing detached account/SDK fixture and the actual scene's button bindings.
// Init/Show/Button.onClick are product methods; no request is started by a test-only purchase API.
public partial class StaffStageMigrationCollectionTests
{
    private readonly List<BackendManager> _paidButtonOwners = new List<BackendManager>();

    [TearDown]
    public void ReleasePaidButtonOwners()
    {
        foreach (var owner in _paidButtonOwners) if (owner != null) owner.DestroyEditorOfflineOwner();
        _paidButtonOwners.Clear();
    }

    [Test]
    public void PaidButtons_ActualInitAndSingleElevenInputsCommitOneFixedPurchaseAndRestoreLifecycleState()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        foreach (StaffGachaPurchaseType kind in new[] { StaffGachaPurchaseType.Single, StaffGachaPurchaseType.Multi })
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, PurchaseIds(kind));
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                ui.Staff.Init(ui.View); // A second Init must not register duplicate purchase listeners.
                ui.Staff.Show();
                ui.AssertLabels();
                ui.AssertAvailability(true, true);
                int completions = 0;
                fixture.Manager.StaffPurchaseCompleted += _ =>
                {
                    completions++;
                    ui.Single.onClick.Invoke();
                    ui.Multi.onClick.Invoke();
                };
                Button selected = kind == StaffGachaPurchaseType.Single ? ui.Single : ui.Multi;
                selected.onClick.Invoke();
                var operation = fixture.Manager.CurrentStaffPurchaseExecution;
                Assert.That(operation, Is.Not.Null, "The actual scene Button.onClick did not reach TryStartStaffPurchase");
                Assert.That(operation.Plan.PurchaseType, Is.EqualTo(kind));
                Assert.That(operation.DrawnStaff.Count, Is.EqualTo(kind == StaffGachaPurchaseType.Single ? 1 : 11));
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                Assert.That(wallet.DiamondValue, Is.EqualTo(110));
                Assert.That(operation.IsCompleted, Is.False);
                string frozen = JsonConvert.SerializeObject(operation.Plan);
                ui.AssertAvailability(false, false);
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    ui.Single.onClick.Invoke();
                    ui.Multi.onClick.Invoke();
                }
                Assert.That(fixture.Game.Writes, Is.EqualTo(1));
                Assert.That(draws(), Is.EqualTo(operation.DrawnStaff.Count));
                ui.Staff.Hide();
                ui.Staff.Show();
                ui.AssertAvailability(false, false);
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.SameAs(operation));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(operation.IsCompleted, Is.True);
                Assert.That(operation.CompletionCount, Is.EqualTo(1));
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(wallet.DiamondValue, Is.EqualTo(kind == StaffGachaPurchaseType.Single ? 100 : 10));
                Assert.That(fixture.Game.Writes, Is.EqualTo(2), "Purchase confirmation retains the one latest automatic save");
                fixture.Game.WriteReplies[1](Bro("204", ""));
                Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(fixture.Game.Writes, Is.EqualTo(2));
                Assert.That(JsonConvert.SerializeObject(operation.Plan), Is.EqualTo(frozen));

                ui.CloseResult();
                ui.Staff.Hide();
                ui.Staff.Show();
                ui.AssertLabels();
                ui.AssertAvailability(true, kind == StaffGachaPurchaseType.Single);
                Assert.That(fixture.Manager.LastCompletedStaffPurchaseExecution, Is.SameAs(operation));
                Assert.That(draws(), Is.EqualTo(operation.DrawnStaff.Count));
            }
        }
    }

    [Test]
    public void PaidButtons_BalanceInvalidRestoreUnknownAndFreeQuestRejectBeforeDrawOrSend()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        foreach (string reason in new[] { "single-poor", "multi-poor", "restore-invalid", "response-unknown", "free-quest", "current-free-offer" })
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                ui.Staff.Show();
                if (reason == "single-poor") wallet.DiamondValue = 9;
                if (reason == "multi-poor") wallet.DiamondValue = 99;
                if (reason == "restore-invalid") fixture.Manager.InvalidateGameDataRestore();
                if (reason == "free-quest")
                    PaidSet(ui.Staff, "_questId", "MainReward01"); // Only view mode, not a forged entitlement or grant.
                if (reason == "current-free-offer")
                {
                    Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager,
                        new MemoryQuestStaffState { Quest = "MainReward01" });
                    Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out _), Is.True);
                }
                if (reason == "response-unknown")
                {
                    ui.Single.onClick.Invoke();
                    fixture.Game.WriteReplies[0](null);
                    Assert.That(fixture.Manager.CurrentStaffPurchaseExecution.Status,
                        Is.EqualTo(StaffGachaPurchaseExecutionStatus.Indeterminate));
                }
                int writes = fixture.Game.Writes, selected = draws(), balance = wallet.DiamondValue;
                var account = fixture.Manager.StaffRuntime.Snapshot;
                // Direct event invocation also tests stale UI input, without relying on Selectable to suppress it.
                if (reason != "multi-poor") ui.Single.onClick.Invoke();
                ui.Multi.onClick.Invoke();
                Assert.That(fixture.Game.Writes, Is.EqualTo(writes), reason);
                Assert.That(draws(), Is.EqualTo(selected), reason);
                Assert.That(wallet.DiamondValue, Is.EqualTo(balance), reason);
                Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(account), reason);
                if (reason == "response-unknown")
                    Assert.That(fixture.Manager.CurrentStaffPurchaseExecution.IsCompleted, Is.False);
            }
        }
    }

    [Test]
    public void PaidButtons_FreeQuestLabelFitsOneLineInsideExistingButtonWithoutStartingGrant()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            Field(typeof(BackendManager), "_questStaffTutorialState").SetValue(fixture.Manager,
                new MemoryQuestStaffState { Quest = "MainReward01" });
            Assert.That(fixture.Manager.TryGetCurrentQuestStaffOffer(out _, out _), Is.True);
            var account = fixture.Manager.StaffRuntime.Snapshot;
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                Vector2 originalSize = ((RectTransform)ui.Single.transform).sizeDelta;
                typeof(UIStaffGacha).GetMethod("PrepareQuestEntry", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(ui.Staff, new object[] { fixture.Manager, "MainReward01", null });
                ui.Staff.Show();
                var button = PaidField<Button>(ui.Staff, "_questButton");
                var label = PaidField<TextMeshProUGUI>(ui.Staff, "_questLabel");
                for (int entry = 0; entry < 2; entry++)
                {
                    Canvas.ForceUpdateCanvases();
                    label.ForceMeshUpdate(true, true);
                    Assert.That(label.text, Is.EqualTo("무료 뽑기"));
                    Assert.That(label.enableWordWrapping, Is.False);
                    Assert.That(label.enableAutoSizing, Is.True);
                    Assert.That(label.textInfo.lineCount, Is.EqualTo(1));
                    Assert.That(label.isTextOverflowing, Is.False);
                    RectTransform rect = (RectTransform)button.transform;
                    Assert.That(rect.sizeDelta, Is.EqualTo(originalSize), "Keep the existing button art dimensions");
                    var corners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                        Assert.That(rect.rect.Contains(rect.InverseTransformPoint(corner)), Is.True,
                            "The free label must remain inside its existing button, not below its art");
                    Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
                    Assert.That(button.GetComponent<ButtonPressEffect>().Interactable, Is.True);
                    Assert.That(ui.Single.gameObject.activeSelf || ui.Multi.gameObject.activeSelf, Is.False);
                    ui.Staff.Hide(); ui.Staff.Show();
                    Assert.That(PaidField<Button>(ui.Staff, "_questButton"), Is.SameAs(button));
                }
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(draws(), Is.Zero);
                Assert.That(wallet.DiamondValue, Is.EqualTo(110));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(account));
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.Null);
                Assert.That(fixture.Manager.LastCompletedQuestStaffGrant, Is.Null);
            }
        }
    }

    [Test]
    public void PaidButtons_RetiredDirectGrantAndAnimatorStepsCannotBypassConfirmedPurchase()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                ui.Staff.Show();
                var account = fixture.Manager.StaffRuntime.Snapshot;
                int steps = 0;
                ui.View.GachaStepHandler += _ => steps++;
                var data = GachaStaffData.Create(resources.Staff("STAFF23"));
                try
                {
                    ui.Staff.GetStaff(data);
                    ui.Staff.StartAddStaff(data);
                    for (int step = 2; step <= 5; step++) ui.Staff.SetStep(step);
                }
                finally { Object.DestroyImmediate(data); }
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(draws(), Is.Zero);
                Assert.That(wallet.DiamondValue, Is.EqualTo(110));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(account));
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.Null);
                Assert.That(steps, Is.Zero, "Old AnimationEvents must not signal grant/tutorial progression");
                Assert.That(ui.View.IsStartGacha, Is.False);
                ui.AssertAvailability(true, true);
                // Idle initialization is still permitted without reopening the retired acquisition states.
                ui.Staff.SetStep(1);
                ui.AssertLabels();
                ui.AssertAvailability(true, true);
            }
        }
    }

    [Test]
    public void PaidButtons_SynchronousSuccessObserversCannotReenterEitherPaidInput()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                int completed = 0;
                fixture.Manager.StaffPurchaseCompleted += _ =>
                {
                    completed++;
                    ui.Single.onClick.Invoke();
                    ui.Multi.onClick.Invoke();
                };
                fixture.Game.OnUpdate = (_, __, callback) => callback(Bro("204", ""));
                ui.Single.onClick.Invoke();
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(draws(), Is.EqualTo(1));
                Assert.That(fixture.Game.Writes, Is.EqualTo(2), "Only purchase and its latest autosave are sent");
                Assert.That(wallet.DiamondValue, Is.EqualTo(100));
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution.CompletionCount, Is.EqualTo(1));
                Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
                ui.AssertAvailability(true, true);
            }
        }
    }

    [Test]
    public void PaidButtons_EditorAdmissionRejectsUnapprovedNextPurchaseWithoutLockingOrdinarySaves()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture,
                PurchaseIds(StaffGachaPurchaseType.Single).Concat(PurchaseIds(StaffGachaPurchaseType.Multi)).ToArray());
            bool multiApproved = false;
            fixture.Manager.EditorStaffPurchaseAdmission = kind => kind == StaffGachaPurchaseType.Single
                ? fixture.Manager.LastCompletedStaffPurchaseExecution == null : multiApproved;
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                ui.AssertAvailability(true, false);
                ui.Single.onClick.Invoke();
                var single = fixture.Manager.CurrentStaffPurchaseExecution;
                Assert.That(single, Is.Not.Null);
                Assert.That(single.Plan.PurchaseType, Is.EqualTo(StaffGachaPurchaseType.Single));
                fixture.Game.WriteReplies[0](Bro("204", ""));
                fixture.Game.WriteReplies[1](Bro("204", ""));
                Assert.That(single.IsCompleted, Is.True);
                Assert.That(single.CompletionCount, Is.EqualTo(1));
                Assert.That(wallet.DiamondValue, Is.EqualTo(100));
                Assert.That(draws(), Is.EqualTo(1));
                var accountAfterSingle = fixture.Manager.StaffRuntime.Snapshot;
                string singlePlan = JsonConvert.SerializeObject(single.Plan);
                ui.Staff.Hide(); ui.Staff.Show();
                ui.AssertAvailability(false, false);

                Assert.That(fixture.Manager.CanStartStaffPurchase(StaffGachaPurchaseType.Multi, out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                ui.Multi.onClick.Invoke(); // Stale/forced button event must be rejected before drawing or transport.
                ui.Single.onClick.Invoke();
                Assert.That(fixture.Manager.TryStartStaffPurchase(StaffGachaPurchaseType.Multi, out var rejected, out error), Is.False);
                Assert.That(rejected, Is.Null); Assert.That(error, Is.Not.Empty);
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.SameAs(single));
                Assert.That(fixture.Manager.LastCompletedStaffPurchaseExecution, Is.SameAs(single));
                Assert.That(fixture.Game.Writes, Is.EqualTo(2), "Admission denial must never reach the injected SDK update boundary");
                Assert.That(draws(), Is.EqualTo(1));
                Assert.That(wallet.DiamondValue, Is.EqualTo(100));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(accountAfterSingle));
                Assert.That(((StaffPurchaseDiamondWallet)fixture.Manager.StaffPurchaseWallet).HasReservation, Is.False);
                Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
                Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.CanStartPurchase, Is.True,
                    "The restriction is admission-only, not a synthetic unknown request or a target lock");

                var ordinary = fixture.Manager.RequestGameDataAutosave();
                Assert.That(ordinary.Accepted, Is.True);
                Assert.That(fixture.Game.Writes, Is.EqualTo(3), "Unapproved next purchase must not suppress normal latest-value saves");
                fixture.Game.WriteReplies[2](Bro("204", ""));
                Assert.That(ordinary.Status, Is.EqualTo(GameDataSaveRequestStatus.SuccessConfirmed));
                Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.SameAs(single));

                multiApproved = true;
                ui.Staff.Show();
                ui.AssertAvailability(false, true);
                ui.Multi.onClick.Invoke();
                var multi = fixture.Manager.CurrentStaffPurchaseExecution;
                Assert.That(multi, Is.Not.SameAs(single));
                Assert.That(multi.Plan.PurchaseType, Is.EqualTo(StaffGachaPurchaseType.Multi));
                Assert.That(multi.Plan.DiamondCost, Is.EqualTo(100));
                Assert.That(multi.Plan.ResultCount, Is.EqualTo(11));
                Assert.That(fixture.Game.Writes, Is.EqualTo(4));
                Assert.That(draws(), Is.EqualTo(12));
                Assert.That(wallet.DiamondValue, Is.EqualTo(100), "Approved request still waits for its actual success response");
                fixture.Game.WriteReplies[3](Bro("204", ""));
                fixture.Game.WriteReplies[4](Bro("204", ""));
                Assert.That(multi.IsCompleted, Is.True); Assert.That(multi.CompletionCount, Is.EqualTo(1));
                Assert.That(wallet.DiamondValue, Is.Zero);
                Assert.That(fixture.Manager.StaffRuntime.Snapshot.PandaTokens, Is.EqualTo(120),
                    "The prior single already owns STAFF23, so its two later copies are ordinary duplicates");
                Assert.That(fixture.Game.Writes, Is.EqualTo(5));
                Assert.That(JsonConvert.SerializeObject(single.Plan), Is.EqualTo(singlePlan));
                Assert.That(((StaffPurchaseDiamondWallet)fixture.Manager.StaffPurchaseWallet).HasReservation, Is.False);
                Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.Idle));
            }
        }
    }

    [Test]
    public void PaidButtons_EditorAdmissionDenialOrExceptionCannotStartAndApprovalCannotBypassProductChecks()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        foreach (string reason in new[] { "deny", "throw", "approved-poor", "approved-stale" })
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            fixture.Manager.EditorStaffPurchaseAdmission = _ => reason == "throw"
                ? throw new InvalidOperationException("Detached QA admission observer failure") : reason != "deny";
            if (reason == "approved-poor") wallet.DiamondValue = 9;
            if (reason == "approved-stale") fixture.Manager.InvalidateGameDataRestore();
            int before = wallet.DiamondValue;
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                ui.AssertAvailability(false, false);
                foreach (var kind in new[] { StaffGachaPurchaseType.Single, StaffGachaPurchaseType.Multi })
                {
                    Assert.That(fixture.Manager.CanStartStaffPurchase(kind, out string error), Is.False, reason);
                    Assert.That(error, Is.Not.Empty, reason);
                    Assert.That(fixture.Manager.TryStartStaffPurchase(kind, out var rejected, out error), Is.False, reason);
                    Assert.That(rejected, Is.Null, reason); Assert.That(error, Is.Not.Empty, reason);
                }
                ui.Single.onClick.Invoke(); ui.Multi.onClick.Invoke();
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.Null, reason);
                Assert.That(fixture.Manager.LastCompletedStaffPurchaseExecution, Is.Null, reason);
                Assert.That(fixture.Game.Writes, Is.Zero, reason);
                Assert.That(draws(), Is.Zero, reason);
                Assert.That(wallet.DiamondValue, Is.EqualTo(before), reason);
                Assert.That(((StaffPurchaseDiamondWallet)fixture.Manager.StaffPurchaseWallet).HasReservation, Is.False, reason);
            }
        }
    }

    [Test]
    public void PaidButtons_ExplicitResultCloseSurvivesReentryAndDisplayRecreation_RetiredReplayStaysHiddenAndNextPurchaseRemainsIndependent()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture,
                PurchaseIds(StaffGachaPurchaseType.Single).Concat(PurchaseIds(StaffGachaPurchaseType.Multi)).ToArray());
            int completions = 0;
            fixture.Manager.StaffPurchaseCompleted += _ => completions++;
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                ui.PrepareIdleAnimator();
                ui.Single.onClick.Invoke();
                var single = fixture.Manager.CurrentStaffPurchaseExecution;
                fixture.Game.WriteReplies[0](Bro("204", ""));
                fixture.Game.WriteReplies[1](Bro("204", ""));
                string singlePlan = JsonConvert.SerializeObject(single.Plan);
                var afterSingle = fixture.Manager.StaffRuntime.Snapshot;
                ui.AdvanceDisplayToResult();
                Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1));
                Assert.That(ui.Display.EditorCurrentItem, Is.SameAs(single.Plan.AccountResult.Acquisition.Items[0]));

                ui.ClickResultClose(); // Real card listener, not the lifecycle Close method.
                Assert.That(ui.View.IsStartGacha, Is.False);
                Assert.That(ui.Animator.fireEvents, Is.True, "The owning machine restores its original event setting");
                for (int reentry = 0; reentry < 2; reentry++)
                {
                    ui.Staff.Hide(); ui.Staff.Show(); ui.Display.Tick();
                    Assert.That(ui.Display.EditorIsResultVisible, Is.False, "Acknowledged result must not reopen on machine entry");
                    Assert.That(ui.Display.EditorIsAnimating, Is.False);
                    Assert.That(ui.View.IsStartGacha, Is.False);
                }
                Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1));

                var oldDisplay = ui.Display;
                ui.ClickViewClose();
                // The production binding disposes the old display and creates another, while the
                // account-owned completion and its immutable data survive the scene presentation.
                ui.Staff.BindPurchaseDisplayOwner(ui.View, fixture.Manager);
                ui.View.Show(); ui.PrepareIdleAnimator(); ui.Display.Tick();
                Assert.That(ui.Display, Is.Not.SameAs(oldDisplay));
                Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                Assert.That(ui.Display.EditorIsAnimating, Is.False);
                Assert.That(ui.Display.EditorAnimationStartCount, Is.Zero);
                ui.AssertRetiredResultControlHidden();
                ui.Staff.Hide(); ui.Staff.Show(); ui.Display.Tick();
                Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                Assert.That(fixture.Manager.CurrentStaffPurchaseExecution, Is.SameAs(single));
                Assert.That(fixture.Manager.StaffRuntime.Snapshot, Is.SameAs(afterSingle));
                Assert.That(wallet.DiamondValue, Is.EqualTo(100));
                Assert.That(draws(), Is.EqualTo(1));
                Assert.That(fixture.Game.Writes, Is.EqualTo(2));
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(JsonConvert.SerializeObject(single.Plan), Is.EqualTo(singlePlan));

                ui.PrepareIdleAnimator();
                ui.Multi.onClick.Invoke();
                var multi = fixture.Manager.CurrentStaffPurchaseExecution;
                Assert.That(multi, Is.Not.SameAs(single));
                fixture.Game.WriteReplies[2](Bro("204", ""));
                fixture.Game.WriteReplies[3](Bro("204", ""));
                ui.AdvanceDisplayToResult();
                Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1), "A different completion must still auto-present once");
                Assert.That(ui.Display.EditorResultCount, Is.EqualTo(11));
                Assert.That(ui.Display.EditorCurrentItem, Is.SameAs(multi.Plan.AccountResult.Acquisition.Items[0]));
                Assert.That(ui.Display.EditorCurrentItem.IsDuplicate, Is.True, "The prior single already acquired STAFF23");
                Assert.That(wallet.DiamondValue, Is.Zero);
                Assert.That(draws(), Is.EqualTo(12));
                Assert.That(fixture.Game.Writes, Is.EqualTo(4));
                Assert.That(completions, Is.EqualTo(2));
                // The first reveal is not the multi-result close boundary. Use the
                // native skip/cascade/+1 path, then the completed summary background.
                ui.AdvanceMultiToSummary();
                ui.ClickResultClose();
                Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                Assert.That(fixture.Game.Writes, Is.EqualTo(4));
                Assert.That(draws(), Is.EqualTo(12));
                Assert.That(completions, Is.EqualTo(2));
            }
        }
    }

    [Test]
    public void PaidButtons_HiddenCompletionAndInterruptedAnimationRemainUnacknowledgedUntilRealResultClose()
    {
        using (var resources = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateNativePaidButtonFixture(out var wallet);
            Func<int> draws = UsePurchaseSelection(fixture, "STAFF23");
            int completions = 0;
            fixture.Manager.StaffPurchaseCompleted += _ => completions++;
            using (var ui = new PaidButtonView(fixture.Manager))
            {
                ui.Single.onClick.Invoke();
                var operation = fixture.Manager.CurrentStaffPurchaseExecution;
                ui.ClickViewClose(); // Closing the machine while the request is pending is not acknowledging a result.
                fixture.Game.WriteReplies[0](Bro("204", ""));
                fixture.Game.WriteReplies[1](Bro("204", ""));
                ui.Display.Tick();
                Assert.That(operation.IsCompleted, Is.True);
                Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                Assert.That(ui.Display.EditorAnimationStartCount, Is.Zero);
                Assert.That(ui.View.gameObject.activeSelf, Is.False, "A hidden completion cannot reopen the screen");

                ui.View.Show(); ui.PrepareIdleAnimator(); ui.Display.Tick();
                Assert.That(ui.Display.EditorIsAnimating, Is.True, ui.Display.EditorAnimationError);
                Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1));
                Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                Assert.That(ui.Animator.fireEvents, Is.False);
                // Native item-style playback hides the global chrome, including X.
                // Exercise a lifecycle interruption instead of clicking an unavailable control.
                Assert.That(ui.View.transform.Find("Anime UI/UI Components/Exit Button")
                    .gameObject.activeInHierarchy, Is.False);
                ui.View.Hide(); // Interrupted animation, still no explicit card acknowledgment.
                ui.Display.Tick();
                Assert.That(ui.Display.EditorIsAnimating, Is.False);
                Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                Assert.That(ui.Animator.fireEvents, Is.True);
                Assert.That(ui.View.gameObject.activeSelf, Is.False);

                ui.View.Show(); ui.PrepareIdleAnimator(); ui.Display.Tick();
                Assert.That(ui.Display.EditorIsResultVisible, Is.True, "Unacknowledged interrupted completion reopens its fixed card");
                Assert.That(ui.Display.EditorIsAnimating, Is.False);
                Assert.That(ui.Display.EditorAnimationStartCount, Is.EqualTo(1), "Interrupted animation cannot replay automatically");
                Assert.That(ui.Display.EditorCurrentItem, Is.SameAs(operation.Plan.AccountResult.Acquisition.Items[0]));
                ui.ClickResultClose();
                ui.Staff.Hide(); ui.Staff.Show(); ui.Display.Tick();
                Assert.That(ui.Display.EditorIsResultVisible, Is.False);
                Assert.That(ui.View.IsStartGacha, Is.False);
                Assert.That(fixture.Manager.LastCompletedStaffPurchaseExecution, Is.SameAs(operation));
                Assert.That(wallet.DiamondValue, Is.EqualTo(100));
                Assert.That(draws(), Is.EqualTo(1));
                Assert.That(fixture.Game.Writes, Is.EqualTo(2));
                Assert.That(completions, Is.EqualTo(1));
            }
        }
    }

    private Fixture CreateNativePaidButtonFixture(out MemoryStaffWallet wallet)
    {
        var fixture = CreatePurchaseFixture(out var diamondWallet, out wallet);
        // Native Unity references must not use the older FormatterServices fake-MonoBehaviour fixture.
        fixture.Manager = BackendManager.CreateEditorOfflineOwner(fixture.Game, fixture.Stage,
            diamondWallet, _ => null, _ => { });
        _paidButtonOwners.Add(fixture.Manager);
        Authenticate(fixture, fixture.Account);
        RestoreGameData(fixture, true);
        return fixture;
    }

    private static void PaidSet(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static T PaidField<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private sealed class PaidButtonView : IDisposable
    {
        private readonly Scene _scene;
        private readonly GameObject _root;
        private readonly StaffGachaOfflineFonts _fonts;
        private readonly GachaStaffData[] _priorWrappers;
        private readonly Hash128 _sourceHash;
        private readonly Sprite[] _priceSprites;
        public readonly UIGacha View;
        public readonly UIStaffGacha Staff;
        public readonly Button Single, Multi;

        public PaidButtonView(BackendManager owner)
        {
            _priorWrappers = Resources.FindObjectsOfTypeAll<GachaStaffData>();
            _sourceHash = AssetDatabase.GetAssetDependencyHash(StaffGachaOfflineViewFactory.SourceScenePath);
            _scene = EditorSceneManager.NewPreviewScene();
            _root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(_scene, out View, out Staff);
            _fonts = new StaffGachaOfflineFonts();
            _fonts.BindBeforeActivation(_root);
            View.ConfigureEditorOfflineView(Staff); // Isolates the source UI; purchase buttons below still use actual Init.
            Single = Staff.SingleButton;
            Multi = PaidField<Button>(Staff, "_tenButton");
            _priceSprites = new[] { Single, Multi }.Select(button => button.transform.Find("Money Image")
                .GetComponent<Image>().sprite).ToArray();
            foreach (Button button in new[] { Single, Multi })
                if (button.GetComponent<ButtonPressEffect>() == null) button.gameObject.AddComponent<ButtonPressEffect>();
            // Native results now use the actual product slot template. Clone only that
            // subtree so its fonts can be isolated before Init creates the ten cards.
            var slot = Object.Instantiate(PaidField<UIGachaCardSlot>(Staff, "_slotPrefab"), _root.transform, false);
            slot.gameObject.SetActive(false);
            PaidSet(Staff, "_slotPrefab", slot);
            _fonts.BindBeforeActivation(_root);
            Staff.BindPurchaseDisplayOwner(View, owner);
            // RuntimeStaffScope supplies the registered ID dictionary for mutation tests. The real
            // machine Init additionally reads the loader's array, so supply the same resource set
            // only for that call and restore the previous global array even when Init throws.
            FieldInfo catalogArray = Field(typeof(StaffDataManager), "_staffDatas", true);
            object originalCatalogArray = catalogArray.GetValue(null);
            var registered = (Dictionary<string, StaffData>)Field(typeof(StaffDataManager), "_staffDataDic", true).GetValue(null);
            try
            {
                catalogArray.SetValue(null, registered.Values.ToArray());
                Staff.Init(View);
            }
            finally { catalogArray.SetValue(null, originalCatalogArray); }
            Assert.That(_root.activeSelf, Is.False, "EditMode must not run the game scene");
            // Only the sanitized display hierarchy is activated, never the source Stage scene.
            // This also exercises the production active/Appeared input checks rather than bypassing them.
            _root.SetActive(true);
            View.SetEditorOfflineVisible(true);
        }

        public void AssertAvailability(bool single, bool multi)
        {
            foreach (var pair in new[] { Tuple.Create(Single, single), Tuple.Create(Multi, multi) })
            {
                Assert.That(pair.Item1.interactable, Is.EqualTo(pair.Item2), pair.Item1.name);
                Assert.That(pair.Item1.GetComponent<ButtonPressEffect>().Interactable, Is.EqualTo(pair.Item2), pair.Item1.name);
            }
        }

        public void AssertLabels()
        {
            int index = 0;
            foreach (Button button in new[] { Single, Multi })
            {
                Transform price = button.transform.Find("Money Image");
                Assert.That(price.gameObject.activeSelf, Is.True);
                Assert.That(price.GetComponent<Image>().sprite, Is.SameAs(_priceSprites[index]));
                Transform description = button.transform.Find("Description Text");
                if (description != null) Assert.That(description.gameObject.activeSelf, Is.True);
                var texts = button.GetComponentsInChildren<TextMeshProUGUI>(true).Select(text => text.text).ToArray();
                Assert.That(string.Join("|", texts), Does.Not.Contain("준비 중"));
                Assert.That(texts, Does.Contain(index == 0 ? "1회" : "10+1회"));
                Assert.That(string.Join("|", texts), Does.Contain(index == 0 ? "10" : "100"));
                index++;
            }
        }

        public void CloseResult() => PaidField<StaffGachaPurchaseDisplay>(Staff, "_purchaseDisplay").Close();

        public StaffGachaPurchaseDisplay Display => Staff.EditorOfflinePurchaseDisplay;
        public Animator Animator => PaidField<Animator>(Staff, "_gachaMacineAnimator");

        public void PrepareIdleAnimator()
        {
            Animator.fireEvents = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animator.Rebind();
            Animator.Play("Base Layer.Idle", 0, 0f);
            Animator.Update(0f);
            PaidField<UIGachaCard>(Staff, "_gachaCard").gameObject.SetActive(false);
            Animator.fireEvents = true;
        }

        public void AdvanceDisplayToResult()
        {
            Display.Tick();
            Assert.That(Display.EditorIsAnimating, Is.True, Display.EditorAnimationError);
            int wait = UnityEngine.Animator.StringToHash("Base Layer.Wait_Gacha");
            int open = UnityEngine.Animator.StringToHash("Base Layer.Open_Gacha");
            for (int frame = 0; frame < 1200; frame++)
            {
                Assert.That(Animator.fireEvents, Is.False);
                Animator.Update(0.05f);
                Display.Tick();
                if (Animator.GetCurrentAnimatorStateInfo(0).fullPathHash == wait && !Animator.IsInTransition(0)) break;
            }
            Assert.That(Animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(wait));
            Assert.That(Animator.IsInTransition(0), Is.False);
            Assert.That(Display.EditorIsResultVisible, Is.False, "The closed capsule waits for the user's native input");
            Animator.Update(0.5f);
            Display.Tick();
            Assert.That(Animator.GetCurrentAnimatorStateInfo(0).fullPathHash, Is.EqualTo(wait));
            // EditMode samples Animator time, not Time.unscaledTime. Expire only
            // the input debounce; do not write phase, result index or Animator state.
            PaidSet(Display, "_nextInput", 0f);
            var screen = PaidField<Button>(Staff, "_screenButton");
            Assert.That(screen.gameObject.activeInHierarchy && screen.interactable, Is.True);
            screen.onClick.Invoke();
            bool sawOpen = false;
            for (int frame = 0; frame < 1200 && !Display.EditorIsResultVisible; frame++)
            {
                Animator.Update(0.05f);
                sawOpen |= Animator.GetCurrentAnimatorStateInfo(0).fullPathHash == open;
                Display.Tick();
            }
            Assert.That(sawOpen, Is.True, "Native input must pass through the authored capsule-open clip");
            Assert.That(Display.EditorIsResultVisible, Is.True, "The actual machine controller did not reach its result card");
            Assert.That(Display.EditorIsAnimating, Is.False);
        }

        public void AdvanceMultiToSummary()
        {
            Assert.That(Display.EditorResultCount, Is.EqualTo(11));
            Assert.That(Display.EditorPresentationPhase, Is.EqualTo("Card"));
            var skip = PaidField<Button>(Staff, "_skipButton");
            Assert.That(skip.gameObject.activeInHierarchy && skip.interactable, Is.True);
            Assert.That(skip.GetComponentInChildren<TextMeshProUGUI>(true).text, Is.EqualTo("건너뛰기"));
            skip.onClick.Invoke();
            Assert.That(Display.EditorPresentationPhase, Is.EqualTo("Accumulating"));
            for (int index = 0; index < 10; index++)
            {
                PaidSet(Display, "_nextSlot", 0f);
                Display.Tick();
                Assert.That(Display.EditorVisibleSlotCount, Is.EqualTo(index + 1));
            }
            PaidSet(Display, "_nextSlot", 0f);
            Display.Tick();
            Assert.That(Display.EditorPresentationPhase, Is.EqualTo("CapsuleMoving"));
            var capsule = PaidField<GachaCapsule>(Staff, "_capsule");
            // Complete the real capsule movement tween using its own callbacks.
            foreach (TweenData tween in PaidField<RectTransform>(capsule, "_rectTransform").GetComponents<TweenData>())
            {
                if (!tween.enabled) continue;
                if (typeof(TweenData).GetField("_percentHandler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tween) == null)
                    typeof(TweenData).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tween, null);
                MethodInfo update = tween.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                update.Invoke(tween, null);
                typeof(TweenData).GetField("ElapsedDuration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tween, float.MaxValue);
                update.Invoke(tween, null);
            }
            Assert.That(capsule.IsMoving, Is.False);
            Display.Tick();
            Assert.That(Display.EditorPresentationPhase, Is.EqualTo("CapsuleOpening"));
            PaidField<Animator>(capsule, "_animator").Update(0.71f);
            Display.Tick();
            Assert.That(Display.EditorPresentationPhase, Is.EqualTo("Summary"));
            Assert.That(Display.EditorResultIndex, Is.EqualTo(10));
            Assert.That(Display.EditorVisibleSlotCount, Is.EqualTo(10));
        }

        public void ClickResultClose()
        {
            var button = PaidField<Button>(Staff, "_skipButton");
            if (Display.EditorResultCount == 11)
            {
                Assert.That(Display.EditorPresentationPhase, Is.EqualTo("Summary"));
                Assert.That(button.gameObject.activeSelf, Is.False, "The completed eleven-card summary has no close button");
                var screen = PaidField<Button>(Staff, "_screenButton");
                Assert.That(screen.gameObject.activeInHierarchy && screen.interactable, Is.True);
                PaidSet(Display, "_nextInput", 0f);
                screen.onClick.Invoke();
            }
            else
            {
                Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
                Assert.That(button.GetComponentInChildren<TextMeshProUGUI>(true).text, Is.EqualTo("닫기"));
                button.onClick.Invoke();
            }
            Assert.That(Display.EditorIsResultVisible, Is.False);
        }

        public void AssertRetiredResultControlHidden()
        {
            var button = PaidField<Button>(Staff, "_skipButton");
            Assert.That(button.gameObject.activeInHierarchy, Is.False);
            Assert.That(button.interactable, Is.False);
            button.onClick.Invoke();
            Assert.That(Display.EditorPresentationPhase, Is.EqualTo("Closed"));
            Assert.That(Display.EditorIsResultVisible || Display.EditorIsAnimating, Is.False);
        }

        public void ClickViewClose()
        {
            var button = View.transform.Find("Anime UI/UI Components/Exit Button")?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            button.onClick.Invoke();
        }

        public void Dispose()
        {
            try
            {
                CloseResult();
                var scrolling = PaidField<ScrollingImage>(Staff, "_scrollImage");
                var material = PaidField<Material>(scrolling, "_material");
                if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
                Object.DestroyImmediate(_root);
                _fonts.Dispose();
                foreach (var wrapper in Resources.FindObjectsOfTypeAll<GachaStaffData>().Except(_priorWrappers).ToArray())
                    if (wrapper != null && !EditorUtility.IsPersistent(wrapper)) Object.DestroyImmediate(wrapper);
                Assert.That(AssetDatabase.GetAssetDependencyHash(StaffGachaOfflineViewFactory.SourceScenePath), Is.EqualTo(_sourceHash));
            }
            finally { if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        }
    }
}
#endif
