using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual popup prefab and pointer handlers, in memory only. No scene, SDK or account setup.</summary>
public sealed class PopupStoreSalesCounterTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";

    [Test]
    public void AuthoredCounterHasFiveIndependentIconsAndOnlyClearEnabled()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var counters = prefab.GetComponentsInChildren<PopupStoreSalesCounter>(true);
        Assert.That(counters, Has.Length.EqualTo(1));
        var counter = counters[0];
        Assert.That(Read(counter, "_catalog"), Is.SameAs(Catalog()));
        var icons = SlotIcons(counter);
        Assert.That(icons, Has.Length.EqualTo(5));
        Assert.That(icons.All(icon => icon != null), Is.True);
        CollectionAssert.AllItemsAreUnique(icons.Select(icon => icon.GetInstanceID()).ToArray());
        foreach (var icon in icons)
        {
            Assert.That(icon.GetComponentInParent<PopupStoreSalesCounter>(true), Is.SameAs(counter));
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.sprite, Is.Null);
        }
        var clear = ClearButton(counter);
        Assert.That(clear, Is.Not.Null);
        Assert.That(clear.interactable, Is.True);
        Assert.That(clear.onClick.GetPersistentEventCount(), Is.Zero);
        var displayOnly = (Button[])Read(prefab.GetComponent<UIPopupStore>(), "_displayOnlyButtons");
        Assert.That(displayOnly, Has.Length.EqualTo(1));
        Assert.That(displayOnly.Contains(clear), Is.False);
        foreach (var button in displayOnly) Assert.That(button.interactable, Is.False);
        foreach (var list in prefab.GetComponentsInChildren<PopupStoreMaterialList>(true))
            Assert.That(Read(list, "_counter"), Is.SameAs(counter));
    }

    [Test]
    public void ActualClicksAppendFiveMixedMaterialsInOrderWithTheirCatalogSprites()
    {
        using (var host = new Host())
        {
            string[] ids = { "STAFF28", "BOX_PINK", "RIBBON_MINT", "STAFF29", "BOX_BLUE" };
            for (int i = 0; i < ids.Length; i++)
            {
                host.Click(ids[i]);
                AssertRendered(host.Counter, ids.Take(i + 1).ToArray());
            }
        }
    }

    [Test]
    public void RepeatedClicksAllowDuplicateMaterialsWithoutReplacingEarlierSlots()
    {
        using (var host = new Host())
        {
            string[] ids = { "STAFF01", "STAFF01", "BOX_PINK", "BOX_PINK", "RIBBON_PINK" };
            foreach (string id in ids) host.Click(id);
            AssertRendered(host.Counter, ids);
            Assert.That(host.Counter.Materials[0], Is.SameAs(host.Counter.Materials[1]));
            Assert.That(host.Counter.Materials[2], Is.SameAs(host.Counter.Materials[3]));
        }
    }

    [Test]
    public void SixthActualClickLeavesAllFiveMaterialsAndSpritesUnchanged()
    {
        using (var host = new Host())
        {
            string[] ids = { "STAFF01", "STAFF02", "BOX_BLUE", "RIBBON_YELLOW", "STAFF29" };
            foreach (string id in ids) host.Click(id);
            var before = host.Counter.Materials.ToArray();
            host.Click("STAFF32");
            AssertRendered(host.Counter, ids);
            for (int i = 0; i < before.Length; i++) Assert.That(host.Counter.Materials[i], Is.SameAs(before[i]));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    public void ActualClearButtonEmptiesOnlyTheCounterAndCanBePressedAgain(int filled)
    {
        using (var host = new Host())
        {
            for (int i = 0; i < filled; i++) host.Click("STAFF28");
            var iconIds = host.Lists.SelectMany(ListIcons).Select(icon => icon.GetInstanceID()).ToArray();
            var positions = host.Lists.Select(list => Content(list).anchoredPosition).ToArray();
            ClearButton(host.Counter).onClick.Invoke();
            ClearButton(host.Counter).onClick.Invoke();
            AssertRendered(host.Counter);
            CollectionAssert.AreEqual(iconIds, host.Lists.SelectMany(ListIcons).Select(icon => icon.GetInstanceID()).ToArray());
            CollectionAssert.AreEqual(positions, host.Lists.Select(list => Content(list).anchoredPosition).ToArray());
            Assert.That(iconIds, Has.Length.EqualTo(38));
        }
    }

    [Test]
    public void ClearThenSelectStartsAgainAtTheFirstSlot()
    {
        using (var host = new Host())
        {
            host.Click("STAFF01");
            host.Click("BOX_MINT");
            ClearButton(host.Counter).onClick.Invoke();
            host.Click("RIBBON_YELLOW");
            host.Click("STAFF32");
            AssertRendered(host.Counter, "RIBBON_YELLOW", "STAFF32");
        }
    }

    [TestCase(PopupStoreMaterialKind.Plush, 32)]
    [TestCase(PopupStoreMaterialKind.Box, 3)]
    [TestCase(PopupStoreMaterialKind.Ribbon, 3)]
    public void EveryApprovedMaterialIsSelectableThroughItsActualIcon(PopupStoreMaterialKind kind, int count)
    {
        using (var host = new Host())
        {
            Assert.That(Catalog().TryGetMaterials(kind, out var entries, out string error), Is.True, error);
            Assert.That(entries, Has.Length.EqualTo(count));
            foreach (var entry in entries)
            {
                host.Click(entry.Id);
                AssertRendered(host.Counter, entry.Id);
                Assert.That(host.Counter.Materials[0], Is.SameAs(entry));
                ClearButton(host.Counter).onClick.Invoke();
            }
            AssertRendered(host.Counter);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DirectCounterApiRejectsNullOrAnEntryFromADifferentCatalog(bool foreignEntry)
    {
        using (var host = new Host())
        {
            var otherCatalog = Object.Instantiate(Catalog());
            try
            {
                Assert.That(otherCatalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var entries, out string error), Is.True, error);
                host.Click("BOX_BLUE");
                Assert.That(host.Counter.TryAdd(foreignEntry ? entries[0] : null), Is.False);
                AssertRendered(host.Counter, "BOX_BLUE");
            }
            finally { Object.DestroyImmediate(otherCatalog); }
        }
    }

    [TestCase("drag")]
    [TestCase("wheel")]
    [TestCase("next-page")]
    [TestCase("previous-page")]
    [TestCase("refresh")]
    [TestCase("hide-reopen")]
    public void ScrollOrRebuildBetweenPressAndReleaseCancelsMaterialSelection(string interruption)
    {
        using (var host = new Host())
        {
            var icon = host.Reveal("STAFF16");
            var list = icon.GetComponent<PopupStoreMaterialItem>().Owner;
            var data = Pointer(icon);
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler), Is.True);
            switch (interruption)
            {
                case "drag": Drag(list, data); break;
                case "wheel":
                    data.scrollDelta = Vector2.down;
                    ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.scrollHandler);
                    data.scrollDelta = Vector2.zero;
                    break;
                case "next-page": PageButton(list, true).onClick.Invoke(); break;
                case "previous-page": PageButton(list, false).onClick.Invoke(); break;
                case "refresh": Assert.That(list.Refresh(), Is.True); break;
                case "hide-reopen": host.HideAndReopenWithLifecycle(); break;
            }
            // Even a late, otherwise eligible click must not resurrect the cancelled press.
            data.dragging = false;
            data.eligibleForClick = true;
            data.position = data.pressPosition;
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler);
            AssertRendered(host.Counter);
            if (interruption == "hide-reopen")
            {
                host.Click("STAFF16");
                AssertRendered(host.Counter, "STAFF16");
            }
        }
    }

    [TestCase(PopupStoreMaterialKind.Box, "BOX_PINK")]
    [TestCase(PopupStoreMaterialKind.Ribbon, "RIBBON_PINK")]
    public void DraggingAShortNonScrollingListCannotSelectAMaterial(PopupStoreMaterialKind kind, string id)
    {
        using (var host = new Host())
        {
            var icon = host.Reveal(id);
            var list = host.List(kind);
            Assert.That(Scroll(list).enabled, Is.False);
            Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(icon.gameObject), Is.SameAs(list.gameObject));
            var data = Pointer(icon);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler);
            Drag(list, data);
            data.dragging = false;
            data.eligibleForClick = true;
            data.position = data.pressPosition;
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler);
            AssertRendered(host.Counter);
        }
    }

    [Test]
    public void AClickFromAnotherPointerCannotCompleteTheOriginalPress()
    {
        using (var host = new Host())
        {
            var icon = host.Reveal("STAFF01");
            var data = Pointer(icon);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.pointerId++;
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler);
            AssertRendered(host.Counter);
        }
    }

    [TestCase(PointerEventData.InputButton.Right)]
    [TestCase(PointerEventData.InputButton.Middle)]
    public void NonLeftPointerInputDoesNotAppendMaterials(PointerEventData.InputButton button)
    {
        using (var host = new Host())
        {
            var icon = host.Reveal("STAFF01");
            var data = Pointer(icon);
            data.button = button;
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler);
            AssertRendered(host.Counter);
        }
    }

    [Test]
    public void ScrollingAndPageButtonsDoNotAlterAnAlreadyFilledCounter()
    {
        using (var host = new Host())
        {
            host.Click("STAFF29");
            host.Click("BOX_BLUE");
            foreach (var list in host.Lists)
            {
                var data = new PointerEventData(null) { scrollDelta = Vector2.down };
                ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.scrollHandler);
                PageButton(list, false).onClick.Invoke();
                PageButton(list, true).onClick.Invoke();
                AssertRendered(host.Counter, "STAFF29", "BOX_BLUE");
            }
        }
    }

    [Test]
    public void DisabledPackAndPauseControlsCannotChangeCounterOrAccountState()
    {
        long money = UserInfo.Money;
        int dia = UserInfo.Dia;
        int visits = UserInfo.TotalCumulativeCustomerCount;
        long context = UserInfo.CustomerEncounterContextGeneration;
        var scoreField = typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(scoreField, Is.Not.Null);
        object score = scoreField.GetValue(null); // Avoid the public Score getter's GameManager access.
        using (var host = new Host())
        {
            host.Click("STAFF28");
            host.Click("BOX_PINK");
            var disabled = (Button[])Read(host.View, "_displayOnlyButtons");
            Assert.That(disabled, Has.Length.EqualTo(1));
            foreach (var button in disabled)
            {
                Assert.That(button.interactable, Is.False);
                button.onClick.Invoke();
                AssertRendered(host.Counter, "STAFF28", "BOX_PINK");
            }
        }
        Assert.That(UserInfo.Money, Is.EqualTo(money));
        Assert.That(UserInfo.Dia, Is.EqualTo(dia));
        Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(visits));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(context));
        Assert.That(scoreField.GetValue(null), Is.EqualTo(score));
    }

    [Test]
    public void SamePopupReentryPreservesOnlyMemoryAndNewInstanceStartsEmptyWithoutAccountChanges()
    {
        long money = UserInfo.Money;
        int dia = UserInfo.Dia;
        int visits = UserInfo.TotalCumulativeCustomerCount;
        long context = UserInfo.CustomerEncounterContextGeneration;
        using (var host = new Host())
        {
            var firstClick = host.Click("STAFF28");
            ExecuteEvents.Execute(firstClick.pointerPress, firstClick, ExecuteEvents.pointerClickHandler);
            AssertRendered(host.Counter, "STAFF28");
            host.Click("RIBBON_PINK");
            for (int i = 0; i < 3; i++)
            {
                host.HideAndReopenWithLifecycle();
                AssertRendered(host.Counter, "STAFF28", "RIBBON_PINK");
            }
            host.Click("BOX_MINT");
            AssertRendered(host.Counter, "STAFF28", "RIBBON_PINK", "BOX_MINT");
            ClearButton(host.Counter).onClick.Invoke();
            AssertRendered(host.Counter);
            host.Click("STAFF29");
            AssertRendered(host.Counter, "STAFF29");
        }
        using (var fresh = new Host()) AssertRendered(fresh.Counter);
        Assert.That(UserInfo.Money, Is.EqualTo(money));
        Assert.That(UserInfo.Dia, Is.EqualTo(dia));
        Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(visits));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(context));
    }

    private static void AssertRendered(PopupStoreSalesCounter counter, params string[] ids)
    {
        Assert.That(counter.Count, Is.EqualTo(ids.Length));
        CollectionAssert.AreEqual(ids, counter.Materials.Select(material => material.Id).ToArray());
        var icons = SlotIcons(counter);
        Assert.That(icons, Has.Length.EqualTo(5));
        for (int i = 0; i < icons.Length; i++)
        {
            Assert.That(icons[i].gameObject.activeSelf, Is.EqualTo(i < ids.Length), "Slot " + i);
            Assert.That(icons[i].raycastTarget, Is.False);
            if (i < ids.Length)
            {
                var entry = Entry(ids[i]);
                Assert.That(counter.Materials[i], Is.SameAs(entry));
                Assert.That(icons[i].sprite, Is.SameAs(entry.Sprite));
                Assert.That(icons[i].preserveAspect, Is.True);
            }
            else Assert.That(icons[i].sprite, Is.Null);
        }
    }

    private static void Drag(PopupStoreMaterialList list, PointerEventData data)
    {
        ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.beginDragHandler);
        data.dragging = true;
        data.position += Vector2.left * 70f;
        ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(list.gameObject, data, ExecuteEvents.endDragHandler);
    }

    private static PointerEventData Pointer(Image icon)
    {
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
        var hit = new RaycastResult { gameObject = icon.gameObject, worldPosition = icon.rectTransform.position };
        return new PointerEventData(null)
        {
            button = PointerEventData.InputButton.Left, pointerId = 11,
            position = point, pressPosition = point, eligibleForClick = true,
            pointerPress = icon.gameObject, rawPointerPress = icon.gameObject, pointerEnter = icon.gameObject,
            pointerPressRaycast = hit, pointerCurrentRaycast = hit
        };
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreMaterialList[] Lists;
        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-counter-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Lists = Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
                Assert.That(Counter, Is.Not.Null);
                View.Init();
                View.Show();
                // Explicit session start keeps these regression fixtures on the real runtime clock path.
                Assert.That(Root.GetComponent<PopupStoreBusinessClock>().TryStartSession(), Is.True);
                Assert.That(Counter.Refresh(), Is.True);
                foreach (var list in Lists) Assert.That(list.Refresh(), Is.True);
                Canvas.ForceUpdateCanvases();
                foreach (var list in Lists) Scroll(list).Rebuild(CanvasUpdate.PostLayout);
                AssertRendered(Counter);
            }
            catch { Dispose(); throw; }
        }
        internal PopupStoreMaterialList List(PopupStoreMaterialKind kind) => Lists.Single(list => (PopupStoreMaterialKind)Read(list, "_kind") == kind);
        internal Image Reveal(string id)
        {
            var list = List(Entry(id).Kind);
            var icon = ListIcons(list).Single(image => image.GetComponent<PopupStoreMaterialItem>().Material.Id == id);
            var scroll = Scroll(list);
            float range = scroll.content.rect.width - scroll.viewport.rect.width;
            if (range > 0)
                scroll.horizontalNormalizedPosition = Mathf.Clamp01((icon.rectTransform.anchoredPosition.x - scroll.viewport.rect.width * .5f) / range);
            scroll.StopMovement();
            scroll.Rebuild(CanvasUpdate.PostLayout);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, null), Is.True, id);
            return icon;
        }
        internal PointerEventData Click(string id)
        {
            var icon = Reveal(id);
            var data = Pointer(icon);
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerDownHandler), Is.True);
            ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerUpHandler);
            Assert.That(ExecuteEvents.Execute(icon.gameObject, data, ExecuteEvents.pointerClickHandler), Is.True);
            return data;
        }
        internal void HideAndReopenWithLifecycle()
        {
            var activeItems = Root.GetComponentsInChildren<PopupStoreMaterialItem>(false);
            View.Hide();
            Assert.That(Root.activeSelf, Is.False);
            // EditMode does not dispatch these non-ExecuteAlways callbacks reliably.
            // Invoke the production callbacks; this does not replace actual Play validation.
            foreach (var item in activeItems) InvokeLifecycle(item, "OnDisable");
            foreach (var list in Lists) InvokeLifecycle(list, "OnDisable");
            InvokeLifecycle(Counter, "OnDisable");
            Assert.That(Counter.IsReady, Is.False);
            foreach (var list in Lists) Assert.That(list.IsReady, Is.False);

            View.Show();
            Assert.That(Root.activeSelf, Is.True);
            InvokeLifecycle(Counter, "OnEnable");
            foreach (var list in Lists) InvokeLifecycle(list, "OnEnable");
            Assert.That(Counter.IsReady, Is.True);
            foreach (var list in Lists) Assert.That(list.IsReady, Is.True);
            Canvas.ForceUpdateCanvases();
            foreach (var list in Lists) Scroll(list).Rebuild(CanvasUpdate.PostLayout);
        }
        private static void InvokeLifecycle(Component component, string methodName)
        {
            var method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, component.GetType().Name + "." + methodName);
            method.Invoke(component, null);
        }
        public void Dispose() { if (Root != null) Object.DestroyImmediate(Root); }
    }

    private static PopupStoreMaterialCatalog Catalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        return catalog;
    }
    private static PopupStoreMaterialEntry Entry(string id)
    {
        var entries = (PopupStoreMaterialEntry[])Read(Catalog(), "_entries");
        return entries.Single(entry => entry.Id == id);
    }
    private static Image[] SlotIcons(PopupStoreSalesCounter counter) => (Image[])Read(counter, "_slotIcons");
    private static Button ClearButton(PopupStoreSalesCounter counter) => (Button)Read(counter, "_clearButton");
    private static ScrollRect Scroll(PopupStoreMaterialList list) => (ScrollRect)Read(list, "_scrollRect");
    private static RectTransform Content(PopupStoreMaterialList list) => (RectTransform)Read(list, "_content");
    private static Image[] ListIcons(PopupStoreMaterialList list) => Content(list).GetComponentsInChildren<Image>(false);
    private static Button PageButton(PopupStoreMaterialList list, bool next) => (Button)Read(list, next ? "_nextButton" : "_previousButton");
    private static object Read(object target, string field)
    {
        var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, target.GetType().Name + "." + field);
        return info.GetValue(target);
    }
}
