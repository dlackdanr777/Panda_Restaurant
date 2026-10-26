using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit local-clock sessions on the real popup, supply and packing paths; no real account writes.</summary>
public sealed class PopupStoreBusinessClockTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [Test]
    public void AuthoredClockStartsAtNinetyAndUpdatesTheExistingTimeTextWithoutChangingItsFont()
    {
        using (var host = new Host())
        {
            Assert.That(Read(host.Clock, "_initialSeconds"), Is.EqualTo(90d));
            Assert.That(Read(host.Clock, "_entryCutoffSeconds"), Is.EqualTo(10d));
            Assert.That(host.Root.GetComponents<PopupStoreBusinessClock>(), Has.Length.EqualTo(1));
            var text = (TMP_Text)Read(host.Clock, "_timeText");
            var font = text.font;
            var position = text.rectTransform.anchoredPosition;
            float scale = Time.timeScale;
            Assert.That(host.PackButton.IsInteractable(), Is.False);
            host.Start();
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(90d));
            Assert.That(text.text, Is.EqualTo("남은 시간 01:30"));
            Assert.That(host.PackButton.IsInteractable(), Is.True, "Starting must update the existing button without a material click.");
            host.Clock.SetManualPaused(true);
            Assert.That(host.PackButton.IsInteractable(), Is.False);
            host.Clock.SetManualPaused(false);
            Assert.That(host.PackButton.IsInteractable(), Is.True);
            Assert.That(host.Clock.AdvanceTime(60), Is.True);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(30d));
            Assert.That(text.text, Is.EqualTo("남은 시간 00:30"));
            Assert.That(host.Clock.AdvanceTime(29.9), Is.True);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(.1d).Within(.000000001d));
            Assert.That(text.text, Is.EqualTo("남은 시간 00:01"));
            Assert.That(host.Clock.AdvanceTime(1), Is.True);
            Assert.That(text.text, Is.EqualTo("남은 시간 00:00"));
            Assert.That(host.PackButton.IsInteractable(), Is.False);
            Assert.That(text.font, Is.SameAs(font));
            Assert.That(text.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }
    }

    [Test]
    public void AnUnstartedSessionCannotSupplyOrPackThroughTheRealPrefab()
    {
        using (var host = new Host())
        {
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(host.Clock.CanStartCustomerEntry, Is.False);
            Assert.That(host.Clock.CanCommitPacking, Is.False);
            host.BeginCustomers();
            host.Entrance.TickEntrance(100);
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            Assert.That(host.Entrance.SuppliedOrderCount, Is.Zero);
            Assert.That(host.Packing.TryPack().Status, Is.EqualTo(PopupStorePackingStatus.Unavailable));
            host.AssertNoTransaction(materials);
        }
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidInitialTimeCannotStartASession(double initial)
    {
        using (var host = new Host())
        {
            Write(host.Clock, "_initialSeconds", initial);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreBusinessClock: "));
            Assert.That(host.Clock.TryStartSession(), Is.False);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(host.Clock.RemainingSeconds, Is.Zero);
            Assert.That(host.Clock.EndedCount, Is.Zero);
            Assert.That(host.Clock.CanCommitPacking, Is.False);
        }
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidElapsedTimeDoesNotChangeTheRunningSession(double elapsed)
    {
        using (var host = new Host())
        {
            host.Start();
            host.Clock.AdvanceTime(3.25);
            double before = host.Clock.RemainingSeconds;
            int changes = 0;
            host.Clock.StateChanged += () => changes++;
            Assert.That(host.Clock.AdvanceTime(elapsed), Is.False);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(before));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(changes, Is.Zero);
            Assert.That(host.Clock.EndedCount, Is.Zero);
        }
    }

    [TestCase("NotStarted")]
    [TestCase("Paused")]
    [TestCase("Ended")]
    public void OnlyRunningMayConsumeElapsedTime(string state)
    {
        using (var host = new Host())
        {
            if (state != "NotStarted") host.Start();
            if (state == "Paused") host.Clock.SetManualPaused(true);
            if (state == "Ended") host.Clock.AdvanceTime(90);
            double before = host.Clock.RemainingSeconds;
            int ended = host.Clock.EndedCount;
            Assert.That(host.Clock.State.ToString(), Is.EqualTo(state));
            Assert.That(host.Clock.AdvanceTime(12), Is.False);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(before));
            Assert.That(host.Clock.EndedCount, Is.EqualTo(ended));
            Assert.That(host.Clock.CanStartCustomerEntry, Is.False);
            Assert.That(host.Clock.CanCommitPacking, Is.False);
        }
    }

    [Test]
    public void RepeatedStartCannotResetOrReplaceTheExistingSession()
    {
        using (var host = new Host())
        {
            host.Start();
            host.Clock.AdvanceTime(17.5);
            for (int i = 0; i < 4; i++) host.Clock.TryStartSession();
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(72.5d));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            host.Clock.SetManualPaused(true);
            host.Clock.TryStartSession();
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(72.5d));
        }
    }

    [Test]
    public void ZeroInitialTimeEndsExactlyOnceWithoutAllowingAnyTransaction()
    {
        using (var host = new Host())
        {
            int ends = 0;
            host.Clock.Ended += () => ends++;
            host.Start(0);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Clock.RemainingSeconds, Is.Zero);
            Assert.That(host.Clock.EndedCount, Is.EqualTo(1));
            Assert.That(ends, Is.EqualTo(1));
            Assert.That(host.Clock.CanStartCustomerEntry, Is.False);
            Assert.That(host.Clock.CanCommitPacking, Is.False);
            Assert.That(host.Packing.TryPack().Success, Is.False);
            Assert.That(host.Packing.LastReceipt, Is.Null);
        }
    }

    [Test]
    public void ALongFrameClampsAtZeroAndNeverPublishesASecondEnd()
    {
        using (var host = new Host())
        {
            int ends = 0;
            host.Clock.Ended += () => ends++;
            host.Start();
            Assert.That(host.Clock.AdvanceTime(double.MaxValue), Is.True);
            for (int i = 0; i < 10; i++)
            {
                host.Clock.AdvanceTime(999);
                host.Clock.SynchronizeNow();
                host.Clock.TryStartSession();
            }
            Assert.That(host.Clock.RemainingSeconds, Is.Zero);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(ends, Is.EqualTo(1));
            Assert.That(host.Clock.EndedCount, Is.EqualTo(1));
        }
    }

    [TestCase(11d, true)]
    [TestCase(10d, false)]
    [TestCase(9d, false)]
    public void TheActualSupplyStartUsesTheInclusiveTenSecondCutoff(double remaining, bool allowed)
    {
        using (var host = new Host())
        {
            host.Start();
            host.BeginCustomers();
            host.Clock.AdvanceTime(90 - remaining);
            Assert.That(host.Clock.CanStartCustomerEntry, Is.EqualTo(allowed));
            host.Entrance.TickEntrance(0);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(allowed ? 1 : 0));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(allowed ? 19 : 20));
            Assert.That(host.Entrance.GetState(0).ToString(), Is.EqualTo(allowed ? "Entering" : "Empty"));
        }
    }

    [Test]
    public void RunningSupplyStillUsesFourSecondStartsAndAtMostThreeOccupiedSlots()
    {
        using (var host = new Host())
        {
            host.Start();
            host.BeginCustomers();
            host.Entrance.TickEntrance(0);
            host.Entrance.TickEntrance(3.99f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            host.Entrance.TickEntrance(.02f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(2));
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.OccupiedSlotCount, Is.EqualTo(3));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(17));
        }
    }

    [Test]
    public void ACustomerAlreadyEnteringFinishesAfterTheCutoffWithoutStartingTheNextOrder()
    {
        using (var host = new Host())
        {
            host.Start();
            host.BeginCustomers();
            host.Entrance.TickEntrance(0);
            var order = host.SourceOrders[0];
            host.Clock.AdvanceTime(80);
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.GetState(0).ToString(), Is.EqualTo("Waiting"));
            Assert.That(host.SourceOrders[0], Is.SameAs(order));
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Not.Empty);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(19));
        }
    }

    [Test]
    public void WaitingOrdersRemainVisibleAndUnpackedWhenTheBusinessEnds()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var orders = host.SourceOrders.ToArray();
            var materials = Enumerable.Range(0, 3).Select(slot => host.Orders.GetDisplayedMaterials(slot).ToArray()).ToArray();
            host.Clock.AdvanceTime(90);
            host.Entrance.TickEntrance(100);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.Entrance.GetState(i).ToString(), Is.EqualTo("Waiting"));
                Assert.That(host.SourceOrders[i], Is.SameAs(orders[i]));
                Assert.That(orders[i].IsPacked, Is.False);
                Assert.That(host.Orders.Slots[i].CustomerImage.gameObject.activeSelf, Is.True);
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.True);
                CollectionAssert.AreEqual(materials[i], host.Orders.GetDisplayedMaterials(i));
            }
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(17));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExpiredButtonAndDirectApiCannotConsumeMaterialsOrCreateAReceipt(bool throughButton)
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            var order = host.SourceOrders[0];
            host.Clock.AdvanceTime(90);
            Assert.That(host.PackButton.IsInteractable(), Is.False);
            if (throughButton)
                ExecuteEvents.Execute(host.PackButton.gameObject, new PointerEventData(null)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            else
                Assert.That(host.Packing.TryPack().Status, Is.EqualTo(PopupStorePackingStatus.Unavailable));
            host.AssertNoTransaction(materials);
            Assert.That(order.IsPacked, Is.False);
            Assert.That(order.PackingReceipt, Is.Null);
        }
    }

    [Test]
    public void ClosingPausesAndReopeningResumesTheSameRemainingTimeAndSupplyHistory()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(12.5);
            var history = host.Entrance.SuppliedOrders.ToArray();
            host.Close();
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Assert.That(host.Clock.AdvanceTime(500), Is.False);
            host.Entrance.TickEntrance(500);
            host.Reopen();
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(77.5d));
            CollectionAssert.AreEqual(history, host.Entrance.SuppliedOrders.ToArray());
            Assert.That(host.Clock.AdvanceTime(.5), Is.True);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(77d));
        }
    }

    [Test]
    public void FocusApplicationManualAndViewPauseReasonsMustAllBeReleasedBeforeResume()
    {
        using (var host = new Host())
        {
            host.Start();
            host.Clock.AdvanceTime(5);
            host.Clock.SetApplicationFocused(false);
            host.Clock.SetApplicationPaused(true);
            host.Clock.SetManualPaused(true);
            host.Clock.SetViewVisible(false);
            host.Clock.SetApplicationFocused(true);
            host.Clock.SetViewVisible(true);
            host.Clock.SetApplicationPaused(false);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Assert.That(host.Clock.AdvanceTime(100), Is.False);
            Assert.That(host.Clock.CanStartCustomerEntry, Is.False);
            Assert.That(host.Clock.CanCommitPacking, Is.False);
            host.Clock.SetManualPaused(false);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(85d));
            Assert.That(host.Clock.AdvanceTime(1), Is.True);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(84d));
        }
    }

    [Test]
    public void TheMonotonicSourceExcludesBackgroundAndHiddenElapsedTime()
    {
        using (var host = new Host())
        {
            double now = 100;
            Write(host.Clock, "_timeSource", new Func<double>(() => now));
            host.Start();
            now += 2;
            host.Clock.SetApplicationFocused(false);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(88d));
            now += 1000;
            host.Clock.SynchronizeNow();
            host.Clock.SetApplicationFocused(true);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(88d));
            now += 3;
            host.View.Hide();
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(85d));
            now += 1000;
            host.View.Show();
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(85d));
            now += 4;
            host.Clock.SynchronizeNow();
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(81d));
        }
    }

    [Test]
    public void DirectPackingSynchronizesExpiredTimeBeforeSelectingAnExactOrder()
    {
        using (var host = new Host())
        {
            double now = 0;
            Write(host.Clock, "_timeSource", new Func<double>(() => now));
            host.StartThree();
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            now = 90;
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running), "No clock update has been requested yet.");
            Assert.That(host.Packing.TryPack().Status, Is.EqualTo(PopupStorePackingStatus.Unavailable));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            host.AssertNoTransaction(materials);
        }
    }

    [Test]
    public void TimeExpiringBetweenEligibilityAndFinalCommitRefusesTheTransactionAtomically()
    {
        using (var host = new Host())
        {
            Write(host.Clock, "_timeSource", new Func<double>(() => 0));
            host.StartThree();
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            int reads = 0;
            // The first real packing gate sees time remaining; the commit gate observes expiration.
            Write(host.Clock, "_timeSource", new Func<double>(() => ++reads == 1 ? 0 : 90));
            var result = host.Packing.TryPack();
            Assert.That(reads, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.Status, Is.EqualTo(PopupStorePackingStatus.Stale));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            host.AssertNoTransaction(materials);
            Assert.That(host.SourceOrders.All(order => !order.IsPacked), Is.True);
        }
    }

    [Test]
    public void ExpirationDuringSuccessPublicationPreservesTheAlreadyCommittedReceipt()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Fill(0);
            PopupStorePackingResult nested = null;
            PopupStorePackingReceipt receiptDuringCallback = null;
            Action<PopupStoreSalesCounter.ChangeReason> handler = reason =>
            {
                if (reason != PopupStoreSalesCounter.ChangeReason.Packed) return;
                receiptDuringCallback = host.SourceOrders[0].PackingReceipt;
                host.Clock.AdvanceTime(90);
                nested = host.Packing.TryPack();
            };
            host.Counter.Changed += handler;
            PopupStorePackingResult result;
            try { result = host.Packing.TryPack(); }
            finally { host.Counter.Changed -= handler; }
            Assert.That(result.Success, Is.True);
            Assert.That(nested.Status, Is.EqualTo(PopupStorePackingStatus.Busy));
            Assert.That(receiptDuringCallback, Is.SameAs(result.Receipt));
            Assert.That(host.Packing.LastReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.Packing.TryTakeReceipt(out var taken), Is.True);
            Assert.That(taken, Is.SameAs(result.Receipt));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [TestCase("Boom")]
    [TestCase("Move")]
    [TestCase("Leaving")]
    public void AlreadyCommittedDeliveryAndDepartureFinishAfterZeroWithoutAdditionalSupply(string phase)
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Fill(0);
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            host.Presentation.TickPresentation(0);
            host.ToPhase(phase == "Leaving" ? "Idle" : phase);
            if (phase == "Leaving") Assert.That(host.Entrance.GetState(0).ToString(), Is.EqualTo("Leaving"));
            host.Clock.AdvanceTime(90);
            if (host.Presentation.CurrentPhase.ToString() != "Idle") host.ToPhase("Idle");
            host.Entrance.TickEntrance(100);
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes[0].Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Entrance.GetState(0).ToString(), Is.EqualTo("Empty"));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(17));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.Packing.TryPack().Success, Is.False);
        }
    }

    [Test]
    public void EndCallbacksAndReopeningCannotReenterOrResetAnEndedSession()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            int events = 0;
            PopupStorePackingResult nested = null;
            host.Clock.Ended += () =>
            {
                events++;
                host.Clock.AdvanceTime(1);
                host.Clock.TryStartSession();
                nested = host.Packing.TryPack();
            };
            host.Clock.AdvanceTime(90);
            host.Close();
            host.Reopen();
            host.Clock.TryStartSession();
            Assert.That(nested.Success, Is.False);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(host.Clock.EndedCount, Is.EqualTo(1));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Clock.RemainingSeconds, Is.Zero);
            host.AssertNoTransaction(materials);
        }
    }

    [Test]
    public void ClosingTheBusinessPreservesAuthoredTwentyOrdersMaterialsAndAccountData()
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            var templates = (PopupStoreOrder[])Read(host.Entrance, "_testOrders");
            var signatures = templates.Select(order => string.Join("/", host.Resolve(order).Select(item => item.Id))).ToArray();
            host.StartThree();
            host.Fill(1);
            var counter = host.Counter.Materials.ToArray();
            var orders = host.SourceOrders.ToArray();
            host.Clock.AdvanceTime(90);
            host.Entrance.TickEntrance(100);
            CollectionAssert.AreEqual(counter, host.Counter.Materials.ToArray());
            CollectionAssert.AreEqual(orders, host.SourceOrders);
            CollectionAssert.AreEqual(signatures, templates.Select(order => string.Join("/", host.Resolve(order).Select(item => item.Id))).ToArray());
            Assert.That(templates, Has.Length.EqualTo(20));
            Assert.That(templates.All(order => !order.IsPacked), Is.True);
            Assert.That(host.Entrance.CompletedOrderCount, Is.Zero);
            Assert.That(host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true).Sum(list => list.MaterialCount), Is.EqualTo(38));
            account.AssertUnchanged();
        }
        account.AssertUnchanged();
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal Button PackButton => (Button)Read(Packing, "_packButton");

        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-business-clock-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Clock = Root.GetComponent<PopupStoreBusinessClock>();
                Assert.That(Clock, Is.Not.Null);
                Write(Clock, "_autoStartForEditorReview", false);
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                View.Init();
                View.Show();
                RefreshExisting();
                Assert.That(Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            }
            catch { Dispose(); throw; }
        }

        internal void Start(double seconds = 90)
        {
            Write(Clock, "_initialSeconds", seconds);
            Assert.That(Clock.TryStartSession(), Is.True);
        }
        internal void BeginCustomers()
        {
            Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Presentation.BeginVisibleSession(), Is.True);
        }
        internal void StartThree()
        {
            Start();
            BeginCustomers();
            Entrance.TickEntrance(0);
            Entrance.TickEntrance(4);
            Entrance.TickEntrance(4);
            Entrance.TickEntrance((float)Read(Entrance, "_entryDuration") + .01f);
            Assert.That(Entrance.SuppliedOrderCount, Is.EqualTo(3));
            for (int i = 0; i < 3; i++) Assert.That(Entrance.IsWaiting(i), Is.True);
        }
        internal PopupStoreMaterialEntry[] Resolve(PopupStoreOrder order)
        {
            Assert.That(order.TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            return materials;
        }
        internal void Fill(int slot)
        {
            foreach (var entry in Resolve(SourceOrders[slot])) Assert.That(Counter.TryAdd(entry), Is.True);
        }
        internal void AssertNoTransaction(PopupStoreMaterialEntry[] materials)
        {
            CollectionAssert.AreEqual(materials, Counter.Materials.ToArray());
            Assert.That(Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(Packing.LastReceipt, Is.Null);
            Assert.That(Packing.TryTakeReceipt(out _), Is.False);
        }
        internal void ToPhase(string phase)
        {
            for (int i = 0; i < 4000 && Presentation.CurrentPhase.ToString() != phase; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
        }
        internal void Close()
        {
            if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            View.Hide();
            // EditMode needs the real lifecycle callbacks explicitly; no runtime-only clock bypass is used.
            Entrance.PauseVisibleSession();
            Presentation.EndVisibleSession();
            Invoke(Packing, "OnDisable");
            Invoke(Progress, "OnDisable");
            Invoke(Counter, "OnDisable");
            Invoke(Orders, "OnDisable");
        }
        internal void Reopen()
        {
            View.Show();
            RefreshExisting();
            BeginCustomers();
        }
        private void RefreshExisting()
        {
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True);
            Assert.That(Progress.Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True);
        }
        public void Dispose()
        {
            if (Entrance != null) Entrance.PauseVisibleSession();
            if (Presentation != null)
            {
                if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
                Presentation.EndVisibleSession();
            }
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private sealed class AccountSnapshot
    {
        private readonly long _money = UserInfo.Money, _context = UserInfo.CustomerEncounterContextGeneration;
        private readonly int _dia = UserInfo.Dia, _visits = UserInfo.TotalCumulativeCustomerCount;
        private readonly object _score = typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money));
            Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits));
            Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.EqualTo(_score));
        }
    }
    private static void Invoke(object target, string name)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(target, null);
    }
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
