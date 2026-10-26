using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Real popup packing, delivery and departure in memory. No Scene, account or restaurant initialization.</summary>
public sealed class PopupStoreCustomerDepartureTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [Test]
    public void PackingAndArrivalDoNotStartDepartureUntilTheFadeHasFullyFinished()
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipt = host.Pack(0);
            Assert.That(host.State(0), Is.EqualTo("Packed"));
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            host.Presentation.TickPresentation(0);
            foreach (string phase in new[] { "Boom", "Grow", "Settle", "Move", "Arrive", "Fade" })
            {
                host.ToPhase(phase);
                Assert.That(host.State(0), Is.EqualTo("Packed"), phase);
                Assert.That(host.Entrance.StartedDepartureCount, Is.Zero, phase);
                Assert.That(host.Presentation.VisualOutcomes, Is.Empty, phase);
                Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId),
                    Is.EqualTo(phase == "Arrive" || phase == "Fade"), phase);
            }
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_fadeDuration") * .25f);
            Assert.That(host.State(0), Is.EqualTo("Packed"));
            bool observedHidden = false, observedIdle = false;
            int callbacks = 0;
            Action<PopupStoreVisualOutcome> observer = outcome =>
            {
                callbacks++;
                observedHidden = !host.Result.gameObject.activeSelf && !host.Boom.gameObject.activeSelf;
                observedIdle = host.Presentation.CurrentPhase == PopupStorePackingPresentation.Phase.Idle
                    && host.Presentation.CurrentReceipt == null && host.Presentation.CurrentTarget == null;
            };
            host.Presentation.VisualEnded += observer;
            try { host.FinishVisual(); }
            finally { host.Presentation.VisualEnded -= observer; }
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(observedHidden && observedIdle, Is.True, "Departure notification follows visual cleanup.");
            Assert.That(host.State(0), Is.EqualTo("Leaving"));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(host.Presentation.VisualOutcomes.Single().Receipt, Is.SameAs(receipt));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void OnlyTheReceiptSlotLeavesAndItsCompletedOrderRemainsAfterRelease(int slot)
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var orders = host.SourceOrders.ToArray();
            var ids = orders.Select(order => order.RuntimeId).ToArray();
            var images = host.Orders.Slots.Select(view => view.CustomerImage).ToArray();
            var homes = images.Select(image => image.rectTransform.anchoredPosition3D).ToArray();
            var sprites = images.Select(image => image.sprite).ToArray();
            var receipt = host.Pack(slot);
            host.CompleteVisual();
            Assert.That(host.State(slot), Is.EqualTo("Leaving"));
            Assert.That(host.Orders.GetDisplayedMaterials(slot), Is.Empty);
            Assert.That(host.Progress.GetMatchedCount(slot), Is.Zero);
            host.Fill(slot);
            var counter = host.Counter.Materials.ToArray();
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.None));
            Assert.That(host.ClickPack().Status, Is.EqualTo(PopupStorePackingStatus.None));
            CollectionAssert.AreEqual(counter, host.Counter.Materials.ToArray());
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Assert.That(host.State(slot), Is.EqualTo("Empty"));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(images[slot].gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.Slots[slot].OrderFrame.gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.Slots[slot].MaterialIcons.All(icon => !icon.gameObject.activeSelf), Is.True);
            Assert.That(Vector3.Distance(images[slot].rectTransform.anchoredPosition3D, homes[slot]), Is.LessThan(.01f));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.SourceOrders[i], Is.SameAs(orders[i]));
                Assert.That(host.SourceOrders[i].RuntimeId, Is.EqualTo(ids[i]));
                Assert.That(images[i].sprite, Is.SameAs(sprites[i]));
                if (i == slot) continue;
                Assert.That(host.State(i), Is.EqualTo("Waiting"));
                Assert.That(images[i].gameObject.activeSelf, Is.True);
                Assert.That(images[i].rectTransform.anchoredPosition3D, Is.EqualTo(homes[i]));
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.True);
                Assert.That(host.Orders.GetDisplayedMaterials(i).Count, Is.EqualTo(host.Resolve(i).Length));
            }
            Assert.That(orders[slot].IsPacked, Is.True);
            Assert.That(orders[slot].PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.PackedEvents, Is.EqualTo(1));
            CollectionAssert.AreEqual(counter, host.Counter.Materials.ToArray());
        }
        account.AssertUnchanged();
    }

    [Test]
    public void RepeatedReceiptNotificationsAndRefreshesDoNotRestartOrRepeatDeparture()
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipt = host.Pack(0);
            host.CompleteVisual();
            var outcome = host.Presentation.VisualOutcomes.Single();
            host.Entrance.TickEntrance(host.ExitDuration * .4f);
            var position = host.Image(0).rectTransform.anchoredPosition3D;
            for (int i = 0; i < 4; i++)
            {
                Invoke(host.Entrance, "HandleVisualEnded", outcome);
                Assert.That(host.Orders.Refresh(), Is.True);
                Assert.That(host.Entrance.BeginVisibleSession(), Is.True);
            }
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(Vector3.Distance(host.Image(0).rectTransform.anchoredPosition3D, position), Is.LessThan(.01f));
            host.Entrance.TickEntrance(host.ExitDuration);
            for (int i = 0; i < 4; i++)
            {
                Invoke(host.Entrance, "HandleVisualEnded", outcome);
                host.Entrance.TickEntrance(100);
            }
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(1));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
        }
    }

    [TestCase("customer-id")]
    [TestCase("order-id")]
    [TestCase("order-reference")]
    [TestCase("image-reference")]
    public void ChangedCommittedIdentityIsRejectedInsteadOfDepartingAnotherCustomer(string change)
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var originalOrder = host.SourceOrders[0];
            var receipt = host.Pack(0);
            // Model a missed notification without hiding the actual delivery target.
            Invoke(host.Entrance, "UnbindPresentation");
            host.CompleteVisual();
            Assert.That(host.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            if (change == "customer-id") Write(originalOrder, "_customerId", "CUSTOMER04");
            else if (change == "order-id") Write(originalOrder, "_runtimeId", "expired-order-id");
            else if (change == "order-reference") host.SourceOrders[0] = MakeOrder("CUSTOMER01", "STAFF01");
            else Write(host.Orders.Slots[0], "_customerImage", host.Image(1));
            Assert.That(host.Entrance.BeginVisibleSession(), Is.True);
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            Assert.That(host.Entrance.ReleasedCount, Is.Zero);
            Assert.That(originalOrder.IsPacked, Is.True);
            Assert.That(originalOrder.PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.State(1), Is.EqualTo("Waiting"));
            Assert.That(host.State(2), Is.EqualTo("Waiting"));
        }
    }

    [TestCase("receipt")]
    [TestCase("target")]
    public void AnUnrecordedOrForgedVisualNotificationCannotStartDeparture(string change)
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipt = host.Pack(0);
            var suppliedReceipt = change == "receipt"
                ? new PopupStorePackingReceipt(receipt.OrderId, 0, receipt.CustomerId, host.Resolve(0)) : receipt;
            var target = host.Image(change == "target" ? 1 : 0).rectTransform;
            var constructor = typeof(PopupStoreVisualOutcome).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(PopupStorePackingReceipt), typeof(RectTransform), typeof(PopupStoreVisualOutcomeKind), typeof(string) }, null);
            Assert.That(constructor, Is.Not.Null);
            var forged = (PopupStoreVisualOutcome)constructor.Invoke(new object[]
                { suppliedReceipt, target, PopupStoreVisualOutcomeKind.VisualCompleted, null });
            Invoke(host.Entrance, "HandleVisualEnded", forged);
            Assert.That(host.State(0), Is.EqualTo("Packed"));
            Assert.That(host.State(1), Is.EqualTo("Waiting"));
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            host.CompleteVisual();
            Assert.That(host.State(0), Is.EqualTo("Leaving"), "A rejected fake does not consume the authentic receipt.");
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void IdenticalCustomerIdsStillUseTheReceiptSlotAndOrderIdentityForDeparture()
    {
        using (var host = new Host())
        {
            host.SetOrders(MakeOrder("CUSTOMER01", "STAFF01"), MakeOrder("CUSTOMER01", "STAFF02"));
            host.ReadyCustomers();
            var firstHome = host.Image(0).rectTransform.anchoredPosition3D;
            var receipt = host.Pack(1);
            host.CompleteVisual();
            Assert.That(receipt.SlotIndex, Is.EqualTo(1));
            Assert.That(host.SourceOrders[0].RuntimeId, Is.Not.EqualTo(receipt.OrderId));
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(host.State(1), Is.EqualTo("Leaving"));
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Assert.That(host.State(1), Is.EqualTo("Empty"));
            Assert.That(host.Image(0).gameObject.activeSelf, Is.True);
            Assert.That(host.Image(0).rectTransform.anchoredPosition3D, Is.EqualTo(firstHome));
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
        }
    }

    [TestCase("Packed")]
    [TestCase("Boom")]
    [TestCase("Grow")]
    [TestCase("Settle")]
    [TestCase("Move")]
    [TestCase("Arrive")]
    [TestCase("Fade")]
    public void ClosingEachPendingVisualPhaseRecoversThePackedSlotWithoutReplayingOrClaimingVisualCompletion(string phase)
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipt = host.Pack(0);
            if (phase != "Packed") { host.Presentation.TickPresentation(0); host.ToPhase(phase); }
            bool arrived = host.Presentation.IsDeliveryCompleted(receipt.ReceiptId);
            int starts = host.Presentation.StartedPresentationCount;
            host.Close();
            var outcome = host.Presentation.VisualOutcomes.Single();
            Assert.That(outcome.Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
            Assert.That(outcome.Receipt, Is.SameAs(receipt));
            Assert.That(outcome.Target, Is.SameAs(host.Image(0).rectTransform));
            Assert.That(outcome.Reason, Is.Not.Null.And.Not.Empty);
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            host.Reopen();
            Assert.That(host.State(0), Is.EqualTo("Leaving"));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            host.Presentation.TickPresentation(100);
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(starts));
            Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.EqualTo(arrived));
            Assert.That(host.Presentation.VisualOutcomes.Single(), Is.SameAs(outcome));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.PackedEvents, Is.EqualTo(1));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ClosingDuringDepartureOrImmediatelyBeforeReleaseResumesTheSameMovementOnce(bool beforeRelease)
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipt = host.Pack(0);
            host.CompleteVisual();
            host.Entrance.TickEntrance(host.ExitDuration * (beforeRelease ? .999f : .4f));
            Assert.That(host.State(0), Is.EqualTo("Leaving"));
            var position = host.Image(0).rectTransform.anchoredPosition3D;
            host.Close();
            host.Entrance.TickEntrance(100);
            Assert.That(host.Image(0).rectTransform.anchoredPosition3D, Is.EqualTo(position));
            host.Reopen();
            Assert.That(host.State(0), Is.EqualTo("Leaving"));
            Assert.That(Vector3.Distance(host.Image(0).rectTransform.anchoredPosition3D, position), Is.LessThan(.01f));
            host.Entrance.TickEntrance(host.ExitDuration);
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
        }
    }

    [Test]
    public void ClosingDuringEntranceDoesNotProduceADepartureOrAnArtificialPackedOrder()
    {
        using (var host = new Host())
        {
            host.Begin();
            host.Entrance.TickEntrance(.3f);
            var position = host.Image(0).rectTransform.anchoredPosition3D;
            host.Close();
            host.Reopen();
            Assert.That(host.State(0), Is.EqualTo("Entering"));
            Assert.That(Vector3.Distance(host.Image(0).rectTransform.anchoredPosition3D, position), Is.LessThan(.01f));
            host.Entrance.TickEntrance(10);
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(host.Entrance.StartedDepartureCount, Is.Zero);
            Assert.That(host.Presentation.VisualOutcomes, Is.Empty);
            Assert.That(host.SourceOrders.Any(order => order.IsPacked), Is.False);
        }
    }

    [TestCase(2)]
    [TestCase(3)]
    public void ConsecutiveDeliveriesReleaseOnlyTheirOwnSlotsAndNeverRecreateCompletedOrders(int count)
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipts = Enumerable.Range(0, count).Select(host.Pack).ToArray();
            for (int i = 0; i < count; i++)
            {
                host.CompleteVisual();
                Assert.That(host.State(i), Is.EqualTo("Leaving"));
                Assert.That(host.Presentation.VisualOutcomes[i].Receipt, Is.SameAs(receipts[i]));
                host.Entrance.TickEntrance(host.ExitDuration + .01f);
                Assert.That(host.State(i), Is.EqualTo("Empty"));
                for (int j = i + 1; j < count; j++) Assert.That(host.State(j), Is.EqualTo("Packed"));
            }
            host.Close();
            host.Reopen();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.State(i), Is.EqualTo(i < count ? "Empty" : "Waiting"));
                Assert.That(host.Image(i).gameObject.activeSelf, Is.EqualTo(i >= count));
            }
            for (int i = 0; i < 3; i++) { host.Entrance.TickEntrance(100); Assert.That(host.Orders.Refresh(), Is.True); }
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(3));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(count));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(count));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(count));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(count));
            Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(host.SourceOrders[i].IsPacked, Is.True);
                Assert.That(host.SourceOrders[i].PackingReceipt, Is.SameAs(receipts[i]));
                Assert.That(host.Orders.GetDisplayedMaterials(i), Is.Empty);
            }
        }
    }

    [Test]
    public void ThreeOverlappingDeparturesAdvanceIndependentlyAndKeepTheExistingMaterialLists()
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var lists = host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
            var scrolls = lists.Select(list => (ScrollRect)Read(list, "_scrollRect")).ToArray();
            var scrollPositions = scrolls.Select(scroll => scroll.horizontalNormalizedPosition).ToArray();
            var materials = lists.Select(list => list.MaterialCount).ToArray();
            for (int i = 0; i < 3; i++) host.Pack(i);
            host.CompleteVisual();
            host.Entrance.TickEntrance(host.ExitDuration * .3f);
            var firstPosition = host.Image(0).rectTransform.anchoredPosition3D;
            host.CompleteVisual();
            Assert.That(host.Image(0).rectTransform.anchoredPosition3D, Is.EqualTo(firstPosition));
            host.Entrance.TickEntrance(host.ExitDuration * .3f);
            Assert.That(host.Image(0).rectTransform.anchoredPosition3D, Is.Not.EqualTo(firstPosition));
            host.CompleteVisual();
            for (int i = 0; i < 3; i++) Assert.That(host.State(i), Is.EqualTo("Leaving"));
            host.Entrance.TickEntrance(host.ExitDuration * .41f);
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.State(1), Is.EqualTo("Leaving"));
            Assert.That(host.State(2), Is.EqualTo("Leaving"));
            host.Entrance.TickEntrance(host.ExitDuration);
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(materials, lists.Select(list => list.MaterialCount).ToArray());
            for (int i = 0; i < lists.Length; i++)
            {
                Assert.That(Read(lists[i], "_scrollRect"), Is.SameAs(scrolls[i]));
                Assert.That(scrolls[i].horizontalNormalizedPosition, Is.EqualTo(scrollPositions[i]).Within(.001f));
            }
            Assert.That(host.Counter.Count, Is.Zero);
        }
    }

    [TestCase("default")]
    [TestCase("anchors-pivot-scaled-parent")]
    [TestCase("canvas-resize")]
    public void DepartureMovesTheWholePortraitBeyondTheLeftBoundaryThenRestoresItsAuthoredTransform(string layout)
    {
        using (var host = new Host())
        {
            var image = host.Image(0);
            var rect = image.rectTransform;
            if (layout == "anchors-pivot-scaled-parent")
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.2f, .85f);
                rect.pivot = new Vector2(.1f, .9f);
                rect.parent.localScale = new Vector3(.7f, 1.3f, 1);
                rect.parent.localRotation = Quaternion.Euler(0, 0, 7);
            }
            Vector3 home = rect.anchoredPosition3D, scale = rect.localScale;
            Vector2 size = rect.sizeDelta;
            var sprite = image.sprite;
            host.ReadyCustomers();
            host.Pack(0);
            host.CompleteVisual();
            Assert.That(Vector3.Distance(rect.anchoredPosition3D, home), Is.LessThan(.01f));
            host.Entrance.TickEntrance(host.ExitDuration * .4f);
            if (layout == "canvas-resize")
            {
                host.Root.GetComponent<RectTransform>().sizeDelta = new Vector2(2560, 1440);
                host.Root.GetComponent<Canvas>().scaleFactor = 1.5f;
                host.Root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(2560, 1440);
                Canvas.ForceUpdateCanvases();
            }
            host.Entrance.TickEntrance(host.ExitDuration * .5999f);
            Assert.That(host.State(0), Is.EqualTo("Leaving"));
            var boundary = (RectTransform)Read(host.Entrance, "_exitBoundary");
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float right = corners.Max(point => boundary.InverseTransformPoint(point).x);
            Assert.That(right, Is.LessThan(boundary.rect.xMin), "The complete portrait is outside before its slot is released.");
            Assert.That(image.gameObject.activeSelf, Is.True);
            Assert.That(rect.localScale, Is.EqualTo(scale));
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.preserveAspect, Is.True);
            host.Entrance.TickEntrance(host.ExitDuration * .01f);
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(image.gameObject.activeSelf, Is.False);
            Assert.That(Vector3.Distance(rect.anchoredPosition3D, home), Is.LessThan(.01f));
        }
    }

    [Test]
    public void ChangingAutomaticEntryAfterReleaseCannotReviveTheCompletedCustomer()
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipt = host.Pack(0);
            host.CompleteVisual();
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Write(host.Entrance, "_automaticEntry", false);
            Assert.That(host.Orders.Refresh(), Is.True);
            host.Close();
            host.Reopen();
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.ControlsOrders, Is.True, "A started session retains its state machine.");
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.Image(0).gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(3));
            Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ClosingWithSeveralQueuedReceiptsArchivesEachOnceAndRecoversAllOccupiedSlots()
    {
        using (var host = new Host())
        {
            host.ReadyCustomers();
            var receipts = Enumerable.Range(0, 3).Select(host.Pack).ToArray();
            host.Presentation.TickPresentation(0);
            host.Close();
            Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(3));
            Assert.That(host.Presentation.VisualOutcomes.All(outcome => outcome.Kind == PopupStoreVisualOutcomeKind.Cancelled), Is.True);
            CollectionAssert.AreEquivalent(receipts, host.Presentation.VisualOutcomes.Select(outcome => outcome.Receipt).ToArray());
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.Zero);
            host.Reopen();
            for (int i = 0; i < 3; i++) Assert.That(host.State(i), Is.EqualTo("Leaving"));
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(3));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(3));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
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
        internal readonly Image Result;
        internal readonly Animator Boom;
        internal int PackedEvents;
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");
        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-customer-departure-memory-test";
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
                Result = (Image)Read(Presentation, "_resultImage");
                Boom = (Animator)Read(Presentation, "_boomAnimator");
                View.Init();
                View.Show();
                RefreshExisting();
                Counter.Changed += OnCounterChanged;
            }
            catch { Dispose(); throw; }
        }
        internal void Begin()
        {
            Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Presentation.BeginVisibleSession(), Is.True);
        }
        internal void ReadyCustomers()
        {
            Write(Entrance, "_entryInterval", 0f);
            Begin();
            Entrance.TickEntrance((float)Read(Entrance, "_entryDuration") + .01f);
            for (int i = 0; i < SourceOrders.Length; i++) Assert.That(State(i), Is.EqualTo("Waiting"));
        }
        internal string State(int slot) => Entrance.GetState(slot).ToString();
        internal Image Image(int slot) => Orders.Slots[slot].CustomerImage;
        internal void SetOrders(params PopupStoreOrder[] orders)
        {
            Write(Orders, "_sampleOrders", orders);
            Assert.That(Orders.Refresh(), Is.True);
        }
        internal PopupStoreMaterialEntry[] Resolve(int slot)
        {
            Assert.That(SourceOrders[slot].TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var entries, out var reason), Is.True, reason);
            return entries;
        }
        internal void Fill(int slot) { foreach (var material in Resolve(slot)) Assert.That(Counter.TryAdd(material), Is.True); }
        internal PopupStorePackingReceipt Pack(int slot)
        {
            Fill(slot);
            var result = ClickPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(slot));
            return result.Receipt;
        }
        internal PopupStorePackingResult ClickPack()
        {
            var button = (Button)Read(Packing, "_packButton");
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(ExecuteEvents.Execute(button.gameObject, new PointerEventData(null)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
            return Packing.LastResult;
        }
        internal void ToPhase(string phase)
        {
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != phase; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
        }
        internal void CompleteVisual() { Presentation.TickPresentation(0); Assert.That(Presentation.CurrentReceipt, Is.Not.Null); FinishVisual(); }
        internal void FinishVisual() => ToPhase("Idle");
        internal void Close()
        {
            View.Hide();
            // Non-ExecuteAlways EditMode objects need the same real lifecycle calls made explicitly.
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
            Begin();
        }
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
        public void Dispose()
        {
            if (Entrance != null) Entrance.PauseVisibleSession();
            if (Presentation != null) Presentation.EndVisibleSession();
            if (Counter != null) Counter.Changed -= OnCounterChanged;
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
    private static PopupStoreOrder MakeOrder(string customer, string plush)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", new[] { plush });
        Write(order, "_boxId", "BOX_PINK");
        Write(order, "_ribbonId", "RIBBON_YELLOW");
        return order;
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
