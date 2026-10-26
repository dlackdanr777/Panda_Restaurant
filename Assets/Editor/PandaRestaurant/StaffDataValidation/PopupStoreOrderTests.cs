using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Order data and authored display in memory only. No generation, matching, sales, AI or account setup.</summary>
public sealed class PopupStoreOrderTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ValidOrderResolvesPlushesThenBoxAndRibbonToRegisteredMaterials(int plushCount)
    {
        string[] plushes = new[] { "STAFF03", "STAFF05", "STAFF02" }.Take(plushCount).ToArray();
        var order = MakeOrder("CUSTOMER11", plushes, "BOX_MINT", "RIBBON_MINT");
        Assert.That(order.TryResolve(Catalog(), out var resolved, out string error), Is.True, error);
        string[] expected = plushes.Concat(new[] { "BOX_MINT", "RIBBON_MINT" }).ToArray();
        CollectionAssert.AreEqual(expected, resolved.Select(entry => entry.Id).ToArray());
        CollectionAssert.AreEqual(plushes, order.PlushIds.ToArray());
        Assert.That(order.CustomerId, Is.EqualTo("CUSTOMER11"));
        Assert.That(order.BoxId, Is.EqualTo("BOX_MINT"));
        Assert.That(order.RibbonId, Is.EqualTo("RIBBON_MINT"));
        for (int i = 0; i < resolved.Length; i++) Assert.That(resolved[i], Is.SameAs(Entry(expected[i])));
    }

    [Test]
    public void RepeatedPlushIdsArePreservedInOrderWithoutDeduplication()
    {
        var order = MakeOrder("CUSTOMER04", new[] { "STAFF01", "STAFF01", "STAFF29" }, "BOX_BLUE", "RIBBON_PINK");
        Assert.That(order.TryResolve(Catalog(), out var resolved, out string error), Is.True, error);
        CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF01", "STAFF29", "BOX_BLUE", "RIBBON_PINK" },
            resolved.Select(entry => entry.Id).ToArray());
        Assert.That(resolved[0], Is.SameAs(resolved[1]));
    }

    [TestCase("null-plush-list")]
    [TestCase("zero-plushes")]
    [TestCase("four-plushes")]
    [TestCase("unknown-plush")]
    [TestCase("box-as-plush")]
    [TestCase("missing-box")]
    [TestCase("unknown-box")]
    [TestCase("ribbon-as-box")]
    [TestCase("missing-ribbon")]
    [TestCase("unknown-ribbon")]
    [TestCase("box-as-ribbon")]
    public void InvalidOrderFieldsRejectTheWholeOrderInsteadOfReturningPartialMaterials(string defect)
    {
        var order = MakeOrder("CUSTOMER01", new[] { "STAFF01" }, "BOX_PINK", "RIBBON_YELLOW");
        switch (defect)
        {
            case "null-plush-list": Write(order, "_plushIds", null); break;
            case "zero-plushes": Write(order, "_plushIds", Array.Empty<string>()); break;
            case "four-plushes": Write(order, "_plushIds", new[] { "STAFF01", "STAFF02", "STAFF03", "STAFF04" }); break;
            case "unknown-plush": Write(order, "_plushIds", new[] { "STAFF99" }); break;
            case "box-as-plush": Write(order, "_plushIds", new[] { "BOX_PINK" }); break;
            case "missing-box": Write(order, "_boxId", null); break;
            case "unknown-box": Write(order, "_boxId", "BOX_GOLD"); break;
            case "ribbon-as-box": Write(order, "_boxId", "RIBBON_PINK"); break;
            case "missing-ribbon": Write(order, "_ribbonId", null); break;
            case "unknown-ribbon": Write(order, "_ribbonId", "RIBBON_GOLD"); break;
            case "box-as-ribbon": Write(order, "_ribbonId", "BOX_BLUE"); break;
        }
        Assert.That(order.TryResolve(Catalog(), out var resolved, out string error), Is.False, defect);
        Assert.That(resolved, Is.Empty);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void MissingCatalogCannotResolveAnOtherwiseValidOrder()
    {
        var order = MakeOrder("CUSTOMER01", new[] { "STAFF01" }, "BOX_PINK", "RIBBON_PINK");
        Assert.That(order.TryResolve(null, out var resolved, out string error), Is.False);
        Assert.That(resolved, Is.Empty);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void AuthoredPrefabHasThreeIndependentCustomerSlotsAndApprovedSampleOrders()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var displays = prefab.GetComponentsInChildren<PopupStoreOrderDisplay>(true);
        Assert.That(displays, Has.Length.EqualTo(1));
        Assert.That(displays[0].gameObject, Is.SameAs(prefab));
        var display = displays[0];
        Assert.That(Read(display, "_catalog"), Is.SameAs(Catalog()));
        var slots = Slots(display);
        Assert.That(slots, Has.Length.EqualTo(3));
        Assert.That(slots.All(slot => slot.CustomerImage != null && slot.OrderFrame != null), Is.True);
        CollectionAssert.AllItemsAreUnique(slots.Select(slot => slot.CustomerImage.GetInstanceID()).ToArray());
        CollectionAssert.AllItemsAreUnique(slots.Select(slot => slot.OrderFrame.GetInstanceID()).ToArray());
        CollectionAssert.AllItemsAreUnique(slots.SelectMany(slot => slot.MaterialIcons).Select(icon => icon.GetInstanceID()).ToArray());
        float previous = float.NegativeInfinity;
        foreach (var slot in slots)
        {
            Assert.That(slot.CustomerImage.transform.parent, Is.SameAs(slot.OrderFrame.transform.parent));
            Assert.That(slot.MaterialIcons.Count, Is.EqualTo(5));
            Assert.That(slot.CustomerImage.raycastTarget, Is.False);
            Assert.That(slot.CustomerImage.preserveAspect, Is.True);
            Assert.That(slot.OrderFrame.raycastTarget, Is.False);
            Assert.That(AssetDatabase.GetAssetPath(slot.OrderFrame.sprite), Is.EqualTo("Assets/Sprites/UI/UIMiniGame/UI_프레임_제작완료.png"));
            foreach (var icon in slot.MaterialIcons)
            {
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.transform.parent, Is.SameAs(slot.OrderFrame.transform));
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.preserveAspect, Is.True);
            }
            float x = ((RectTransform)slot.OrderFrame.transform.parent).anchoredPosition.x;
            Assert.That(x, Is.GreaterThan(previous));
            previous = x;
        }
        var orders = Orders(display);
        Assert.That(orders, Has.Length.EqualTo(3));
        CollectionAssert.AreEqual(new[] { "CUSTOMER01", "CUSTOMER04", "CUSTOMER11" }, orders.Select(order => order.CustomerId).ToArray());
        AssertResolved(orders[0], "STAFF01", "BOX_PINK", "RIBBON_YELLOW");
        AssertResolved(orders[1], "STAFF01", "STAFF01", "BOX_BLUE", "RIBBON_PINK");
        AssertResolved(orders[2], "STAFF03", "STAFF05", "STAFF02", "BOX_MINT", "RIBBON_MINT");
        var visuals = (PopupStoreOrderCustomerVisual[])Read(display, "_customerVisuals");
        Assert.That(visuals, Has.Length.EqualTo(3));
        AssertPortrait(visuals, "CUSTOMER01", "Customer01_파니.PNG");
        AssertPortrait(visuals, "CUSTOMER04", "Customer04_밀키.PNG");
        AssertPortrait(visuals, "CUSTOMER11", "Customer11_울리.PNG");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ActualDisplayRendersZeroToThreeOrdersAndClearsUnusedSlots(int count)
    {
        using (var host = new Host())
        {
            var orders = host.AuthoredOrders.Take(count).ToArray();
            Write(host.Display, "_sampleOrders", orders);
            Assert.That(host.Display.Refresh(), Is.True);
            AssertDisplay(host.Display, orders);
        }
    }

    [TestCase("null-orders")]
    [TestCase("null-order")]
    [TestCase("four-orders")]
    [TestCase("unknown-customer")]
    [TestCase("invalid-later-order")]
    public void InvalidDisplayInputClearsAllPreviousCustomersAndIconsWithoutPartialOutput(string defect)
    {
        using (var host = new Host())
        {
            AssertDisplay(host.Display, host.AuthoredOrders);
            var orders = host.AuthoredOrders.ToArray();
            switch (defect)
            {
                case "null-orders": orders = null; break;
                case "null-order": orders[1] = null; break;
                case "four-orders": orders = orders.Concat(new[] { orders[0] }).ToArray(); break;
                case "unknown-customer": orders[1] = MakeOrder("CUSTOMER_UNKNOWN", new[] { "STAFF01" }, "BOX_PINK", "RIBBON_PINK"); break;
                case "invalid-later-order": orders[2] = MakeOrder("CUSTOMER11", new[] { "STAFF99" }, "BOX_MINT", "RIBBON_MINT"); break;
            }
            Write(host.Display, "_sampleOrders", orders);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreOrderDisplay: "));
            Assert.That(host.Display.Refresh(), Is.False);
            Assert.That(host.Display.IsReady, Is.False);
            Assert.That(host.Display.DisplayedCount, Is.Zero);
            foreach (var slot in Slots(host.Display)) AssertEmpty(slot);
        }
    }

    [Test]
    public void DamagedInspectorBindingStillClearsThePreviouslyDisplayedImages()
    {
        using (var host = new Host())
        {
            var slots = Slots(host.Display);
            var previousCustomers = slots.Select(slot => slot.CustomerImage).ToArray();
            var previousFrames = slots.Select(slot => slot.OrderFrame).ToArray();
            var previousIcons = slots.SelectMany(slot => slot.MaterialIcons).ToArray();
            Write(slots[1], "_materialIcons", null);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreOrderDisplay: "));
            Assert.That(host.Display.Refresh(), Is.False);
            Assert.That(host.Display.IsReady, Is.False);
            Assert.That(host.Display.DisplayedCount, Is.Zero);
            foreach (var image in previousCustomers.Concat(previousIcons))
            {
                Assert.That(image.gameObject.activeSelf, Is.False);
                Assert.That(image.sprite, Is.Null);
            }
            foreach (var frame in previousFrames) Assert.That(frame.gameObject.activeSelf, Is.False);
        }
    }

    [Test]
    public void FiveIconsFitANarrowFrameWithPositiveSizesNoOverlapAndPreservedAspect()
    {
        using (var host = new Host())
        {
            foreach (var slot in Slots(host.Display)) slot.OrderFrame.rectTransform.sizeDelta = new Vector2(180, 80);
            Assert.That(host.Display.Refresh(), Is.True);
            AssertDisplay(host.Display, host.AuthoredOrders);
            Vector2 padding = (Vector2)Read(host.Display, "_padding");
            foreach (var slot in Slots(host.Display))
            {
                var frame = slot.OrderFrame.rectTransform;
                float previousRight = frame.rect.xMin + padding.x;
                foreach (var icon in slot.MaterialIcons.Where(icon => icon.gameObject.activeSelf))
                {
                    Assert.That(icon.rectTransform.rect.width, Is.GreaterThan(0));
                    Assert.That(icon.rectTransform.rect.height, Is.GreaterThan(0));
                    var worldCorners = new Vector3[4];
                    icon.rectTransform.GetWorldCorners(worldCorners);
                    var local = worldCorners.Select(frame.InverseTransformPoint).ToArray();
                    float left = local.Min(point => point.x), right = local.Max(point => point.x);
                    Assert.That(left, Is.GreaterThanOrEqualTo(previousRight - 0.01f));
                    Assert.That(right, Is.LessThanOrEqualTo(frame.rect.xMax - padding.x + 0.01f));
                    Assert.That(local.Min(point => point.y), Is.GreaterThanOrEqualTo(frame.rect.yMin + padding.y - 0.01f));
                    Assert.That(local.Max(point => point.y), Is.LessThanOrEqualTo(frame.rect.yMax - padding.y + 0.01f));
                    previousRight = right;
                }
            }
        }
    }

    [Test]
    public void RepeatedRefreshAndActualEnableCallbackReuseTheAuthoredDisplayObjects()
    {
        using (var host = new Host())
        {
            var before = Slots(host.Display).SelectMany(slot => slot.MaterialIcons).Select(icon => icon.GetInstanceID()).ToArray();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(host.Display.Refresh(), Is.True);
                host.View.Hide();
                Assert.That(host.Root.activeSelf, Is.False);
                // Dispatch the real callbacks; EditMode is not an actual PlayerLoop verification.
                InvokeLifecycle(host.Display, "OnDisable");
                Assert.That(host.Display.IsReady, Is.False);
                Assert.That(host.Display.DisplayedCount, Is.Zero);
                foreach (var slot in Slots(host.Display)) AssertEmpty(slot);
                host.View.Show();
                InvokeLifecycle(host.Display, "OnEnable");
                AssertDisplay(host.Display, host.AuthoredOrders);
            }
            CollectionAssert.AreEqual(before, Slots(host.Display).SelectMany(slot => slot.MaterialIcons).Select(icon => icon.GetInstanceID()).ToArray());
        }
    }

    [Test]
    public void OrderDisplayDoesNotConsumeCounterMaterialsChangeListsOrWriteAccountState()
    {
        long money = UserInfo.Money;
        int dia = UserInfo.Dia;
        int visits = UserInfo.TotalCumulativeCustomerCount;
        long context = UserInfo.CustomerEncounterContextGeneration;
        using (var host = new Host())
        {
            Assert.That(host.Counter.TryAdd(Entry("STAFF28")), Is.True);
            Assert.That(host.Counter.TryAdd(Entry("BOX_BLUE")), Is.True);
            var originalMaterials = host.Counter.Materials.ToArray();
            var listImages = host.Lists.SelectMany(list => ((RectTransform)Read(list, "_content")).GetComponentsInChildren<Image>(false)).ToArray();
            Assert.That(listImages, Has.Length.EqualTo(38));
            var originalSprites = listImages.Select(image => image.sprite).ToArray();
            var originalPositions = host.Lists.Select(list => ((RectTransform)Read(list, "_content")).anchoredPosition).ToArray();
            Write(host.Display, "_sampleOrders", Array.Empty<PopupStoreOrder>());
            Assert.That(host.Display.Refresh(), Is.True);
            Write(host.Display, "_sampleOrders", host.AuthoredOrders);
            Assert.That(host.Display.Refresh(), Is.True);
            AssertDisplay(host.Display, host.AuthoredOrders);
            CollectionAssert.AreEqual(originalMaterials, host.Counter.Materials.ToArray());
            CollectionAssert.AreEqual(originalSprites, listImages.Select(image => image.sprite).ToArray());
            CollectionAssert.AreEqual(originalPositions, host.Lists.Select(list => ((RectTransform)Read(list, "_content")).anchoredPosition).ToArray());
        }
        Assert.That(UserInfo.Money, Is.EqualTo(money));
        Assert.That(UserInfo.Dia, Is.EqualTo(dia));
        Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(visits));
        Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(context));
    }

    private static void AssertDisplay(PopupStoreOrderDisplay display, PopupStoreOrder[] orders)
    {
        Assert.That(display.IsReady, Is.True);
        Assert.That(display.DisplayedCount, Is.EqualTo(orders.Length));
        var slots = Slots(display);
        var visuals = (PopupStoreOrderCustomerVisual[])Read(display, "_customerVisuals");
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (i >= orders.Length) { AssertEmpty(slot); continue; }
            Assert.That(slot.CustomerImage.gameObject.activeSelf, Is.True);
            Assert.That(slot.OrderFrame.gameObject.activeSelf, Is.True);
            Assert.That(slot.CustomerImage.sprite, Is.SameAs(visuals.Single(visual => visual.CustomerId == orders[i].CustomerId).Sprite));
            Assert.That(slot.CustomerImage.preserveAspect, Is.True);
            Assert.That(orders[i].TryResolve(Catalog(), out var materials, out string error), Is.True, error);
            for (int j = 0; j < slot.MaterialIcons.Count; j++)
            {
                var icon = slot.MaterialIcons[j];
                Assert.That(icon.gameObject.activeSelf, Is.EqualTo(j < materials.Length));
                Assert.That(icon.raycastTarget, Is.False);
                if (j < materials.Length)
                {
                    Assert.That(icon.sprite, Is.SameAs(materials[j].Sprite));
                    Assert.That(icon.preserveAspect, Is.True);
                }
                else Assert.That(icon.sprite, Is.Null);
            }
        }
    }

    private static void AssertEmpty(PopupStoreOrderSlot slot)
    {
        Assert.That(slot.CustomerImage.gameObject.activeSelf, Is.False);
        Assert.That(slot.CustomerImage.sprite, Is.Null);
        Assert.That(slot.OrderFrame.gameObject.activeSelf, Is.False);
        foreach (var icon in slot.MaterialIcons)
        {
            Assert.That(icon.gameObject.activeSelf, Is.False);
            Assert.That(icon.sprite, Is.Null);
        }
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreOrderDisplay Display;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreMaterialList[] Lists;
        internal readonly PopupStoreOrder[] AuthoredOrders;
        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-order-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Display = Root.GetComponent<PopupStoreOrderDisplay>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Lists = Root.GetComponentsInChildren<PopupStoreMaterialList>(true);
                Assert.That(Display, Is.Not.Null);
                AuthoredOrders = Orders(Display).ToArray();
                View.Init();
                View.Show();
                // Explicit session start keeps these regression fixtures on the real runtime clock path.
                Assert.That(Root.GetComponent<PopupStoreBusinessClock>().TryStartSession(), Is.True);
                Assert.That(Counter.Refresh(), Is.True);
                foreach (var list in Lists) Assert.That(list.Refresh(), Is.True);
                InvokeLifecycle(Display, "OnEnable");
                Canvas.ForceUpdateCanvases();
                AssertDisplay(Display, AuthoredOrders);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { if (Root != null) Object.DestroyImmediate(Root); }
    }

    private static PopupStoreOrder MakeOrder(string customerId, string[] plushIds, string boxId, string ribbonId)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customerId);
        Write(order, "_plushIds", plushIds);
        Write(order, "_boxId", boxId);
        Write(order, "_ribbonId", ribbonId);
        return order;
    }
    private static void AssertResolved(PopupStoreOrder order, params string[] expected)
    {
        Assert.That(order.TryResolve(Catalog(), out var entries, out string error), Is.True, error);
        CollectionAssert.AreEqual(expected, entries.Select(entry => entry.Id).ToArray());
    }
    private static void AssertPortrait(PopupStoreOrderCustomerVisual[] visuals, string id, string file)
    {
        var visual = visuals.Single(item => item.CustomerId == id);
        Assert.That(visual.Sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(visual.Sprite),
            Is.EqualTo("Assets/Resources/CustomerData/Sprites/NormalCustomer/Sprite/" + file));
    }
    private static PopupStoreMaterialCatalog Catalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        return catalog;
    }
    private static PopupStoreMaterialEntry Entry(string id) => ((PopupStoreMaterialEntry[])Read(Catalog(), "_entries")).Single(entry => entry.Id == id);
    private static PopupStoreOrderSlot[] Slots(PopupStoreOrderDisplay display) => (PopupStoreOrderSlot[])Read(display, "_slots");
    private static PopupStoreOrder[] Orders(PopupStoreOrderDisplay display) => (PopupStoreOrder[])Read(display, "_sampleOrders");
    private static void InvokeLifecycle(Component component, string name)
    {
        var method = component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, component.GetType().Name + "." + name);
        method.Invoke(component, null);
    }
    private static object Read(object target, string field) => Field(target, field).GetValue(target);
    private static void Write(object target, string field, object value) => Field(target, field).SetValue(target, value);
    private static FieldInfo Field(object target, string field)
    {
        var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, target.GetType().Name + "." + field);
        return info;
    }
}
