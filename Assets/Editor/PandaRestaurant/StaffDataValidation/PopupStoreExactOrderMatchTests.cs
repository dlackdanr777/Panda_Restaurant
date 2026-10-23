using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Pure exact matching and read-only queries of the actual popup in memory; no packing or account setup.</summary>
public sealed class PopupStoreExactOrderMatchTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";

    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "", false)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK,RIBBON_YELLOW", true)]
    [TestCase("STAFF01,STAFF02,BOX_BLUE,RIBBON_PINK", "STAFF01,STAFF02,BOX_BLUE,RIBBON_PINK", true)]
    [TestCase("STAFF03,STAFF05,STAFF02,BOX_MINT,RIBBON_MINT", "STAFF03,STAFF05,STAFF02,BOX_MINT,RIBBON_MINT", true)]
    [TestCase("STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK", "STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK", true)]
    [TestCase("STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK", "STAFF01,BOX_BLUE,RIBBON_PINK", false)]
    [TestCase("STAFF01,STAFF02,BOX_BLUE,RIBBON_PINK", "STAFF02,STAFF01,BOX_BLUE,RIBBON_PINK", false)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_BLUE,RIBBON_YELLOW", false)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK,RIBBON_MINT", false)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01", false)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK,RIBBON_YELLOW,STAFF02", false)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK", false)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,RIBBON_YELLOW,BOX_PINK", false)]
    public void ExactMatchRequiresEveryMaterialInTheRightQuantityOrderAndColour(string orderIds, string counterIds, bool expected)
    {
        var order = Entries(orderIds);
        var counter = Entries(counterIds);
        var orderBefore = order.ToArray();
        var counterBefore = counter.ToArray();
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), order, counter), Is.EqualTo(expected));
        CollectionAssert.AreEqual(orderBefore, order);
        CollectionAssert.AreEqual(counterBefore, counter);
    }

    [Test]
    public void NullOrEmptyInputsAndMissingCatalogCannotBecomeAnExactOrder()
    {
        var valid = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        Assert.That(PopupStoreExactOrderMatch.IsExact(null, valid, valid), Is.False);
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), null, valid), Is.False);
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), valid, null), Is.False);
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), Array.Empty<PopupStoreMaterialEntry>(), Array.Empty<PopupStoreMaterialEntry>()), Is.False);
    }

    [TestCase("BOX_PINK,RIBBON_YELLOW")]
    [TestCase("STAFF01,STAFF02,STAFF03,STAFF04,BOX_PINK,RIBBON_YELLOW")]
    [TestCase("STAFF01,BOX_PINK,BOX_BLUE")]
    [TestCase("STAFF01,RIBBON_YELLOW,BOX_PINK")]
    public void IdenticalArraysWithAnInvalidOrderShapeAreStillRejected(string ids)
    {
        var malformed = Entries(ids);
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), malformed, malformed), Is.False);
    }

    [TestCase("unknown-id")]
    [TestCase("wrong-kind")]
    [TestCase("wrong-case")]
    [TestCase("null-entry")]
    public void InvalidEntryCannotMatchEvenWhenItsSpriteLooksLikeTheExpectedMaterial(string defect)
    {
        var order = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        var counter = order.ToArray();
        switch (defect)
        {
            case "unknown-id":
                order[0] = MakeEntry("STAFF99", PopupStoreMaterialKind.Plush, Entry("STAFF01").Sprite);
                counter[0] = order[0];
                break;
            case "wrong-kind": counter[0] = MakeEntry("STAFF01", PopupStoreMaterialKind.Box, Entry("STAFF01").Sprite); break;
            case "wrong-case": counter[0] = MakeEntry("staff01", PopupStoreMaterialKind.Plush, Entry("STAFF01").Sprite); break;
            case "null-entry": counter[0] = null; break;
        }
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), order, counter), Is.False);
    }

    [Test]
    public void RegisteredKindAndIdMatchWithoutSpriteOrEntryObjectIdentity()
    {
        var order = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        var copied = order.Select(entry => MakeEntry(entry.Id, entry.Kind, Entry("STAFF32").Sprite)).ToArray();
        Assert.That(copied[0], Is.Not.SameAs(order[0]));
        Assert.That(copied[0].Sprite, Is.Not.SameAs(order[0].Sprite));
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), order, copied), Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void CandidateSearchReturnsAllMatchingSlotsForZeroToThreeOrders(int count)
    {
        var materials = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        var orders = Enumerable.Range(0, count).Select(_ => (IReadOnlyList<PopupStoreMaterialEntry>)materials).ToArray();
        var found = PopupStoreExactOrderMatch.FindExactSlots(Catalog(), orders, materials);
        CollectionAssert.AreEqual(Enumerable.Range(0, count).ToArray(), found.ToArray());
        if (count > 0)
        {
            var list = found as IList<int>;
            Assert.That(list, Is.Not.Null);
            Assert.That(list.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => list[0] = 99);
        }
    }

    [Test]
    public void CandidateSearchRejectsUnsupportedCollectionsAndSkipsInvalidIndividualOrders()
    {
        var valid = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        IReadOnlyList<PopupStoreMaterialEntry>[] tooMany = { valid, valid, valid, valid };
        Assert.That(PopupStoreExactOrderMatch.FindExactSlots(Catalog(), null, valid), Is.Empty);
        Assert.That(PopupStoreExactOrderMatch.FindExactSlots(Catalog(), new[] { (IReadOnlyList<PopupStoreMaterialEntry>)valid }, null), Is.Empty);
        Assert.That(PopupStoreExactOrderMatch.FindExactSlots(Catalog(), tooMany, valid), Is.Empty);
        IReadOnlyList<PopupStoreMaterialEntry>[] mixed = { null, Entries("STAFF01,BOX_PINK,BOX_BLUE"), valid };
        CollectionAssert.AreEqual(new[] { 2 }, PopupStoreExactOrderMatch.FindExactSlots(Catalog(), mixed, valid).ToArray());
    }

    [Test]
    public void DiagnosticResultCopiesAllCandidatesAndExposesNoWritableOrPreferredTarget()
    {
        var input = new List<PopupStoreExactOrderCandidate>
        {
            new PopupStoreExactOrderCandidate(0, "CUSTOMER01"),
            new PopupStoreExactOrderCandidate(2, "CUSTOMER01")
        };
        var result = new PopupStoreExactMatchResult(input);
        input.Clear();
        AssertResult(result, PopupStoreExactMatchKind.Multiple, new[] { 0, 2 }, new[] { "CUSTOMER01", "CUSTOMER01" });
        var mutable = result.Candidates as IList<PopupStoreExactOrderCandidate>;
        Assert.That(mutable, Is.Not.Null);
        Assert.That(mutable.IsReadOnly, Is.True);
        Assert.Throws<NotSupportedException>(() => mutable[0] = new PopupStoreExactOrderCandidate(1, "CUSTOMER04"));
        AssertResult(PopupStoreExactMatchResult.Empty, PopupStoreExactMatchKind.None, Array.Empty<int>(), Array.Empty<string>());
    }

    [TestCase(0, "CUSTOMER01")]
    [TestCase(1, "CUSTOMER04")]
    [TestCase(2, "CUSTOMER11")]
    public void ActualMaterialClicksIdentifyOnlyTheExactCustomerAmongThreeDisplayedOrders(int index, string customerId)
    {
        using (var host = new Host())
        {
            var expected = host.Display.GetDisplayedMaterials(index).Select(entry => entry.Id).ToArray();
            foreach (string id in expected) host.Click(id);
            AssertResult(host.Query(), PopupStoreExactMatchKind.Single, new[] { index }, new[] { customerId });
            Assert.That(host.Display.GetDisplayedCustomerId(index), Is.EqualTo(customerId));
            CollectionAssert.AreEqual(expected, host.Counter.Materials.Select(entry => entry.Id).ToArray());
            Assert.That(host.Display.DisplayedCount, Is.EqualTo(3));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IdenticalOrdersReturnBothSlotsEvenWhenTheCustomerIdsAreAlsoIdentical(bool sameCustomer)
    {
        using (var host = new Host())
        {
            string secondId = sameCustomer ? "CUSTOMER01" : "CUSTOMER04";
            host.SetOrders(MakeOrder("CUSTOMER01", "STAFF01", "BOX_PINK", "RIBBON_YELLOW"),
                MakeOrder(secondId, "STAFF01", "BOX_PINK", "RIBBON_YELLOW"), host.AuthoredOrders[2]);
            host.Click("STAFF01");
            host.Click("BOX_PINK");
            host.Click("RIBBON_YELLOW");
            AssertResult(host.Query(), PopupStoreExactMatchKind.Multiple, new[] { 0, 1 }, new[] { "CUSTOMER01", secondId });
            Assert.That(host.Counter.Count, Is.EqualTo(3));
            Assert.That(host.Display.DisplayedCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void QueryUsesDisplayedCustomerAndMaterialSnapshotsUntilTheOrdersAreActuallyRefreshed()
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            host.Click("BOX_PINK");
            host.Click("RIBBON_YELLOW");
            var original = host.Query();
            Write(host.AuthoredOrders[0], "_customerId", "CUSTOMER04");
            Write(host.AuthoredOrders[0], "_plushIds", new[] { "STAFF03" });
            Assert.That(host.Display.GetDisplayedCustomerId(0), Is.EqualTo("CUSTOMER01"));
            AssertResult(host.Query(), PopupStoreExactMatchKind.Single, new[] { 0 }, new[] { "CUSTOMER01" });
            Assert.That(host.Display.Refresh(), Is.True);
            Assert.That(host.Display.GetDisplayedCustomerId(0), Is.EqualTo("CUSTOMER04"));
            AssertResult(host.Query(), PopupStoreExactMatchKind.None, Array.Empty<int>(), Array.Empty<string>());
            AssertResult(original, PopupStoreExactMatchKind.Single, new[] { 0 }, new[] { "CUSTOMER01" });
            Assert.That(host.Counter.Count, Is.EqualTo(3));
        }
    }

    [Test]
    public void UnavailableViewsAndCountersReturnNoCandidateWithoutRefreshingOrAddingMaterials()
    {
        foreach (string condition in new[] { "closed", "display-disabled", "counter-disabled", "display-unready", "counter-unready", "null-counter", "no-customers" })
        {
            using (var host = new Host())
            {
                host.Click("STAFF01");
                host.Click("BOX_PINK");
                host.Click("RIBBON_YELLOW");
                switch (condition)
                {
                    case "closed": host.View.Hide(); break;
                    case "display-disabled": host.Display.enabled = false; break;
                    case "counter-disabled": host.Counter.enabled = false; break;
                    case "display-unready": InvokeLifecycle(host.Display, "OnDisable"); break;
                    case "counter-unready": InvokeLifecycle(host.Counter, "OnDisable"); break;
                    case "no-customers": host.SetOrders(); break;
                }
                var before = new StateSnapshot(host);
                var result = host.Display.QueryExactMatches(condition == "null-counter" ? null : host.Counter);
                AssertResult(result, PopupStoreExactMatchKind.None, Array.Empty<int>(), Array.Empty<string>());
                before.AssertUnchanged(host, condition);
            }
        }
    }

    [Test]
    public void CounterFromAnotherPopupInstanceCannotProvideCandidatesForThisDisplay()
    {
        using (var first = new Host())
        using (var other = new Host())
        {
            foreach (var host in new[] { first, other })
            {
                host.Click("STAFF01");
                host.Click("BOX_PINK");
                host.Click("RIBBON_YELLOW");
            }
            var firstBefore = new StateSnapshot(first);
            var otherBefore = new StateSnapshot(other);
            AssertResult(first.Display.QueryExactMatches(other.Counter), PopupStoreExactMatchKind.None, Array.Empty<int>(), Array.Empty<string>());
            firstBefore.AssertUnchanged(first, "first popup");
            otherBefore.AssertUnchanged(other, "other popup");
        }
    }

    [Test]
    public void RepeatedExactQueriesDoNotChangeCounterOrdersRingsFeedbackAccountOrDisabledPacking()
    {
        long money = UserInfo.Money;
        int dia = UserInfo.Dia;
        int visits = UserInfo.TotalCumulativeCustomerCount;
        long context = UserInfo.CustomerEncounterContextGeneration;
        object score = typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        using (var host = new Host())
        {
            host.Click("STAFF01");
            host.Click("BOX_PINK");
            host.Click("RIBBON_YELLOW");
            var before = new StateSnapshot(host);
            for (int i = 0; i < 10; i++)
            {
                AssertResult(host.Query(), PopupStoreExactMatchKind.Single, new[] { 0 }, new[] { "CUSTOMER01" });
                before.AssertUnchanged(host, "Query " + i);
            }
            foreach (var button in (Button[])Read(host.View, "_displayOnlyButtons")) Assert.That(button.interactable, Is.False);
            Assert.That(host.Counter.Count, Is.EqualTo(3));
        }
        Assert.That(UserInfo.Money, Is.EqualTo(money));
        Assert.That(UserInfo.Dia, Is.EqualTo(dia));
        Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(visits));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(context));
        Assert.That(typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.EqualTo(score));
    }

    [Test]
    public void PrefixHintFullExactAndExtraMaterialRemainDistinctAndClearRemovesCandidates()
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            Assert.That(host.Progress.GetMatchedCount(0), Is.EqualTo(1));
            AssertResult(host.Query(), PopupStoreExactMatchKind.None, Array.Empty<int>(), Array.Empty<string>());
            host.Click("BOX_PINK");
            host.Click("RIBBON_YELLOW");
            AssertResult(host.Query(), PopupStoreExactMatchKind.Single, new[] { 0 }, new[] { "CUSTOMER01" });
            host.Click("STAFF32");
            Assert.That(host.Progress.GetMatchedCount(0), Is.EqualTo(3));
            AssertResult(host.Query(), PopupStoreExactMatchKind.None, Array.Empty<int>(), Array.Empty<string>());
            Assert.That(host.Counter.Count, Is.EqualTo(4));
            ((Button)Read(host.Counter, "_clearButton")).onClick.Invoke();
            Assert.That(host.Counter.Count, Is.Zero);
            AssertResult(host.Query(), PopupStoreExactMatchKind.None, Array.Empty<int>(), Array.Empty<string>());
        }
    }

    private sealed class StateSnapshot
    {
        private readonly PopupStoreMaterialEntry[] _counter;
        private readonly PopupStoreMaterialEntry[][] _orders;
        private readonly string[] _customers;
        private readonly string[] _authoredData;
        private readonly Image[] _images;
        private readonly Sprite[] _sprites;
        private readonly bool[] _active;
        private readonly Vector3[] _scales;
        private readonly Vector2[] _positions;
        private readonly int[] _matched;
        private readonly int _feedback, _counterEvents, _orderEvents;
        private readonly bool _counterReady, _orderReady;
        private readonly int _displayedCount;

        internal StateSnapshot(Host host)
        {
            _counter = host.Counter.Materials.ToArray();
            _orders = Enumerable.Range(0, 3).Select(index => host.Display.GetDisplayedMaterials(index).ToArray()).ToArray();
            _customers = Enumerable.Range(0, 3).Select(host.Display.GetDisplayedCustomerId).ToArray();
            _authoredData = AuthoredData(host.Display);
            _images = host.Root.GetComponentsInChildren<Image>(true);
            _sprites = _images.Select(image => image.sprite).ToArray();
            _active = _images.Select(image => image.gameObject.activeSelf).ToArray();
            _scales = _images.Select(image => image.transform.localScale).ToArray();
            _positions = _images.Select(image => image.rectTransform.anchoredPosition).ToArray();
            _matched = Enumerable.Range(0, 3).Select(host.Progress.GetMatchedCount).ToArray();
            _feedback = host.FeedbackCount;
            _counterEvents = host.CounterEvents;
            _orderEvents = host.OrderEvents;
            _counterReady = host.Counter.IsReady;
            _orderReady = host.Display.IsReady;
            _displayedCount = host.Display.DisplayedCount;
        }

        internal void AssertUnchanged(Host host, string context)
        {
            CollectionAssert.AreEqual(_counter, host.Counter.Materials.ToArray(), context);
            for (int i = 0; i < 3; i++) CollectionAssert.AreEqual(_orders[i], host.Display.GetDisplayedMaterials(i).ToArray(), context);
            CollectionAssert.AreEqual(_customers, Enumerable.Range(0, 3).Select(host.Display.GetDisplayedCustomerId).ToArray(), context);
            CollectionAssert.AreEqual(_authoredData, AuthoredData(host.Display), context);
            CollectionAssert.AreEqual(_images.Select(image => image.GetInstanceID()).ToArray(), host.Root.GetComponentsInChildren<Image>(true).Select(image => image.GetInstanceID()).ToArray(), context);
            CollectionAssert.AreEqual(_sprites, _images.Select(image => image.sprite).ToArray(), context);
            CollectionAssert.AreEqual(_active, _images.Select(image => image.gameObject.activeSelf).ToArray(), context);
            CollectionAssert.AreEqual(_scales, _images.Select(image => image.transform.localScale).ToArray(), context);
            CollectionAssert.AreEqual(_positions, _images.Select(image => image.rectTransform.anchoredPosition).ToArray(), context);
            CollectionAssert.AreEqual(_matched, Enumerable.Range(0, 3).Select(host.Progress.GetMatchedCount).ToArray(), context);
            Assert.That(host.FeedbackCount, Is.EqualTo(_feedback), context);
            Assert.That(host.CounterEvents, Is.EqualTo(_counterEvents), context);
            Assert.That(host.OrderEvents, Is.EqualTo(_orderEvents), context);
            Assert.That(host.Counter.IsReady, Is.EqualTo(_counterReady), context);
            Assert.That(host.Display.IsReady, Is.EqualTo(_orderReady), context);
            Assert.That(host.Display.DisplayedCount, Is.EqualTo(_displayedCount), context);
        }
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Display;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStoreMaterialList[] Lists;
        internal readonly PopupStoreOrder[] AuthoredOrders;
        internal int FeedbackCount, CounterEvents, OrderEvents;

        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-exact-order-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Display = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Lists = Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
                AuthoredOrders = ((PopupStoreOrder[])Read(Display, "_sampleOrders")).ToArray();
                View.Init();
                View.Show();
                // Dispatch existing initialization explicitly for these non-ExecuteAlways EditMode objects.
                Assert.That(Counter.Refresh(), Is.True);
                foreach (var list in Lists) Assert.That(list.Refresh(), Is.True);
                Assert.That(Display.Refresh(), Is.True);
                Assert.That(Progress.Refresh(), Is.True);
                Canvas.ForceUpdateCanvases();
                foreach (var list in Lists) ((ScrollRect)Read(list, "_scrollRect")).Rebuild(CanvasUpdate.PostLayout);
                Progress.FeedbackTriggered += OnFeedback;
                Counter.Changed += OnCounterChanged;
                Display.Refreshed += OnOrdersRefreshed;
            }
            catch { Dispose(); throw; }
        }

        internal PopupStoreExactMatchResult Query() => Display.QueryExactMatches(Counter);
        internal void SetOrders(params PopupStoreOrder[] orders)
        {
            Write(Display, "_sampleOrders", orders);
            Assert.That(Display.Refresh(), Is.True);
        }
        internal void Click(string id)
        {
            var list = Lists.Single(item => (PopupStoreMaterialKind)Read(item, "_kind") == Entry(id).Kind);
            var content = (RectTransform)Read(list, "_content");
            var icon = content.GetComponentsInChildren<PopupStoreMaterialItem>(false).Single(item => item.Material.Id == id).GetComponent<Image>();
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
                button = PointerEventData.InputButton.Left, pointerId = 19,
                position = point, pressPosition = point, eligibleForClick = true,
                pointerPress = icon.gameObject, rawPointerPress = icon.gameObject, pointerEnter = icon.gameObject,
                pointerPressRaycast = hit, pointerCurrentRaycast = hit
            };
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler), Is.True);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerUpHandler);
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler), Is.True);
        }
        private void OnFeedback() => FeedbackCount++;
        private void OnCounterChanged(PopupStoreSalesCounter.ChangeReason reason) => CounterEvents++;
        private void OnOrdersRefreshed() => OrderEvents++;
        public void Dispose()
        {
            if (Progress != null) Progress.FeedbackTriggered -= OnFeedback;
            if (Counter != null) Counter.Changed -= OnCounterChanged;
            if (Display != null) Display.Refreshed -= OnOrdersRefreshed;
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private static void AssertResult(PopupStoreExactMatchResult result, PopupStoreExactMatchKind kind, int[] slots, string[] customers)
    {
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Kind, Is.EqualTo(kind));
        CollectionAssert.AreEqual(slots, result.Candidates.Select(candidate => candidate.SlotIndex).ToArray());
        CollectionAssert.AreEqual(customers, result.Candidates.Select(candidate => candidate.CustomerId).ToArray());
    }
    private static string[] AuthoredData(PopupStoreOrderDisplay display)
        => ((PopupStoreOrder[])Read(display, "_sampleOrders")).Select(order => order.CustomerId + "|" + string.Join(",", order.PlushIds) + "|" + order.BoxId + "|" + order.RibbonId).ToArray();
    private static PopupStoreMaterialCatalog Catalog() => AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
    private static PopupStoreMaterialEntry Entry(string id)
        => ((PopupStoreMaterialEntry[])Read(Catalog(), "_entries")).Single(entry => entry.Id == id);
    private static PopupStoreMaterialEntry[] Entries(string csv)
        => string.IsNullOrEmpty(csv) ? Array.Empty<PopupStoreMaterialEntry>() : csv.Split(',').Select(Entry).ToArray();
    private static PopupStoreMaterialEntry MakeEntry(string id, PopupStoreMaterialKind kind, Sprite sprite)
    {
        var entry = new PopupStoreMaterialEntry();
        Write(entry, "_id", id);
        Write(entry, "_kind", kind);
        Write(entry, "_sprite", sprite);
        return entry;
    }
    private static PopupStoreOrder MakeOrder(string customer, string plush, string box, string ribbon)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", new[] { plush });
        Write(order, "_boxId", box);
        Write(order, "_ribbonId", ribbon);
        return order;
    }
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
