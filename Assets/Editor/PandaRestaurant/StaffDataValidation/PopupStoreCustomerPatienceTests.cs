using System;
using System.Collections.Generic;
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

/// <summary>Local patience on the authored popup and real order/packing/supply paths, without account or scene writes.</summary>
public sealed class PopupStoreCustomerPatienceTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [Test]
    public void AuthoredThreeGaugesUseIndependentNonRaycastingImagesAndTheExistingFont()
    {
        using (var host = new Host())
        {
            Assert.That(host.Root.GetComponents<PopupStoreCustomerPatience>(), Has.Length.EqualTo(1));
            Assert.That(Read(host.Patience, "_clock"), Is.SameAs(host.Clock));
            Assert.That(Read(host.Patience, "_orders"), Is.SameAs(host.Orders));
            Assert.That(Read(host.Patience, "_onePlushSeconds"), Is.EqualTo(18d));
            Assert.That(Read(host.Patience, "_twoPlushSeconds"), Is.EqualTo(22d));
            Assert.That(Read(host.Patience, "_threePlushSeconds"), Is.EqualTo(26d));
            var font = ((TMP_Text)Read(host.Clock, "_timeText")).font;
            var roots = new List<int>();
            var fills = new List<int>();
            var texts = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                roots.Add(host.GaugeRoot(i).GetInstanceID());
                fills.Add(host.FillImage(i).GetInstanceID());
                texts.Add(host.TimeText(i).GetInstanceID());
                Assert.That(host.GaugeRoot(i).GetComponentInParent<UIPopupStore>(true), Is.SameAs(host.View));
                Assert.That(host.FillImage(i).type, Is.EqualTo(Image.Type.Filled));
                Assert.That(host.FillImage(i).fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
                Assert.That(host.FillImage(i).raycastTarget, Is.False);
                Assert.That(host.TimeText(i).raycastTarget, Is.False);
                Assert.That(host.TimeText(i).font, Is.SameAs(font));
                Assert.That(host.GaugeRoot(i).activeInHierarchy, Is.False);
            }
            CollectionAssert.AllItemsAreUnique(roots);
            CollectionAssert.AllItemsAreUnique(fills);
            CollectionAssert.AllItemsAreUnique(texts);
        }
    }

    [TestCase(0, 1, 18d)]
    [TestCase(1, 2, 22d)]
    [TestCase(2, 3, 26d)]
    public void ActualArrivedOrderUsesItsPlushCountAndFullIdentity(int slot, int plushes, double seconds)
    {
        using (var host = new Host())
        {
            host.StartThree();
            var timer = host.Timer(slot);
            Assert.That(timer.Order, Is.SameAs(host.SourceOrders[slot]));
            Assert.That(timer.OrderId, Is.EqualTo(host.SourceOrders[slot].RuntimeId));
            Assert.That(timer.CustomerId, Is.EqualTo(host.SourceOrders[slot].CustomerId));
            Assert.That(timer.SlotIndex, Is.EqualTo(slot));
            Assert.That(timer.PlushCount, Is.EqualTo(plushes));
            Assert.That(timer.InitialPatience, Is.EqualTo(seconds));
            Assert.That(timer.RemainingPatience, Is.EqualTo(seconds));
            Assert.That(timer.IsRunning, Is.True);
            Assert.That(timer.IsExpired, Is.False);
            Assert.That(host.Patience.IsCurrent(timer), Is.True);
            Assert.That(host.Orders.Slots[slot].OrderFrame.gameObject.activeInHierarchy, Is.True);
            Assert.That(host.GaugeRoot(slot).activeInHierarchy, Is.True);
            Assert.That(host.FillImage(slot).fillAmount, Is.EqualTo(1f));
            Assert.That(host.TimeText(slot).text, Does.Contain(seconds.ToString("0")));
        }
    }

    [Test]
    public void EmptyAndEnteringHaveNoTimerAndArrivalDoesNotChargeThePreviousTravelTime()
    {
        using (var host = new Host())
        {
            host.Start();
            for (int i = 0; i < 3; i++) Assert.That(host.Patience.GetTimer(i), Is.Null);
            host.Entrance.TickEntrance(0);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            host.Clock.AdvanceTime(7);
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            Assert.That(host.Entrance.IsWaiting(0), Is.True);
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(18d));
            host.Clock.AdvanceTime(1.25);
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(16.75d));
        }
    }

    [Test]
    public void ThreeArrivalsHaveIndependentAnchorsAndDuplicateWaitingNeverRestartsThem()
    {
        using (var host = new Host())
        {
            host.Start();
            host.Entrance.TickEntrance(0);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            host.Clock.AdvanceTime(2);
            host.Entrance.TickEntrance(4);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            host.Clock.AdvanceTime(3);
            host.Entrance.TickEntrance(4);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            var timers = Enumerable.Range(0, 3).Select(host.Timer).ToArray();
            Assert.That(timers.Select(timer => timer.RemainingPatience), Is.EqualTo(new[] { 13d, 19d, 26d }));
            for (int i = 0; i < 5; i++)
            {
                host.Orders.ApplyCustomerEntryState();
                Assert.That(host.Entrance.BeginVisibleSession(), Is.True);
                Assert.That(host.Patience.Refresh(), Is.True);
            }
            for (int i = 0; i < 3; i++) Assert.That(host.Timer(i), Is.SameAs(timers[i]));
            Assert.That(timers.Select(timer => timer.RemainingPatience), Is.EqualTo(new[] { 13d, 19d, 26d }));
            host.Clock.AdvanceTime(.5);
            Assert.That(timers.Select(timer => timer.RemainingPatience), Is.EqualTo(new[] { 12.5d, 18.5d, 25.5d }));
        }
    }

    [Test]
    public void DuplicatePlushEntriesCountTwiceAndCounterEditsNeverChangeInitialPatience()
    {
        using (var host = new Host())
        {
            host.StartThree();
            Assert.That(host.SourceOrders[1].PlushIds, Is.EqualTo(new[] { "STAFF01", "STAFF01" }));
            var timer = host.Timer(1);
            host.Fill(2);
            Assert.That(host.Counter.Count, Is.EqualTo(5));
            Assert.That(timer.PlushCount, Is.EqualTo(2));
            Assert.That(timer.InitialPatience, Is.EqualTo(22d));
            host.Counter.Clear();
            host.Counter.Clear();
            Assert.That(host.Timer(1), Is.SameAs(timer));
            Assert.That(timer.RemainingPatience, Is.EqualTo(22d));
        }
    }

    [Test]
    public void InspectorDurationsApplyToFreshOrdersWithoutChangingTheirMaterials()
    {
        using (var host = new Host())
        {
            Write(host.Patience, "_onePlushSeconds", 11d);
            Write(host.Patience, "_twoPlushSeconds", 13d);
            Write(host.Patience, "_threePlushSeconds", 17d);
            Assert.That(host.Patience.Refresh(), Is.True);
            host.StartThree();
            Assert.That(Enumerable.Range(0, 3).Select(i => host.Timer(i).InitialPatience), Is.EqualTo(new[] { 11d, 13d, 17d }));
            Assert.That(host.SourceOrders.Select(order => order.PlushIds.Count), Is.EqualTo(new[] { 1, 2, 3 }));
        }
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidPatienceDurationFailsValidationWithoutCreatingAnOrderTimer(double duration)
    {
        using (var host = new Host())
        {
            Write(host.Patience, "_onePlushSeconds", duration);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreCustomerPatience: "));
            Assert.That(host.Patience.Refresh(), Is.False);
            Assert.That(host.Patience.IsReady, Is.False);
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            Write(host.Patience, "_onePlushSeconds", 18d);
            Assert.That(host.Patience.Refresh(), Is.True);
        }
    }

    [Test]
    public void ZeroDurationExpiresOnceOnlyAfterActualArrival()
    {
        using (var host = new Host())
        {
            Write(host.Patience, "_onePlushSeconds", 0d);
            Assert.That(host.Patience.Refresh(), Is.True);
            int events = 0;
            host.Patience.Expired += timer => events++;
            host.Start();
            host.Entrance.TickEntrance(0);
            Assert.That(host.Patience.ExpiredCount, Is.Zero);
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            Assert.That(host.Timer(0).IsExpired, Is.True);
            Assert.That(host.Timer(0).RemainingPatience, Is.Zero);
            Assert.That(events, Is.EqualTo(1));
            host.Patience.Refresh();
            host.Clock.AdvanceTime(.5);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(host.SourceOrders[0].IsExpired, Is.True);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
        }
    }

    [Test]
    public void AVisibleZeroDurationOrderDoesNotExpireBeforeTheBusinessStarts()
    {
        using (var host = new Host(false, new[] { MakeOrder("CUSTOMER01") }, 0d))
        {
            var timer = host.Timer(0);
            Assert.That(timer.RemainingPatience, Is.Zero);
            Assert.That(timer.IsExpired, Is.False);
            Assert.That(timer.IsRunning, Is.False);
            Assert.That(host.Patience.ExpiredCount, Is.Zero);
            int events = 0;
            host.Patience.Expired += value => events++;
            host.Patience.Refresh();
            Assert.That(events, Is.Zero);
            host.Start();
            Assert.That(timer.IsExpired, Is.True);
            Assert.That(events, Is.EqualTo(1));
            host.Patience.Refresh();
            Assert.That(events, Is.EqualTo(1));
        }
    }

    [Test]
    public void DisabledOrderDisplayRetainsTimersButDoesNotChargeTheHiddenOrderInterval()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(2);
            var timers = Enumerable.Range(0, 3).Select(host.Timer).ToArray();
            var before = host.Remaining();
            host.Orders.enabled = false;
            Invoke(host.Orders, "OnDisable");
            Assert.That(host.Patience.Refresh(), Is.True);
            host.Clock.AdvanceTime(3);
            Assert.That(timers.Select(timer => timer.RemainingPatience), Is.EqualTo(before));
            Assert.That(timers.All(timer => !timer.IsRunning), Is.True);
            host.Orders.enabled = true;
            Assert.That(host.Orders.Refresh(), Is.True);
            Assert.That(host.Entrance.BeginVisibleSession(), Is.True);
            Assert.That(host.Patience.Refresh(), Is.True);
            host.Clock.AdvanceTime(1);
            Assert.That(host.Remaining(), Is.EqualTo(before.Select(value => value - 1).ToArray()));
            for (int i = 0; i < 3; i++) Assert.That(host.Timer(i), Is.SameAs(timers[i]));
        }
    }

    [TestCase("foreign-root")]
    [TestCase("order-frame")]
    public void InvalidGaugeRootCannotHideAnotherPopupOrTheExistingOrderFrame(string invalid)
    {
        using (var host = new Host())
        using (var other = Host.Static())
        {
            host.StartThree();
            other.Start();
            var slot = ((Array)Read(host.Patience, "_slots")).GetValue(0);
            var original = host.GaugeRoot(0);
            var protectedRoot = invalid == "foreign-root" ? other.GaugeRoot(0) : host.Orders.Slots[0].OrderFrame.gameObject;
            Assert.That(protectedRoot.activeInHierarchy, Is.True);
            try
            {
                Write(slot, "_root", protectedRoot);
                LogAssert.Expect(LogType.Error, new Regex("^PopupStoreCustomerPatience: "));
                Assert.That(host.Patience.Refresh(), Is.False);
                Assert.That(protectedRoot.activeInHierarchy, Is.True);
                Assert.That(host.SourceOrders[0].IsPacked, Is.False);
            }
            finally
            {
                Write(slot, "_root", original);
                Assert.That(host.Patience.Refresh(), Is.True);
            }
        }
    }

    [Test]
    public void AnUnstartedBusinessDoesNotRunAlreadyVisibleStaticOrderTimers()
    {
        using (var host = Host.Static())
        {
            var timer = host.Timer(0);
            Assert.That(timer.IsRunning, Is.False);
            Assert.That(host.Clock.AdvanceTime(100), Is.False);
            Assert.That(timer.RemainingPatience, Is.EqualTo(18d));
            host.Start();
            host.Clock.AdvanceTime(2);
            Assert.That(timer.IsRunning, Is.True);
            Assert.That(timer.RemainingPatience, Is.EqualTo(16d));
        }
    }

    [TestCase("manual")]
    [TestCase("background")]
    [TestCase("focus")]
    public void BusinessPauseStopsAllTimersAndResumeConsumesOnlyNewRunningTime(string reason)
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(2);
            var before = host.Remaining();
            SetPause(host.Clock, reason, true);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Assert.That(host.Clock.AdvanceTime(100), Is.False);
            Assert.That(host.Remaining(), Is.EqualTo(before));
            Assert.That(Enumerable.Range(0, 3).All(i => !host.Timer(i).IsRunning), Is.True);
            SetPause(host.Clock, reason, false);
            host.Clock.AdvanceTime(1);
            Assert.That(host.Remaining(), Is.EqualTo(before.Select(value => value - 1).ToArray()));
        }
    }

    [Test]
    public void RepeatedCloseAndReopenPreservesThreeTimersWithoutExtraListenersOrObjects()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(2);
            var timers = Enumerable.Range(0, 3).Select(host.Timer).ToArray();
            var before = host.Remaining();
            int count = host.Root.GetComponentsInChildren<Transform>(true).Length;
            for (int round = 0; round < 4; round++)
            {
                host.Close();
                Assert.That(host.Clock.AdvanceTime(100), Is.False);
                Assert.That(timers.Select(timer => timer.RemainingPatience), Is.EqualTo(before));
                host.Reopen();
                for (int i = 0; i < 3; i++) Assert.That(host.Timer(i), Is.SameAs(timers[i]));
            }
            host.Clock.AdvanceTime(1);
            Assert.That(host.Remaining(), Is.EqualTo(before.Select(value => value - 1).ToArray()));
            Assert.That(host.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
        }
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidClockDeltaCannotCorruptAnyPatience(double elapsed)
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(1);
            var before = host.Remaining();
            Assert.That(host.Clock.AdvanceTime(elapsed), Is.False);
            Assert.That(host.Remaining(), Is.EqualTo(before));
            Assert.That(host.Patience.ExpiredCount, Is.Zero);
        }
    }

    [Test]
    public void OversizedFinalFrameConsumesOnlyRemainingBusinessTimeAndReentryStaysFrozen()
    {
        using (var host = Host.Static())
        {
            host.Start(5);
            host.Clock.AdvanceTime(100);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(5d));
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(13d));
            Assert.That(host.Timer(0).IsRunning, Is.False);
            Assert.That(host.Patience.ExpiredCount, Is.Zero);
            host.Close();
            host.Reopen();
            Assert.That(host.Clock.AdvanceTime(10), Is.False);
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(13d));
            Assert.That(host.Timer(0).IsRunning, Is.False);
        }
    }

    [Test]
    public void SharedMonotonicClockQueriesAndTransitionSynchronizationsDoNotDoubleCharge()
    {
        using (var host = new Host())
        {
            double now = 100;
            Write(host.Clock, "_timeSource", new Func<double>(() => now));
            host.StartThree();
            now += 2.25;
            host.Clock.SynchronizeNow();
            host.Clock.SynchronizeNow();
            Assert.That(host.Clock.CanCommitPacking, Is.True);
            Assert.That(host.Patience.Refresh(), Is.True);
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(15.75d));
            host.Close();
            now += 1000;
            host.Reopen();
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(15.75d));
            now += .75;
            host.Clock.SynchronizeNow();
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(15d));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(3d));
        }
    }

    [Test]
    public void TimeAcceptedBeforeWaitingCannotBeChargedRetroactivelyOnArrival()
    {
        using (var host = new Host())
        {
            double now = 0;
            Write(host.Clock, "_timeSource", new Func<double>(() => now));
            host.Start();
            host.Entrance.TickEntrance(0);
            now = 6;
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            host.Clock.SynchronizeNow();
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(18d));
            now = 7;
            host.Clock.SynchronizeNow();
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(17d));
        }
    }

    [Test]
    public void ScrollInputAndMaterialSelectionPreserveCounterRulesWhilePatienceContinues()
    {
        using (var host = new Host())
        {
            host.StartThree();
            Assert.That(host.Counter.TryAdd(host.Resolve(host.SourceOrders[0])[0]), Is.True);
            var materials = host.Counter.Materials.ToArray();
            int feedback = host.FeedbackCount;
            foreach (var list in host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true))
            {
                var scroll = (ScrollRect)Read(list, "_scrollRect");
                scroll.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0, -1) });
                scroll.horizontalNormalizedPosition = .5f;
                host.Clock.AdvanceTime(.5);
            }
            Assert.That(host.Timer(0).RemainingPatience, Is.EqualTo(16.5d));
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            Assert.That(host.Progress.GetMatchedCount(0), Is.EqualTo(1));
            Assert.That(host.Progress.GetMatchedCount(1), Is.EqualTo(1));
            Assert.That(host.FeedbackCount, Is.EqualTo(feedback));
            Assert.That(host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true).Sum(list => list.MaterialCount), Is.EqualTo(38));
        }
    }

    [Test]
    public void CommittedOrderStopsBeforeCounterPublicationAndBusyReentryDoesNotStopOtherTimers()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(2);
            var timer = host.Timer(0);
            host.Fill(0);
            bool observed = false;
            PopupStorePackingResult nested = null;
            host.Counter.Changed += reason =>
            {
                if (reason != PopupStoreSalesCounter.ChangeReason.Packed) return;
                observed = true;
                Assert.That(host.SourceOrders[0].IsPacked, Is.True);
                Assert.That(timer.IsRunning, Is.False, "Receipt has committed before OrderDisplay publishes completion.");
                host.Clock.AdvanceTime(2);
                nested = host.Packing.TryPack();
            };
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(observed, Is.True);
            Assert.That(nested.Status, Is.EqualTo(PopupStorePackingStatus.Busy));
            Assert.That(timer.RemainingPatience, Is.EqualTo(16d));
            Assert.That(host.Timer(1).RemainingPatience, Is.EqualTo(18d));
            Assert.That(host.Timer(2).RemainingPatience, Is.EqualTo(22d));
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
        }
    }

    [TestCase("None")]
    [TestCase("Multiple")]
    [TestCase("Busy")]
    [TestCase("Stale")]
    public void RejectedPackingDoesNotStopOrResetAnUncommittedOrder(string status)
    {
        var orders = status == "Multiple"
            ? new[] { MakeOrder("CUSTOMER01"), MakeOrder("CUSTOMER04") }
            : new[] { MakeOrder("CUSTOMER01") };
        using (var host = Host.Static(orders))
        {
            host.Start();
            host.Clock.AdvanceTime(2);
            var timer = host.Timer(0);
            if (status != "None") host.Fill(0);
            if (status == "Busy") Write(host.Packing, "_busy", true);
            if (status == "Stale") Write(host.SourceOrders[0], "_plushIds", new[] { "STAFF02" });
            var materials = host.Counter.Materials.ToArray();
            var result = host.Packing.TryPack();
            Assert.That(result.Status.ToString(), Is.EqualTo(status));
            if (status == "Busy") Write(host.Packing, "_busy", false);
            if (status == "Stale") Write(host.SourceOrders[0], "_plushIds", new[] { "STAFF01" });
            host.Clock.AdvanceTime(1);
            Assert.That(host.Timer(0), Is.SameAs(timer));
            Assert.That(timer.RemainingPatience, Is.EqualTo(15d));
            Assert.That(timer.IsRunning, Is.True);
            Assert.That(host.SourceOrders.All(order => !order.IsPacked), Is.True);
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
        }
    }

    [Test]
    public void ExpirationClampsAndPublishesOnceWithIdentityAndBlocksPackingWithoutConsumingMaterials()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var timer = host.Timer(0);
            host.Fill(0);
            var counterBefore = host.Counter.Materials.ToArray();
            var events = new List<PopupStorePatienceTimer>();
            host.Patience.Expired += events.Add;
            host.Clock.AdvanceTime(18);
            Assert.That(timer.RemainingPatience, Is.Zero);
            Assert.That(timer.IsExpired, Is.True);
            Assert.That(timer.IsRunning, Is.False);
            Assert.That(host.Patience.ExpiredCount, Is.EqualTo(1));
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0], Is.SameAs(timer));
            Assert.That(events[0].OrderId, Is.EqualTo(host.SourceOrders[0].RuntimeId));
            Assert.That(events[0].CustomerId, Is.EqualTo("CUSTOMER01"));
            Assert.That(events[0].SlotIndex, Is.Zero);
            // E3 replaces the E2 marker-only policy: the same terminal identity is now unavailable
            // for packing, and its order/gauge is hidden before the complaint/departure animation.
            Assert.That(host.SourceOrders[0].IsExpired, Is.True);
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.None));
            Assert.That(host.Progress.GetMatchedCount(0), Is.Zero);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(69d));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(18d));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
            CollectionAssert.AreEqual(counterBefore, host.Counter.Materials.ToArray());
            host.Clock.AdvanceTime(1);
            host.Orders.ApplyCustomerEntryState();
            host.Patience.Refresh();
            Assert.That(host.Patience.ExpiredCount, Is.EqualTo(1));
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(timer.RemainingPatience, Is.Zero);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
            Assert.That(host.Packing.TryPack().Success, Is.False, "E3 expiration must reject the expired order without consuming its materials.");
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(host.Packing.LastReceipt, Is.Null);
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.Null);
            CollectionAssert.AreEqual(counterBefore, host.Counter.Materials.ToArray());
        }
    }

    [Test]
    public void ExpiredOrderNeverRestartsAcrossRepeatedRefreshAndHideReopen()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(18);
            var timer = host.Timer(0);
            for (int i = 0; i < 3; i++) { host.Close(); host.Reopen(); host.Patience.Refresh(); }
            Assert.That(host.Timer(0), Is.SameAs(timer));
            Assert.That(timer.RemainingPatience, Is.Zero);
            Assert.That(timer.IsExpired, Is.True);
            Assert.That(timer.IsRunning, Is.False);
            Assert.That(host.Patience.ExpiredCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ExpirationCallbackMayReadRefreshAndCloseWithoutDoubleChargingOtherSlots()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var timers = Enumerable.Range(0, 3).Select(host.Timer).ToArray();
            int events = 0;
            host.Patience.Expired += timer =>
            {
                events++;
                Assert.That(timer, Is.SameAs(timers[0]));
                Assert.That(timers[1].RemainingPatience, Is.EqualTo(4d));
                Assert.That(timers[2].RemainingPatience, Is.EqualTo(8d));
                host.Clock.SynchronizeNow();
                host.Patience.Refresh();
                host.View.Hide();
            };
            host.Clock.AdvanceTime(18);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            Assert.That(timers.Select(timer => timer.RemainingPatience), Is.EqualTo(new[] { 0d, 4d, 8d }));
            host.Reopen();
            host.Clock.AdvanceTime(1);
            Assert.That(timers.Select(timer => timer.RemainingPatience), Is.EqualTo(new[] { 0d, 3d, 7d }));
            Assert.That(events, Is.EqualTo(1));
        }
    }

    [Test]
    public void GaugeFillAndCeiledTimeFollowTheirOwnTimersWithoutChangingIconsOrTextFont()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var icons = host.Orders.Slots.SelectMany(slot => slot.MaterialIcons).Select(image => image.sprite).ToArray();
            var fonts = Enumerable.Range(0, 3).Select(i => host.TimeText(i).font).ToArray();
            host.Clock.AdvanceTime(4.25);
            var expected = new[] { 13.75d, 17.75d, 21.75d };
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.FillImage(i).fillAmount, Is.EqualTo((float)(expected[i] / host.Timer(i).InitialPatience)).Within(.0001f));
                Assert.That(host.TimeText(i).text, Does.Contain(Math.Ceiling(expected[i]).ToString("0")));
                Assert.That(host.TimeText(i).font, Is.SameAs(fonts[i]));
            }
            CollectionAssert.AreEqual(icons, host.Orders.Slots.SelectMany(slot => slot.MaterialIcons).Select(image => image.sprite).ToArray());
        }
    }

    [Test]
    public void GaugeColorThresholdsAndVisibilityOptionsDoNotResetTheUnderlyingTimer()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var timer = host.Timer(0);
            Assert.That(host.FillImage(0).color, Is.EqualTo((Color)Read(host.Patience, "_normalColor")));
            host.Clock.AdvanceTime(9);
            Assert.That(host.FillImage(0).color, Is.EqualTo((Color)Read(host.Patience, "_cautionColor")));
            host.Clock.AdvanceTime(6);
            Assert.That(host.FillImage(0).color, Is.EqualTo((Color)Read(host.Patience, "_urgentColor")));
            Write(host.Patience, "_showGauge", false);
            Assert.That(host.Patience.Refresh(), Is.True);
            Assert.That(host.FillImage(0).enabled, Is.False);
            Assert.That(host.TimeText(0).enabled, Is.True);
            Write(host.Patience, "_showTime", false);
            Assert.That(host.Patience.Refresh(), Is.True);
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            host.Clock.AdvanceTime(1);
            Write(host.Patience, "_showGauge", true);
            Write(host.Patience, "_showTime", true);
            Write(host.Patience, "_timeFormat", "{0:0} remaining");
            Assert.That(host.Patience.Refresh(), Is.True);
            Assert.That(host.Timer(0), Is.SameAs(timer));
            Assert.That(timer.RemainingPatience, Is.EqualTo(2d));
            Assert.That(host.TimeText(0).text, Is.EqualTo("2 remaining"));
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.True);
        }
    }

    [TestCase("Packed")]
    [TestCase("Leaving")]
    [TestCase("Empty")]
    public void CompletedLifecycleAndReentryCannotResurrectTheOldTimer(string phase)
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(2);
            var old = host.Timer(0);
            var order = host.SourceOrders[0];
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            Assert.That(receipt, Is.Not.Null);
            if (phase != "Packed") host.CompleteVisual();
            if (phase == "Empty")
            {
                host.Clock.AdvanceTime(90);
                host.Entrance.TickEntrance(host.ExitDuration + .01f);
            }
            Assert.That(host.Entrance.GetState(0).ToString(), Is.EqualTo(phase));
            Assert.That(old.RemainingPatience, Is.EqualTo(16d));
            Assert.That(old.IsRunning, Is.False);
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            host.Close();
            host.Reopen();
            host.Clock.AdvanceTime(1);
            Assert.That(order.PackingReceipt, Is.SameAs(receipt));
            Assert.That(old.RemainingPatience, Is.EqualTo(16d));
            Assert.That(old.IsRunning, Is.False);
            Assert.That(host.GaugeRoot(0).activeInHierarchy, Is.False);
            if (phase == "Empty") Assert.That(host.Patience.GetTimer(0), Is.Null);
        }
    }

    [Test]
    public void ActualReuseStartsAFreshTimerForSameCustomerAndOldVisualCallbacksCannotAffectIt()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(5);
            var old = host.Timer(0);
            var peers = new[] { host.Timer(1), host.Timer(2) };
            host.Fill(0);
            Assert.That(host.Packing.TryPack().Success, Is.True);
            host.CompleteVisual();
            var outcome = host.Presentation.VisualOutcomes[0];
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            for (int i = 0; i < 20 && host.Entrance.SuppliedOrderCount < 4; i++) host.Entrance.TickEntrance(.5f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(4));
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            var fresh = host.Timer(0);
            Assert.That(fresh.CustomerId, Is.EqualTo(old.CustomerId));
            Assert.That(fresh.OrderId, Is.Not.EqualTo(old.OrderId));
            Assert.That(fresh.Order, Is.Not.SameAs(old.Order));
            Assert.That(fresh.InitialPatience, Is.EqualTo(18d));
            Assert.That(fresh.RemainingPatience, Is.EqualTo(18d));
            Assert.That(host.Patience.IsCurrent(old), Is.False);
            for (int i = 0; i < 3; i++) Invoke(host.Entrance, "HandleVisualEnded", outcome);
            host.Clock.AdvanceTime(1);
            Assert.That(fresh.RemainingPatience, Is.EqualTo(17d));
            Assert.That(old.RemainingPatience, Is.EqualTo(13d));
            Assert.That(host.Timer(1), Is.SameAs(peers[0]));
            Assert.That(host.Timer(2), Is.SameAs(peers[1]));
            Assert.That(peers.Select(timer => timer.RemainingPatience), Is.EqualTo(new[] { 16d, 20d }));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
        }
    }

    [TestCase("order-reference")]
    [TestCase("order-id")]
    [TestCase("customer-id")]
    [TestCase("slot")]
    public void TimerIdentityRejectsChangedOrderReferenceIdCustomerOrSlot(string change)
    {
        using (var host = Host.Static())
        {
            host.Start();
            var timer = host.Timer(0);
            switch (change)
            {
                case "order-reference": host.SourceOrders[0] = MakeOrder("CUSTOMER01").CreateRuntimeCopy(); break;
                case "order-id": Write(host.SourceOrders[0], "_runtimeId", Guid.NewGuid().ToString("N")); break;
                case "customer-id": Write(host.SourceOrders[0], "_customerId", "CUSTOMER04"); break;
                case "slot": Write(host.Orders, "_sampleOrders", new[] { MakeOrder("CUSTOMER04"), timer.Order }); break;
            }
            Assert.That(host.Patience.IsCurrent(timer), Is.False);
            Assert.That(timer.RemainingPatience, Is.EqualTo(18d));
            Assert.That(timer.IsRunning, Is.False);
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
        }
    }

    [Test]
    public void ACommittedDeliveryStillFinishesAfterClosingTimeWhileAllUnpackedPatienceFreezes()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Clock.AdvanceTime(2);
            var packed = host.Timer(0);
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            host.Presentation.TickPresentation(0);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
            host.Clock.AdvanceTime(100);
            var frozen = new[] { host.Timer(1).RemainingPatience, host.Timer(2).RemainingPatience };
            host.CompleteVisual(false);
            host.Entrance.TickEntrance(100);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(packed.RemainingPatience, Is.EqualTo(16d));
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            host.Clock.AdvanceTime(100);
            Assert.That(new[] { host.Timer(1).RemainingPatience, host.Timer(2).RemainingPatience }, Is.EqualTo(frozen));
            Assert.That(host.Packing.TryPack().Success, Is.False);
        }
    }

    [Test]
    public void AllTwentyOrdersKeepTheirSequenceDistributionAndIndependentTimerIdentities()
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            var templates = (PopupStoreOrder[])Read(host.Entrance, "_testOrders");
            var expected = templates.Select(Signature).ToArray();
            Assert.That(templates.Count(order => order.PlushIds.Count == 1), Is.EqualTo(8));
            Assert.That(templates.Count(order => order.PlushIds.Count == 2), Is.EqualTo(8));
            Assert.That(templates.Count(order => order.PlushIds.Count == 3), Is.EqualTo(4));
            Assert.That(Read(host.Entrance, "_entryInterval"), Is.EqualTo(4f));
            int objects = host.Root.GetComponentsInChildren<Transform>(true).Length;
            var timers = new Dictionary<string, PopupStorePatienceTimer>();
            host.Start();
            int safety = 0;
            while (host.Entrance.CompletedOrderCount < 20 && safety++ < 1000)
            {
                host.Entrance.TickEntrance(.5f);
                int slot = Enumerable.Range(0, 3).Where(host.Entrance.IsWaiting).DefaultIfEmpty(-1).First();
                if (slot < 0) continue;
                var timer = host.Timer(slot);
                Assert.That(timer.Order, Is.SameAs(host.SourceOrders[slot]));
                Assert.That(timer.InitialPatience, Is.EqualTo(new[] { 18d, 22d, 26d }[timer.PlushCount - 1]));
                Assert.That(timers.ContainsKey(timer.OrderId), Is.False);
                timers.Add(timer.OrderId, timer);
                host.Clock.AdvanceTime(.1);
                host.Fill(slot);
                Assert.That(host.Packing.TryPack().Success, Is.True);
                Assert.That(timer.IsRunning, Is.False);
                host.CompleteVisual();
                host.Entrance.TickEntrance(host.ExitDuration + .01f);
            }
            Assert.That(safety, Is.LessThan(1000));
            for (int i = 0; i < 5; i++) host.Entrance.TickEntrance(100);
            Assert.That(timers, Has.Count.EqualTo(20));
            Assert.That(host.Entrance.CompletedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(20));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.OccupiedSlotCount, Is.Zero);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(20));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(20));
            CollectionAssert.AreEqual(expected, host.Entrance.SuppliedOrders.Select(Signature).ToArray());
            Assert.That(timers.Values.All(timer => !timer.IsRunning && !host.Patience.IsCurrent(timer)), Is.True);
            Assert.That(host.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
            Assert.That(Time.timeScale, Is.EqualTo(account.TimeScale));
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
        internal readonly PopupStoreCustomerPatience Patience;
        private readonly bool _automatic;
        internal int FeedbackCount;
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float EntryDuration => (float)Read(Entrance, "_entryDuration");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");

        internal Host(bool automatic = true, PopupStoreOrder[] orders = null, double? onePlushSeconds = null)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                _automatic = automatic;
                Root.name = "popup-store-patience-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Clock = Root.GetComponent<PopupStoreBusinessClock>();
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Patience = Root.GetComponent<PopupStoreCustomerPatience>();
                Assert.That(Patience, Is.Not.Null);
                if (onePlushSeconds.HasValue) Write(Patience, "_onePlushSeconds", onePlushSeconds.Value);
                Write(Clock, "_autoStartForEditorReview", false);
                Write(Entrance, "_automaticEntry", automatic);
                if (!automatic) Write(Entrance, "_fixedTestSupply", false);
                if (orders != null) Write(Orders, "_sampleOrders", orders);
                View.Init();
                View.Show();
                RefreshExisting();
                if (_automatic) Assert.That(Entrance.BeginVisibleSession(), Is.True);
                Assert.That(Presentation.BeginVisibleSession(), Is.True);
                Assert.That(Patience.Refresh(), Is.True);
                Progress.FeedbackTriggered += CountFeedback;
                Assert.That(Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            }
            catch { Dispose(); throw; }
        }

        internal static Host Static(PopupStoreOrder[] orders = null)
            => new Host(false, orders ?? new[] { MakeOrder("CUSTOMER01") });
        internal void Start(double seconds = 90)
        {
            Write(Clock, "_initialSeconds", seconds);
            Assert.That(Clock.TryStartSession(), Is.True);
        }
        internal void StartThree()
        {
            Start();
            Entrance.TickEntrance(0);
            Entrance.TickEntrance(4);
            Entrance.TickEntrance(4);
            Entrance.TickEntrance(EntryDuration + .01f);
            for (int i = 0; i < 3; i++) Assert.That(Entrance.IsWaiting(i), Is.True);
        }
        internal PopupStorePatienceTimer Timer(int slot)
        {
            var timer = Patience.GetTimer(slot);
            Assert.That(timer, Is.Not.Null, "slot " + slot);
            return timer;
        }
        internal double[] Remaining() => Enumerable.Range(0, 3).Select(i => Timer(i).RemainingPatience).ToArray();
        internal PopupStoreMaterialEntry[] Resolve(PopupStoreOrder order)
        {
            Assert.That(order.TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            return materials;
        }
        internal void Fill(int slot)
        {
            foreach (var material in Resolve(SourceOrders[slot])) Assert.That(Counter.TryAdd(material), Is.True);
        }
        private object Gauge(int slot) => ((Array)Read(Patience, "_slots")).GetValue(slot);
        internal GameObject GaugeRoot(int slot) => (GameObject)Read(Gauge(slot), "_root");
        internal Image FillImage(int slot) => (Image)Read(Gauge(slot), "_fill");
        internal TMP_Text TimeText(int slot) => (TMP_Text)Read(Gauge(slot), "_timeText");
        internal void CompleteVisual(bool begin = true)
        {
            if (begin) Presentation.TickPresentation(0);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != "Idle"; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo("Idle"));
        }
        internal void Close()
        {
            if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            View.Hide();
            Entrance.PauseVisibleSession();
            Presentation.EndVisibleSession();
            Invoke(Patience, "OnDisable");
            Invoke(Packing, "OnDisable");
            Invoke(Progress, "OnDisable");
            Invoke(Counter, "OnDisable");
            Invoke(Orders, "OnDisable");
        }
        internal void Reopen()
        {
            View.Show();
            RefreshExisting();
            if (_automatic) Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Presentation.BeginVisibleSession(), Is.True);
            Assert.That(Patience.Refresh(), Is.True);
        }
        private void RefreshExisting()
        {
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True);
            Assert.That(Progress.Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True);
        }
        private void CountFeedback() => FeedbackCount++;
        public void Dispose()
        {
            if (Progress != null) Progress.FeedbackTriggered -= CountFeedback;
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
        internal readonly float TimeScale = Time.timeScale;
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money));
            Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits));
            Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.EqualTo(_score));
            Assert.That(Time.timeScale, Is.EqualTo(TimeScale));
        }
    }
    private static PopupStoreOrder MakeOrder(string customer)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", new[] { "STAFF01" });
        Write(order, "_boxId", "BOX_PINK");
        Write(order, "_ribbonId", "RIBBON_YELLOW");
        return order;
    }
    private static string Signature(PopupStoreOrder order)
        => order.CustomerId + ":" + string.Join(",", order.PlushIds) + ":" + order.BoxId + ":" + order.RibbonId;
    private static void SetPause(PopupStoreBusinessClock clock, string reason, bool value)
    {
        if (reason == "manual") clock.SetManualPaused(value);
        else if (reason == "background") clock.SetApplicationPaused(value);
        else clock.SetApplicationFocused(!value);
    }
    private static void Invoke(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(target, args);
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
