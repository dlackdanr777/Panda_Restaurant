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

/// <summary>Prefix calculation and actual prefab input/events in memory. Feedback counts are requests, not audio playback.</summary>
public sealed class PopupStoreOrderMatchTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";

    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "", 0)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01", 1)]
    [TestCase("STAFF01,STAFF02,BOX_PINK,RIBBON_YELLOW", "STAFF01,STAFF02", 2)]
    [TestCase("STAFF01,STAFF02,STAFF03,BOX_PINK,RIBBON_YELLOW", "STAFF01,STAFF02,STAFF03", 3)]
    [TestCase("STAFF01,STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,STAFF01", 2)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF02,BOX_PINK,RIBBON_YELLOW", 0)]
    [TestCase("STAFF01,STAFF02,BOX_PINK,RIBBON_YELLOW", "STAFF01,STAFF03,BOX_PINK,RIBBON_YELLOW", 1)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_BLUE,RIBBON_YELLOW", 1)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,RIBBON_PINK,RIBBON_YELLOW", 1)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK,RIBBON_MINT", 2)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK,BOX_MINT", 2)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK,RIBBON_YELLOW", 3)]
    [TestCase("STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK", "STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK", 4)]
    [TestCase("STAFF03,STAFF05,STAFF02,BOX_MINT,RIBBON_MINT", "STAFF03,STAFF05,STAFF02,BOX_MINT,RIBBON_MINT", 5)]
    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", "STAFF01,BOX_PINK,RIBBON_YELLOW,STAFF32,BOX_BLUE", 3)]
    public void PrefixStopsAtTheFirstMismatchAndNeverCountsBeyondTheOrder(string orderIds, string counterIds, int expected)
    {
        var order = Entries(orderIds);
        var counter = Entries(counterIds);
        var beforeOrder = order.ToArray();
        var beforeCounter = counter.ToArray();
        Assert.That(PopupStoreOrderMatch.CountPrefix(order, counter), Is.EqualTo(expected));
        CollectionAssert.AreEqual(beforeOrder, order);
        CollectionAssert.AreEqual(beforeCounter, counter);
    }

    [TestCase("order")]
    [TestCase("counter")]
    [TestCase("both")]
    public void NullAndEmptyInputsHaveNoProgress(string missing)
    {
        var order = missing == "order" || missing == "both" ? null : Entries("STAFF01");
        var counter = missing == "counter" || missing == "both" ? null : Entries("STAFF01");
        Assert.That(PopupStoreOrderMatch.CountPrefix(order, counter), Is.Zero);
        Assert.That(PopupStoreOrderMatch.CountPrefix(Array.Empty<PopupStoreMaterialEntry>(), Entries("STAFF01")), Is.Zero);
    }

    [Test]
    public void NullEntriesBreakThePrefixRatherThanMatchingLaterMaterials()
    {
        var order = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        var counter = new[] { Entry("STAFF01"), null, Entry("RIBBON_YELLOW") };
        Assert.That(PopupStoreOrderMatch.CountPrefix(order, counter), Is.EqualTo(1));
        order[1] = null;
        Assert.That(PopupStoreOrderMatch.CountPrefix(order, counter), Is.EqualTo(1));
    }

    [Test]
    public void MatchingRequiresBothMaterialKindAndOrdinalId()
    {
        var expected = new[] { Entry("STAFF01") };
        var wrongKind = MakeEntry("STAFF01", PopupStoreMaterialKind.Box, Entry("STAFF01").Sprite);
        var wrongCase = MakeEntry("staff01", PopupStoreMaterialKind.Plush, Entry("STAFF01").Sprite);
        Assert.That(PopupStoreOrderMatch.CountPrefix(expected, new[] { wrongKind }), Is.Zero);
        Assert.That(PopupStoreOrderMatch.CountPrefix(expected, new[] { wrongCase }), Is.Zero);
    }

    [Test]
    public void MatchingUsesStableIdsRatherThanSpriteOrEntryObjectIdentity()
    {
        var equivalent = MakeEntry("STAFF01", PopupStoreMaterialKind.Plush, Entry("STAFF32").Sprite);
        Assert.That(equivalent, Is.Not.SameAs(Entry("STAFF01")));
        Assert.That(equivalent.Sprite, Is.Not.SameAs(Entry("STAFF01").Sprite));
        Assert.That(PopupStoreOrderMatch.CountPrefix(Entries("STAFF01"), new[] { equivalent }), Is.EqualTo(1));
    }

    [Test]
    public void AuthoredProgressHasFifteenIndependentNonBlockingCirclesUnderTheirOrderIcons()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var components = prefab.GetComponentsInChildren<PopupStoreOrderProgress>(true);
        Assert.That(components, Has.Length.EqualTo(1));
        var progress = components[0];
        Assert.That(progress.gameObject, Is.SameAs(prefab));
        var orders = prefab.GetComponent<PopupStoreOrderDisplay>();
        Assert.That(Read(progress, "_orders"), Is.SameAs(orders));
        Assert.That(Read(progress, "_counter"), Is.SameAs(prefab.GetComponentInChildren<PopupStoreSalesCounter>(true)));
        var circles = Circles(progress);
        Assert.That(circles, Has.Length.EqualTo(3));
        CollectionAssert.AllItemsAreUnique(circles.SelectMany(group => group).Select(image => image.GetInstanceID()).ToArray());
        for (int slot = 0; slot < 3; slot++)
        {
            Assert.That(circles[slot], Has.Length.EqualTo(5));
            for (int item = 0; item < 5; item++)
            {
                var circle = circles[slot][item];
                Assert.That(circle, Is.Not.Null);
                Assert.That(circle.sprite, Is.Not.Null);
                Assert.That(circle.transform.parent, Is.SameAs(orders.Slots[slot].MaterialIcons[item].transform));
                Assert.That(circle.raycastTarget, Is.False);
                Assert.That(circle.GetComponent<Selectable>(), Is.Null);
                Assert.That(circle.rectTransform.rect.width, Is.GreaterThan(0));
                Assert.That(circle.rectTransform.rect.height, Is.GreaterThan(0));
            }
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ZeroToThreeDisplayedCustomersCompareTheSameActualCounterIndependently(int count)
    {
        using (var host = new Host())
        {
            host.SetOrders(host.AuthoredOrders.Take(count).ToArray());
            AssertProgress(host, 0, 0, 0);
            host.Click("STAFF01");
            AssertProgress(host, count > 0 ? 1 : 0, count > 1 ? 1 : 0, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(count > 0 ? 1 : 0), "A shared prefix requests at most one feedback per add.");
            Assert.That(host.Counter.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void RepeatedPlushClicksExtendOnlyTheMatchingCustomerAndDoNotDuplicateFeedback()
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            AssertProgress(host, 1, 1, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
            host.Click("STAFF01");
            AssertProgress(host, 1, 2, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(2));
            host.Click("BOX_BLUE");
            AssertProgress(host, 1, 3, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(3));
            host.Click("RIBBON_PINK");
            AssertProgress(host, 1, 4, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(4));
            AssertCounter(host, "STAFF01", "STAFF01", "BOX_BLUE", "RIBBON_PINK");
        }
    }

    [Test]
    public void ActualWrongMiddleClickAllowsLaterMaterialsButNeverMarksOrSoundsThem()
    {
        using (var host = new Host())
        {
            host.SetOrders(MakeOrder("CUSTOMER01", new[] { "STAFF01", "STAFF02" }, "BOX_PINK", "RIBBON_YELLOW"));
            host.Click("STAFF01");
            host.Click("STAFF03");
            host.Click("BOX_PINK");
            host.Click("RIBBON_YELLOW");
            AssertProgress(host, 1, 0, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
            AssertCounter(host, "STAFF01", "STAFF03", "BOX_PINK", "RIBBON_YELLOW");
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void FullyMatchedThreeFourOrFiveItemOrdersRemainOnlyHintsWithoutPackingOrConsumption(int authoredIndex)
    {
        long money = UserInfo.Money;
        int dia = UserInfo.Dia;
        int visits = UserInfo.TotalCumulativeCustomerCount;
        long context = UserInfo.CustomerEncounterContextGeneration;
        object score = StaticScore();
        using (var host = new Host())
        {
            host.SetOrders(host.AuthoredOrders[authoredIndex]);
            var expected = host.Display.GetDisplayedMaterials(0).Select(entry => entry.Id).ToArray();
            foreach (string id in expected) host.Click(id);
            AssertProgress(host, expected.Length, 0, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(expected.Length));
            var expectedCounter = expected.ToList();
            while (expectedCounter.Count < 5) { host.Click("STAFF32"); expectedCounter.Add("STAFF32"); }
            AssertCounter(host, expectedCounter.ToArray());
            AssertProgress(host, expected.Length, 0, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(expected.Length), "Extra materials are not new order progress.");
            foreach (var button in (Button[])Read(host.View, "_displayOnlyButtons"))
            {
                Assert.That(button.interactable, Is.False);
                button.onClick.Invoke();
            }
            AssertCounter(host, expectedCounter.ToArray());
            Assert.That(host.Display.DisplayedCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(expected, host.Display.GetDisplayedMaterials(0).Select(entry => entry.Id).ToArray());
        }
        Assert.That(UserInfo.Money, Is.EqualTo(money));
        Assert.That(UserInfo.Dia, Is.EqualTo(dia));
        Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(visits));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(context));
        Assert.That(StaticScore(), Is.EqualTo(score));
    }

    [Test]
    public void ActualClearButtonRemovesAllProgressSilentlyAndPreservesOrders()
    {
        using (var host = new Host())
        {
            var originalOrders = Enumerable.Range(0, 3).Select(index => host.Display.GetDisplayedMaterials(index).ToArray()).ToArray();
            host.Click("STAFF01");
            host.Click("STAFF01");
            ((Button)Read(host.Counter, "_clearButton")).onClick.Invoke();
            AssertCounter(host);
            AssertProgress(host, 0, 0, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(2));
            for (int i = 0; i < 3; i++) CollectionAssert.AreEqual(originalOrders[i], host.Display.GetDisplayedMaterials(i).ToArray());
            ((Button)Read(host.Counter, "_clearButton")).onClick.Invoke();
            Assert.That(host.FeedbackCount, Is.EqualTo(2));
            host.Click("STAFF01");
            AssertProgress(host, 1, 1, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void ReplacingDisplayedOrdersRecomputesSilentlyWithoutChangingTheCounter()
    {
        using (var host = new Host())
        {
            host.Click("STAFF03");
            AssertProgress(host, 0, 0, 1);
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
            host.SetOrders(MakeOrder("CUSTOMER01", new[] { "STAFF03" }, "BOX_PINK", "RIBBON_YELLOW"),
                MakeOrder("CUSTOMER04", new[] { "STAFF03", "STAFF03" }, "BOX_BLUE", "RIBBON_PINK"));
            AssertProgress(host, 1, 1, 0);
            AssertCounter(host, "STAFF03");
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
            host.SetOrders();
            AssertProgress(host, 0, 0, 0);
            AssertCounter(host, "STAFF03");
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void RepeatedCounterOrderAndProgressRefreshAreSilentAndDoNotMultiplyListeners()
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            for (int i = 0; i < 4; i++)
            {
                Assert.That(host.Counter.Refresh(), Is.True);
                Assert.That(host.Display.Refresh(), Is.True);
                Assert.That(host.Progress.Refresh(), Is.True);
                AssertProgress(host, 1, 1, 0);
                Assert.That(host.FeedbackCount, Is.EqualTo(1));
            }
            host.Click("STAFF01");
            AssertProgress(host, 1, 2, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void InvalidOrderRefreshClearsTheDisplayedSnapshotAndAllPreviousProgressSilently()
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            Write(host.Display, "_sampleOrders", new[] { MakeOrder("CUSTOMER01", new[] { "STAFF99" }, "BOX_PINK", "RIBBON_YELLOW") });
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreOrderDisplay: "));
            Assert.That(host.Display.Refresh(), Is.False);
            Assert.That(host.Display.IsReady, Is.False);
            for (int i = 0; i < 3; i++) Assert.That(host.Display.GetDisplayedMaterials(i), Is.Empty);
            AssertProgress(host, 0, 0, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
            AssertCounter(host, "STAFF01");
            host.SetOrders(host.AuthoredOrders);
            AssertProgress(host, 1, 1, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ActualViewHideReentryRestoresMemoryProgressSilentlyWithoutDuplicateSubscriptions()
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            var circles = Circles(host.Progress).SelectMany(group => group).Select(image => image.GetInstanceID()).ToArray();
            for (int i = 0; i < 3; i++)
            {
                host.HideAndReopenWithLifecycle();
                AssertProgress(host, 1, 1, 0);
                AssertCounter(host, "STAFF01");
                Assert.That(host.FeedbackCount, Is.EqualTo(1));
            }
            host.Click("STAFF01");
            AssertProgress(host, 1, 2, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(2));
            CollectionAssert.AreEqual(circles, Circles(host.Progress).SelectMany(group => group).Select(image => image.GetInstanceID()).ToArray());
        }
    }

    [TestCase("drag")]
    [TestCase("wheel")]
    [TestCase("page")]
    public void ActualScrollInputCancelsThePressedMaterialAndCannotAdvanceProgressOrFeedback(string action)
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            var icon = host.Reveal("STAFF01");
            var list = icon.GetComponent<PopupStoreMaterialItem>().Owner;
            var data = Pointer(icon);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler);
            switch (action)
            {
                case "drag":
                    ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.beginDragHandler);
                    data.dragging = true;
                    data.position += Vector2.left * 70;
                    ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.dragHandler);
                    ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.endDragHandler);
                    break;
                case "wheel":
                    data.scrollDelta = Vector2.down;
                    ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.scrollHandler);
                    break;
                case "page": ((Button)Read(list, "_nextButton")).onClick.Invoke(); break;
            }
            data.dragging = false;
            data.eligibleForClick = true;
            data.scrollDelta = Vector2.zero;
            data.position = data.pressPosition;
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler);
            AssertCounter(host, "STAFF01");
            AssertProgress(host, 1, 1, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void FailedAddsAndSixthClickEmitNoAddedEventOrFeedbackAndKeepTheFiveMaterials()
    {
        using (var host = new Host())
        {
            var reasons = new List<PopupStoreSalesCounter.ChangeReason>();
            host.Counter.Changed += reasons.Add;
            host.SetOrders(host.AuthoredOrders[2]);
            string[] ids = { "STAFF03", "STAFF05", "STAFF02", "BOX_MINT", "RIBBON_MINT" };
            foreach (string id in ids) host.Click(id);
            Assert.That(reasons.Count(reason => reason == PopupStoreSalesCounter.ChangeReason.Added), Is.EqualTo(5));
            Assert.That(host.Counter.TryAdd(null), Is.False);
            host.Click("STAFF01");
            Assert.That(reasons.Count, Is.EqualTo(5));
            Assert.That(host.FeedbackCount, Is.EqualTo(5));
            AssertProgress(host, 5, 0, 0);
            AssertCounter(host, ids);
        }
    }

    [Test]
    public void ProgressReadsOnlyTheLastValidatedDisplayedOrderSnapshot()
    {
        using (var host = new Host())
        {
            var snapshot = host.Display.GetDisplayedMaterials(0);
            var snapshotIds = snapshot.Select(entry => entry.Id).ToArray();
            Write(host.Display, "_sampleOrders", new[] { MakeOrder("CUSTOMER01", new[] { "STAFF03" }, "BOX_PINK", "RIBBON_YELLOW") });
            host.Click("STAFF01");
            AssertProgress(host, 1, 1, 0);
            CollectionAssert.AreEqual(snapshotIds, host.Display.GetDisplayedMaterials(0).Select(entry => entry.Id).ToArray());
            Assert.That(host.Display.GetDisplayedMaterials(-1), Is.Empty);
            Assert.That(host.Display.GetDisplayedMaterials(3), Is.Empty);
            var writableView = snapshot as IList<PopupStoreMaterialEntry>;
            Assert.That(writableView, Is.Not.Null);
            Assert.That(writableView.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => writableView[0] = Entry("STAFF03"));
            Assert.That(host.Display.Refresh(), Is.True);
            AssertProgress(host, 0, 0, 0);
            Assert.That(host.FeedbackCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(snapshotIds, snapshot.Select(entry => entry.Id).ToArray());
            AssertCounter(host, "STAFF01");
        }
    }

    private static void AssertProgress(Host host, params int[] expected)
    {
        Assert.That(expected, Has.Length.EqualTo(3));
        var circles = Circles(host.Progress);
        for (int slot = 0; slot < 3; slot++)
        {
            Assert.That(host.Progress.GetMatchedCount(slot), Is.EqualTo(expected[slot]), "Customer " + slot);
            for (int item = 0; item < 5; item++)
            {
                var circle = circles[slot][item];
                Assert.That(circle.gameObject.activeSelf, Is.EqualTo(item < expected[slot]), "Circle " + slot + ":" + item);
                Assert.That(circle.raycastTarget, Is.False);
                if (item < expected[slot]) Assert.That(circle.gameObject.activeInHierarchy, Is.True);
            }
        }
    }

    private static void AssertCounter(Host host, params string[] ids)
    {
        Assert.That(host.Counter.Count, Is.EqualTo(ids.Length));
        CollectionAssert.AreEqual(ids, host.Counter.Materials.Select(entry => entry.Id).ToArray());
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
        internal int FeedbackCount;

        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-order-match-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Display = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Lists = Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
                Assert.That(Progress, Is.Not.Null);
                AuthoredOrders = ((PopupStoreOrder[])Read(Display, "_sampleOrders")).ToArray();
                Progress.FeedbackTriggered += OnFeedback;
                View.Init();
                View.Show();
                // Explicit session start keeps these regression fixtures on the real runtime clock path.
                Assert.That(Root.GetComponent<PopupStoreBusinessClock>().TryStartSession(), Is.True);
                Assert.That(Counter.Refresh(), Is.True);
                foreach (var list in Lists) Assert.That(list.Refresh(), Is.True);
                Assert.That(Display.Refresh(), Is.True);
                Assert.That(Progress.Refresh(), Is.True);
                Rebuild();
                Assert.That(FeedbackCount, Is.Zero);
                AssertCounter(this);
                AssertProgress(this, 0, 0, 0);
            }
            catch { Dispose(); throw; }
        }

        internal void SetOrders(params PopupStoreOrder[] orders)
        {
            Write(Display, "_sampleOrders", orders);
            Assert.That(Display.Refresh(), Is.True);
        }

        internal Image Reveal(string id)
        {
            var list = Lists.Single(item => (PopupStoreMaterialKind)Read(item, "_kind") == Entry(id).Kind);
            var content = (RectTransform)Read(list, "_content");
            var icon = content.GetComponentsInChildren<PopupStoreMaterialItem>(false).Single(item => item.Material.Id == id).GetComponent<Image>();
            var scroll = (ScrollRect)Read(list, "_scrollRect");
            float range = scroll.content.rect.width - scroll.viewport.rect.width;
            if (range > 0)
                scroll.horizontalNormalizedPosition = Mathf.Clamp01((icon.rectTransform.anchoredPosition.x - scroll.viewport.rect.width * .5f) / range);
            scroll.StopMovement();
            scroll.Rebuild(CanvasUpdate.PostLayout);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, null), Is.True, id);
            return icon;
        }

        internal void Click(string id)
        {
            var icon = Reveal(id);
            var data = Pointer(icon);
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler), Is.True);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerUpHandler);
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler), Is.True);
        }

        internal void HideAndReopenWithLifecycle()
        {
            var activeItems = Root.GetComponentsInChildren<PopupStoreMaterialItem>(false);
            View.Hide();
            Assert.That(Root.activeSelf, Is.False);
            // EditMode does not reliably dispatch non-ExecuteAlways callbacks; use the real methods, not substitute state.
            InvokeLifecycle(Progress, "OnDisable");
            foreach (var item in activeItems) InvokeLifecycle(item, "OnDisable");
            foreach (var list in Lists) InvokeLifecycle(list, "OnDisable");
            InvokeLifecycle(Counter, "OnDisable");
            InvokeLifecycle(Display, "OnDisable");
            AssertProgress(this, 0, 0, 0);
            Assert.That(Progress.IsReady, Is.False);
            Assert.That(Counter.IsReady, Is.False);
            Assert.That(Display.IsReady, Is.False);
            View.Show();
            InvokeLifecycle(Counter, "OnEnable");
            foreach (var list in Lists) InvokeLifecycle(list, "OnEnable");
            InvokeLifecycle(Display, "OnEnable");
            InvokeLifecycle(Progress, "OnEnable");
            Assert.That(Progress.IsReady, Is.True);
            Rebuild();
        }

        private void Rebuild()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var list in Lists) ((ScrollRect)Read(list, "_scrollRect")).Rebuild(CanvasUpdate.PostLayout);
        }
        private void OnFeedback() => FeedbackCount++;
        public void Dispose()
        {
            if (Progress != null) Progress.FeedbackTriggered -= OnFeedback;
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private static PointerEventData Pointer(Image icon)
    {
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
        var hit = new RaycastResult { gameObject = icon.gameObject, worldPosition = icon.rectTransform.position };
        return new PointerEventData(null)
        {
            button = PointerEventData.InputButton.Left, pointerId = 17,
            position = point, pressPosition = point, eligibleForClick = true,
            pointerPress = icon.gameObject, rawPointerPress = icon.gameObject, pointerEnter = icon.gameObject,
            pointerPressRaycast = hit, pointerCurrentRaycast = hit
        };
    }
    private static Image[][] Circles(PopupStoreOrderProgress progress)
        => ((Array)Read(progress, "_slots")).Cast<object>().Select(slot => (Image[])Read(slot, "_circles")).ToArray();
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
    private static PopupStoreOrder MakeOrder(string customer, string[] plushes, string box, string ribbon)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", plushes);
        Write(order, "_boxId", box);
        Write(order, "_ribbonId", ribbon);
        return order;
    }
    private static object StaticScore()
    {
        var field = typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return field.GetValue(null); // The public Score getter accesses GameManager.
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
