using System;
using System.Collections.Generic;
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

/// <summary>Receipt-bound delivery and UI coordinates using real popup objects in memory, without a new Scene or account setup.</summary>
public sealed class PopupStoreDeliveryTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void EachReceiptReachesOnlyItsCommittedSlotOnceWithoutMovingCustomersOrChangingTheTransaction(int slot)
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            var receipt = host.Pack(slot);
            var transaction = new TransactionSnapshot(host);
            var customers = host.Orders.Slots.Select(item => new RectSnapshot(item.CustomerImage.rectTransform)).ToArray();
            var target = host.Orders.Slots[slot].CustomerImage.rectTransform;
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(receipt, out var resolved, out var reason), Is.True, reason);
            Assert.That(resolved, Is.SameAs(target));
            host.AdvanceTo("Move");
            Assert.That(host.Presentation.CurrentTarget, Is.SameAs(target));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.Zero);
            Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.False);
            host.AdvanceTo("Arrive");
            AssertAtTarget(host.Result.rectTransform, target, host.TargetOffset);
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.True);
            Assert.That(host.Result.gameObject.activeSelf, Is.True);
            host.Finish();
            for (int i = 0; i < 8; i++) host.Presentation.TickPresentation(1);
            host.AssertHiddenAndHome();
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Presentation.LastCancellationReason, Is.Null.Or.Empty);
            transaction.AssertUnchanged(host);
            for (int i = 0; i < customers.Length; i++)
            {
                customers[i].AssertUnchanged(host.Orders.Slots[i].CustomerImage.rectTransform);
                Assert.That(host.Orders.Slots[i].CustomerImage.gameObject.activeSelf, Is.True);
                Assert.That(host.Orders.Slots[i].OrderFrame.gameObject.activeSelf, Is.EqualTo(i != slot));
            }
        }
        account.AssertUnchanged();
    }

    [Test]
    public void SameCustomerIdInTwoSlotsDoesNotRedirectTheSecondOrdersDeliveryToTheFirstCustomer()
    {
        using (var host = new Host())
        {
            Write(host.SourceOrders[1], "_customerId", host.SourceOrders[0].CustomerId);
            Assert.That(host.Orders.Refresh(), Is.True);
            var receipt = host.Pack(1);
            Assert.That(receipt.CustomerId, Is.EqualTo(host.SourceOrders[0].CustomerId));
            Assert.That(receipt.OrderId, Is.Not.EqualTo(host.SourceOrders[0].RuntimeId));
            host.AdvanceTo("Arrive");
            Assert.That(host.Presentation.CurrentTarget, Is.SameAs(host.Orders.Slots[1].CustomerImage.rectTransform));
            AssertAtTarget(host.Result.rectTransform, host.Orders.Slots[1].CustomerImage.rectTransform, host.TargetOffset);
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
            Assert.That(host.SourceOrders[1].IsPacked, Is.True);
        }
    }

    [TestCase("negative-slot")]
    [TestCase("out-of-range-slot")]
    [TestCase("wrong-customer")]
    [TestCase("wrong-order")]
    public void MismatchedReceiptIdentityCannotResolveAnyCustomer(string defect)
    {
        using (var host = new Host())
        {
            var materials = host.Orders.GetDisplayedMaterials(0).ToArray();
            var original = host.Pack(0);
            int slot = defect == "negative-slot" ? -1 : defect == "out-of-range-slot" ? 3 : 0;
            var receipt = new PopupStorePackingReceipt(defect == "wrong-order" ? "expired-order-id" : original.OrderId,
                slot, defect == "wrong-customer" ? "CUSTOMER04" : original.CustomerId, materials);
            var before = new TransactionSnapshot(host);
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(receipt, out var target, out var reason), Is.False);
            Assert.That(target, Is.Null);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.Zero);
            before.AssertUnchanged(host);
        }
    }

    [Test]
    public void AReceiptForAnUncompletedOrderCannotResolveADeliveryTarget()
    {
        using (var host = new Host())
        {
            var receipt = new PopupStorePackingReceipt(host.SourceOrders[0].RuntimeId, 0,
                host.SourceOrders[0].CustomerId, host.Orders.GetDisplayedMaterials(0));
            Assert.That(host.SourceOrders[0].IsPacked, Is.False);
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(receipt, out var target, out var reason), Is.False);
            Assert.That(target, Is.Null);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
        }
    }

    [TestCase("inactive-image")]
    [TestCase("disabled-image-component")]
    [TestCase("reference-before-first-tick")]
    [TestCase("reference-during-move")]
    public void MissingOrReboundCustomerImageCancelsWithoutDeliveryToAnotherSlot(string change)
    {
        using (var host = new Host())
        {
            var receipt = host.Pack(0);
            if (change == "reference-during-move") host.AdvanceTo("Move");
            var originalTarget = host.Orders.Slots[0].CustomerImage;
            switch (change)
            {
                case "inactive-image": originalTarget.gameObject.SetActive(false); break;
                case "disabled-image-component": originalTarget.enabled = false; break;
                default:
                    var replacement = Object.Instantiate(originalTarget.gameObject, originalTarget.transform.parent).GetComponent<Image>();
                    replacement.name = "replacement-customer-in-memory";
                    Write(host.Orders.Slots[0], "_customerImage", replacement);
                    // Even a valid new displayed binding must not replace the image captured by the transaction.
                    Assert.That(host.Orders.Refresh(), Is.True);
                    break;
            }
            var transaction = new TransactionSnapshot(host);
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(receipt, out _, out _), Is.False);
            ExpectCancellation();
            host.Presentation.TickPresentation(.01f);
            host.AssertHiddenAndHome();
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.Zero);
            Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.False);
            Assert.That(host.Presentation.LastCancellationReason, Is.Not.Null.And.Not.Empty);
            Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(receipt));
            transaction.AssertUnchanged(host);
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
        }
    }

    [TestCase("customer-without-refresh")]
    [TestCase("order-id-without-refresh")]
    [TestCase("replaced-order-without-refresh")]
    [TestCase("replaced-order-after-refresh")]
    public void ChangedOrderIdentityDuringMovementStopsTheOldReceiptWithoutRollingBackItsCommit(string change)
    {
        using (var host = new Host())
        {
            var originalOrder = host.SourceOrders[0];
            var receipt = host.Pack(0);
            host.AdvanceTo("Move");
            if (change == "customer-without-refresh") Write(originalOrder, "_customerId", "CUSTOMER04");
            else if (change == "order-id-without-refresh") Write(originalOrder, "_runtimeId", "expired-order-id");
            else
            {
                host.SourceOrders[0] = MakeOrder(originalOrder.CustomerId);
                if (change == "replaced-order-after-refresh") Assert.That(host.Orders.Refresh(), Is.True);
            }
            var transaction = new TransactionSnapshot(host);
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(receipt, out _, out _), Is.False);
            ExpectCancellation();
            host.Presentation.TickPresentation(.01f);
            host.AssertHiddenAndHome();
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.Zero);
            Assert.That(host.Presentation.LastCancellationReason, Is.Not.Null.And.Not.Empty);
            Assert.That(originalOrder.IsPacked, Is.True);
            Assert.That(originalOrder.PackingReceipt, Is.SameAs(receipt));
            transaction.AssertUnchanged(host);
        }
    }

    [Test]
    public void RefreshingTheSameCommittedOrderAndImageDoesNotCancelAValidDelivery()
    {
        using (var host = new Host())
        {
            var receipt = host.Pack(0);
            var original = host.Orders.Slots[0].CustomerImage.rectTransform;
            host.AdvanceTo("Move");
            Assert.That(host.Orders.Refresh(), Is.True);
            Assert.That(host.Orders.TryGetPackedDeliveryTarget(receipt, out var target, out var reason), Is.True, reason);
            Assert.That(target, Is.SameAs(original));
            host.AdvanceTo("Arrive");
            AssertAtTarget(host.Result.rectTransform, original, host.TargetOffset);
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
        }
    }

    [TestCase("anchors-pivots")]
    [TestCase("different-scaled-parent")]
    [TestCase("layout-and-canvas-change-during-move")]
    public void ActualDeliveryUsesTheCurrentTargetInItsOwnCoordinateSpace(string layout)
    {
        using (var host = new Host(false))
        {
            var target = host.Orders.Slots[2].CustomerImage.rectTransform;
            var result = host.Result.rectTransform;
            target.anchorMin = target.anchorMax = new Vector2(.2f, .85f);
            target.pivot = new Vector2(.1f, .9f);
            result.anchorMin = result.anchorMax = new Vector2(.8f, .15f);
            result.pivot = new Vector2(.75f, .25f);
            Write(host.Presentation, "_targetOffset", new Vector2(17, -13));
            if (layout == "different-scaled-parent")
            {
                var parent = new GameObject("result-layer-in-memory", typeof(RectTransform)).GetComponent<RectTransform>();
                parent.SetParent(host.Root.transform, false);
                parent.sizeDelta = new Vector2(900, 600);
                parent.localScale = new Vector3(.7f, 1.4f, 1);
                parent.localRotation = Quaternion.Euler(0, 0, 11);
                result.SetParent(parent, false);
            }
            host.CaptureHome();
            host.Begin();
            host.Pack(2);
            host.AdvanceTo("Move");
            if (layout == "layout-and-canvas-change-during-move")
            {
                host.Presentation.TickPresentation((float)Read(host.Presentation, "_moveDuration") * .25f);
                host.Root.GetComponent<RectTransform>().sizeDelta = new Vector2(2560, 1440);
                host.Root.GetComponent<Canvas>().scaleFactor = 1.6f;
                var scaler = host.Root.GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(2560, 1440);
                scaler.matchWidthOrHeight = .7f;
                target.anchoredPosition += new Vector2(47, -31);
                Canvas.ForceUpdateCanvases();
            }
            var customerPosition = target.position;
            host.AdvanceTo("Arrive");
            AssertAtTarget(result, target, host.TargetOffset);
            Assert.That(target.position, Is.EqualTo(customerPosition));
            host.Finish();
            host.AssertHiddenAndHome();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DifferentRootCanvasesUseTheirRespectiveCamerasForScreenAlignedDestination(bool resultIsOverlay)
    {
        using (var fixture = new CoordinateFixture(resultIsOverlay))
        {
            var offset = new Vector2(23, -19);
            Assert.That(PopupStorePackingPresentation.TryGetTargetLocalPosition(fixture.Result, fixture.Target, offset, out var local, out var reason), Is.True, reason);
            fixture.Result.localPosition = local;
            AssertAtTarget(fixture.Result, fixture.Target, offset);
        }
    }

    [Test]
    public void DifferentCanvasWithoutItsRequiredCameraCannotGuessADeliveryDestination()
    {
        using (var fixture = new CoordinateFixture(true))
        {
            var targetCanvas = fixture.Target.GetComponentInParent<Canvas>();
            var camera = targetCanvas.worldCamera;
            // A camera-less ScreenSpaceCamera becomes Overlay in this Editor; WorldSpace still requires projection.
            targetCanvas.renderMode = RenderMode.WorldSpace;
            targetCanvas.worldCamera = null;
            Assert.That(targetCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(targetCanvas.worldCamera, Is.Null);
            Assert.That(targetCanvas.rootCanvas, Is.Not.SameAs(fixture.Result.GetComponentInParent<Canvas>().rootCanvas));
            Assert.That(PopupStorePackingPresentation.TryGetTargetLocalPosition(fixture.Result, fixture.Target, Vector2.zero, out _, out var reason), Is.False,
                "mode=" + targetCanvas.renderMode + "; camera=" + targetCanvas.worldCamera
                + "; root=" + targetCanvas.rootCanvas.name + "; resultRoot=" + fixture.Result.GetComponentInParent<Canvas>().rootCanvas.name);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            targetCanvas.worldCamera = camera;
            Assert.That(PopupStorePackingPresentation.TryGetTargetLocalPosition(fixture.Result, fixture.Target, Vector2.zero, out var local, out reason), Is.True, reason);
            fixture.Result.localPosition = local;
            AssertAtTarget(fixture.Result, fixture.Target, Vector2.zero);
        }
    }

    [Test]
    public void DuplicateReceiptDoesNotMoveAgainOrCompleteDeliveryTwice()
    {
        using (var host = new Host())
        {
            var receipt = host.Pack(0);
            host.AdvanceTo("Move");
            ((Queue<PopupStorePackingReceipt>)Read(host.Packing, "_receipts")).Enqueue(receipt);
            host.Finish();
            for (int i = 0; i < 5; i++) host.Presentation.TickPresentation(2);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts, Has.Count.EqualTo(1));
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            host.AssertHiddenAndHome();
        }
    }

    [Test]
    public void ConsecutiveDeliveriesWaitForThePriorFadeAndKeepReceiptOrder()
    {
        using (var host = new Host())
        {
            var receipts = new[] { host.Pack(0), host.Pack(1), host.Pack(2) };
            var transaction = new TransactionSnapshot(host);
            for (int i = 0; i < receipts.Length; i++)
            {
                host.Presentation.TickPresentation(0);
                Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipts[i]));
                Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(i + 1));
                host.AdvanceTo("Arrive");
                Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipts[i]));
                Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(i + 1));
                host.AdvanceTo("Fade");
                Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipts[i]));
                Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(i + 1));
                host.Finish();
                host.AssertHiddenAndHome();
            }
            CollectionAssert.AreEqual(receipts, host.Presentation.ReceivedReceipts.ToArray());
            foreach (var receipt in receipts) Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.True);
            transaction.AssertUnchanged(host);
        }
    }

    [TestCase("Move")]
    [TestCase("Arrive")]
    [TestCase("Fade")]
    public void ClosingInEachDeliveryPhaseRestoresTheImageAndNeverReplaysOnReentry(string phase)
    {
        using (var host = new Host())
        {
            var receipt = host.Pack(1);
            host.AdvanceTo(phase);
            int delivered = phase == "Move" ? 0 : 1;
            int objects = host.Root.GetComponentsInChildren<Transform>(true).Length;
            var transaction = new TransactionSnapshot(host);
            host.Close();
            host.AssertHiddenAndHome();
            host.Reopen();
            for (int i = 0; i < 4; i++) host.Presentation.TickPresentation(10);
            host.AssertHiddenAndHome();
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(delivered));
            Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.EqualTo(delivered == 1));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(host.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
            transaction.AssertUnchanged(host);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CloseImmediatelyBeforeOrAfterArrivalPreservesTheObservedCompletionBoundary(bool afterArrival)
    {
        using (var host = new Host())
        {
            Write(host.Presentation, "_moveDuration", 1f);
            var receipt = host.Pack(0);
            host.AdvanceTo("Move");
            host.Presentation.TickPresentation(.98f);
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Move"));
            if (afterArrival) host.Presentation.TickPresentation(.03f);
            Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.EqualTo(afterArrival));
            host.Close();
            host.Reopen();
            host.Presentation.TickPresentation(100);
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(afterArrival ? 1 : 0));
            Assert.That(host.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.EqualTo(afterArrival));
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            host.AssertHiddenAndHome();
        }
    }

    [Test]
    public void ANewReceiptAfterCancelledDeliveryStartsAtHomeAndDoesNotReuseThePreviousTarget()
    {
        using (var host = new Host())
        {
            var cancelled = host.Pack(0);
            host.AdvanceTo("Move");
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_moveDuration") * .4f);
            host.Close();
            host.Reopen();
            var fresh = host.Pack(2);
            host.Presentation.TickPresentation(0);
            Assert.That(host.Result.rectTransform.anchoredPosition3D, Is.EqualTo(host.HomePosition));
            host.AdvanceTo("Arrive");
            AssertAtTarget(host.Result.rectTransform, host.Orders.Slots[2].CustomerImage.rectTransform, host.TargetOffset);
            Assert.That(host.Presentation.IsDeliveryCompleted(cancelled.ReceiptId), Is.False);
            Assert.That(host.Presentation.IsDeliveryCompleted(fresh.ReceiptId), Is.True);
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(fresh));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(2));
            host.Finish();
            host.AssertHiddenAndHome();
        }
    }

    private static void ExpectCancellation() => LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));

    private static void AssertAtTarget(RectTransform result, RectTransform target, Vector2 offset)
    {
        var point = target.TransformPoint((Vector3)(target.rect.center + offset));
        Vector2 expected = RectTransformUtility.WorldToScreenPoint(CanvasCamera(target), point);
        Vector2 actual = RectTransformUtility.WorldToScreenPoint(CanvasCamera(result), result.TransformPoint(result.rect.center));
        Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.05f), "Result centre must reach the target centre plus its local offset in screen space.");
    }
    private static Camera CanvasCamera(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal readonly Image Result;
        internal readonly Animator Boom;
        internal int PackedEvents, FeedbackEvents;
        internal Vector3 HomePosition, HomeScale;
        internal Color HomeColor;
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal Vector2 TargetOffset => (Vector2)Read(Presentation, "_targetOffset");
        internal Host(bool begin = true)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-delivery-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Result = (Image)Read(Presentation, "_resultImage");
                Boom = (Animator)Read(Presentation, "_boomAnimator");
                View.Init();
                View.Show();
                // Explicit session start keeps these regression fixtures on the real runtime clock path.
                Assert.That(Root.GetComponent<PopupStoreBusinessClock>().TryStartSession(), Is.True);
                RefreshExisting();
                Counter.Changed += OnCounterChanged;
                Progress.FeedbackTriggered += OnFeedback;
                CaptureHome();
                if (begin) Begin();
            }
            catch { Dispose(); throw; }
        }
        internal void CaptureHome()
        {
            HomePosition = Result.rectTransform.anchoredPosition3D;
            HomeScale = Result.rectTransform.localScale;
            HomeColor = Result.color;
        }
        internal void Begin() => Assert.That(Presentation.BeginVisibleSession(), Is.True);
        internal PopupStorePackingReceipt Pack(int slot)
        {
            foreach (var material in Orders.GetDisplayedMaterials(slot)) Assert.That(Counter.TryAdd(material), Is.True);
            var button = (Button)Read(Packing, "_packButton");
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(ExecuteEvents.Execute(button.gameObject, new PointerEventData(null) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
            Assert.That(Packing.LastResult.Success, Is.True);
            return Packing.LastResult.Receipt;
        }
        internal void AdvanceTo(string phase)
        {
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != phase; i++) Presentation.TickPresentation(.01f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
        }
        internal void Finish() => AdvanceTo("Idle");
        internal void AssertHiddenAndHome()
        {
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo("Idle"));
            Assert.That(Presentation.CurrentReceipt, Is.Null);
            Assert.That(Presentation.CurrentTarget, Is.Null);
            Assert.That(Result.gameObject.activeSelf, Is.False);
            Assert.That(Boom.gameObject.activeSelf, Is.False);
            Assert.That(Result.sprite, Is.Null);
            Assert.That(Result.rectTransform.anchoredPosition3D, Is.EqualTo(HomePosition));
            Assert.That(Result.rectTransform.localScale, Is.EqualTo(HomeScale));
            Assert.That(Result.color, Is.EqualTo(HomeColor));
        }
        internal void Close()
        {
            if (Presentation.CurrentReceipt != null) ExpectCancellation();
            View.Hide();
            // Invoke real lifecycle methods explicitly for these non-ExecuteAlways EditMode objects.
            Presentation.EndVisibleSession();
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
        private void OnCounterChanged(PopupStoreSalesCounter.ChangeReason reason) { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) PackedEvents++; }
        private void OnFeedback() => FeedbackEvents++;
        public void Dispose()
        {
            if (Presentation != null)
            {
                if (Presentation.CurrentReceipt != null) ExpectCancellation();
                Presentation.EndVisibleSession();
            }
            if (Counter != null) Counter.Changed -= OnCounterChanged;
            if (Progress != null) Progress.FeedbackTriggered -= OnFeedback;
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private sealed class CoordinateFixture : IDisposable
    {
        private readonly GameObject _root;
        internal readonly RectTransform Result, Target;
        internal CoordinateFixture(bool resultIsOverlay)
        {
            _root = new GameObject("delivery-coordinate-memory-test");
            try
            {
                var camera = new GameObject("delivery-ui-camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(_root.transform, false);
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 400;
                camera.enabled = false;
                var resultCanvas = MakeCanvas("result-canvas", resultIsOverlay, camera);
                var targetCanvas = MakeCanvas("target-canvas", !resultIsOverlay, camera);
                var parent = new GameObject("result-parent", typeof(RectTransform)).GetComponent<RectTransform>();
                parent.SetParent(resultCanvas.transform, false);
                parent.sizeDelta = new Vector2(700, 400);
                parent.pivot = new Vector2(.3f, .7f);
                parent.localScale = new Vector3(.75f, 1.1f, 1);
                Result = new GameObject("result", typeof(RectTransform)).GetComponent<RectTransform>();
                Result.SetParent(parent, false);
                Result.anchorMin = Result.anchorMax = new Vector2(.8f, .2f);
                Result.pivot = new Vector2(.7f, .1f);
                Result.localPosition += new Vector3(0, 0, 3);
                Result.localRotation = Quaternion.Euler(9, 0, 17);
                Target = new GameObject("target", typeof(RectTransform)).GetComponent<RectTransform>();
                Target.SetParent(targetCanvas.transform, false);
                Target.anchorMin = Target.anchorMax = new Vector2(.35f, .65f);
                Target.pivot = new Vector2(.2f, .8f);
                Target.sizeDelta = new Vector2(145, 180);
                Target.anchoredPosition = new Vector2(70, -45);
                Canvas.ForceUpdateCanvases();
            }
            catch { Dispose(); throw; }
        }
        private Canvas MakeCanvas(string name, bool overlay, Camera camera)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.transform.SetParent(_root.transform, false);
            canvas.renderMode = overlay ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = overlay ? null : camera;
            canvas.planeDistance = 10;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = overlay ? .25f : .75f;
            return canvas;
        }
        public void Dispose() { if (_root != null) Object.DestroyImmediate(_root); }
    }

    private sealed class RectSnapshot
    {
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        private readonly Vector2 _size, _anchorMin, _anchorMax, _pivot;
        internal RectSnapshot(RectTransform rect)
        {
            _position = rect.anchoredPosition3D; _scale = rect.localScale; _rotation = rect.localRotation;
            _size = rect.sizeDelta; _anchorMin = rect.anchorMin; _anchorMax = rect.anchorMax; _pivot = rect.pivot;
        }
        internal void AssertUnchanged(RectTransform rect)
        {
            Assert.That(rect.anchoredPosition3D, Is.EqualTo(_position));
            Assert.That(rect.localScale, Is.EqualTo(_scale));
            Assert.That(rect.localRotation, Is.EqualTo(_rotation));
            Assert.That(rect.sizeDelta, Is.EqualTo(_size));
            Assert.That(rect.anchorMin, Is.EqualTo(_anchorMin));
            Assert.That(rect.anchorMax, Is.EqualTo(_anchorMax));
            Assert.That(rect.pivot, Is.EqualTo(_pivot));
        }
    }
    private sealed class TransactionSnapshot
    {
        private readonly PopupStoreMaterialEntry[] _counter;
        private readonly string[] _orderIds;
        private readonly bool[] _packed;
        private readonly PopupStorePackingReceipt[] _receipts;
        private readonly int _successes, _events, _feedback;
        internal TransactionSnapshot(Host host)
        {
            _counter = host.Counter.Materials.ToArray();
            _orderIds = host.SourceOrders.Select(order => order.RuntimeId).ToArray();
            _packed = host.SourceOrders.Select(order => order.IsPacked).ToArray();
            _receipts = host.SourceOrders.Select(order => order.PackingReceipt).ToArray();
            _successes = host.Packing.SuccessfulPackCount; _events = host.PackedEvents; _feedback = host.FeedbackEvents;
        }
        internal void AssertUnchanged(Host host)
        {
            CollectionAssert.AreEqual(_counter, host.Counter.Materials.ToArray());
            CollectionAssert.AreEqual(_orderIds, host.SourceOrders.Select(order => order.RuntimeId).ToArray());
            CollectionAssert.AreEqual(_packed, host.SourceOrders.Select(order => order.IsPacked).ToArray());
            CollectionAssert.AreEqual(_receipts, host.SourceOrders.Select(order => order.PackingReceipt).ToArray());
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(_successes));
            Assert.That(host.PackedEvents, Is.EqualTo(_events));
            Assert.That(host.FeedbackEvents, Is.EqualTo(_feedback));
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
