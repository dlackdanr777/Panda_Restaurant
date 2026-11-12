#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises the isolated supply seam with real authored orders, transactions, delivery and release.</summary>
public sealed class PopupStorePhasePreviewSupplyTests
{
    [Test]
    public void OrdinaryPrefabRetainsItsTwentyAuthoredOrdersWithoutAnyPreviewBinding()
    {
        using (var h = new Host(false))
        {
            Assert.That(h.Entrance.EditorPhaseSupplyBound, Is.False);
            Assert.That(h.Templates.Length, Is.EqualTo(20));
            CollectionAssert.AreEqual(new[] { 8, 8, 4 }, Enumerable.Range(1, 3)
                .Select(count => h.Templates.Count(order => order.PlushIds.Count == count)).ToArray());
            h.Entrance.TickEntrance(0);
            Assert.That(h.Entrance.SuppliedOrderCount, Is.EqualTo(1));
            Assert.That(h.Entrance.TotalTestOrderCount, Is.EqualTo(20));
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(90));
        }
    }

    [Test]
    public void BindingPreparesThreeFreshOrdersButCannotStartThemUntilTheOwnerAllowsSupply()
    {
        using (var h = new Host())
        {
            h.Supply.CanSupply = false;
            h.Entrance.TickEntrance(100);
            Assert.That(h.Entrance.SuppliedOrders, Is.Empty);
            Assert.That(h.Orders.DisplayedCount, Is.EqualTo(3));
            Assert.That(h.SourceOrders.All(h.Supply.OwnsOrder), Is.True);
            Assert.That(h.Orders.Slots.All(slot => !slot.OrderFrame.gameObject.activeSelf), Is.True);
            CollectionAssert.AllItemsAreUnique(h.SourceOrders.Select(order => order.RuntimeId).ToArray());
            Assert.That(h.Entrance.TryBeginEditorPhase(h.Supply, out _), Is.False);
            Assert.That(h.Entrance.BindEditorPhaseSupply(h.Supply, out _), Is.False);
            h.Supply.CanSupply = true;
            h.Entrance.TickEntrance(0);
            Assert.That(h.Entrance.SuppliedOrderCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void DynamicSupplyDoesNotModifyTheOriginalTwentyDefinitionsOrFourSecondSetting()
    {
        using (var h = new Host(false))
        {
            var originals = h.Templates.Select(JsonUtility.ToJson).ToArray();
            var interval = Read(h.Entrance, "_entryInterval");
            h.Bind(); h.ReadyThree(); h.Cleanup();
            h.Supply.Generation++;
            Assert.That(h.Entrance.TryBeginEditorPhase(h.Supply, out var error), Is.True, error);
            CollectionAssert.AreEqual(originals, h.Templates.Select(JsonUtility.ToJson).ToArray());
            Assert.That(Read(h.Entrance, "_entryInterval"), Is.EqualTo(interval));
            Assert.That(h.Templates.All(order => !order.IsPacked && !order.IsExpired), Is.True);
        }
    }

    [Test]
    public void SupplyUsesActualStartsAndNeverCatchesUpSeveralCustomersInOneLargeTick()
    {
        using (var h = new Host())
        {
            h.Entrance.TickEntrance(100);
            Assert.That(h.Entrance.StartedEntranceCount, Is.EqualTo(1));
            h.Entrance.TickEntrance(h.Supply.EntryInterval - .01f);
            Assert.That(h.Entrance.StartedEntranceCount, Is.EqualTo(1));
            h.Entrance.TickEntrance(.02f);
            Assert.That(h.Entrance.StartedEntranceCount, Is.EqualTo(2));
            h.Entrance.TickEntrance(100);
            Assert.That(h.Entrance.StartedEntranceCount, Is.EqualTo(3));
        }
    }

    [TestCase("Entering")]
    [TestCase("Waiting")]
    [TestCase("Packed")]
    [TestCase("Leaving")]
    public void DynamicSupplyCannotReplaceAnyOccupiedSlot(string state)
    {
        using (var h = new Host())
        {
            if (state == "Entering") Write(h.Entrance, "_entryDuration", 1000f);
            h.ReadyThree(state != "Entering");
            if (state == "Packed" || state == "Leaving")
                for (int i = 0; i < 3; i++) h.Pack(i);
            if (state == "Leaving") for (int i = 0; i < 3; i++) h.FinishVisual();
            var orders = h.SourceOrders.ToArray();
            h.Entrance.TickEntrance(state == "Leaving" ? h.ExitDuration * .2f : .1f);
            CollectionAssert.AreEqual(orders, h.SourceOrders);
            Assert.That(h.Entrance.SuppliedOrderCount, Is.EqualTo(3));
            Assert.That(h.Entrance.OccupiedSlotCount, Is.EqualTo(3));
            for (int i = 0; i < 3; i++) Assert.That(h.Entrance.GetState(i).ToString(), Is.EqualTo(state));
        }
    }

    [Test]
    public void TheExistingTenSecondCutoffStillBlocksNewPreviewCustomerEntry()
    {
        using (var h = new Host())
        {
            Assert.That(h.Clock.AdvanceTime(80), Is.True);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(10));
            h.Entrance.TickEntrance(0);
            Assert.That(h.Entrance.SuppliedOrders, Is.Empty);
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
        }
    }

    [Test]
    public void CleanupWaitsForDeliveryVisualCompletionAndActualCustomerRelease()
    {
        using (var h = new Host())
        {
            h.ReadyThree();
            var receipt = h.Pack(0);
            h.Supply.CanSupply = false; h.Supply.CanCleanup = true;
            Assert.That(h.Entrance.TryCleanupEditorPhase(h.Supply, out _), Is.False);
            Assert.That(h.Entrance.EditorReleasedReceipts, Is.Empty);
            h.FinishVisual();
            Assert.That(h.Presentation.IsDeliveryCompleted(receipt.ReceiptId), Is.True);
            Assert.That(h.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving));
            Assert.That(h.Entrance.TryCleanupEditorPhase(h.Supply, out _), Is.False);
            h.Entrance.TickEntrance(h.ExitDuration * .5f);
            Assert.That(h.Entrance.EditorReleasedReceipts, Is.Empty);
            h.Entrance.TickEntrance(h.ExitDuration * .6f);
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Entrance.TryCleanupEditorPhase(h.Supply, out var error), Is.True, error);
            Assert.That(h.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(h.SourceOrders.Count(order => order.IsExpired), Is.Zero);
        }
    }

    [Test]
    public void ReleasedReceiptEvidenceSurvivesSlotReuseAndNextPhasePreparation()
    {
        using (var h = new Host())
        {
            h.ReadyThree();
            var previous = h.SourceOrders[0];
            var receipt = h.Pack(0);
            h.FinishVisual();
            h.Entrance.TickEntrance(h.ExitDuration + h.Supply.EntryInterval);
            Assert.That(h.Entrance.SuppliedOrderCount, Is.EqualTo(4));
            Assert.That(h.SourceOrders[0], Is.Not.SameAs(previous));
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
            h.Cleanup(); h.Supply.Generation++;
            Assert.That(h.Entrance.TryBeginEditorPhase(h.Supply, out var error), Is.True, error);
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.SourceOrders.All(order => !order.IsPacked && !order.IsExpired && h.Supply.OwnsOrder(order)), Is.True);
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(previous.PackingReceipt, Is.SameAs(receipt));
        }
    }

    [Test]
    public void CancelledPresentationIsNotAcceptedAsCompletedPreviewCleanup()
    {
        using (var h = new Host())
        {
            h.ReadyThree(); h.Pack(0);
            h.Presentation.TickPresentation(0);
            LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            h.Presentation.EndVisibleSession();
            h.Supply.CanSupply = false; h.Supply.CanCleanup = true;
            Assert.That(h.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
            Assert.That(h.Entrance.TryCleanupEditorPhase(h.Supply, out _), Is.False);
            Assert.That(h.Entrance.EditorReleasedReceipts, Is.Empty);
            // The original general-mode cancellation recovery can leave the customer; it still is not delivery proof.
            Assert.That(h.Entrance.BeginVisibleSession(), Is.True);
            h.Entrance.TickEntrance(h.ExitDuration + .01f);
            Assert.That(h.Entrance.EditorPhaseSupplyFaulted, Is.True);
            Assert.That(h.Entrance.TryCleanupEditorPhase(h.Supply, out _), Is.False);
            Assert.That(h.Entrance.EditorReleasedReceipts, Is.Empty);
        }
    }

    [Test]
    public void UnfinishedCleanupHidesCustomersWithoutExpirationPenaltyReceiptOrScore()
    {
        using (var h = new Host())
        {
            h.ReadyThree();
            var orders = h.SourceOrders.ToArray();
            double remaining = h.Clock.RemainingSeconds;
            h.Cleanup();
            Assert.That(h.Entrance.OccupiedSlotCount, Is.Zero);
            Assert.That(h.Entrance.ReleasedCount, Is.Zero, "Unfinished cleanup does not forge normal release history.");
            Assert.That(h.Entrance.StartedComplaintCount, Is.Zero);
            Assert.That(h.Orders.Slots.All(slot => !slot.CustomerImage.gameObject.activeSelf && !slot.OrderFrame.gameObject.activeSelf), Is.True);
            Assert.That(orders.All(order => !order.IsExpired && !order.IsPacked), Is.True);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
            Assert.That(h.Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(h.Entrance.TryCleanupEditorPhase(h.Supply, out _), Is.True);
        }
    }

    [TestCase("duplicate")]
    [TestCase("unknown-plush")]
    [TestCase("foreign-ownership")]
    public void InvalidInitialSupplyIsRejectedWithoutReplacingAnyOriginalOrder(string defect)
    {
        using (var h = new Host(false))
        {
            var orders = h.SourceOrders.ToArray();
            if (defect == "duplicate") h.Supply.FixedOrder = h.Supply.Create(0);
            if (defect == "unknown-plush") h.Supply.InvalidPlush = true;
            if (defect == "foreign-ownership") h.Supply.RejectOwnership = true;
            Assert.That(h.Entrance.BindEditorPhaseSupply(h.Supply, out _), Is.False);
            CollectionAssert.AreEqual(orders, h.SourceOrders);
            Assert.That(h.Entrance.StartedEntranceCount, Is.Zero);
            Assert.That(h.Entrance.EditorPhaseSupplyFaulted, Is.True);
        }
    }

    [Test]
    public void AnAlreadyEnteredOriginalCustomerCannotBeReboundToPreviewSupply()
    {
        using (var h = new Host(false))
        {
            h.Entrance.TickEntrance(0);
            var order = h.SourceOrders[0];
            Assert.That(h.Entrance.BindEditorPhaseSupply(h.Supply, out _), Is.False);
            Assert.That(h.SourceOrders[0], Is.SameAs(order));
            Assert.That(h.Entrance.EditorPhaseSupplyBound, Is.False);
        }
    }

    [Test]
    public void WrongOwnerAndChangedOrderIdentityCannotApprovePhaseCleanup()
    {
        using (var h = new Host())
        {
            h.ReadyThree(); h.Supply.CanSupply = false; h.Supply.CanCleanup = true;
            var impostor = new Supply(h.View) { CanCleanup = true };
            Assert.That(h.Entrance.TryCleanupEditorPhase(impostor, out _), Is.False);
            Write(h.SourceOrders[0], "_runtimeId", "foreign-preview-order");
            Assert.That(h.Entrance.TryCleanupEditorPhase(h.Supply, out _), Is.False);
            Assert.That(h.Entrance.ReleasedCount, Is.Zero);
        }
    }

    [TestCase(5)]
    [TestCase(8)]
    [TestCase(12)]
    [TestCase(16)]
    [TestCase(24)]
    [TestCase(32)]
    public void PlushFilterShowsOnlyTheApprovedPrefixAndPreservesCatalogOrder(int count)
    {
        using (var h = new Host())
        {
            var before = h.PlushEntries.Select(entry => entry.Id).ToArray();
            Assert.That(h.PlushList.TrySetEditorPlushFilter(h.View, before.Take(count).ToArray(), out var error), Is.True, error);
            Assert.That(h.PlushList.MaterialCount, Is.EqualTo(count));
            CollectionAssert.AreEqual(before.Take(count).ToArray(), h.PlushList.GetComponentsInChildren<PopupStoreMaterialItem>()
                .Where(item => item.isActiveAndEnabled).Select(item => item.Material.Id).ToArray());
            Assert.That(h.Catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var after, out error), Is.True, error);
            CollectionAssert.AreEqual(before, after.Select(entry => entry.Id).ToArray());
            foreach (var list in h.Root.GetComponentsInChildren<PopupStoreMaterialList>(true))
                if (list != h.PlushList) Assert.That(list.MaterialCount, Is.EqualTo(3));
        }
    }

    [TestCase("duplicate")]
    [TestCase("unknown")]
    [TestCase("wrong-kind")]
    public void InvalidPlushFilterDoesNotReplaceTheLastValidFilter(string defect)
    {
        using (var h = new Host())
        {
            var ids = h.PlushEntries.Take(5).Select(entry => entry.Id).ToArray();
            Assert.That(h.PlushList.TrySetEditorPlushFilter(h.View, ids, out _), Is.True);
            var broken = (string[])ids.Clone();
            broken[4] = defect == "duplicate" ? broken[0] : defect == "wrong-kind" ? "BOX_PINK" : "STAFF99";
            Assert.That(h.PlushList.TrySetEditorPlushFilter(h.View, broken, out _), Is.False);
            Assert.That(h.PlushList.MaterialCount, Is.EqualTo(5));
        }
    }

    [Test]
    public void FilterOwnsAnImmutableCopyAndRejectsClicksFromThePreviousLayout()
    {
        using (var h = new Host())
        {
            var first = h.PlushList.GetComponentsInChildren<PopupStoreMaterialItem>().First(item => item.isActiveAndEnabled);
            uint previousVersion = h.PlushList.InputVersion;
            var ids = h.PlushEntries.Take(5).Select(entry => entry.Id).ToArray();
            Assert.That(h.PlushList.TrySetEditorPlushFilter(h.View, ids, out _), Is.True);
            ids[0] = "STAFF32";
            Assert.That(h.PlushList.Refresh(), Is.True);
            Assert.That(h.PlushList.TrySelect(first, previousVersion), Is.False);
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.PlushList.GetComponentsInChildren<PopupStoreMaterialItem>().Any(item => item.Material.Id == "STAFF32"), Is.False);
        }
    }

    [Test]
    public void TheDirectCounterApiRejectsLockedPlushWhileKeepingAllBoxesAndRibbonsSelectable()
    {
        using (var h = new Host())
        {
            h.AllowedIds = new HashSet<string>(h.PlushEntries.Take(5).Select(entry => entry.Id));
            Assert.That(h.Counter.TryAdd(h.PlushEntries[5]), Is.False);
            Assert.That(h.Counter.TryAdd(h.PlushEntries[0]), Is.True);
            foreach (var kind in new[] { PopupStoreMaterialKind.Box, PopupStoreMaterialKind.Ribbon })
            {
                Assert.That(h.Catalog.TryGetMaterials(kind, out var entries, out _), Is.True);
                foreach (var material in entries)
                {
                    h.Counter.Clear();
                    Assert.That(h.Counter.TryAdd(material), Is.True);
                }
            }
        }
    }

    [Test]
    public void TransitionGateBlocksDirectAddAndClearButAllowsOwnerOnlyNeutralCleanup()
    {
        using (var h = new Host())
        {
            Assert.That(h.Counter.TryAdd(h.PlushEntries[0]), Is.True);
            h.CanInput = false;
            Assert.That(h.Counter.TryAdd(h.PlushEntries[1]), Is.False);
            h.Counter.Clear();
            Assert.That(h.Counter.Count, Is.EqualTo(1));
            Assert.That(h.Counter.EditorClearForPhaseCleanup(h.View), Is.False);
            h.Supply.CanCleanup = true;
            Assert.That(h.Counter.EditorClearForPhaseCleanup(h.Entrance), Is.False);
            Assert.That(h.Counter.EditorClearForPhaseCleanup(h.View), Is.True);
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
        }
    }

    [Test]
    public void CleanupCannotClearMaterialsDuringTheExistingPackingTransactionLock()
    {
        using (var h = new Host())
        {
            Assert.That(h.Counter.TryAdd(h.PlushEntries[0]), Is.True);
            h.Supply.CanCleanup = true;
            Assert.That(h.Counter.BeginPackingLock(), Is.True);
            try
            {
                Assert.That(h.Counter.EditorClearForPhaseCleanup(h.View), Is.False);
                Assert.That(h.Counter.Count, Is.EqualTo(1));
            }
            finally { h.Counter.EndPackingLock(); }
            Assert.That(h.Counter.EditorClearForPhaseCleanup(h.View), Is.True);
        }
    }

    private sealed class Supply : IPopupStorePhasePreviewSupply
    {
        private readonly Dictionary<PopupStoreOrder, int> _owned = new Dictionary<PopupStoreOrder, int>();
        public UIPopupStore Popup { get; }
        public bool CanSupply { get; set; } = true;
        public bool CanCleanup { get; set; }
        public float EntryInterval { get; set; } = 3.2f;
        internal int Generation;
        internal bool InvalidPlush, RejectOwnership;
        internal PopupStoreOrder FixedOrder;
        internal Supply(UIPopupStore popup) { Popup = popup; }
        public bool OwnsOrder(PopupStoreOrder order)
            => !RejectOwnership && order != null && _owned.TryGetValue(order, out int phase) && phase == Generation;
        public bool TryCreateOrder(int slotIndex, out PopupStoreOrder order)
        { order = FixedOrder ?? Create(slotIndex); return true; }
        internal PopupStoreOrder Create(int slot)
        {
            var definition = new PopupStoreOrder();
            Write(definition, "_customerId", new[] { "CUSTOMER01", "CUSTOMER04", "CUSTOMER11" }[slot]);
            Write(definition, "_plushIds", new[] { InvalidPlush ? "STAFF99" : "STAFF0" + (slot + 1) });
            Write(definition, "_boxId", "BOX_PINK");
            Write(definition, "_ribbonId", "RIBBON_YELLOW");
            var runtime = definition.CreateRuntimeCopy();
            _owned.Add(runtime, Generation);
            return runtime;
        }
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal readonly PopupStoreMaterialCatalog Catalog;
        internal readonly PopupStoreMaterialList PlushList;
        internal readonly PopupStoreMaterialEntry[] PlushEntries;
        internal readonly Supply Supply;
        internal bool CanInput = true;
        internal HashSet<string> AllowedIds;
        internal PopupStoreOrder[] Templates => (PopupStoreOrder[])Read(Entrance, "_testOrders");
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");
        internal Host(bool bind = true)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/PopupStore/UIPopupStore.prefab"));
            try
            {
                Root.name = "phase-preview-supply-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>(); Clock = Root.GetComponent<PopupStoreBusinessClock>();
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>(); Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Packing = Root.GetComponent<PopupStorePacking>(); Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Catalog = (PopupStoreMaterialCatalog)Read(Orders, "_catalog");
                PlushList = Root.GetComponentsInChildren<PopupStoreMaterialList>(true)
                    .Single(list => (PopupStoreMaterialKind)Read(list, "_kind") == PopupStoreMaterialKind.Plush);
                Assert.That(Catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out PlushEntries, out _), Is.True);
                AllowedIds = new HashSet<string>(PlushEntries.Select(entry => entry.Id));
                View.Init(); View.Show();
                Assert.That(PopupStoreApprovedTestSession.Start(Root), Is.True);
                Assert.That(Counter.Refresh(), Is.True);
                foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
                Assert.That(Orders.Refresh(), Is.True);
                Assert.That(Root.GetComponent<PopupStoreOrderProgress>().Refresh(), Is.True);
                Assert.That(Packing.Refresh(), Is.True);
                Assert.That(Entrance.BeginVisibleSession(), Is.True);
                Assert.That(Presentation.BeginVisibleSession(), Is.True);
                Supply = new Supply(View);
                if (bind) Bind();
                Assert.That(Counter.BindEditorPhaseInput(View, () => CanInput,
                    material => material.Kind != PopupStoreMaterialKind.Plush || AllowedIds.Contains(material.Id),
                    () => Supply.CanCleanup), Is.True);
            }
            catch { Dispose(); throw; }
        }
        internal void Bind() => Assert.That(Entrance.BindEditorPhaseSupply(Supply, out var error), Is.True, error);
        internal void ReadyThree(bool wait = true)
        {
            Entrance.TickEntrance(0); Entrance.TickEntrance(Supply.EntryInterval); Entrance.TickEntrance(Supply.EntryInterval);
            if (wait) Entrance.TickEntrance((float)Read(Entrance, "_entryDuration") + .01f);
            Assert.That(Entrance.SuppliedOrderCount, Is.EqualTo(3));
        }
        internal PopupStorePackingReceipt Pack(int slot)
        {
            Assert.That(SourceOrders[slot].TryResolve(Catalog, out var entries, out var error), Is.True, error);
            foreach (var entry in entries.Reverse()) Assert.That(Counter.TryAdd(entry), Is.True);
            var result = Packing.TryPack();
            Assert.That(result.Success, Is.True, result.Status.ToString());
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(slot));
            return result.Receipt;
        }
        internal void FinishVisual()
        {
            Presentation.TickPresentation(0);
            Assert.That(Presentation.CurrentReceipt, Is.Not.Null);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Idle; i++)
                Presentation.TickPresentation(.01f);
            Assert.That(Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Idle));
        }
        internal void Cleanup()
        {
            Supply.CanSupply = false; Supply.CanCleanup = true;
            Assert.That(Entrance.TryCleanupEditorPhase(Supply, out var error), Is.True, error);
        }
        public void Dispose()
        {
            if (Entrance != null) Entrance.PauseVisibleSession();
            if (Presentation != null) Presentation.EndVisibleSession();
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
#endif
