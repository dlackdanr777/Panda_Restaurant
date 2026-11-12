#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DEV-UI-02 uses authored child TMP separators, not a new runtime observer. These tests exercise the
/// original order/packing/arrival lifecycle and measure the actual prefab at three landscape sizes.
/// Memory-only Editor fixtures are not evidence of an actual GameView or a Player build.
/// </summary>
public sealed class PopupStoreMainVisualTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string SeparatorName = "Order Separator";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GachaCollectionPreviewSceneWitness _workspace;
    private UnityEngine.Random.State _random;

    [SetUp]
    public void ObserveTheExistingEditorWorkspace()
    {
        _workspace = GachaCollectionPreviewSceneWitness.Capture();
        _random = UnityEngine.Random.state;
    }

    [TearDown]
    public void MemoryVisualChecksPreserveWorkspaceAndDoNotDrawRewards()
    {
        _workspace.AssertUnchanged("popup main visual checks");
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void AuthoredPlusSignsAreTwelveNonInteractiveFontChildrenOfTheExistingOrderIcons()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var display = root.GetComponent<PopupStoreOrderDisplay>();
        var font = Read<TMP_Text>(root.GetComponent<PopupStoreScore>(), "_scoreText").font;
        var pluses = root.GetComponentsInChildren<TMP_Text>(true).Where(text => text.name == SeparatorName).ToArray();
        Assert.That(pluses, Has.Length.EqualTo(12));
        CollectionAssert.AllItemsAreUnique(pluses.Select(text => text.GetInstanceID()).ToArray());
        foreach (var slot in display.Slots)
        {
            Assert.That(slot.MaterialIcons.Count, Is.EqualTo(5));
            for (int i = 0; i < slot.MaterialIcons.Count; i++)
            {
                var labels = slot.MaterialIcons[i].GetComponentsInChildren<TMP_Text>(true)
                    .Where(text => text.name == SeparatorName).ToArray();
                Assert.That(labels.Length, Is.EqualTo(i == 0 ? 0 : 1));
                if (i == 0) continue;
                var label = labels[0];
                Assert.That(label.transform.parent, Is.SameAs(slot.MaterialIcons[i].transform));
                Assert.That(label.text, Is.EqualTo("+"));
                Assert.That(label.font, Is.SameAs(font));
                Assert.That(label.raycastTarget, Is.False);
                Assert.That(label.GetComponent<Selectable>(), Is.Null);
                Assert.That(label.enableAutoSizing, Is.True);
            }
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    [TestCase(2340, 1080)]
    public void ThreeFourAndFiveMaterialOrdersAreLargerAndTheirPlusGlyphsFitOnlyBetweenMaterials(int width, int height)
    {
        using (var h = new Host(width, height, true))
        {
            for (int slotIndex = 0; slotIndex < 3; slotIndex++)
            {
                var slot = h.Orders.Slots[slotIndex];
                var materials = h.Orders.GetDisplayedMaterials(slotIndex);
                Assert.That(materials.Count, Is.EqualTo(slotIndex + 3));
                AssertPlusCount(slot, materials.Count - 1);
                for (int i = 0; i < materials.Count; i++)
                {
                    var icon = slot.MaterialIcons[i];
                    Assert.That(icon.sprite, Is.SameAs(materials[i].Sprite), "Original catalog order and sprite remain authoritative.");
                    Assert.That(icon.preserveAspect, Is.True);
                    // V3 supersedes UI02's arbitrary >64 rule with the approved 84px-high content.
                    // The 4/5-item expectations follow the actual 327/315px contents and 18px gaps.
                    float[] approvedWidths = { 84, 68.25f, 48.6f };
                    Assert.That(icon.rectTransform.rect.width, Is.EqualTo(approvedWidths[slotIndex]).Within(.25f));
                    Assert.That(icon.GetComponent<PopupStoreMaterialVisual>(), Is.Not.Null,
                        "Visible source artwork, not transparent PNG margins, fits the logical icon slot.");
                    AssertInside(Bounds(icon.rectTransform, slot.OrderFrame.rectTransform), slot.OrderFrame.rectTransform.rect, icon.name);
                    if (i > 0) AssertSeparatorBetween(slot, i);
                }
                Assert.That(slot.CustomerImage.preserveAspect, Is.True);
                var clip = slot.CustomerImage.transform.parent.GetComponent<PopupStoreCustomerVisualClip>();
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.Owns(slot.CustomerImage, slot.OrderFrame.transform.parent), Is.True);
                Assert.That(clip.BodyArea.rect.width, Is.GreaterThan(0));
                Assert.That(slot.CustomerImage.rectTransform.rect.width / slot.CustomerImage.rectTransform.rect.height,
                    Is.EqualTo(1).Within(.001f), "Original portrait canvas proportions remain square.");
            }
        }
    }

    [Test]
    public void CompactCounterAndScoreFrameRetainFiveSlotsWhileTheClockGainsProminence()
    {
        using (var h = new Host(1920, 1080, true))
        {
            var tray = h.Root.transform.Find("Sales Counter - Five Slots/Approved Tray").GetComponent<Image>();
            // V3's approved tray is 902px plus 4px padding per side. Its outer width may exceed
            // the old UI02 1000 logical-unit threshold after the shared contain transform.
            PopupStoreFinalGuideTests.AssertGuideRect(tray.rectTransform, h.Root.GetComponent<RectTransform>(),
                new Rect(382, 526, 910, 175), "approved v3 tray");
            Assert.That(tray.preserveAspect, Is.True);
            Assert.That(tray.rectTransform.rect.width / tray.rectTransform.rect.height,
                Is.EqualTo(tray.sprite.rect.width / tray.sprite.rect.height).Within(.02f));
            var slotIcons = Read<Image[]>(h.Counter, "_slotIcons");
            Assert.That(slotIcons, Has.Length.EqualTo(5));
            for (int i = 0; i < slotIcons.Length; i++)
            {
                var slot = slotIcons[i];
                Assert.That(slot.rectTransform.rect.width, Is.GreaterThanOrEqualTo(80), "A smaller tray must retain useful material visibility.");
                AssertInside(Bounds(slot.rectTransform, tray.rectTransform), tray.rectTransform.rect, slot.name);
                if (i > 0) Assert.That(Bounds(slotIcons[i - 1].rectTransform, tray.rectTransform).xMax,
                    Is.LessThan(Bounds(slot.rectTransform, tray.rectTransform).xMin));
            }
            foreach (string id in new[] { "FG_HUD_ScoreFrame", "FG_Icon_ClockPink" })
                PopupStoreCleanPartsTests.AssertMappedRect(PopupStoreCleanPartsTests.MappedImages(h.Root, id).Single(),
                    h.Root.GetComponent<RectTransform>(), PopupStoreCleanPartsTests.Map().replacements.Single(row => row.old_id == id));
            foreach (var text in new[] { Read<TMP_Text>(h.Root.GetComponent<PopupStoreScore>(), "_scoreText"), Read<TMP_Text>(h.Clock, "_timeText") })
            {
                text.ForceMeshUpdate(true, true);
                Assert.That(text.isTextOverflowing, Is.False, text.name);
                Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(26), text.name);
            }
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EnteringCustomersHaveNoPlusSignsUntilTheirOwnOrderBecomesVisible(int arrivedCount)
    {
        using (var h = new Host(1920, 1080, false))
        {
            Assert.That(PopupStoreApprovedTestSession.Start(h.Root), Is.True);
            h.Entrance.TickEntrance(0);
            for (int i = 0; i < arrivedCount; i++)
            {
                if (i > 0) h.Entrance.TickEntrance(4);
                h.Entrance.TickEntrance(Read<float>(h.Entrance, "_entryDuration") + .01f);
            }
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.That(h.Entrance.IsWaiting(slot), Is.EqualTo(slot < arrivedCount));
                AssertPlusCount(h.Orders.Slots[slot], slot < arrivedCount ? slot + 2 : 0);
            }
        }
    }

    [Test]
    public void RepeatedOrderDisplayEventsKeepTheSameDecorationObjectsAndAllBusinessData()
    {
        using (var h = new Host(1920, 1080, true))
        {
            Assert.That(h.Counter.TryAdd(h.Orders.GetDisplayedMaterials(0)[0]), Is.True);
            var before = new BusinessSnapshot(h);
            int[] labels = h.Pluses.Select(text => text.GetInstanceID()).ToArray();
            int feedback = 0;
            h.Root.GetComponent<PopupStoreOrderProgress>().FeedbackTriggered += () => feedback++;
            for (int i = 0; i < 20; i++)
            {
                Assert.That(h.Orders.Refresh(), Is.True);
                h.Orders.ApplyCustomerEntrySlot(i % 3);
                Canvas.ForceUpdateCanvases();
            }
            before.AssertUnchanged(h);
            CollectionAssert.AreEqual(labels, h.Pluses.Select(text => text.GetInstanceID()).ToArray());
            Assert.That(feedback, Is.Zero, "A decoration refresh must not trigger match sounds.");
            for (int i = 0; i < 3; i++) AssertPlusCount(h.Orders.Slots[i], i + 2);
        }
    }

    [Test]
    public void ReentryUsesTheSamePlusChildrenWithoutDuplicatingOrRevivingCompletedOrders()
    {
        using (var h = new Host(1920, 1080, true))
        {
            h.Pack(0); h.CompleteDeliveryAndDeparture();
            var before = new BusinessSnapshot(h);
            int[] labels = h.Pluses.Select(text => text.GetInstanceID()).ToArray();
            for (int i = 0; i < 3; i++)
            {
                h.Close();
                Assert.That(h.Pluses.Count(text => text.isActiveAndEnabled), Is.Zero);
                h.Reopen();
                AssertPlusCount(h.Orders.Slots[0], 0);
                AssertPlusCount(h.Orders.Slots[1], 3);
                AssertPlusCount(h.Orders.Slots[2], 4);
                CollectionAssert.AreEqual(labels, h.Pluses.Select(text => text.GetInstanceID()).ToArray());
            }
            before.AssertUnchanged(h);
        }
    }

    [Test]
    public void SuccessfulPackingHidesOnlyItsOrderPlusesAndPreservesTheOriginalReceiptDelivery()
    {
        using (var h = new Host(1920, 1080, true))
        {
            string[] otherOrders = new[] { h.Orders.GetDisplayedOrderId(1), h.Orders.GetDisplayedOrderId(2) };
            var receipt = h.Pack(0);
            AssertPlusCount(h.Orders.Slots[0], 0);
            AssertPlusCount(h.Orders.Slots[1], 3); AssertPlusCount(h.Orders.Slots[2], 4);
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreScore>().TotalScore, Is.EqualTo(100));
            h.CompleteDeliveryAndDeparture();
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Empty));
            Assert.That(h.Entrance.StartedDepartureCount, Is.EqualTo(1));
            Assert.That(h.Entrance.ReleasedCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(otherOrders, new[] { h.Orders.GetDisplayedOrderId(1), h.Orders.GetDisplayedOrderId(2) });
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
        }
    }

    [Test]
    public void PatienceExpirationHidesOnlyThatOrdersPlusesWithoutPackingOrConsumingPreparedMaterials()
    {
        using (var h = new Host(1920, 1080, true))
        {
            var prepared = h.Orders.GetDisplayedMaterials(1)[0];
            Assert.That(h.Counter.TryAdd(prepared), Is.True);
            Assert.That(h.Clock.AdvanceTime(18), Is.True);
            Assert.That(h.Orders.IsOrderExpired(0), Is.True);
            Assert.That(h.Orders.IsOrderExpired(1) || h.Orders.IsOrderExpired(2), Is.False);
            AssertPlusCount(h.Orders.Slots[0], 0);
            AssertPlusCount(h.Orders.Slots[1], 3); AssertPlusCount(h.Orders.Slots[2], 4);
            Assert.That(h.Counter.Materials.Single(), Is.SameAs(prepared));
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
            Assert.That(h.Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void NarrowInspectorFramesRetainPositiveIconsAndKeepTheAuthoredPlusesInsideTheirGaps()
    {
        using (var h = new Host(1920, 1080, true))
        {
            foreach (var slot in h.Orders.Slots) slot.OrderFrame.rectTransform.sizeDelta = new Vector2(180, 80);
            Assert.That(h.Orders.Refresh(), Is.True);
            for (int slot = 0; slot < 3; slot++)
            {
                AssertPlusCount(h.Orders.Slots[slot], slot + 2);
                for (int i = 1; i < slot + 3; i++) AssertSeparatorBetween(h.Orders.Slots[slot], i);
            }
        }
    }

    [Test]
    public void EmptyOrderSlotsCannotLeaveOrCreateFloatingPlusSigns()
    {
        using (var h = new Host(1920, 1080, false))
        {
            Write(h.Orders, "_sampleOrders", Array.Empty<PopupStoreOrder>());
            Assert.That(h.Orders.Refresh(), Is.True);
            Assert.That(h.Orders.DisplayedCount, Is.Zero);
            foreach (var slot in h.Orders.Slots) AssertPlusCount(slot, 0);
            Assert.That(h.Pluses.Length, Is.EqualTo(12), "Empty orders hide authored objects rather than creating or deleting UI.");
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
        }
    }

    private static void AssertSeparatorBetween(PopupStoreOrderSlot slot, int rightIndex)
    {
        var label = Plus(slot.MaterialIcons[rightIndex]);
        Assert.That(label.isActiveAndEnabled, Is.True);
        Assert.That(label.raycastTarget, Is.False);
        var frame = slot.OrderFrame.rectTransform;
        Rect left = Bounds(slot.MaterialIcons[rightIndex - 1].rectTransform, frame);
        Rect right = Bounds(slot.MaterialIcons[rightIndex].rectTransform, frame);
        Rect plus = Bounds(label.rectTransform, frame);
        Assert.That(plus.center.x, Is.EqualTo((left.xMax + right.xMin) * .5f).Within(.05f));
        Assert.That(plus.xMin, Is.GreaterThanOrEqualTo(left.xMax - .05f));
        Assert.That(plus.xMax, Is.LessThanOrEqualTo(right.xMin + .05f));
        AssertInside(plus, frame.rect, label.name);
        label.ForceMeshUpdate(true, true);
        Assert.That(label.isTextOverflowing, Is.False);
        Assert.That(label.textInfo.characterCount, Is.EqualTo(1));
        Assert.That(label.textInfo.characterInfo[0].isVisible, Is.True, "The plus glyph must actually render.");
    }

    private static void AssertPlusCount(PopupStoreOrderSlot slot, int count)
        => Assert.That(slot.OrderFrame.GetComponentsInChildren<TMP_Text>(true)
            .Count(text => text.name == SeparatorName && text.isActiveAndEnabled), Is.EqualTo(count), slot.OrderFrame.name);
    private static TMP_Text Plus(Image icon) => icon.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == SeparatorName);

    private sealed class Host : IDisposable
    {
        private readonly IDisposable _geometry;
        private readonly object _navigation;
        internal readonly GameObject Root;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStorePacking Packing;
        internal TMP_Text[] Pluses => Root.GetComponentsInChildren<TMP_Text>(true).Where(text => text.name == SeparatorName).ToArray();
        internal Host(int width, int height, bool start)
        {
            // The established UI fixture uses the real prefab, isolated admission and original navigation,
            // with cloned offline fonts and no Scene creation, saved settings, or GameView changes.
            var type = typeof(PopupStoreApprovedUiTests).GetNestedType("Host", BindingFlags.NonPublic);
            _geometry = (IDisposable)Activator.CreateInstance(type, Fields, null, new object[] { width, height, start }, CultureInfo.InvariantCulture);
            _navigation = Read<object>(_geometry, "_navigationHost");
            Root = Read<GameObject>(_geometry, "Root");
            Orders = Root.GetComponent<PopupStoreOrderDisplay>(); Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
            Clock = Root.GetComponent<PopupStoreBusinessClock>(); Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
            Packing = Root.GetComponent<PopupStorePacking>();
        }
        internal PopupStorePackingReceipt Pack(int slot) => (PopupStorePackingReceipt)Call(_navigation, "Pack", slot);
        internal void CompleteDeliveryAndDeparture() { Call(_navigation, "CompleteVisual", true); Call(_navigation, "FinishDepartures"); }
        internal void Close() => Call(_navigation, "Close");
        internal void Reopen() => Call(_navigation, "Reopen");
        public void Dispose() => _geometry?.Dispose();
    }

    private sealed class BusinessSnapshot
    {
        private readonly string _value;
        private readonly PopupStorePackingReceipt[] _receipts;
        private readonly PopupStoreGradeResult _final;
        private readonly PopupStoreRewardDecision _reward;
        internal BusinessSnapshot(Host host)
        {
            _value = Describe(host);
            _receipts = host.Packing.CommittedReceipts.ToArray();
            _final = host.Root.GetComponent<PopupStoreGradeSession>().FinalResult;
            _reward = host.Root.GetComponent<PopupStoreRewardSession>().Result;
        }
        internal void AssertUnchanged(Host host)
        {
            Assert.That(Describe(host), Is.EqualTo(_value));
            CollectionAssert.AreEqual(_receipts, host.Packing.CommittedReceipts.ToArray());
            Assert.That(host.Root.GetComponent<PopupStoreGradeSession>().FinalResult, Is.SameAs(_final));
            Assert.That(host.Root.GetComponent<PopupStoreRewardSession>().Result, Is.SameAs(_reward));
        }
        private static string Describe(Host h)
        {
            var score = h.Root.GetComponent<PopupStoreScore>();
            var grade = h.Root.GetComponent<PopupStoreGradeSession>();
            var reward = h.Root.GetComponent<PopupStoreRewardSession>();
            return string.Join("|", h.Clock.State, h.Clock.RemainingSeconds.ToString("R", CultureInfo.InvariantCulture),
                h.Clock.ComplaintPenaltyCount, score.TotalScore, score.SuccessfulOrderCount, grade.SessionId,
                grade.FinalizationCount, reward.RandomDrawCount, reward.DecisionCount, h.Packing.SuccessfulPackCount,
                h.Entrance.StartedEntranceCount, h.Entrance.StartedDepartureCount, h.Entrance.ReleasedCount,
                string.Join(",", h.Counter.Materials.Select(material => material.Id)),
                string.Join(",", Read<PopupStoreOrder[]>(h.Orders, "_sampleOrders").Select(order => order.RuntimeId + ":" + order.IsPacked + ":" + order.IsExpired)));
        }
    }

    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
    private static void Write(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    private static Rect Bounds(RectTransform child, RectTransform root)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        var points = corners.Select(root.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    private static void AssertInside(Rect child, Rect parent, string description)
    {
        Assert.That(child.xMin, Is.GreaterThanOrEqualTo(parent.xMin - .05f), description);
        Assert.That(child.xMax, Is.LessThanOrEqualTo(parent.xMax + .05f), description);
        Assert.That(child.yMin, Is.GreaterThanOrEqualTo(parent.yMin - .05f), description);
        Assert.That(child.yMax, Is.LessThanOrEqualTo(parent.yMax + .05f), description);
    }
}
#endif
