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

/// <summary>Actual popup inputs and packing transactions in memory; no scene, SDK, reward or account setup.</summary>
public sealed class PopupStorePackingTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";

    [Test]
    public void AuthoredPackingUsesTheExistingButtonAndOnlyPauseRemainsDisplayOnly()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var packing = prefab.GetComponent<PopupStorePacking>();
        Assert.That(packing, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PopupStorePacking>(true), Has.Length.EqualTo(1));
        Assert.That(Read(packing, "_counter"), Is.SameAs(prefab.GetComponentInChildren<PopupStoreSalesCounter>(true)));
        Assert.That(Read(packing, "_orders"), Is.SameAs(prefab.GetComponent<PopupStoreOrderDisplay>()));
        var button = (Button)Read(packing, "_packButton");
        Assert.That(button, Is.Not.Null);
        Assert.That(button.GetComponentInParent<UIPopupStore>(true), Is.SameAs(prefab.GetComponent<UIPopupStore>()));
        Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(((Image)button.targetGraphic).sprite), Is.EqualTo("Assets/Sprites/PopupStore/UI_Pack.png"));
        var displayOnly = (Button[])Read(prefab.GetComponent<UIPopupStore>(), "_displayOnlyButtons");
        Assert.That(displayOnly, Has.Length.EqualTo(1));
        Assert.That(displayOnly.Contains(button), Is.False);
        Assert.That(displayOnly[0].interactable, Is.False);
    }

    [TestCase("STAFF01", "BOX_PINK", "RIBBON_YELLOW")]
    [TestCase("STAFF01,STAFF02", "BOX_BLUE", "RIBBON_PINK")]
    [TestCase("STAFF03,STAFF05,STAFF02", "BOX_MINT", "RIBBON_MINT")]
    [TestCase("STAFF01,STAFF01", "BOX_BLUE", "RIBBON_PINK")]
    public void ActualPackClickConsumesTheWholeExactOrderOnceAndKeepsOtherCustomersAndAccount(string plushIds, string box, string ribbon)
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            host.SetOrders(MakeOrder("CUSTOMER01", plushIds, box, ribbon),
                MakeOrder("CUSTOMER04", "STAFF28", "BOX_MINT", "RIBBON_YELLOW"));
            var other = host.Display.GetDisplayedMaterials(1).ToArray();
            string otherId = host.Display.GetDisplayedOrderId(1);
            string orderId = host.Display.GetDisplayedOrderId(0);
            var ids = plushIds.Split(',').Concat(new[] { box, ribbon }).ToArray();
            host.Fill(ids);
            int feedbackBefore = host.FeedbackCount;
            var result = host.ClickPack();
            Assert.That(result.Status, Is.EqualTo(PopupStorePackingStatus.Succeeded));
            Assert.That(result.Success, Is.True);
            AssertReceipt(result.Receipt, orderId, 0, "CUSTOMER01", ids, box, ribbon);
            Assert.That(host.Packing.LastReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.PackedEvents, Is.EqualTo(1));
            AssertCounterEmpty(host);
            Assert.That(host.Display.IsOrderPacked(0), Is.True);
            Assert.That(host.Orders[0].PackingReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Display.GetDisplayedMaterials(0), Is.Empty);
            Assert.That(host.Display.Slots[0].CustomerImage.gameObject.activeSelf, Is.True);
            Assert.That(host.Display.Slots[0].OrderFrame.gameObject.activeSelf, Is.False);
            Assert.That(host.Display.IsOrderPacked(1), Is.False);
            Assert.That(host.Display.GetDisplayedOrderId(1), Is.EqualTo(otherId));
            CollectionAssert.AreEqual(other, host.Display.GetDisplayedMaterials(1).ToArray());
            AssertProgressEmpty(host);
            Assert.That(host.FeedbackCount, Is.EqualTo(feedbackBefore));
            Assert.That(host.Lists.Sum(list => list.MaterialCount), Is.EqualTo(38));
        }
        account.AssertUnchanged();
    }

    [TestCase("empty")]
    [TestCase("missing")]
    [TestCase("extra")]
    [TestCase("box-colour")]
    [TestCase("ribbon-colour")]
    [TestCase("order")]
    [TestCase("first-material")]
    [TestCase("no-customers")]
    public void NoneFromActualPackClickPreservesMaterialsOrdersRingsAndAccount(string defect)
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            host.SetOrders(MakeOrder("CUSTOMER01", "STAFF01", "BOX_PINK", "RIBBON_YELLOW"));
            string[] ids = { "STAFF01", "BOX_PINK", "RIBBON_YELLOW" };
            switch (defect)
            {
                case "empty": ids = Array.Empty<string>(); break;
                case "missing": ids = new[] { "STAFF01", "BOX_PINK" }; break;
                case "extra": ids = new[] { "STAFF01", "BOX_PINK", "RIBBON_YELLOW", "STAFF02" }; break;
                case "box-colour": ids[1] = "BOX_BLUE"; break;
                case "ribbon-colour": ids[2] = "RIBBON_MINT"; break;
                case "order": ids = new[] { "BOX_PINK", "STAFF01", "RIBBON_YELLOW" }; break;
                case "first-material": ids[0] = "STAFF02"; break;
                case "no-customers": host.SetOrders(); break;
            }
            host.Fill(ids);
            var before = new StateSnapshot(host);
            var result = host.ClickPack();
            Assert.That(result.Status, Is.EqualTo(PopupStorePackingStatus.None));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Receipt, Is.Null);
            before.AssertUnchanged(host);
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
        account.AssertUnchanged();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MultipleCandidatesNeverChooseTheFirstSlotOrConsumeMaterials(bool sameCustomer)
    {
        using (var host = new Host())
        {
            string second = sameCustomer ? "CUSTOMER01" : "CUSTOMER04";
            host.SetOrders(MakeOrder("CUSTOMER01", "STAFF01", "BOX_PINK", "RIBBON_YELLOW"),
                MakeOrder(second, "STAFF01", "BOX_PINK", "RIBBON_YELLOW"));
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            var before = new StateSnapshot(host);
            var result = host.ClickPack();
            Assert.That(result.Status, Is.EqualTo(PopupStorePackingStatus.Multiple));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Receipt, Is.Null);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.Matches.Candidates.Select(candidate => candidate.SlotIndex).ToArray());
            CollectionAssert.AreEqual(new[] { "CUSTOMER01", second }, result.Matches.Candidates.Select(candidate => candidate.CustomerId).ToArray());
            CollectionAssert.AreEqual(host.Orders.Select(order => order.RuntimeId).ToArray(), result.Matches.Candidates.Select(candidate => candidate.OrderId).ToArray());
            before.AssertUnchanged(host);
        }
    }

    [Test]
    public void RefillingACompletedOrderCannotPackItAgain()
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            var first = host.ClickPack();
            Assert.That(first.Success, Is.True);
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            var before = new StateSnapshot(host);
            Assert.That(host.ClickPack().Status, Is.EqualTo(PopupStorePackingStatus.None));
            before.AssertUnchanged(host);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Orders[0].PackingReceipt, Is.SameAs(first.Receipt));
        }
    }

    [Test]
    public void RapidRepeatedButtonClicksConsumeAndCompleteOnlyOnce()
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            Assert.That(host.ClickPack().Success, Is.True);
            for (int i = 0; i < 4; i++) Assert.That(host.ClickPack().Success, Is.False);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.PackedEvents, Is.EqualTo(1));
            AssertCounterEmpty(host);
            Assert.That(host.Packing.TryTakeReceipt(out var receipt), Is.True);
            Assert.That(receipt, Is.SameAs(host.Packing.LastReceipt));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [TestCase("counter")]
    [TestCase("orders")]
    public void PublicationObserversSeeBothCommittedStatesAndCannotReenterOrMutateTheTransaction(string publication)
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            int observations = 0;
            bool sawCommitted = false, addAccepted = true;
            PopupStorePackingStatus nestedStatus = PopupStorePackingStatus.Unavailable;
            Action observe = () =>
            {
                observations++;
                if (observations > 1) return;
                sawCommitted = host.Counter.Count == 0 && host.Display.IsOrderPacked(0)
                    && host.Packing.LastReceipt != null && host.Orders[0].PackingReceipt == host.Packing.LastReceipt;
                nestedStatus = host.Packing.TryPack().Status;
                addAccepted = host.Counter.TryAdd(Entry("STAFF28"));
                host.Counter.Clear();
                host.Counter.Refresh();
                host.Display.Refresh();
            };
            if (publication == "counter") host.Counter.Changed += reason => { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) observe(); };
            else host.Display.Refreshed += observe;
            var result = host.ClickPack();
            Assert.That(result.Success, Is.True);
            Assert.That(observations, Is.EqualTo(1));
            Assert.That(sawCommitted, Is.True);
            Assert.That(nestedStatus, Is.EqualTo(PopupStorePackingStatus.Busy));
            Assert.That(addAccepted, Is.False);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.PackedEvents, Is.EqualTo(1));
            AssertCounterEmpty(host);
            AssertProgressEmpty(host);
        }
    }

    [TestCase("order-reference")]
    [TestCase("customer")]
    [TestCase("materials")]
    [TestCase("other-order-becomes-identical")]
    public void AChangedLiveOrderCannotBeCommittedUsingTheOldDisplayedCandidate(string change)
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            Assert.That(host.Display.QueryExactMatches(host.Counter).Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
            switch (change)
            {
                case "order-reference": host.Orders[0] = MakeOrder("CUSTOMER01", "STAFF01", "BOX_PINK", "RIBBON_YELLOW"); break;
                case "customer": Write(host.Orders[0], "_customerId", "CUSTOMER04"); break;
                case "materials": Write(host.Orders[0], "_plushIds", new[] { "STAFF02" }); break;
                case "other-order-becomes-identical":
                    Write(host.Orders[1], "_plushIds", new[] { "STAFF01" });
                    Write(host.Orders[1], "_boxId", "BOX_PINK");
                    Write(host.Orders[1], "_ribbonId", "RIBBON_YELLOW");
                    break;
            }
            var before = new StateSnapshot(host);
            var result = host.ClickPack();
            Assert.That(result.Status, Is.EqualTo(PopupStorePackingStatus.Stale));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Receipt, Is.Null);
            before.AssertUnchanged(host);
        }
    }

    [Test]
    public void PackingRequeriesTheCurrentCounterInsteadOfReusingAPreviousSingleResult()
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            var old = host.Display.QueryExactMatches(host.Counter);
            Assert.That(old.Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
            host.Counter.Clear();
            host.Fill("STAFF28", "BOX_MINT", "RIBBON_YELLOW");
            var before = new StateSnapshot(host);
            Assert.That(host.ClickPack().Status, Is.EqualTo(PopupStorePackingStatus.None));
            before.AssertUnchanged(host);
            Assert.That(old.Kind, Is.EqualTo(PopupStoreExactMatchKind.Single));
        }
    }

    [Test]
    public void CloseAndReentryPreserveCompletedOrderIdentityAndDoNotMultiplyPackListeners()
    {
        using (var host = new Host())
        {
            string id = host.Display.GetDisplayedOrderId(0);
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            var receipt = host.ClickPack().Receipt;
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            int feedback = host.FeedbackCount;
            for (int i = 0; i < 3; i++) host.HideAndReopen();
            Assert.That(host.Display.GetDisplayedOrderId(0), Is.EqualTo(id));
            Assert.That(host.Display.IsOrderPacked(0), Is.True);
            Assert.That(host.Orders[0].PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.FeedbackCount, Is.EqualTo(feedback));
            Assert.That(host.ClickPack().Success, Is.False);
            Assert.That(host.Counter.Count, Is.EqualTo(3));
            host.Counter.Clear();
            host.Fill("STAFF01", "STAFF01", "BOX_BLUE", "RIBBON_PINK");
            Assert.That(host.ClickPack().Success, Is.True);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(2));
            Assert.That(host.PackedEvents, Is.EqualTo(2));
        }
    }

    [Test]
    public void RepeatedPackingRefreshBindsOnlyOneActualClickListener()
    {
        using (var host = new Host())
        {
            for (int i = 0; i < 5; i++) Assert.That(host.Packing.Refresh(), Is.True);
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            Assert.That(host.ClickPack().Success, Is.True);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.PackedEvents, Is.EqualTo(1));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.True);
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [Test]
    public void ReceiptsAreImmutableUniqueAndDeliveredOnlyOnceInCommitOrder()
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            var first = host.ClickPack().Receipt;
            host.Fill("STAFF01", "STAFF01", "BOX_BLUE", "RIBBON_PINK");
            var second = host.ClickPack().Receipt;
            Assert.That(first.ReceiptId, Is.Not.Null.And.Not.Empty);
            Assert.That(second.ReceiptId, Is.Not.EqualTo(first.ReceiptId));
            Assert.That(second.OrderId, Is.Not.EqualTo(first.OrderId));
            var items = first.Materials as IList<PopupStorePackedMaterial>;
            Assert.That(items, Is.Not.Null);
            Assert.That(items.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => items[0] = default(PopupStorePackedMaterial));
            Write(host.Orders[0], "_plushIds", new[] { "STAFF32" });
            CollectionAssert.AreEqual(new[] { "STAFF01", "BOX_PINK", "RIBBON_YELLOW" }, first.Materials.Select(item => item.Id).ToArray());
            Assert.That(host.Packing.TryTakeReceipt(out var takenFirst), Is.True);
            Assert.That(takenFirst, Is.SameAs(first));
            Assert.That(host.Packing.TryTakeReceipt(out var takenSecond), Is.True);
            Assert.That(takenSecond, Is.SameAs(second));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void OneThrowingPublicationSubscriberCannotLeaveAPartialCommitOrSkipLaterObservers()
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            int laterCounter = 0, laterOrders = 0;
            Action<PopupStoreSalesCounter.ChangeReason> counterFailure = reason => { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) throw new InvalidOperationException("packing-test-counter-observer"); };
            Action orderFailure = () => { throw new InvalidOperationException("packing-test-order-observer"); };
            host.Counter.Changed += counterFailure;
            host.Counter.Changed += reason => { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) laterCounter++; };
            host.Display.Refreshed += orderFailure;
            host.Display.Refreshed += () => laterOrders++;
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("packing-test-counter-observer"));
                LogAssert.Expect(LogType.Exception, new Regex("packing-test-order-observer"));
                var result = host.ClickPack();
                Assert.That(result.Success, Is.True);
                Assert.That(laterCounter, Is.EqualTo(1));
                Assert.That(laterOrders, Is.EqualTo(1));
                Assert.That(host.Display.IsOrderPacked(0), Is.True);
                AssertCounterEmpty(host);
                AssertProgressEmpty(host);
                Assert.That(host.Packing.TryTakeReceipt(out var receipt), Is.True);
                Assert.That(receipt, Is.SameAs(result.Receipt));
            }
            finally
            {
                host.Counter.Changed -= counterFailure;
                host.Display.Refreshed -= orderFailure;
            }
        }
    }

    [Test]
    public void HiddenPopupCannotPackEvenIfThePreviousCounterExactlyMatched()
    {
        using (var host = new Host())
        {
            host.Fill("STAFF01", "BOX_PINK", "RIBBON_YELLOW");
            host.View.Hide();
            InvokeLifecycle(host.Packing, "OnDisable");
            var before = new StateSnapshot(host);
            Assert.That(host.Packing.TryPack().Status, Is.EqualTo(PopupStorePackingStatus.Unavailable));
            before.AssertUnchanged(host);
        }
    }

    private static void AssertReceipt(PopupStorePackingReceipt receipt, string orderId, int slot, string customer, string[] ids, string box, string ribbon)
    {
        Assert.That(receipt, Is.Not.Null);
        Assert.That(receipt.Success, Is.True);
        Assert.That(receipt.ReceiptId, Is.Not.Null.And.Not.Empty);
        Assert.That(receipt.OrderId, Is.EqualTo(orderId));
        Assert.That(receipt.SlotIndex, Is.EqualTo(slot));
        Assert.That(receipt.CustomerId, Is.EqualTo(customer));
        CollectionAssert.AreEqual(ids, receipt.Materials.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(ids.Select(id => Entry(id).Kind).ToArray(), receipt.Materials.Select(item => item.Kind).ToArray());
        Assert.That(receipt.BoxId, Is.EqualTo(box));
        Assert.That(receipt.RibbonId, Is.EqualTo(ribbon));
    }
    private static void AssertCounterEmpty(Host host)
    {
        Assert.That(host.Counter.Count, Is.Zero);
        Assert.That(host.Counter.Materials, Is.Empty);
        foreach (var icon in (Image[])Read(host.Counter, "_slotIcons"))
        {
            Assert.That(icon.sprite, Is.Null);
            Assert.That(icon.gameObject.activeSelf, Is.False);
        }
    }
    private static void AssertProgressEmpty(Host host)
    {
        for (int i = 0; i < 3; i++) Assert.That(host.Progress.GetMatchedCount(i), Is.Zero);
        foreach (var slot in ((Array)Read(host.Progress, "_slots")).Cast<object>())
            foreach (var circle in (Image[])Read(slot, "_circles")) Assert.That(circle.gameObject.activeSelf, Is.False);
    }

    private sealed class StateSnapshot
    {
        private readonly PopupStoreMaterialEntry[] _materials;
        private readonly string[] _orders;
        private readonly bool[] _packed;
        private readonly string[] _orderIds;
        private readonly Image[] _images;
        private readonly Sprite[] _sprites;
        private readonly bool[] _active;
        private readonly Vector3[] _scales;
        private readonly int _successes, _packedEvents, _feedback;
        private readonly PopupStorePackingReceipt _receipt;
        internal StateSnapshot(Host host)
        {
            _materials = host.Counter.Materials.ToArray();
            _orders = host.Orders.Select(OrderText).ToArray();
            _packed = host.Orders.Select(order => order.IsPacked).ToArray();
            _orderIds = host.Orders.Select(order => order.RuntimeId).ToArray();
            _images = host.Root.GetComponentsInChildren<Image>(true);
            _sprites = _images.Select(image => image.sprite).ToArray();
            _active = _images.Select(image => image.gameObject.activeSelf).ToArray();
            _scales = _images.Select(image => image.transform.localScale).ToArray();
            _successes = host.Packing.SuccessfulPackCount;
            _packedEvents = host.PackedEvents;
            _feedback = host.FeedbackCount;
            _receipt = host.Packing.LastReceipt;
        }
        internal void AssertUnchanged(Host host)
        {
            CollectionAssert.AreEqual(_materials, host.Counter.Materials.ToArray());
            CollectionAssert.AreEqual(_orders, host.Orders.Select(OrderText).ToArray());
            CollectionAssert.AreEqual(_packed, host.Orders.Select(order => order.IsPacked).ToArray());
            CollectionAssert.AreEqual(_orderIds, host.Orders.Select(order => order.RuntimeId).ToArray());
            CollectionAssert.AreEqual(_sprites, _images.Select(image => image.sprite).ToArray());
            CollectionAssert.AreEqual(_active, _images.Select(image => image.gameObject.activeSelf).ToArray());
            CollectionAssert.AreEqual(_scales, _images.Select(image => image.transform.localScale).ToArray());
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(_successes));
            Assert.That(host.PackedEvents, Is.EqualTo(_packedEvents));
            Assert.That(host.FeedbackCount, Is.EqualTo(_feedback));
            Assert.That(host.Packing.LastReceipt, Is.SameAs(_receipt));
        }
    }

    private sealed class AccountSnapshot
    {
        private readonly long _money = UserInfo.Money;
        private readonly int _dia = UserInfo.Dia;
        private readonly int _visits = UserInfo.TotalCumulativeCustomerCount;
        private readonly long _context = UserInfo.CustomerEncounterContextGeneration;
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

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Display;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStoreMaterialList[] Lists;
        internal int FeedbackCount, PackedEvents;
        internal PopupStoreOrder[] Orders => (PopupStoreOrder[])Read(Display, "_sampleOrders");
        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-packing-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Display = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Lists = Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
                Assert.That(Packing, Is.Not.Null);
                View.Init();
                View.Show();
                // Explicit session start keeps these regression fixtures on the real runtime clock path.
                Assert.That(Root.GetComponent<PopupStoreBusinessClock>().TryStartSession(), Is.True);
                Assert.That(Counter.Refresh(), Is.True);
                foreach (var list in Lists) Assert.That(list.Refresh(), Is.True);
                Assert.That(Display.Refresh(), Is.True);
                Assert.That(Progress.Refresh(), Is.True);
                Assert.That(Packing.Refresh(), Is.True);
                Rebuild();
                Progress.FeedbackTriggered += OnFeedback;
                Counter.Changed += OnCounterChanged;
            }
            catch { Dispose(); throw; }
        }
        internal void SetOrders(params PopupStoreOrder[] orders)
        {
            Write(Display, "_sampleOrders", orders);
            Assert.That(Display.Refresh(), Is.True);
        }
        internal void Fill(params string[] ids) { foreach (string id in ids) ClickMaterial(id); }
        internal PopupStorePackingResult ClickPack()
        {
            var button = (Button)Read(Packing, "_packButton");
            Assert.That(button.IsActive(), Is.True);
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(ExecuteEvents.Execute(button.gameObject, new PointerEventData(null) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
            return Packing.LastResult;
        }
        private void ClickMaterial(string id)
        {
            var list = Lists.Single(item => (PopupStoreMaterialKind)Read(item, "_kind") == Entry(id).Kind);
            var icon = ((RectTransform)Read(list, "_content")).GetComponentsInChildren<PopupStoreMaterialItem>(false).Single(item => item.Material.Id == id).GetComponent<Image>();
            var scroll = (ScrollRect)Read(list, "_scrollRect");
            float range = scroll.content.rect.width - scroll.viewport.rect.width;
            if (range > 0) scroll.horizontalNormalizedPosition = Mathf.Clamp01((icon.rectTransform.anchoredPosition.x - scroll.viewport.rect.width * .5f) / range);
            scroll.StopMovement();
            scroll.Rebuild(CanvasUpdate.PostLayout);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, null), Is.True, id);
            var hit = new RaycastResult { gameObject = icon.gameObject, worldPosition = icon.rectTransform.position };
            var data = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left, pointerId = 23,
                position = point, pressPosition = point, eligibleForClick = true,
                pointerPress = icon.gameObject, rawPointerPress = icon.gameObject, pointerEnter = icon.gameObject,
                pointerPressRaycast = hit, pointerCurrentRaycast = hit
            };
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler), Is.True);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerUpHandler);
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler), Is.True);
        }
        internal void HideAndReopen()
        {
            var items = Root.GetComponentsInChildren<PopupStoreMaterialItem>(false);
            View.Hide();
            Assert.That(Root.activeSelf, Is.False);
            // Invoke the real callbacks in EditMode; this fixture does not claim actual Play validation.
            InvokeLifecycle(Packing, "OnDisable");
            InvokeLifecycle(Progress, "OnDisable");
            foreach (var item in items) InvokeLifecycle(item, "OnDisable");
            foreach (var list in Lists) InvokeLifecycle(list, "OnDisable");
            InvokeLifecycle(Counter, "OnDisable");
            InvokeLifecycle(Display, "OnDisable");
            View.Show();
            InvokeLifecycle(Counter, "OnEnable");
            foreach (var list in Lists) InvokeLifecycle(list, "OnEnable");
            InvokeLifecycle(Display, "OnEnable");
            InvokeLifecycle(Progress, "OnEnable");
            InvokeLifecycle(Packing, "OnEnable");
            Rebuild();
        }
        private void Rebuild()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var list in Lists) ((ScrollRect)Read(list, "_scrollRect")).Rebuild(CanvasUpdate.PostLayout);
        }
        private void OnFeedback() => FeedbackCount++;
        private void OnCounterChanged(PopupStoreSalesCounter.ChangeReason reason) { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) PackedEvents++; }
        public void Dispose()
        {
            if (Progress != null) Progress.FeedbackTriggered -= OnFeedback;
            if (Counter != null) Counter.Changed -= OnCounterChanged;
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private static PopupStoreMaterialEntry Entry(string id)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
        return ((PopupStoreMaterialEntry[])Read(catalog, "_entries")).Single(entry => entry.Id == id);
    }
    private static PopupStoreOrder MakeOrder(string customer, string plushCsv, string box, string ribbon)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", plushCsv.Split(','));
        Write(order, "_boxId", box);
        Write(order, "_ribbonId", ribbon);
        return order;
    }
    private static string OrderText(PopupStoreOrder order)
        => order.CustomerId + "|" + string.Join(",", order.PlushIds) + "|" + order.BoxId + "|" + order.RibbonId;
    private static void InvokeLifecycle(Component component, string method)
    {
        var info = component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, component.GetType().Name + "." + method);
        info.Invoke(component, null);
    }
    private static object Read(object target, string field) => Field(target, field).GetValue(target);
    private static void Write(object target, string field, object value) => Field(target, field).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        Assert.That(target, Is.Not.Null, name);
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
