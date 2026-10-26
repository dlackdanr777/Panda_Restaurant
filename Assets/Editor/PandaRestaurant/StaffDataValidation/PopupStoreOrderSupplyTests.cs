using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real twenty-order supply, packing, delivery and slot reuse on memory instances of the authored popup.</summary>
public sealed class PopupStoreOrderSupplyTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [Test]
    public void AuthoredTwentyOrdersUseTheApprovedCatalogAndTheEightEightFourDistribution()
    {
        using (var host = new Host())
        {
            Assert.That((bool)Read(host.Entrance, "_fixedTestSupply"), Is.True);
            Assert.That(host.Templates, Has.Length.EqualTo(20));
            Assert.That(host.Templates.Count(order => order.PlushIds.Count == 1), Is.EqualTo(8));
            Assert.That(host.Templates.Count(order => order.PlushIds.Count == 2), Is.EqualTo(8));
            Assert.That(host.Templates.Count(order => order.PlushIds.Count == 3), Is.EqualTo(4));
            CollectionAssert.AllItemsAreUnique(host.Templates.Select(host.Signature).ToArray());
            CollectionAssert.AreEqual(new[] { "CUSTOMER01", "CUSTOMER04", "CUSTOMER11" }, host.Templates.Take(3).Select(order => order.CustomerId).ToArray());
            CollectionAssert.AreEqual(new[] { "STAFF01", "BOX_PINK", "RIBBON_YELLOW" }, host.Resolve(host.Templates[0]).Select(entry => entry.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF01", "BOX_BLUE", "RIBBON_PINK" }, host.Resolve(host.Templates[1]).Select(entry => entry.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "STAFF03", "STAFF05", "STAFF02", "BOX_MINT", "RIBBON_MINT" }, host.Resolve(host.Templates[2]).Select(entry => entry.Id).ToArray());
            Assert.That(host.Root.GetComponents<PopupStoreCustomerEntrance>(), Has.Length.EqualTo(1));
            Assert.That(host.Orders.Slots.Count, Is.EqualTo(3));
            CollectionAssert.AllItemsAreUnique(host.Orders.Slots.Select(slot => slot.CustomerImage.GetInstanceID()).ToArray());
        }
    }

    [Test]
    public void TheFirstThreeCustomersCountTowardTwentyAndHistoryRecordsOnlyActualStarts()
    {
        using (var host = new Host())
        {
            host.Begin();
            Assert.That(host.Entrance.TotalTestOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.Zero);
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.SuppliedOrders, Is.Empty);
            Assert.That(host.Entrance.OccupiedSlotCount, Is.Zero);
            host.Step(0);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            Assert.That(host.Entrance.SuppliedOrders.Single(), Is.SameAs(host.SourceOrders[0]));
            host.Step(4);
            host.Step(4);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(17));
            Assert.That(host.Entrance.OccupiedSlotCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(host.Templates.Take(3).Select(host.Signature).ToArray(), host.Entrance.SuppliedOrders.Select(host.Signature).ToArray());
            for (int i = 0; i < 3; i++) Assert.That(host.SourceOrders[i], Is.Not.SameAs(host.Templates[i]));
        }
    }

    [Test]
    public void FourSecondIntervalUsesTheLastActualStartAndNeverCatchesUpMultipleCustomersInOneTick()
    {
        using (var host = new Host())
        {
            Assert.That(host.Interval, Is.EqualTo(4));
            host.Begin();
            host.Step(100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            float first = host.Entrance.LastEntryStartedAt;
            host.Step(0);
            host.Step(3.9f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            host.Step(.11f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(2));
            Assert.That(host.Entrance.LastEntryStartedAt - first, Is.GreaterThanOrEqualTo(4));
            host.Step(100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.OccupiedSlotCount, Is.EqualTo(3));
        }
    }

    [TestCase("Entering")]
    [TestCase("Waiting")]
    [TestCase("Packed")]
    [TestCase("Leaving")]
    public void EveryOccupiedStatePreventsTheFourthOrderFromBeingAssigned(string state)
    {
        using (var host = new Host())
        {
            if (state == "Entering") Write(host.Entrance, "_entryDuration", 1000f);
            host.StartThree(state != "Entering");
            if (state == "Packed" || state == "Leaving")
                for (int i = 0; i < 3; i++) host.Pack(i);
            if (state == "Leaving")
                for (int i = 0; i < 3; i++) host.CompleteVisual();
            var orders = host.SourceOrders.ToArray();
            for (int i = 0; i < 3; i++) Assert.That(host.State(i), Is.EqualTo(state));
            host.Step(state == "Leaving" ? host.ExitDuration * .5f : 100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.OccupiedSlotCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(orders, host.SourceOrders);
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(17));
        }
    }

    [TestCase("Boom")]
    [TestCase("Grow")]
    [TestCase("Settle")]
    [TestCase("Move")]
    [TestCase("Arrive")]
    [TestCase("Fade")]
    public void NoDeliveryPhaseCanReleaseOrReuseThePackedCustomerSlotEarly(string phase)
    {
        using (var host = new Host())
        {
            host.StartThree();
            var order = host.SourceOrders[0];
            var receipt = host.Pack(0);
            host.Presentation.TickPresentation(0);
            host.ToPhase(phase);
            host.Step(100);
            Assert.That(host.State(0), Is.EqualTo("Packed"));
            Assert.That(host.SourceOrders[0], Is.SameAs(order));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            Assert.That(order.PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
        }
    }

    [Test]
    public void AReleasedSlotReceivesAFreshOrderEvenWhenTheCustomerIdIsTheSame()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var old = host.SourceOrders[0];
            var image = host.Image(0);
            var receipt = host.Pack(0);
            host.CompleteVisual();
            host.Step(host.ExitDuration * .99f);
            Assert.That(host.State(0), Is.EqualTo("Leaving"));
            Assert.That(host.SourceOrders[0], Is.SameAs(old));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            host.Step(host.ExitDuration * .02f);
            host.WaitForSupply(4);
            var next = host.SourceOrders[0];
            Assert.That(next, Is.Not.SameAs(old));
            Assert.That(next.CustomerId, Is.EqualTo(old.CustomerId));
            Assert.That(next.RuntimeId, Is.Not.Empty.And.Not.EqualTo(old.RuntimeId));
            Assert.That(next.IsPacked, Is.False);
            Assert.That(next.PackingReceipt, Is.Null);
            Assert.That(host.Image(0), Is.SameAs(image));
            Assert.That(host.State(0), Is.EqualTo("Entering"));
            Assert.That(host.Signature(next), Is.EqualTo(host.Signature(host.Templates[3])));
            Assert.That(old.IsPacked, Is.True);
            Assert.That(old.PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.Entrance.SuppliedOrders[0], Is.SameAs(old));
            Assert.That(host.Entrance.SuppliedOrders[3], Is.SameAs(next));
        }
    }

    [Test]
    public void PreparedMaterialsWaitForTheReplacementToArriveThenRecalculateSilentlyWithoutSelling()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Pack(0);
            host.CompleteVisual();
            host.Step(host.ExitDuration + .01f);
            foreach (var material in host.Resolve(host.Templates[3])) Assert.That(host.Counter.TryAdd(material), Is.True);
            var contents = host.Counter.Materials.ToArray();
            int sounds = host.FeedbackEvents;
            host.WaitForSupply(4);
            Assert.That(host.State(0), Is.EqualTo("Entering"));
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            Assert.That(host.Progress.GetMatchedCount(0), Is.Zero);
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.None));
            Assert.That(host.ClickPack().Status, Is.EqualTo(PopupStorePackingStatus.None));
            host.Step(host.EntryDuration + .01f);
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.True);
            Assert.That(host.Progress.GetMatchedCount(0), Is.EqualTo(contents.Length));
            Assert.That(host.FeedbackEvents, Is.EqualTo(sounds));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(contents, host.Counter.Materials.ToArray());
            var exact = host.Orders.QueryExactMatches(host.Counter);
            Assert.That(exact.Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
            Assert.That(exact.Candidates.Single().SlotIndex, Is.Zero);
            Assert.That(host.ClickPack().Success, Is.True);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(2));
        }
    }

    [TestCase("Entering")]
    [TestCase("Waiting")]
    [TestCase("Packed")]
    public void AnOldReceiptNotificationCannotMoveOrCompleteANewOrderInTheReusedSlot(string newState)
    {
        using (var host = new Host())
        {
            host.StartThree();
            var oldReceipt = host.Pack(0);
            host.CompleteVisual();
            var oldOutcome = host.Presentation.VisualOutcomes.Single();
            host.Step(host.ExitDuration + .01f);
            host.WaitForSupply(4);
            if (newState != "Entering") host.Step(host.EntryDuration + .01f);
            PopupStorePackingReceipt nextReceipt = null;
            if (newState == "Packed") nextReceipt = host.Pack(0);
            var next = host.SourceOrders[0];
            var position = host.Image(0).rectTransform.anchoredPosition3D;
            int departures = host.Entrance.StartedDepartureCount;
            for (int i = 0; i < 4; i++) Invoke(host.Entrance, "HandleVisualEnded", oldOutcome);
            Assert.That(host.State(0), Is.EqualTo(newState));
            Assert.That(host.SourceOrders[0], Is.SameAs(next));
            Assert.That(host.Image(0).rectTransform.anchoredPosition3D, Is.EqualTo(position));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(departures));
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(oldReceipt, out _, out _), Is.False);
            Assert.That(host.Orders.TryGetCommittedCustomerBinding(oldReceipt, out _, out _), Is.False);
            Assert.That(next.PackingReceipt, Is.SameAs(nextReceipt));
            Assert.That(host.Entrance.SuppliedOrders[0].PackingReceipt, Is.SameAs(oldReceipt));
        }
    }

    [Test]
    public void ReplacementPreservesTheOtherTwoSlotsProgressAnActiveDeliveryCounterAndScrollPositions()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Pack(0);
            var otherReceipt = host.Pack(2);
            host.CompleteVisual();
            host.Presentation.TickPresentation(0);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(otherReceipt));
            var target = host.Presentation.CurrentTarget;
            Assert.That(host.Counter.TryAdd(host.Resolve(host.SourceOrders[1])[0]), Is.True);
            var counter = host.Counter.Materials.ToArray();
            var orders = host.SourceOrders.ToArray();
            var sprites = host.Orders.Slots.Select(slot => slot.CustomerImage.sprite).ToArray();
            var positions = host.Orders.Slots.Select(slot => slot.CustomerImage.rectTransform.anchoredPosition3D).ToArray();
            int progress = host.Progress.GetMatchedCount(1), sounds = host.FeedbackEvents;
            var lists = host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
            var scrolls = lists.Select(list => (ScrollRect)Read(list, "_scrollRect")).ToArray();
            scrolls[0].horizontalNormalizedPosition = .37f;
            var scrollPositions = scrolls.Select(scroll => scroll.horizontalNormalizedPosition).ToArray();
            host.Step(host.ExitDuration + .01f);
            host.WaitForSupply(4);
            for (int i = 1; i < 3; i++)
            {
                Assert.That(host.SourceOrders[i], Is.SameAs(orders[i]));
                Assert.That(host.Image(i).sprite, Is.SameAs(sprites[i]));
                Assert.That(host.Image(i).rectTransform.anchoredPosition3D, Is.EqualTo(positions[i]));
            }
            Assert.That(host.State(1), Is.EqualTo("Waiting"));
            Assert.That(host.State(2), Is.EqualTo("Packed"));
            Assert.That(host.Progress.GetMatchedCount(1), Is.EqualTo(progress));
            Assert.That(host.Progress.GetMatchedCount(0), Is.Zero);
            Assert.That(host.FeedbackEvents, Is.EqualTo(sounds));
            Assert.That(host.Presentation.CurrentTarget, Is.SameAs(target));
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(otherReceipt, out var current, out var reason), Is.True, reason);
            Assert.That(current, Is.SameAs(target));
            CollectionAssert.AreEqual(counter, host.Counter.Materials.ToArray());
            for (int i = 0; i < scrolls.Length; i++)
            {
                Assert.That(Read(lists[i], "_scrollRect"), Is.SameAs(scrolls[i]));
                Assert.That(scrolls[i].horizontalNormalizedPosition, Is.EqualTo(scrollPositions[i]).Within(.001f));
            }
            Assert.That(lists.Sum(list => list.MaterialCount), Is.EqualTo(38));
        }
    }

    [TestCase("Entering")]
    [TestCase("Waiting")]
    [TestCase("Packed")]
    [TestCase("Leaving")]
    public void ClosingAndReopeningPreservesSupplyHistoryAndNeverReassignsAnOccupiedSlot(string state)
    {
        using (var host = new Host())
        {
            if (state == "Entering") Write(host.Entrance, "_entryDuration", 1000f);
            host.StartThree(state != "Entering");
            if (state == "Packed" || state == "Leaving") host.Pack(0);
            if (state == "Leaving") { host.CompleteVisual(); host.Step(host.ExitDuration * .4f); }
            var history = host.Entrance.SuppliedOrders.ToArray();
            var ids = history.Select(order => order.RuntimeId).ToArray();
            float lastStart = host.Entrance.LastEntryStartedAt;
            host.Close();
            host.Step(100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            host.Reopen();
            Assert.That(host.State(0), Is.EqualTo(state == "Packed" ? "Leaving" : state));
            Assert.That(host.Entrance.LastEntryStartedAt, Is.EqualTo(lastStart));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(history, host.Entrance.SuppliedOrders.ToArray());
            CollectionAssert.AreEqual(ids, host.Entrance.SuppliedOrders.Select(order => order.RuntimeId).ToArray());
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(state == "Packed" || state == "Leaving" ? 1 : 0));
        }
    }

    [Test]
    public void HiddenTimeDoesNotAdvanceTheFourSecondSupplyClockAndRepeatedBeginDoesNotDuplicateListeners()
    {
        using (var host = new Host())
        {
            host.Begin();
            host.Step(0);
            host.Step(2);
            host.Close();
            host.Step(1000);
            host.Reopen();
            for (int i = 0; i < 5; i++) Assert.That(host.Entrance.BeginVisibleSession(), Is.True);
            host.Step(1.9f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            host.Step(.11f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(2));
            Assert.That(host.Entrance.LastEntryStartedAt, Is.LessThan(5));
            host.Step(4);
            host.Step(host.EntryDuration + .01f);
            host.Pack(0);
            host.CompleteVisual();
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void TwoReleasedSlotsReceiveOneReplacementPerActualStartIntervalWithoutACatchUpBurst()
    {
        using (var host = new Host())
        {
            host.StartThree();
            host.Pack(0);
            host.Pack(1);
            host.CompleteVisual();
            host.CompleteVisual();
            host.Step(100);
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(2));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(4));
            Assert.That(host.State(0), Is.EqualTo("Entering"));
            Assert.That(host.State(1), Is.EqualTo("Empty"));
            float start = host.Entrance.LastEntryStartedAt;
            host.Step(0);
            host.Step(3.9f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(4));
            host.Step(.11f);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(5));
            Assert.That(host.Entrance.LastEntryStartedAt - start, Is.GreaterThanOrEqualTo(4));
            Assert.That(host.State(1), Is.EqualTo("Entering"));
            Assert.That(host.SourceOrders[0], Is.SameAs(host.Entrance.SuppliedOrders[3]));
            Assert.That(host.SourceOrders[1], Is.SameAs(host.Entrance.SuppliedOrders[4]));
        }
    }

    [Test]
    public void AnInitializedSessionUsesItsFixedOrderSnapshotAcrossInspectorChangesAndReentry()
    {
        using (var host = new Host())
        {
            var fourth = host.Signature(host.Templates[3]);
            host.StartThree();
            Write(host.Entrance, "_fixedTestSupply", false);
            Write(host.Templates[3], "_plushIds", new[] { "STAFF32" });
            Write(host.Entrance, "_testOrders", Array.Empty<PopupStoreOrder>());
            host.Close();
            host.Reopen();
            host.Pack(0);
            host.CompleteVisual();
            host.Step(host.ExitDuration + .01f);
            host.WaitForSupply(4);
            Assert.That(host.Signature(host.SourceOrders[0]), Is.EqualTo(fourth));
            Assert.That(host.Entrance.TotalTestOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.RemainingOrderCount, Is.EqualTo(16));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(4));
        }
    }

    [Test]
    public void AllTwentyRealTransactionsFinishOnceAndTheSameThreeSlotsRemainEmptyWithoutFurtherSupply()
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            var expected = host.Templates.Select(host.Signature).ToArray();
            var originalImages = host.Orders.Slots.Select(slot => slot.CustomerImage.GetInstanceID()).ToArray();
            int objectCount = host.Root.GetComponentsInChildren<Transform>(true).Length;
            Write(host.Entrance, "_entryInterval", 0f);
            host.Begin();
            int safety = 0;
            while (host.Entrance.CompletedOrderCount < 20 && safety++ < 2000)
            {
                host.Step(.25f);
                Assert.That(host.Entrance.OccupiedSlotCount, Is.InRange(0, 3));
                Assert.That(host.Entrance.SuppliedOrderCount, Is.InRange(0, 20));
                Assert.That(host.Entrance.SupplyEnded, Is.EqualTo(host.Entrance.SuppliedOrderCount == 20));
                int slot = Enumerable.Range(0, 3).Where(index => host.State(index) == "Waiting").DefaultIfEmpty(-1).First();
                if (slot < 0) continue;
                host.Pack(slot);
                host.CompleteVisual();
                host.Step(host.ExitDuration + .01f);
            }
            Assert.That(safety, Is.LessThan(2000));
            for (int i = 0; i < 20; i++) { host.Step(100); host.Presentation.TickPresentation(100); }
            Assert.That(host.Entrance.TotalTestOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.RemainingOrderCount, Is.Zero);
            Assert.That(host.Entrance.CompletedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.SupplyEnded, Is.True);
            Assert.That(host.Entrance.OccupiedSlotCount, Is.Zero);
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(20));
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(20));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(20));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(20));
            Assert.That(host.PackedEvents, Is.EqualTo(20));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(20));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(20));
            Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(20));
            Assert.That(host.Entrance.SuppliedOrders.All(order => order.IsPacked && order.PackingReceipt != null), Is.True);
            CollectionAssert.AllItemsAreUnique(host.Entrance.SuppliedOrders.Select(order => order.RuntimeId).ToArray());
            CollectionAssert.AllItemsAreUnique(host.Entrance.SuppliedOrders.Select(order => order.PackingReceipt.ReceiptId).ToArray());
            CollectionAssert.AreEqual(expected, host.Entrance.SuppliedOrders.Select(host.Signature).ToArray());
            CollectionAssert.AreEqual(originalImages, host.Orders.Slots.Select(slot => slot.CustomerImage.GetInstanceID()).ToArray());
            Assert.That(host.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
            Assert.That(host.Counter.Count, Is.Zero);
            host.Close();
            host.Reopen();
            host.Step(100);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.State(i), Is.EqualTo("Empty"));
                Assert.That(host.Image(i).gameObject.activeSelf, Is.False);
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.False);
                Assert.That(host.Orders.GetDisplayedMaterials(i), Is.Empty);
            }
        }
        account.AssertUnchanged();
    }

    [TestCase("nineteen-orders")]
    [TestCase("wrong-distribution")]
    [TestCase("duplicate-exact-order")]
    [TestCase("unknown-material")]
    [TestCase("unknown-customer")]
    [TestCase("null-order")]
    public void InvalidFixedSupplyIsRejectedBeforeAnyCustomerOrOrderIsAssigned(string defect)
    {
        using (var host = new Host())
        {
            var existing = host.SourceOrders.ToArray();
            var existingIds = existing.Select(order => order.RuntimeId).ToArray();
            if (defect == "nineteen-orders") Write(host.Entrance, "_testOrders", host.Templates.Take(19).ToArray());
            else if (defect == "wrong-distribution") Write(host.Templates[19], "_plushIds", new[] { "STAFF32" });
            else if (defect == "duplicate-exact-order") host.Templates[3] = host.Templates[0].CreateRuntimeCopy();
            else if (defect == "unknown-material") Write(host.Templates[19], "_boxId", "BOX_UNKNOWN");
            else if (defect == "unknown-customer") Write(host.Templates[19], "_customerId", "CUSTOMER_UNKNOWN");
            else host.Templates[19] = null;
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreCustomerEntrance: "));
            Assert.That(host.Entrance.BeginVisibleSession(), Is.False);
            Assert.That(host.Entrance.IsReady, Is.False);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.Zero);
            Assert.That(host.Entrance.SuppliedOrders, Is.Empty);
            Assert.That(host.Entrance.StartedEntranceCount, Is.Zero);
            CollectionAssert.AreEqual(existing, host.SourceOrders);
            CollectionAssert.AreEqual(existingIds, host.SourceOrders.Select(order => order.RuntimeId).ToArray());
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Write(host.Entrance, "_automaticEntry", false);
            Write(host.Entrance, "_fixedTestSupply", false);
            Assert.That(host.Orders.Refresh(), Is.True);
            Assert.That(host.Entrance.BeginVisibleSession(), Is.False);
            Assert.That(host.Entrance.ControlsOrders, Is.True, "A failed fixed session cannot fall back to static sellable orders.");
            foreach (var material in host.Resolve(existing[0])) Assert.That(host.Counter.TryAdd(material), Is.True);
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.None));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.Orders.GetDisplayedMaterials(i), Is.Empty);
                Assert.That(host.Image(i).gameObject.activeSelf, Is.False);
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.False);
            }
        }
    }

    [Test]
    public void SupplyDoesNotChangeFiveMaterialCapacityOrTheRealClearButton()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var entry = host.Resolve(host.SourceOrders[0])[0];
            for (int i = 0; i < 5; i++) Assert.That(host.Counter.TryAdd(entry), Is.True);
            Assert.That(host.Counter.TryAdd(entry), Is.False);
            host.Step(100);
            Assert.That(host.Counter.Count, Is.EqualTo(5));
            Click((Button)Read(host.Counter, "_clearButton"));
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void RuntimeCopiesDoNotReuseCompletedIdentityReceiptOrTheAuthoredPlushArray()
    {
        using (var host = new Host())
        {
            host.StartThree();
            var original = host.SourceOrders[0];
            var receipt = host.Pack(0);
            var copy = original.CreateRuntimeCopy();
            Assert.That(copy.RuntimeId, Is.Not.Empty.And.Not.EqualTo(original.RuntimeId));
            Assert.That(copy.IsPacked, Is.False);
            Assert.That(copy.PackingReceipt, Is.Null);
            Assert.That(host.Signature(copy), Is.EqualTo(host.Signature(original)));
            Assert.That(Read(copy, "_plushIds"), Is.Not.SameAs(Read(original, "_plushIds")));
            ((string[])Read(copy, "_plushIds"))[0] = "STAFF32";
            Assert.That(original.PlushIds[0], Is.EqualTo("STAFF01"));
            Assert.That(original.PackingReceipt, Is.SameAs(receipt));
        }
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal int PackedEvents, FeedbackEvents;
        internal PopupStoreOrder[] Templates => (PopupStoreOrder[])Read(Entrance, "_testOrders");
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float EntryDuration => (float)Read(Entrance, "_entryDuration");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");
        internal float Interval => (float)Read(Entrance, "_entryInterval");
        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-order-supply-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Assert.That((bool)Read(Entrance, "_fixedTestSupply"), Is.True, "The real game prefab enables the authored twenty-order mode.");
                View.Init();
                View.Show();
                // Explicit session start keeps these regression fixtures on the real runtime clock path.
                Assert.That(Root.GetComponent<PopupStoreBusinessClock>().TryStartSession(), Is.True);
                RefreshExisting();
                Counter.Changed += OnCounterChanged;
                Progress.FeedbackTriggered += OnFeedback;
            }
            catch { Dispose(); throw; }
        }
        internal void Begin()
        {
            Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Presentation.BeginVisibleSession(), Is.True);
        }
        internal void Step(float seconds) => Entrance.TickEntrance(seconds);
        internal void StartThree(bool wait = true)
        {
            Begin();
            Step(0);
            Step(Interval);
            Step(Interval);
            if (wait) Step(EntryDuration + .01f);
            Assert.That(Entrance.SuppliedOrderCount, Is.EqualTo(3));
        }
        internal void WaitForSupply(int count)
        {
            for (int i = 0; i < 1000 && Entrance.SuppliedOrderCount < count; i++) Step(.05f);
            Assert.That(Entrance.SuppliedOrderCount, Is.EqualTo(count));
        }
        internal string State(int slot) => Entrance.GetState(slot).ToString();
        internal Image Image(int slot) => Orders.Slots[slot].CustomerImage;
        internal PopupStoreMaterialEntry[] Resolve(PopupStoreOrder order)
        {
            Assert.That(order.TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var entries, out var error), Is.True, error);
            return entries;
        }
        internal string Signature(PopupStoreOrder order) => string.Join("/", Resolve(order).Select(entry => entry.Kind + ":" + entry.Id));
        internal void Fill(int slot) { foreach (var material in Resolve(SourceOrders[slot])) Assert.That(Counter.TryAdd(material), Is.True); }
        internal PopupStorePackingReceipt Pack(int slot)
        {
            Assert.That(State(slot), Is.EqualTo("Waiting"));
            Fill(slot);
            var result = ClickPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(slot));
            return result.Receipt;
        }
        internal PopupStorePackingResult ClickPack() { Click((Button)Read(Packing, "_packButton")); return Packing.LastResult; }
        internal void ToPhase(string phase)
        {
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != phase; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
        }
        internal void CompleteVisual()
        {
            Presentation.TickPresentation(0);
            Assert.That(Presentation.CurrentReceipt, Is.Not.Null);
            ToPhase("Idle");
        }
        internal void Close()
        {
            if (Presentation.CurrentReceipt != null)
                LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            View.Hide();
            // Explicit real lifecycle methods supplement non-ExecuteAlways EditMode instances.
            Entrance.PauseVisibleSession();
            Presentation.EndVisibleSession();
            Invoke(Packing, "OnDisable");
            Invoke(Progress, "OnDisable");
            Invoke(Counter, "OnDisable");
            Invoke(Orders, "OnDisable");
        }
        internal void Reopen() { View.Show(); RefreshExisting(); Begin(); }
        private void RefreshExisting()
        {
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True);
            Assert.That(Progress.Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True);
        }
        private void OnCounterChanged(PopupStoreSalesCounter.ChangeReason reason)
        { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) PackedEvents++; }
        private void OnFeedback() => FeedbackEvents++;
        public void Dispose()
        {
            if (Entrance != null) Entrance.PauseVisibleSession();
            if (Presentation != null)
            {
                if (Presentation.CurrentReceipt != null)
                    LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
                Presentation.EndVisibleSession();
            }
            if (Counter != null) Counter.Changed -= OnCounterChanged;
            if (Progress != null) Progress.FeedbackTriggered -= OnFeedback;
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
    private static void Click(Button button)
    {
        Assert.That(button.IsInteractable(), Is.True);
        Assert.That(ExecuteEvents.Execute(button.gameObject, new PointerEventData(null)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
    }
    private static void Invoke(object target, string name, params object[] arguments)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, target.GetType().Name + "." + name);
        method.Invoke(target, arguments);
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
