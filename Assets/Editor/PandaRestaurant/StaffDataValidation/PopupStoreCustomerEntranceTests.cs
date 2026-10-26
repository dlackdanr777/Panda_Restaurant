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

/// <summary>Real popup entry, waiting gates and packing paths in memory; no new Scene or account initialization.</summary>
public sealed class PopupStoreCustomerEntranceTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ZeroToThreeAuthoredOrdersEnterOnlyTheirOwnOccupiedSlots(int count)
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            host.SetOrders(host.SourceOrders.Take(count).ToArray());
            var orders = host.SourceOrders.ToArray();
            var ids = orders.Select(order => order.RuntimeId).ToArray();
            host.Begin();
            host.TickFor(10);
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(count));
            Assert.That(host.Entrance.ArrivedCount, Is.EqualTo(count));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.State(i), Is.EqualTo(i < count ? "Waiting" : "Empty"));
                Assert.That(host.Orders.Slots[i].CustomerImage.gameObject.activeSelf, Is.EqualTo(i < count));
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.EqualTo(i < count));
                Assert.That(host.Orders.GetDisplayedMaterials(i).Count, Is.EqualTo(i < count ? host.Resolve(i).Length : 0));
            }
            CollectionAssert.AreEqual(orders, host.SourceOrders);
            CollectionAssert.AreEqual(ids, host.SourceOrders.Select(order => order.RuntimeId).ToArray());
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(host.Counter.Count, Is.Zero);
        }
        account.AssertUnchanged();
    }

    [Test]
    public void BeginHidesTheStaticCustomersAndTheFirstTickStartsOnlySlotZero()
    {
        using (var host = new Host())
        {
            Assert.That(host.Root.GetComponents<PopupStoreCustomerEntrance>(), Has.Length.EqualTo(1));
            Assert.That((bool)Read(host.Entrance, "_automaticEntry"), Is.True);
            Assert.That(Read(host.Entrance, "_orders"), Is.SameAs(host.Orders));
            host.Begin();
            Assert.That(host.Entrance.ControlsOrders, Is.True);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.State(i), Is.EqualTo("Empty"));
                Assert.That(host.Orders.Slots[i].CustomerImage.gameObject.activeSelf, Is.False);
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.False);
            }
            host.Entrance.TickEntrance(0);
            Assert.That(host.State(0), Is.EqualTo("Entering"));
            Assert.That(host.State(1), Is.EqualTo("Empty"));
            Assert.That(host.State(2), Is.EqualTo("Empty"));
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ArrivedCount, Is.Zero);
            Assert.That(host.Orders.Slots[0].CustomerImage.gameObject.activeSelf, Is.True);
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyOrEnteringCustomerCannotBeMatchedOrPackedAndCounterMaterialsArePreserved(bool entering)
    {
        using (var host = new Host())
        {
            host.Begin();
            if (entering) host.Entrance.TickEntrance(0);
            host.Fill(0);
            var materials = host.Counter.Materials.ToArray();
            Assert.That(host.State(0), Is.EqualTo(entering ? "Entering" : "Empty"));
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            Assert.That(host.Progress.GetMatchedCount(0), Is.Zero);
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.None));
            Assert.That(host.ClickPack().Status, Is.EqualTo(PopupStorePackingStatus.None));
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [Test]
    public void ArrivalReevaluatesPreparedMaterialsSilentlyAndEnablesTheRealExactMatch()
    {
        using (var host = new Host())
        {
            host.Begin();
            host.Entrance.TickEntrance(0);
            host.Fill(0);
            Assert.That(host.FeedbackEvents, Is.Zero);
            int count = host.Counter.Count;
            host.TickFor(host.Duration + .02f);
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(host.Entrance.IsWaiting(0), Is.True);
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.True);
            Assert.That(host.Progress.GetMatchedCount(0), Is.EqualTo(count));
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
            Assert.That(host.Counter.Count, Is.EqualTo(count));
            Assert.That(host.FeedbackEvents, Is.Zero, "Arrival is a silent order refresh, not a new ingredient click.");
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
        }
    }

    [Test]
    public void EntranceIntervalsAreMeasuredFromStartTimesRatherThanThePriorArrival()
    {
        using (var host = new Host())
        {
            Write(host.Entrance, "_entryDuration", 2f);
            Write(host.Entrance, "_entryInterval", 4f);
            host.Begin();
            host.Entrance.TickEntrance(0);
            host.Entrance.TickEntrance(3.99f);
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(host.State(1), Is.EqualTo("Empty"));
            host.Entrance.TickEntrance(.02f);
            Assert.That(host.State(1), Is.EqualTo("Entering"));
            Assert.That(host.State(2), Is.EqualTo("Empty"));
            host.Entrance.TickEntrance(3.98f);
            Assert.That(host.State(2), Is.EqualTo("Empty"));
            host.Entrance.TickEntrance(.02f);
            Assert.That(host.State(2), Is.EqualTo("Entering"));
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void ZeroIntervalAllowsThreeConcurrentEntrancesWithoutCreatingAdditionalCustomers()
    {
        using (var host = new Host())
        {
            Write(host.Entrance, "_entryInterval", 0f);
            int objects = host.Root.GetComponentsInChildren<Transform>(true).Length;
            host.Begin();
            host.Entrance.TickEntrance(0);
            for (int i = 0; i < 3; i++) Assert.That(host.State(i), Is.EqualTo("Entering"));
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(3));
            host.TickFor(host.Duration + .02f);
            for (int i = 0; i < 3; i++) Assert.That(host.State(i), Is.EqualTo("Waiting"));
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(3));
            Assert.That(host.Entrance.ArrivedCount, Is.EqualTo(3));
            Assert.That(host.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }
    }

    [TestCase("Entering")]
    [TestCase("Waiting")]
    [TestCase("Packed")]
    public void ClosingAndReopeningResumesTheSameCustomersWithoutResettingTheirHomesOrTransactions(string state)
    {
        using (var host = new Host())
        {
            var home = host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D;
            host.Begin();
            host.Entrance.TickEntrance(0);
            host.TickFor(state == "Entering" ? .4f : host.Duration + .02f);
            PopupStorePackingReceipt receipt = null;
            if (state == "Packed")
            {
                host.Fill(0);
                receipt = host.ClickPack().Receipt;
                host.Entrance.SynchronizeOrders();
            }
            Assert.That(host.State(0), Is.EqualTo(state));
            var position = host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D;
            int starts = host.Entrance.StartedEntranceCount, arrivals = host.Entrance.ArrivedCount;
            var ids = host.SourceOrders.Select(order => order.RuntimeId).ToArray();
            host.Close();
            Assert.That(host.Entrance.IsRunning, Is.False);
            host.Entrance.TickEntrance(100);
            Assert.That(host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D, Is.EqualTo(position));
            host.Reopen();
            // A committed but unplayed delivery now uses the cancellation cleanup departure on reentry.
            Assert.That(host.State(0), Is.EqualTo(state == "Packed" ? "Leaving" : state));
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(starts));
            Assert.That(host.Entrance.ArrivedCount, Is.EqualTo(arrivals));
            Assert.That(host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D, Is.EqualTo(position));
            host.TickFor((state == "Packed" ? (float)Read(host.Entrance, "_exitDuration") : host.Duration) + .02f);
            Assert.That(host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D, Is.EqualTo(home));
            Assert.That(host.State(0), Is.EqualTo(state == "Packed" ? "Empty" : "Waiting"));
            CollectionAssert.AreEqual(ids, host.SourceOrders.Select(order => order.RuntimeId).ToArray());
            if (receipt != null)
            {
                Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
                Assert.That(host.Orders.TryGetCommittedCustomerBinding(receipt, out var target, out var reason), Is.True, reason);
                Assert.That(target, Is.SameAs(host.Orders.Slots[0].CustomerImage.rectTransform));
                Assert.That(host.Orders.TryGetPackedDeliveryTarget(receipt, out _, out _), Is.False, "A released portrait is not a delivery target.");
                Assert.That(host.Entrance.StartedDepartureCount, Is.EqualTo(1));
                Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1));
                Assert.That(host.Orders.Slots[0].CustomerImage.gameObject.activeSelf, Is.False);
                var presentation = host.Root.GetComponent<PopupStorePackingPresentation>();
                Assert.That(presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
                Assert.That(presentation.StartedPresentationCount, Is.Zero);
                Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
                Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.False);
            }
        }
    }

    [TestCase("customer-id")]
    [TestCase("runtime-id")]
    [TestCase("order-reference")]
    [TestCase("image-reference")]
    public void ChangedOrderIdentityDuringEntranceIsBlockedInsteadOfDisplayingAnUnrelatedOrder(string change)
    {
        using (var host = new Host())
        {
            host.Begin();
            host.Entrance.TickEntrance(.3f);
            if (change == "customer-id") Write(host.SourceOrders[0], "_customerId", "CUSTOMER04");
            else if (change == "runtime-id") Write(host.SourceOrders[0], "_runtimeId", "replacement-runtime-id");
            else if (change == "image-reference") Write(host.Orders.Slots[0], "_customerImage", host.Orders.Slots[1].CustomerImage);
            else host.SourceOrders[0] = MakeOrder("CUSTOMER01");
            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_ENTRY_CANCELLED"));
            host.Entrance.TickEntrance(.01f);
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.Entrance.IsWaiting(0), Is.False);
            Assert.That(host.Orders.Slots[0].CustomerImage.gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            host.TickFor(10);
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(host.Counter.Count, Is.Zero);
        }
    }

    [Test]
    public void IdenticalWaitingOrdersRemainMultipleWithoutChoosingOrPackingACustomer()
    {
        using (var host = new Host())
        {
            host.SetOrders(MakeOrder("CUSTOMER01"), MakeOrder("CUSTOMER01"));
            host.Begin();
            host.TickFor(6);
            host.Fill(0);
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(host.State(1), Is.EqualTo("Waiting"));
            Assert.That(host.SourceOrders[0].RuntimeId, Is.Not.EqualTo(host.SourceOrders[1].RuntimeId));
            var materials = host.Counter.Materials.ToArray();
            Assert.That(host.Orders.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.Multiple));
            Assert.That(host.ClickPack().Status, Is.EqualTo(PopupStorePackingStatus.Multiple));
            CollectionAssert.AreEqual(materials, host.Counter.Materials.ToArray());
            Assert.That(host.SourceOrders.Any(order => order.IsPacked), Is.False);
        }
    }

    [Test]
    public void PackedFirstSlotDoesNotStealAnIdenticalOrderFromALaterArrivingCustomer()
    {
        using (var host = new Host())
        {
            host.SetOrders(MakeOrder("CUSTOMER01"), MakeOrder("CUSTOMER01"));
            host.Begin();
            host.TickFor(host.Duration + .02f);
            host.Fill(0);
            var first = host.ClickPack();
            Assert.That(first.Success, Is.True);
            host.Entrance.SynchronizeOrders();
            Assert.That(host.State(0), Is.EqualTo("Packed"));
            Assert.That(host.State(1), Is.EqualTo("Empty"));
            host.TickFor(5);
            host.Fill(1);
            var matches = host.Orders.QueryExactMatches(host.Counter);
            Assert.That(matches.Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
            Assert.That(matches.Candidates[0].SlotIndex, Is.EqualTo(1));
            var second = host.ClickPack();
            Assert.That(second.Success, Is.True);
            Assert.That(second.Receipt.OrderId, Is.Not.EqualTo(first.Receipt.OrderId));
            Assert.That(second.Receipt.SlotIndex, Is.EqualTo(1));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(2));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(first.Receipt));
        }
    }

    [Test]
    public void ArrivedCustomerCanBePackedAndReceiveTheSameReceiptExactlyOnceThroughTheExistingPresentation()
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            host.Begin();
            host.TickFor((float)Read(host.Entrance, "_entryInterval") + host.Duration + .02f);
            Assert.That(host.State(1), Is.EqualTo("Waiting"));
            var presentation = host.Root.GetComponent<PopupStorePackingPresentation>();
            Assert.That(presentation.BeginVisibleSession(), Is.True);
            try
            {
                host.Fill(1);
                var result = host.ClickPack();
                Assert.That(result.Success, Is.True);
                Assert.That(result.Receipt.SlotIndex, Is.EqualTo(1));
                Assert.That(result.Receipt.CustomerId, Is.EqualTo(host.SourceOrders[1].CustomerId));
                Assert.That(host.State(1), Is.EqualTo("Packed"));
                presentation.TickPresentation(0);
                for (int i = 0; i < 2000 && presentation.CurrentPhase.ToString() != "Arrive"; i++) presentation.TickPresentation(.01f);
                Assert.That(presentation.CurrentPhase.ToString(), Is.EqualTo("Arrive"));
                Assert.That(presentation.CurrentTarget, Is.SameAs(host.Orders.Slots[1].CustomerImage.rectTransform));
                Assert.That(presentation.CurrentReceipt, Is.SameAs(result.Receipt));
                Assert.That(presentation.IsDeliveryCompleted(result.Receipt.ReceiptId), Is.True);
                for (int i = 0; i < 2000 && presentation.CurrentPhase.ToString() != "Idle"; i++) presentation.TickPresentation(.01f);
                Assert.That(presentation.CurrentPhase.ToString(), Is.EqualTo("Idle"));
                presentation.TickPresentation(100);
                Assert.That(presentation.StartedPresentationCount, Is.EqualTo(1));
                Assert.That(presentation.CompletedDeliveryCount, Is.EqualTo(1));
                Assert.That(presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
                Assert.That(presentation.ReceivedReceipts.Single(), Is.SameAs(result.Receipt));
                Assert.That(host.SourceOrders[1].PackingReceipt, Is.SameAs(result.Receipt));
                Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
                Assert.That(host.Counter.Count, Is.Zero);
                Assert.That(host.Orders.Slots[1].CustomerImage.gameObject.activeSelf, Is.True);
                Assert.That(host.Orders.Slots[1].OrderFrame.gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (presentation.CurrentReceipt != null)
                    LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
                presentation.EndVisibleSession();
            }
        }
        account.AssertUnchanged();
    }

    [TestCase("default")]
    [TestCase("anchors-pivot-scaled-parent")]
    [TestCase("canvas-change-during-entry")]
    public void PortraitStartsFullyOutsideTheRightBoundaryAndArrivesAtItsAuthoredHome(string layout)
    {
        using (var host = new Host())
        {
            var image = host.Orders.Slots[0].CustomerImage;
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
            host.Begin();
            host.Entrance.TickEntrance(0);
            var boundary = (RectTransform)Read(host.Entrance, "_entryBoundary");
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float left = corners.Min(point => boundary.InverseTransformPoint(point).x);
            float offset = ((Vector2)Read(host.Entrance, "_startOffset")).x;
            Assert.That(left, Is.GreaterThanOrEqualTo(boundary.rect.xMax + offset - .05f));
            host.TickFor(.4f);
            if (layout == "canvas-change-during-entry")
            {
                host.Root.GetComponent<RectTransform>().sizeDelta = new Vector2(2560, 1440);
                host.Root.GetComponent<Canvas>().scaleFactor = 1.5f;
                host.Root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(2560, 1440);
                Canvas.ForceUpdateCanvases();
            }
            host.TickFor(host.Duration + .02f);
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(Vector3.Distance(rect.anchoredPosition3D, home), Is.LessThan(.01f));
            Assert.That(rect.localScale, Is.EqualTo(scale));
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.preserveAspect, Is.True);
        }
    }

    [Test]
    public void AllThreePortraitsKeepTheirApprovedSpritesSizesAndAspectDuringMovement()
    {
        using (var host = new Host())
        {
            Write(host.Entrance, "_entryInterval", 0f);
            var images = host.Orders.Slots.Select(slot => slot.CustomerImage).ToArray();
            var sprites = images.Select(image => image.sprite).ToArray();
            var sizes = images.Select(image => image.rectTransform.sizeDelta).ToArray();
            var scales = images.Select(image => image.rectTransform.localScale).ToArray();
            host.Begin();
            host.TickFor(.5f);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.State(i), Is.EqualTo("Entering"));
                Assert.That(images[i].sprite, Is.SameAs(sprites[i]));
                Assert.That(images[i].rectTransform.sizeDelta, Is.EqualTo(sizes[i]));
                Assert.That(images[i].rectTransform.localScale, Is.EqualTo(scales[i]));
                Assert.That(images[i].preserveAspect, Is.True);
            }
        }
    }

    [Test]
    public void DisablingAutomaticEntryKeepsTheExistingStaticOrderAndPackingPathAvailable()
    {
        using (var host = new Host())
        {
            Write(host.Entrance, "_automaticEntry", false);
            host.Entrance.BeginVisibleSession();
            host.Entrance.TickEntrance(100);
            Assert.That(host.Entrance.ControlsOrders, Is.False);
            Assert.That(host.Entrance.StartedEntranceCount, Is.Zero);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.Orders.Slots[i].CustomerImage.gameObject.activeSelf, Is.True);
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.True);
                Assert.That(host.Orders.GetDisplayedMaterials(i), Is.Not.Empty);
            }
            host.Fill(0);
            Assert.That(host.ClickPack().Success, Is.True);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RepeatedBeginAndExtraTicksDoNotRestartOrDuplicateTheThreeEntrances()
    {
        using (var host = new Host())
        {
            host.Begin();
            host.TickFor(.4f);
            var position = host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D;
            for (int i = 0; i < 5; i++) host.Begin();
            Assert.That(host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D, Is.EqualTo(position));
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(1));
            host.TickFor(12);
            for (int i = 0; i < 5; i++) { host.Begin(); host.Entrance.TickEntrance(100); }
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(3));
            Assert.That(host.Entrance.ArrivedCount, Is.EqualTo(3));
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
        }
    }

    [TestCase("Entering")]
    [TestCase("Waiting")]
    public void RefreshingTheSameOrderBindingsDoesNotRestartEntryOrRecaptureAMovingPositionAsHome(string state)
    {
        using (var host = new Host())
        {
            var home = host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D;
            host.Begin();
            host.TickFor(state == "Entering" ? .4f : host.Duration + .02f);
            var position = host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D;
            int starts = host.Entrance.StartedEntranceCount;
            Assert.That(host.Orders.Refresh(), Is.True);
            Assert.That(host.State(0), Is.EqualTo(state));
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(starts));
            Assert.That(host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D, Is.EqualTo(position));
            host.TickFor(host.Duration + .02f);
            Assert.That(host.State(0), Is.EqualTo("Waiting"));
            Assert.That(host.Orders.Slots[0].CustomerImage.rectTransform.anchoredPosition3D, Is.EqualTo(home));
        }
    }

    [Test]
    public void ReplacingACancelledOrderAndRefreshingDoesNotAutomaticallySpawnANewCustomer()
    {
        using (var host = new Host())
        {
            host.Begin();
            host.TickFor(.3f);
            host.SourceOrders[0] = MakeOrder("CUSTOMER04");
            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_ENTRY_CANCELLED"));
            host.Entrance.TickEntrance(.01f);
            Assert.That(host.Orders.Refresh(), Is.True);
            host.TickFor(12);
            Assert.That(host.State(0), Is.EqualTo("Empty"));
            Assert.That(host.Orders.Slots[0].CustomerImage.gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.GetDisplayedMaterials(0), Is.Empty);
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
            Assert.That(host.Entrance.StartedEntranceCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void EntranceLeavesMaterialListsAndTheirIndependentScrollReferencesUntouched()
    {
        using (var host = new Host())
        {
            var lists = host.Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
            var scrolls = lists.Select(list => (ScrollRect)Read(list, "_scrollRect")).ToArray();
            var contents = scrolls.Select(scroll => scroll.content).ToArray();
            var counts = lists.Select(list => list.MaterialCount).ToArray();
            var positions = scrolls.Select(scroll => scroll.horizontalNormalizedPosition).ToArray();
            CollectionAssert.AllItemsAreUnique(scrolls.Select(scroll => scroll.GetInstanceID()).ToArray());
            host.Begin();
            host.TickFor(10);
            Assert.That(counts.Sum(), Is.EqualTo(38));
            CollectionAssert.AreEqual(counts, lists.Select(list => list.MaterialCount).ToArray());
            for (int i = 0; i < lists.Length; i++)
            {
                Assert.That(Read(lists[i], "_scrollRect"), Is.SameAs(scrolls[i]));
                Assert.That(scrolls[i].content, Is.SameAs(contents[i]));
                Assert.That(scrolls[i].horizontalNormalizedPosition, Is.EqualTo(positions[i]).Within(.001f));
            }
            Assert.That(host.Counter.Count, Is.Zero);
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
        internal int FeedbackEvents;
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float Duration => (float)Read(Entrance, "_entryDuration");
        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-customer-entry-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Assert.That(Entrance, Is.Not.Null);
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                View.Init();
                View.Show();
                RefreshExisting();
                Progress.FeedbackTriggered += OnFeedback;
            }
            catch { Dispose(); throw; }
        }
        internal void Begin() => Assert.That(Entrance.BeginVisibleSession(), Is.True);
        internal string State(int slot) => Entrance.GetState(slot).ToString();
        internal void SetOrders(params PopupStoreOrder[] orders)
        {
            Write(Orders, "_sampleOrders", orders);
            Assert.That(Orders.Refresh(), Is.True);
        }
        internal PopupStoreMaterialEntry[] Resolve(int slot)
        {
            var catalog = (PopupStoreMaterialCatalog)Read(Orders, "_catalog");
            Assert.That(SourceOrders[slot].TryResolve(catalog, out var materials, out var reason), Is.True, reason);
            return materials;
        }
        internal void Fill(int slot) { foreach (var material in Resolve(slot)) Assert.That(Counter.TryAdd(material), Is.True); }
        internal PopupStorePackingResult ClickPack()
        {
            var button = (Button)Read(Packing, "_packButton");
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(ExecuteEvents.Execute(button.gameObject, new PointerEventData(null) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
            return Packing.LastResult;
        }
        internal void TickFor(float duration)
        {
            Entrance.TickEntrance(0);
            float elapsed = 0;
            while (elapsed < duration)
            {
                float delta = Mathf.Min(.05f, duration - elapsed);
                Entrance.TickEntrance(delta);
                elapsed += delta;
            }
        }
        internal void Close()
        {
            View.Hide();
            Entrance.PauseVisibleSession();
            // EditMode does not automatically dispatch all non-ExecuteAlways component callbacks.
            Root.GetComponent<PopupStorePackingPresentation>().EndVisibleSession();
            InvokeLifecycle(Packing, "OnDisable");
            InvokeLifecycle(Progress, "OnDisable");
            InvokeLifecycle(Counter, "OnDisable");
            InvokeLifecycle(Orders, "OnDisable");
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
        private void OnFeedback() => FeedbackEvents++;
        public void Dispose()
        {
            if (Progress != null) Progress.FeedbackTriggered -= OnFeedback;
            if (Entrance != null) Entrance.PauseVisibleSession();
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
    private static PopupStoreOrder MakeOrder(string customer)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", new[] { "STAFF01" });
        Write(order, "_boxId", "BOX_PINK");
        Write(order, "_ribbonId", "RIBBON_YELLOW");
        return order;
    }
    private static void InvokeLifecycle(Component component, string method)
        => component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
