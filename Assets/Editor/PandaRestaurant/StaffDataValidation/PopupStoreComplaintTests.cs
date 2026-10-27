using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real expiration, packing, complaint departure and supply boundaries on memory instances of the game prefab.</summary>
public sealed class PopupStoreComplaintTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string AngerPath = "Assets/Sprites/Customer/감정표현/분노.png";

    [Test]
    public void AuthoredComplaintUsesThreeOriginalAngerSpritesUnderTheirOwnUnchangedPortraits()
    {
        using (var host = new Host())
        {
            Assert.That(Read(host.Patience, "_complaintPenaltySeconds"), Is.EqualTo(3d));
            Assert.That(host.ComplaintDuration, Is.EqualTo(.6f));
            var ids = new List<int>();
            for (int slot = 0; slot < 3; slot++)
            {
                var icon = host.Anger(slot);
                ids.Add(icon.GetInstanceID());
                Assert.That(icon.transform.parent, Is.SameAs(host.Portrait(slot).transform));
                Assert.That(AssetDatabase.GetAssetPath(icon.sprite), Is.EqualTo(AngerPath));
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(icon.gameObject.activeSelf, Is.False);
            }
            CollectionAssert.AllItemsAreUnique(ids);
        }
    }

    [TestCase(0, 18d)]
    [TestCase(1, 22d)]
    [TestCase(2, 26d)]
    public void EachRealOrderExpiresAtItsOwnDurationAndOnlyItsCustomerEntersComplaint(int target, double duration)
    {
        var durations = new[] { 60d, 60d, 60d };
        durations[target] = duration;
        using (var host = new Host(durations))
        {
            host.StartCustomers();
            var orders = host.SourceOrders.ToArray();
            var sprites = host.Orders.Slots.Select(slot => slot.CustomerImage.sprite).ToArray();
            var positions = host.Orders.Slots.Select(slot => slot.CustomerImage.transform.localPosition).ToArray();
            var timer = host.Timer(target);
            int signals = 0;
            host.Patience.Expired += value => { signals++; Assert.That(value, Is.SameAs(timer)); };
            host.Clock.AdvanceTime(duration);
            Assert.That(timer.RemainingPatience, Is.Zero);
            Assert.That(timer.IsExpired, Is.True);
            Assert.That(timer.IsRunning, Is.False);
            Assert.That(orders[target].IsExpired, Is.True);
            Assert.That(orders[target].IsPacked, Is.False);
            Assert.That(host.Entrance.GetState(target), Is.EqualTo(PopupStoreCustomerState.Expired));
            Assert.That(host.Entrance.GetDepartureReason(target), Is.EqualTo(PopupStoreDepartureReason.PatienceExpired));
            Assert.That(host.Anger(target).gameObject.activeInHierarchy, Is.True);
            Assert.That(host.Orders.Slots[target].OrderFrame.gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Gauge(target).activeInHierarchy, Is.False);
            Assert.That(signals, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(90 - duration - 3));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(duration));
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.That(host.SourceOrders[slot], Is.SameAs(orders[slot]));
                Assert.That(host.Portrait(slot).sprite, Is.SameAs(sprites[slot]));
                Assert.That(host.Portrait(slot).transform.localPosition, Is.EqualTo(positions[slot]));
                if (slot == target) continue;
                Assert.That(host.Entrance.GetState(slot), Is.EqualTo(PopupStoreCustomerState.Waiting));
                Assert.That(host.Anger(slot).gameObject.activeInHierarchy, Is.False);
                Assert.That(host.Timer(slot).RemainingPatience, Is.EqualTo(60 - duration));
            }
            host.AssertNoSale();
        }
    }

    [Test]
    public void EmptyAndEnteringCannotExpireOrSpendBusinessPenaltyBeforeArrival()
    {
        using (var host = new Host(new[] { 0d, 22d, 26d }))
        {
            host.Start();
            host.Entrance.TickEntrance(0);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            host.Clock.AdvanceTime(2);
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(host.Entrance.StartedComplaintCount, Is.Zero);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(85d));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(2d));
        }
    }

    [Test]
    public void PenaltyDoesNotBroadcastExtraElapsedTimeOrChargeOtherCustomersAndTheClockKeepsItsRate()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            int advances = 0;
            host.Clock.TimeAdvanced += elapsed => advances++;
            float scale = Time.timeScale;
            host.Clock.AdvanceTime(18);
            Assert.That(advances, Is.EqualTo(1));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(18d));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(69d));
            Assert.That(host.Timer(1).RemainingPatience, Is.EqualTo(4d));
            Assert.That(host.Timer(2).RemainingPatience, Is.EqualTo(8d));
            host.Clock.AdvanceTime(.5);
            Assert.That(advances, Is.EqualTo(2));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(68.5d));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(18.5d));
            Assert.That(host.Timer(1).RemainingPatience, Is.EqualTo(3.5d));
            Assert.That(host.Timer(2).RemainingPatience, Is.EqualTo(7.5d));
            Assert.That(Time.timeScale, Is.EqualTo(scale));
        }
    }

    [TestCase(0d)]
    [TestCase(2.5d)]
    public void InspectorPenaltyValueIsAppliedOnceWithoutChangingActualElapsedTime(double penalty)
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            Write(host.Patience, "_complaintPenaltySeconds", penalty);
            Assert.That(host.Patience.Refresh(), Is.True);
            host.StartCustomers();
            host.Clock.AdvanceTime(1);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(penalty));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(89d - penalty));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(1d));
            Assert.That(host.Timer(1).RemainingPatience, Is.EqualTo(59d));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void DuplicateExpiryAndDepartureRequestsCannotRepeatPenaltySignalOrSlotRelease()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            var timer = host.Timer(0);
            int events = 0;
            host.Patience.Expired += value => events++;
            host.Clock.AdvanceTime(1);
            for (int i = 0; i < 5; i++)
            {
                Assert.That(host.Patience.TryProcessExpiredBatch(new[] { timer, timer }), Is.Zero);
                Assert.That(host.Entrance.TryStartComplaint(timer), Is.False);
                host.Patience.Refresh();
            }
            Assert.That(events, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
            for (int i = 0; i < 3; i++) host.Patience.TryProcessExpiredBatch(new[] { timer });
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ExpirationFirstRemovesExactCandidateAndDirectPackingKeepsAllCounterMaterials()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
            host.Clock.AdvanceTime(1);
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.None));
            Assert.That(host.PackButton.IsInteractable(), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(host.Packing.TryPack().Success, Is.False);
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.Null);
            Assert.That(host.Progress.GetMatchedCount(0), Is.Zero);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            host.AssertNoSale();
        }
    }

    [Test]
    public void PackingJustBeforeExpiryKeepsItsReceiptAndLateExpirationCannotComplain()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            var timer = host.Timer(0);
            host.Clock.AdvanceTime(.99);
            host.Fill(0);
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            host.Clock.AdvanceTime(1);
            Assert.That(timer.RemainingPatience, Is.EqualTo(.01d).Within(.000001d));
            Assert.That(timer.IsExpired, Is.False);
            // Simulate a previously queued expiration signal arriving after the immutable successful receipt.
            SetProperty(timer, "IsExpired", true);
            Assert.That(host.Patience.TryProcessExpiredBatch(new[] { timer }), Is.Zero);
            Assert.That(host.Entrance.TryStartComplaint(timer), Is.False);
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            Assert.That(host.SourceOrders[0].IsExpired, Is.False);
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(host.Anger(0).gameObject.activeInHierarchy, Is.False);
            host.CompleteVisual();
            Assert.That(host.Entrance.GetDepartureReason(0), Is.EqualTo(PopupStoreDepartureReason.Packed));
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Entrance.StartedComplaintCount, Is.Zero);
        }
    }

    [Test]
    public void AnExpiredExactMatchCannotHideTheOtherIdenticalStillEligibleOrder()
    {
        var orders = new[] { Order("CUSTOMER01", "STAFF01"), Order("CUSTOMER04", "STAFF01") };
        using (var host = new Host(new[] { 1d, 60d, 60d }, orders))
        {
            host.Start();
            host.Entrance.TickEntrance(0);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            host.Clock.AdvanceTime(.6);
            host.Entrance.TickEntrance(4);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            host.Fill(0);
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.Multiple));
            Assert.That(host.Packing.TryPack().Status, Is.EqualTo(PopupStorePackingStatus.Multiple));
            Assert.That(host.Counter.Count, Is.EqualTo(3));
            host.Clock.AdvanceTime(.4);
            Assert.That(host.SourceOrders[0].IsExpired, Is.True);
            Assert.That(host.SourceOrders[1].IsExpired, Is.False);
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
            Assert.That(host.PackButton.IsInteractable(), Is.True);
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(1));
            Assert.That(result.Receipt.CustomerId, Is.EqualTo("CUSTOMER04"));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.Null);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void SuccessfulCommitPrecedesItsPublicationCallbackEvenIfThatCallbackReachesTheDeadline()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            host.Fill(0);
            var timer = host.Timer(0);
            PopupStorePackingResult nested = null;
            host.Counter.Changed += reason =>
            {
                if (reason != PopupStoreSalesCounter.ChangeReason.Packed) return;
                Assert.That(host.SourceOrders[0].IsPacked, Is.True);
                host.Clock.AdvanceTime(1);
                Assert.That(host.Patience.TryProcessExpiredBatch(new[] { timer }), Is.Zero);
                nested = host.Packing.TryPack();
            };
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(nested.Status, Is.EqualTo(PopupStorePackingStatus.Busy));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(host.Entrance.StartedComplaintCount, Is.Zero);
            Assert.That(host.SourceOrders[0].IsExpired, Is.False);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Counter.Count, Is.Zero);
        }
    }

    [Test]
    public void ExpiryBetweenInitialEligibilityAndFinalCommitRejectsTheStaleTransactionAtomically()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            Write(host.Clock, "_timeSource", new Func<double>(() => 0));
            host.StartCustomers();
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            int reads = 0;
            Write(host.Clock, "_timeSource", new Func<double>(() => ++reads == 1 ? 0 : 1));
            var result = host.Packing.TryPack();
            Assert.That(reads, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.Status, Is.EqualTo(PopupStorePackingStatus.Stale));
            Assert.That(host.SourceOrders[0].IsExpired, Is.True);
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.Null);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            host.AssertNoSale();
        }
    }

    [TestCase(18d)]
    [TestCase(100d)]
    public void NaturalBusinessEndWinsTheSameAcceptedTickAndNeverCreatesANewComplaint(double elapsed)
    {
        using (var host = new Host())
        {
            host.StartCustomers(seconds: 18);
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            var timers = Enumerable.Range(0, 3).Select(host.Timer).ToArray();
            host.Clock.AdvanceTime(elapsed);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Clock.EndedCount, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(host.SourceOrders.All(order => !order.IsExpired), Is.True);
            Assert.That(host.Entrance.StartedComplaintCount, Is.Zero);
            Assert.That(host.Patience.TryProcessExpiredBatch(timers), Is.Zero);
            Assert.That(host.Packing.TryPack().Success, Is.False);
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            for (int i = 0; i < 3; i++) Assert.That(host.Anger(i).gameObject.activeInHierarchy, Is.False);
            host.Close();
            host.Reopen();
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(host.SourceOrders.All(order => !order.IsExpired), Is.True);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void OneAtomicExpiryBatchCountsEveryOrderEvenWhenOnlyTwoBusinessSecondsRemain(int expiries)
    {
        var durations = new[] { 60d, 60d, 60d };
        for (int i = 0; i < expiries; i++) durations[i] = 18;
        using (var host = new Host(durations))
        {
            host.StartCustomers(seconds: 20);
            var timers = Enumerable.Range(0, 3).Select(host.Timer).ToArray();
            int ended = 0, advances = 0, signals = 0;
            host.Clock.Ended += () => ended++;
            host.Clock.TimeAdvanced += elapsed => advances++;
            host.Patience.Expired += timer =>
            {
                signals++;
                Assert.That(host.SourceOrders.Count(order => order.IsExpired), Is.EqualTo(expiries));
                Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(expiries));
            };
            host.Clock.AdvanceTime(18);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Clock.RemainingSeconds, Is.Zero);
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(18d));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(expiries));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(3d * expiries));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(expiries));
            Assert.That(signals, Is.EqualTo(expiries));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(advances, Is.EqualTo(1));
            for (int i = expiries; i < 3; i++) Assert.That(timers[i].RemainingPatience, Is.EqualTo(42d));
            Assert.That(host.Patience.TryProcessExpiredBatch(timers), Is.Zero);
            host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(expiries));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(expiries));
        }
    }

    [Test]
    public void PenaltyCrossingTheTenSecondCutoffPreventsReplacementAfterDeparture()
    {
        using (var host = new Host(new[] { 18d, 60d, 60d }))
        {
            host.StartCustomers(seconds: 31);
            host.Clock.AdvanceTime(18);
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(10d));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Running));
            Assert.That(host.Clock.CanStartCustomerEntry, Is.False);
            host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Empty));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(17));
            Assert.That(host.Anger(0).gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Gauge(0).activeInHierarchy, Is.False);
        }
    }

    [Test]
    public void ExpiryNotificationsCanCloseThePopupWithoutSplittingASimultaneousBatch()
    {
        using (var host = new Host(new[] { 1d, 1d, 1d }))
        {
            host.StartCustomers();
            int signals = 0;
            host.Patience.Expired += timer =>
            {
                signals++;
                Assert.That(host.SourceOrders.All(order => order.IsExpired), Is.True);
                Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(3));
                if (signals == 1) host.View.Hide();
            };
            host.Clock.AdvanceTime(1);
            Assert.That(signals, Is.EqualTo(3));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(3));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(80d));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Paused));
            host.Close();
            host.Reopen();
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(3));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(3));
            Assert.That(host.SourceOrders.All(order => order.IsExpired && !order.IsPacked), Is.True);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DirectDisableAtExpirationSettlesTheFinalIntervalAndReopenCommitsOnlyWhileStillRunning(bool naturalEnd)
    {
        using (var host = new Host(new[] { 18d, 60d, 60d }))
        {
            double now = 0;
            Write(host.Clock, "_timeSource", new Func<double>(() => now));
            host.StartCustomers(seconds: naturalEnd ? 18 : 90);
            var timer = host.Timer(0);
            now = 18;
            // Real direct deactivation occurs before OnDisable. EditMode explicitly invokes the existing lifecycle callback.
            host.Root.SetActive(false);
            Invoke(host.Clock, "OnDisable");
            Invoke(host.Patience, "OnDisable");
            Assert.That(timer.IsExpired, Is.True);
            Assert.That(timer.Order.IsExpired, Is.False, "Inactive view must not start a new complaint transaction.");
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            host.Close();
            now += 1000;
            host.Reopen();
            Assert.That(timer.Order.IsExpired, Is.EqualTo(!naturalEnd));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(naturalEnd ? 0 : 1));
            Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(naturalEnd ? 0d : 69d));
            Assert.That(host.Clock.ElapsedSeconds, Is.EqualTo(18d));
            for (int i = 0; i < 2; i++) { host.Close(); host.Reopen(); }
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(naturalEnd ? 0 : 1));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(naturalEnd ? 0 : 1));
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
        }
    }

    [Test]
    public void SameCustomerIdInTwoDifferentOrdersDoesNotStopTheUnexpiredSibling()
    {
        var orders = new[] { Order("CUSTOMER01", "STAFF01"), Order("CUSTOMER01", "STAFF01,STAFF01") };
        using (var host = new Host(new[] { 1d, 60d, 60d }, orders))
        {
            host.StartCustomers(2);
            var first = host.Timer(0);
            var second = host.Timer(1);
            Assert.That(first.CustomerId, Is.EqualTo(second.CustomerId));
            Assert.That(first.OrderId, Is.Not.EqualTo(second.OrderId));
            host.Clock.AdvanceTime(1);
            Assert.That(first.Order.IsExpired, Is.True);
            Assert.That(second.Order.IsExpired, Is.False);
            Assert.That(second.RemainingPatience, Is.EqualTo(59d));
            Assert.That(second.IsRunning, Is.True);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            Assert.That(host.Entrance.GetState(1), Is.EqualTo(PopupStoreCustomerState.Waiting));
            Assert.That(host.Anger(1).gameObject.activeInHierarchy, Is.False);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void ComplaintKeepsSlotOccupiedAndAngerAttachedUntilTheWholeExitCompletes(int target)
    {
        var durations = new[] { 60d, 60d, 60d };
        durations[target] = 1;
        using (var host = new Host(durations))
        {
            host.StartCustomers(seconds: 14);
            var portrait = host.Portrait(target);
            var sprite = portrait.sprite;
            var size = portrait.rectTransform.sizeDelta;
            host.Clock.AdvanceTime(1); // 14 - 1 - 3 = 10: prevent immediate slot reuse.
            Assert.That(host.Entrance.OccupiedSlotCount, Is.EqualTo(3));
            Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            host.Entrance.TickEntrance(host.ComplaintDuration * .5f);
            Assert.That(host.Entrance.GetState(target), Is.EqualTo(PopupStoreCustomerState.Expired));
            var original = portrait.transform.position;
            host.Entrance.TickEntrance(host.ComplaintDuration * .5f + host.ExitDuration * .5f);
            Assert.That(host.Entrance.GetState(target), Is.EqualTo(PopupStoreCustomerState.Leaving));
            Assert.That(portrait.transform.position.x, Is.LessThan(original.x));
            Assert.That(host.Anger(target).transform.parent, Is.SameAs(portrait.transform));
            Assert.That(host.Anger(target).gameObject.activeInHierarchy, Is.True);
            Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            host.Entrance.TickEntrance(host.ExitDuration * .49f);
            Assert.That(host.Entrance.GetState(target), Is.EqualTo(PopupStoreCustomerState.Leaving));
            Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            host.Entrance.TickEntrance(host.ExitDuration * .02f);
            Assert.That(host.Entrance.GetState(target), Is.EqualTo(PopupStoreCustomerState.Empty));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Entrance.OccupiedSlotCount, Is.EqualTo(2));
            Assert.That(host.Anger(target).gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Patience.GetTimer(target), Is.Null);
            Assert.That(portrait.sprite, Is.SameAs(sprite));
            Assert.That(portrait.rectTransform.sizeDelta, Is.EqualTo(size));
            host.AssertNoSale();
        }
    }

    [TestCase("Expired")]
    [TestCase("Leaving")]
    [TestCase("NearEnd")]
    [TestCase("Empty")]
    [TestCase("Ended")]
    public void ClosingAndReentryPreserveComplaintProgressWithoutDuplicatePenaltyOrResurrection(string phase)
    {
        using (var host = new Host(new[] { phase == "Ended" ? 18d : 1d, 60d, 60d }))
        {
            host.StartCustomers(seconds: phase == "Ended" ? 20 : phase == "Empty" ? 14 : 90);
            host.Clock.AdvanceTime(phase == "Ended" ? 18 : 1);
            var order = host.SourceOrders[0];
            if (phase == "Leaving") host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration * .4f);
            if (phase == "NearEnd") host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration * .99f);
            if (phase == "Empty") host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
            var state = host.Entrance.GetState(0);
            var position = host.Portrait(0).transform.position;
            double remaining = host.Clock.RemainingSeconds;
            int releases = host.Entrance.ReleasedCount;
            for (int i = 0; i < 3; i++)
            {
                host.Close();
                host.Clock.AdvanceTime(100);
                host.Entrance.TickEntrance(100);
                host.Reopen();
                Assert.That(host.Entrance.GetState(0), Is.EqualTo(state));
                Assert.That(host.Portrait(0).transform.position, Is.EqualTo(position));
                Assert.That(host.Clock.RemainingSeconds, Is.EqualTo(remaining));
                Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
                Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(1));
                Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(releases));
                Assert.That(order.IsExpired, Is.True);
                Assert.That(order.IsPacked, Is.False);
            }
        }
    }

    [TestCase("manual")]
    [TestCase("background")]
    [TestCase("focus")]
    public void PausedBusinessFreezesComplaintMovementAndOtherPatienceWithoutLosingCommittedPenalty(string reason)
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            host.Clock.AdvanceTime(1);
            var position = host.Portrait(0).transform.position;
            var other = host.Timer(1).RemainingPatience;
            Pause(host.Clock, reason, true);
            host.Clock.AdvanceTime(100);
            host.Entrance.TickEntrance(100);
            Assert.That(host.Portrait(0).transform.position, Is.EqualTo(position));
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            Assert.That(host.Timer(1).RemainingPatience, Is.EqualTo(other));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Pause(host.Clock, reason, false);
            host.Entrance.TickEntrance(host.ComplaintDuration + .1f);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving));
            Assert.That(host.Portrait(0).transform.position.x, Is.LessThan(position.x));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void EmptySlotReceivesFreshOrderAndOldTimerCannotApplyToSameCustomerReplacement()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            var old = host.Timer(0);
            host.Clock.AdvanceTime(1);
            host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
            for (int i = 0; i < 20 && host.Entrance.SuppliedOrderCount < 4; i++) host.Entrance.TickEntrance(.5f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(4));
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            Assert.That(host.Anger(0).gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Gauge(0).activeInHierarchy, Is.False);
            var enteringPosition = host.Portrait(0).transform.position;
            host.Close();
            host.Reopen();
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Entering));
            Assert.That(host.Portrait(0).transform.position, Is.EqualTo(enteringPosition));
            Assert.That(host.Anger(0).gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Patience.GetTimer(0), Is.Null);
            host.Entrance.TickEntrance(host.EntryDuration + .01f);
            var fresh = host.Timer(0);
            Assert.That(fresh.CustomerId, Is.EqualTo(old.CustomerId));
            Assert.That(fresh.OrderId, Is.Not.EqualTo(old.OrderId));
            Assert.That(fresh.Order, Is.Not.SameAs(old.Order));
            Assert.That(fresh.RemainingPatience, Is.EqualTo(1d));
            Assert.That(fresh.Order.IsExpired, Is.False);
            Assert.That(host.Patience.TryProcessExpiredBatch(new[] { old }), Is.Zero);
            Assert.That(host.Entrance.TryStartComplaint(old), Is.False);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Waiting));
            Assert.That(host.Anger(0).gameObject.activeInHierarchy, Is.False);
            Assert.That(fresh.RemainingPatience, Is.EqualTo(1d));
            Assert.That(host.SourceOrders[0].PlushIds, Is.EqualTo(new[] { "STAFF02" }));
        }
    }

    [Test]
    public void StillWaitingOrForeignPopupTimersCannotBeUsedAsExpirationCommands()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        using (var other = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            other.StartCustomers();
            var current = host.Timer(0);
            Assert.That(host.Patience.TryProcessExpiredBatch(new[] { current }), Is.Zero);
            Assert.That(host.Entrance.TryStartComplaint(current), Is.False);
            other.Clock.AdvanceTime(1);
            var foreign = other.Timer(0);
            Assert.That(host.Patience.TryProcessExpiredBatch(new[] { foreign }), Is.Zero);
            Assert.That(host.Entrance.TryStartComplaint(foreign), Is.False);
            Assert.That(host.SourceOrders.All(order => !order.IsExpired), Is.True);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(host.Entrance.StartedComplaintCount, Is.Zero);
        }
    }

    [TestCase("OrderId")]
    [TestCase("CustomerId")]
    [TestCase("ObjectReference")]
    [TestCase("Slot")]
    public void StaleFullIdentityCannotCommitExpirationOrApplyPenalty(string identity)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var timer = host.Timer(0);
            var original = host.SourceOrders.ToArray();
            string id = timer.OrderId;
            string customer = timer.CustomerId;
            // A queued signal is untrusted input; having an expired marker cannot bypass the live binding.
            SetProperty(timer, "IsExpired", true);
            SetProperty(timer, "RemainingPatience", 0d);
            try
            {
                if (identity == "OrderId") Write(timer.Order, "_runtimeId", Guid.NewGuid().ToString("N"));
                if (identity == "CustomerId") Write(timer.Order, "_customerId", "CUSTOMER04");
                if (identity == "ObjectReference") host.SourceOrders[0] = Order("CUSTOMER01", "STAFF01").CreateRuntimeCopy();
                if (identity == "Slot") { host.SourceOrders[0] = original[1]; host.SourceOrders[1] = original[0]; }
                Assert.That(host.Patience.TryProcessExpiredBatch(new[] { timer }), Is.Zero);
                Assert.That(host.Entrance.TryStartComplaint(timer), Is.False);
                Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
                Assert.That(original.All(order => !order.IsExpired && !order.IsPacked), Is.True);
            }
            finally
            {
                Write(timer.Order, "_runtimeId", id);
                Write(timer.Order, "_customerId", customer);
                for (int i = 0; i < original.Length; i++) host.SourceOrders[i] = original[i];
            }
        }
    }

    [Test]
    public void AnotherComplaintDoesNotCancelCommittedGiftDeliveryOrConsumeCounterAndFeedbackState()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            host.StartCustomers();
            host.Fill(1);
            var receipt = host.Packing.TryPack().Receipt;
            Assert.That(receipt, Is.Not.Null);
            host.Presentation.TickPresentation(0);
            host.Counter.TryAdd(host.Resolve(host.SourceOrders[2])[0]);
            var materials = host.Counter.Materials.ToArray();
            int feedback = host.FeedbackCount;
            int woolyPrefix = host.Progress.GetMatchedCount(2);
            host.Clock.AdvanceTime(1);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Expired));
            Assert.That(host.SourceOrders[1].IsPacked, Is.True);
            Assert.That(host.SourceOrders[1].IsExpired, Is.False);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
            Assert.That(host.SourceOrders[1].PackingReceipt, Is.SameAs(receipt));
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            Assert.That(host.FeedbackCount, Is.EqualTo(feedback));
            Assert.That(host.Progress.GetMatchedCount(2), Is.EqualTo(woolyPrefix));
            host.CompleteVisual(false);
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Entrance.GetDepartureReason(1), Is.EqualTo(PopupStoreDepartureReason.Packed));
            Assert.That(host.Anger(1).gameObject.activeInHierarchy, Is.False);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(2));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
        }
    }

    [Test]
    public void SimultaneousPenaltyEndDoesNotCancelTheOtherCustomersAlreadyCommittedDelivery()
    {
        using (var host = new Host(new[] { 18d, 60d, 60d }))
        {
            host.StartCustomers(seconds: 20);
            host.Fill(1);
            var receipt = host.Packing.TryPack().Receipt;
            host.Presentation.TickPresentation(0);
            host.Clock.AdvanceTime(18);
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.SourceOrders[1].IsPacked, Is.True);
            Assert.That(host.SourceOrders[1].IsExpired, Is.False);
            Assert.That(host.SourceOrders[2].IsExpired, Is.False);
            host.CompleteVisual(false);
            host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(2));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void MixedSuccessfulAndExpiredOrdersExhaustOnlyTheOriginalTwentyAndPreserveAccountState()
    {
        var account = new AccountSnapshot();
        using (var host = new Host(new[] { .5d, .5d, .5d }))
        {
            var templates = (PopupStoreOrder[])Read(host.Entrance, "_testOrders");
            var signatures = templates.Select(Signature).ToArray();
            Assert.That(templates.Count(order => order.PlushIds.Count == 1), Is.EqualTo(8));
            Assert.That(templates.Count(order => order.PlushIds.Count == 2), Is.EqualTo(8));
            Assert.That(templates.Count(order => order.PlushIds.Count == 3), Is.EqualTo(4));
            Assert.That(Read(host.Entrance, "_entryInterval"), Is.EqualTo(4f));
            int objects = host.Root.GetComponentsInChildren<Transform>(true).Length;
            host.Start(1000);
            int decisions = 0, safety = 0;
            while (host.Entrance.ReleasedCount < 20 && safety++ < 2000)
            {
                host.Entrance.TickEntrance(.5f);
                int slot = Enumerable.Range(0, 3).Where(host.Entrance.IsWaiting).DefaultIfEmpty(-1).First();
                if (slot < 0) continue;
                if (decisions++ % 2 == 0)
                {
                    host.Fill(slot);
                    Assert.That(host.Packing.TryPack().Success, Is.True);
                    host.CompleteVisual();
                }
                else host.Clock.AdvanceTime(.5);
                host.Entrance.TickEntrance(host.ComplaintDuration + host.ExitDuration + .01f);
                Assert.That(host.Entrance.OccupiedSlotCount, Is.InRange(0, 3));
                Assert.That(host.Entrance.SuppliedOrderCount, Is.InRange(0, 20));
            }
            Assert.That(safety, Is.LessThan(2000));
            var history = host.Entrance.SuppliedOrders;
            int expired = history.Count(order => order.IsExpired);
            int packed = history.Count(order => order.IsPacked);
            Assert.That(expired, Is.GreaterThan(0));
            Assert.That(packed, Is.GreaterThan(0));
            Assert.That(expired + packed, Is.EqualTo(20));
            Assert.That(history.All(order => order.IsExpired != order.IsPacked), Is.True);
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(20));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.RemainingOrderCount, Is.Zero);
            Assert.That(host.Entrance.OccupiedSlotCount, Is.Zero);
            Assert.That(host.Entrance.CompletedOrderCount, Is.EqualTo(packed));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(packed));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(packed));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(expired));
            Assert.That(host.Clock.ComplaintPenaltySeconds, Is.EqualTo(expired * 3d));
            Assert.That(history.Where(order => order.IsExpired).All(order => order.PackingReceipt == null), Is.True);
            CollectionAssert.AreEqual(signatures, history.Select(Signature).ToArray());
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.Anger(i).gameObject.activeInHierarchy, Is.False);
                Assert.That(host.Gauge(i).activeInHierarchy, Is.False);
                Assert.That(host.Patience.GetTimer(i), Is.Null);
            }
            host.Close();
            host.Reopen();
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(expired));
            Assert.That(host.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
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
        internal int FeedbackCount;
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float EntryDuration => (float)Read(Entrance, "_entryDuration");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");
        internal float ComplaintDuration => (float)Read(Entrance, "_complaintDuration");
        internal Button PackButton => (Button)Read(Packing, "_packButton");

        internal Host(double[] durations = null, PopupStoreOrder[] orders = null)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-complaint-memory-test";
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
                Write(Clock, "_autoStartForEditorReview", false);
                if (durations != null)
                {
                    Write(Patience, "_onePlushSeconds", durations[0]);
                    Write(Patience, "_twoPlushSeconds", durations[1]);
                    Write(Patience, "_threePlushSeconds", durations[2]);
                }
                if (orders != null)
                {
                    Write(Entrance, "_fixedTestSupply", false);
                    Write(Orders, "_sampleOrders", orders);
                }
                View.Init();
                View.Show();
                RefreshExisting();
                Assert.That(Entrance.BeginVisibleSession(), Is.True);
                Assert.That(Presentation.BeginVisibleSession(), Is.True);
                Assert.That(Patience.Refresh(), Is.True);
                Progress.FeedbackTriggered += CountFeedback;
            }
            catch { Dispose(); throw; }
        }
        internal void Start(double seconds = 90)
        {
            Write(Clock, "_initialSeconds", seconds);
            Assert.That(Clock.TryStartSession(), Is.True);
        }
        internal void StartCustomers(int count = 3, double seconds = 90)
        {
            Start(seconds);
            Entrance.TickEntrance(0);
            for (int i = 1; i < count; i++) Entrance.TickEntrance(4);
            Entrance.TickEntrance(EntryDuration + .01f);
            for (int i = 0; i < count; i++) Assert.That(Entrance.IsWaiting(i), Is.True, "arrival " + i);
        }
        internal PopupStorePatienceTimer Timer(int slot)
        {
            var timer = Patience.GetTimer(slot);
            Assert.That(timer, Is.Not.Null, "timer slot " + slot);
            return timer;
        }
        internal Image Anger(int slot) => ((Image[])Read(Entrance, "_complaintIcons"))[slot];
        internal Image Portrait(int slot) => Orders.Slots[slot].CustomerImage;
        internal GameObject Gauge(int slot)
            => (GameObject)Read(((Array)Read(Patience, "_slots")).GetValue(slot), "_root");
        internal PopupStoreMaterialEntry[] Resolve(PopupStoreOrder order)
        {
            Assert.That(order.TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            return materials;
        }
        internal void Fill(int slot)
        {
            foreach (var material in Resolve(SourceOrders[slot])) Assert.That(Counter.TryAdd(material), Is.True);
        }
        internal void AssertNoSale()
        {
            Assert.That(Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(Packing.LastReceipt, Is.Null);
            Assert.That(Packing.TryTakeReceipt(out _), Is.False);
            Assert.That(Presentation.StartedPresentationCount, Is.Zero);
            Assert.That(Presentation.CompletedDeliveryCount, Is.Zero);
            Assert.That(Entrance.CompletedOrderCount, Is.Zero);
        }
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
            Assert.That(Entrance.BeginVisibleSession(), Is.True);
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
        private readonly float _timeScale = Time.timeScale;
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money));
            Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits));
            Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.EqualTo(_score));
            Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        }
    }
    private static PopupStoreOrder Order(string customer, string plush)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", plush.Split(','));
        Write(order, "_boxId", "BOX_PINK");
        Write(order, "_ribbonId", "RIBBON_YELLOW");
        return order;
    }
    private static string Signature(PopupStoreOrder order)
        => order.CustomerId + ":" + string.Join(",", order.PlushIds) + ":" + order.BoxId + ":" + order.RibbonId;
    private static void Pause(PopupStoreBusinessClock clock, string reason, bool value)
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
    private static void SetProperty(object target, string name, object value)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
