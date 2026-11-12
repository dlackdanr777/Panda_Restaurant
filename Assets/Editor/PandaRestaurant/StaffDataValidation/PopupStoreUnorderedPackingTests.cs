using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>DEV-GAME-01: unordered customer fulfilment, strict source identity and the authored sign.</summary>
public sealed class PopupStoreUnorderedPackingTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";

    [TestCase("STAFF01,BOX_PINK,RIBBON_YELLOW", 6)]
    [TestCase("STAFF01,STAFF02,BOX_BLUE,RIBBON_PINK", 24)]
    [TestCase("STAFF03,STAFF05,STAFF02,BOX_MINT,RIBBON_MINT", 120)]
    [TestCase("STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK", 12)]
    public void EveryUniquePermutationMatchesWithoutChangingEitherSequence(string csv, int expectedPermutations)
    {
        var order = Entries(csv);
        var orderBefore = order.ToArray();
        var permutations = Permutations(order).ToArray();
        Assert.That(permutations.Length, Is.EqualTo(expectedPermutations));
        foreach (var counter in permutations)
        {
            var before = counter.ToArray();
            Assert.That(PopupStoreUnorderedOrderMatch.IsExact(Catalog(), order, counter), Is.True, Ids(counter));
            CollectionAssert.AreEqual(orderBefore, order);
            CollectionAssert.AreEqual(before, counter);
        }
    }

    [TestCase("")]
    [TestCase("STAFF01")]
    [TestCase("STAFF01,STAFF01")]
    [TestCase("STAFF01,BOX_BLUE,RIBBON_PINK")]
    [TestCase("STAFF01,STAFF02,BOX_BLUE,RIBBON_PINK")]
    [TestCase("STAFF01,STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK")]
    [TestCase("STAFF01,STAFF01,BOX_PINK,RIBBON_PINK")]
    [TestCase("STAFF01,STAFF01,BOX_BLUE,RIBBON_MINT")]
    [TestCase("STAFF01,STAFF01,BOX_BLUE,BOX_PINK")]
    [TestCase("STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK,STAFF02,RIBBON_MINT")]
    public void MissingExcessSubstitutedOrWrongCategoryMaterialsAreRejected(string csv)
    {
        Assert.That(PopupStoreUnorderedOrderMatch.IsExact(Catalog(),
            Entries("STAFF01,STAFF01,BOX_BLUE,RIBBON_PINK"), Entries(csv)), Is.False);
    }

    [TestCase("null")]
    [TestCase("unknown")]
    [TestCase("wrong-kind")]
    [TestCase("wrong-case")]
    public void MalformedCounterEntriesCannotMatch(string defect)
    {
        var order = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        var counter = order.Reverse().ToArray();
        counter[2] = defect == "null" ? null : MakeEntry(defect == "unknown" ? "STAFF99"
            : defect == "wrong-case" ? "staff01" : "STAFF01",
            defect == "wrong-kind" ? PopupStoreMaterialKind.Box : PopupStoreMaterialKind.Plush);
        Assert.That(PopupStoreUnorderedOrderMatch.IsExact(Catalog(), order, counter), Is.False);
    }

    [Test]
    public void NullInputsMissingCatalogAndInvalidCanonicalOrdersAreRejected()
    {
        var valid = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        Assert.That(PopupStoreUnorderedOrderMatch.IsExact(null, valid, valid), Is.False);
        Assert.That(PopupStoreUnorderedOrderMatch.IsExact(Catalog(), null, valid), Is.False);
        Assert.That(PopupStoreUnorderedOrderMatch.IsExact(Catalog(), valid, null), Is.False);
        foreach (var malformed in new[] { "BOX_PINK,RIBBON_YELLOW", "RIBBON_YELLOW,BOX_PINK,STAFF01",
            "STAFF01,BOX_PINK,BOX_BLUE", "STAFF01,STAFF02,STAFF03,STAFF04,BOX_PINK,RIBBON_YELLOW" })
            Assert.That(PopupStoreUnorderedOrderMatch.IsExact(Catalog(), Entries(malformed), Entries(malformed)), Is.False, malformed);
    }

    [Test]
    public void IdentityUsesKindAndOrdinalIdInsteadOfSpriteOrObjectIdentity()
    {
        var order = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        var counter = order.Reverse().Select(x => MakeEntry(x.Id, x.Kind)).ToArray();
        foreach (var entry in counter) Write(entry, "_sprite", Entry("STAFF32").Sprite);
        Assert.That(PopupStoreUnorderedOrderMatch.IsExact(Catalog(), order, counter), Is.True);
        Assert.That(PopupStoreExactOrderMatch.IsExact(Catalog(), order, counter), Is.False,
            "Strict snapshot comparison must not adopt unordered fulfilment semantics.");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void QueryPreservesAllUnorderedCandidateSlots(int count)
    {
        var order = Entries("STAFF01,STAFF02,BOX_PINK,RIBBON_YELLOW");
        var swapped = Entries("STAFF02,STAFF01,BOX_PINK,RIBBON_YELLOW");
        var orders = Enumerable.Range(0, count).Select(i => (IReadOnlyList<PopupStoreMaterialEntry>)(i == 1 ? swapped : order)).ToArray();
        CollectionAssert.AreEqual(Enumerable.Range(0, count),
            PopupStoreUnorderedOrderMatch.FindExactSlots(Catalog(), orders, order.Reverse().ToArray()));
    }

    [Test]
    public void QueryRejectsMoreThanThreeSlotsAndSkipsMalformedIndividualOrders()
    {
        var order = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        Assert.That(PopupStoreUnorderedOrderMatch.FindExactSlots(Catalog(),
            Enumerable.Repeat((IReadOnlyList<PopupStoreMaterialEntry>)order, 4).ToArray(), order), Is.Empty);
        IReadOnlyList<PopupStoreMaterialEntry>[] mixed = { null, Entries("STAFF01,BOX_PINK,BOX_BLUE"), order };
        CollectionAssert.AreEqual(new[] { 2 }, PopupStoreUnorderedOrderMatch.FindExactSlots(Catalog(), mixed, order));
    }

    [TestCase("RIBBON_YELLOW", "0001")]
    [TestCase("BOX_PINK,STAFF01", "1010")]
    [TestCase("STAFF01,STAFF01,RIBBON_YELLOW", "1101")]
    [TestCase("STAFF01,STAFF01,STAFF01,BOX_PINK,RIBBON_YELLOW", "1111")]
    public void MatchMaskConsumesDuplicateCountsFromLeftToRightWithoutPrefixAssumption(string csv, string expected)
    {
        var order = Entries("STAFF01,STAFF01,BOX_PINK,RIBBON_YELLOW");
        var counter = Entries(csv);
        var before = counter.ToArray();
        var mask = PopupStoreUnorderedOrderMatch.MatchMask(order, counter);
        Assert.That(string.Concat(mask.Select(x => x ? "1" : "0")), Is.EqualTo(expected));
        CollectionAssert.AreEqual(before, counter);
        CollectionAssert.AreEqual(mask, PopupStoreUnorderedOrderMatch.MatchMask(order, counter));
    }

    [Test]
    public void MaskRejectsNullOrForgedMaterialsAndTreatsEachOrderIndependently()
    {
        var order = Entries("STAFF01,BOX_PINK,RIBBON_YELLOW");
        var bad = new[] { MakeEntry("STAFF01", PopupStoreMaterialKind.Box), null, MakeEntry("staff01", PopupStoreMaterialKind.Plush) };
        Assert.That(PopupStoreUnorderedOrderMatch.MatchMask(order, bad).Any(x => x), Is.False);
        var counter = Entries("RIBBON_YELLOW");
        for (int i = 0; i < 3; i++) CollectionAssert.AreEqual(new[] { false, false, true }, PopupStoreUnorderedOrderMatch.MatchMask(order, counter));
    }

    [TestCase("STAFF01", "BOX_PINK", "RIBBON_YELLOW", 100)]
    [TestCase("STAFF01,STAFF02", "BOX_BLUE", "RIBBON_PINK", 140)]
    [TestCase("STAFF03,STAFF05,STAFF02", "BOX_MINT", "RIBBON_MINT", 180)]
    [TestCase("STAFF01,STAFF01", "BOX_BLUE", "RIBBON_PINK", 140)]
    public void ReverseInputCommitsOnceKeepsReceiptOrderAndScoresByKinds(string plush, string box, string ribbon, int points)
    {
        using (var h = new Host(new[] { Order(plush, box, ribbon) }))
        {
            var input = plush.Split(',').Concat(new[] { box, ribbon }).Reverse().ToArray();
            var canonical = h.Display.GetDisplayedMaterials(0).ToArray();
            h.Fill(input);
            int publications = 0;
            PopupStorePackingStatus nested = PopupStorePackingStatus.None;
            h.Packing.SucceededCommitted += _ => { publications++; nested = h.Packing.TryPack().Status; };
            var result = h.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.Zero);
            Assert.That(result.Receipt.OrderId, Is.EqualTo(h.Orders[0].RuntimeId));
            CollectionAssert.AreEqual(input, result.Receipt.Materials.Select(x => x.Id));
            Assert.That(result.Receipt.BoxId, Is.EqualTo(box));
            Assert.That(result.Receipt.RibbonId, Is.EqualTo(ribbon));
            Assert.That(PopupStoreScore.TryCalculate(result.Receipt, out int count, out int calculated, out _), Is.True);
            Assert.That(count, Is.EqualTo(plush.Split(',').Length));
            Assert.That(calculated, Is.EqualTo(points));
            Assert.That(h.Score.Refresh(), Is.True);
            Assert.That(h.Score.TotalScore, Is.EqualTo(points));
            Assert.That(h.Score.SuccessfulOrderCount, Is.EqualTo(1));
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(publications, Is.EqualTo(1));
            Assert.That(nested, Is.EqualTo(PopupStorePackingStatus.Busy));
            Assert.That(h.Packing.TryPack().Success, Is.False);
            Assert.That(h.Packing.SuccessfulPackCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(plush.Split(',').Concat(new[] { box, ribbon }), canonical.Select(x => x.Id));
            Assert.That(h.Display.TryGetPackedDeliveryTarget(result.Receipt, out var target, out _), Is.True);
            Assert.That(target, Is.SameAs(h.Display.Slots[0].CustomerImage.rectTransform));
            Assert.That(h.Packing.TryTakeReceipt(out var one), Is.True);
            Assert.That(one, Is.SameAs(result.Receipt));
            Assert.That(h.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [TestCase(9d, 4d, 7d, 1)]
    [TestCase(4d, 4d, 4d, 0)]
    [TestCase(9d, 4d, 4d, 1)]
    public void MultipleCurrentTimersChooseShortestThenLeftmostOnly(double first, double second, double third, int target)
    {
        using (var h = new Host(ThreeIdentical(), true))
        {
            h.SetRemaining(0, first); h.SetRemaining(1, second); h.SetRemaining(2, third);
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            Assert.That(h.Display.QueryExactMatches(h.Counter).Candidates.Count, Is.EqualTo(3));
            var result = h.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(target));
            Assert.That(h.Orders.Count(x => x.IsPacked), Is.EqualTo(1));
            Assert.That(h.Packing.CommittedReceipts.Count, Is.EqualTo(1));
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(h.Score.Refresh(), Is.True);
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Display.TryGetPackedDeliveryTarget(result.Receipt, out var image, out _), Is.True);
            Assert.That(image, Is.SameAs(h.Display.Slots[target].CustomerImage.rectTransform));
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.Zero);
        }
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(-1d)]
    [TestCase(0d)]
    public void InvalidExistingTimerCannotWinRecipientPriority(double invalid)
    {
        using (var h = new Host(ThreeIdentical(), true))
        {
            h.SetRemaining(0, invalid); h.SetRemaining(1, 5); h.SetRemaining(2, 9);
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            var result = h.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(1));
            Assert.That(h.Orders[0].IsPacked, Is.False);
        }
    }

    [Test]
    public void PureModelWithoutTimersUsesLowestSlotRegardlessOfCandidateEnumeration()
    {
        using (var h = new Host(ThreeIdentical()))
        {
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            var reversed = new PopupStoreExactMatchResult(h.Display.QueryExactMatches(h.Counter).Candidates.Reverse().ToArray());
            Assert.That(Call<bool>(h.Counter, "BeginPackingLock"), Is.True);
            Assert.That(Call<bool>(h.Display, "BeginPackingLock"), Is.True);
            try
            {
                object[] args = { reversed, null };
                Assert.That(Call<bool>(h.Display, "TrySelectPackingCandidate", args), Is.True);
                Assert.That(((PopupStoreExactOrderCandidate)args[1]).SlotIndex, Is.Zero);
            }
            finally { Call(h.Display, "EndPackingLock"); Call(h.Counter, "EndPackingLock"); }
        }
    }

    [Test]
    public void MissingTimerAmongLiveTimersDoesNotBecomeSlotPriorityFallback()
    {
        using (var h = new Host(ThreeIdentical(), true))
        {
            ((PopupStorePatienceTimer[])Read(h.Patience, "_timers"))[0] = null;
            h.SetRemaining(1, 5); h.SetRemaining(2, 9);
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            var result = h.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(1));
            Assert.That(h.Orders[0].IsPacked, Is.False);
        }
    }

    [Test]
    public void StoppedCurrentTimerWithPositiveRemainingCannotWinRecipientPriority()
    {
        using (var h = new Host(ThreeIdentical(), true))
        {
            h.SetRemaining(0, 1); h.SetRemaining(1, 5); h.SetRemaining(2, 9);
            var stopped = h.Patience.GetTimer(0);
            Write(stopped, "Eligible", false);
            Assert.That(h.Patience.IsCurrent(stopped), Is.True);
            Assert.That(stopped.RemainingPatience, Is.GreaterThan(0));
            Assert.That(stopped.IsRunning, Is.False);
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            var result = h.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Receipt.SlotIndex, Is.EqualTo(1));
            Assert.That(h.Orders[0].IsPacked, Is.False);
            Assert.That(h.Orders[1].IsPacked, Is.True);
            Assert.That(h.Orders[2].IsPacked, Is.False);
            Assert.That(h.Packing.CommittedReceipts.Count, Is.EqualTo(1));
            Assert.That(h.Counter.Count, Is.Zero);
        }
    }

    [Test]
    public void DirectCommitCannotBypassShortestRecipientSelection()
    {
        using (var h = new Host(ThreeIdentical(), true))
        {
            h.SetRemaining(0, 9); h.SetRemaining(1, 3); h.SetRemaining(2, 8);
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            var wrong = h.Display.QueryExactMatches(h.Counter).Candidates[0];
            var before = h.Counter.Materials.ToArray();
            Assert.That(Call<bool>(h.Counter, "BeginPackingLock"), Is.True);
            Assert.That(Call<bool>(h.Display, "BeginPackingLock"), Is.True);
            try
            {
                object[] commit = { h.Counter, wrong, null };
                Assert.That(Call<bool>(h.Display, "TryCommitPacking", commit), Is.False);
                Assert.That(commit[2], Is.Null);
                CollectionAssert.AreEqual(before, h.Counter.Materials);
                Assert.That(h.Orders.Any(x => x.IsPacked), Is.False);
            }
            finally { Call(h.Display, "EndPackingLock"); Call(h.Counter, "EndPackingLock"); }
            Assert.That(h.Packing.TryPack().Receipt.SlotIndex, Is.EqualTo(1), "Rejected direct commit releases its lock for the next explicit request.");
        }
    }

    [Test]
    public void SelectedRecipientExpiringOnFinalClockSampleRejectsWithoutRetargeting()
    {
        using (var h = new Host(ThreeIdentical(), true))
        {
            double now = 0;
            Write(h.Clock, "_timeSource", new Func<double>(() => now));
            h.Clock.SynchronizeNow();
            h.SetRemaining(0, 1); h.SetRemaining(1, 10); h.SetRemaining(2, 12);
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            var before = h.Counter.Materials.ToArray();
            Assert.That(Call<bool>(h.Counter, "BeginPackingLock"), Is.True);
            Assert.That(Call<bool>(h.Display, "BeginPackingLock"), Is.True);
            try
            {
                object[] select = { h.Display.QueryExactMatches(h.Counter), null };
                Assert.That(Call<bool>(h.Display, "TrySelectPackingCandidate", select), Is.True);
                Assert.That(((PopupStoreExactOrderCandidate)select[1]).SlotIndex, Is.Zero);
                now = 1;
                object[] commit = { h.Counter, select[1], null };
                Assert.That(Call<bool>(h.Display, "TryCommitPacking", commit), Is.False);
                Assert.That(commit[2], Is.Null);
                Assert.That(h.Orders[0].IsExpired, Is.True);
                Assert.That(h.Orders.Any(x => x.IsPacked), Is.False);
                CollectionAssert.AreEqual(before, h.Counter.Materials);
            }
            finally { Call(h.Display, "EndPackingLock"); Call(h.Counter, "EndPackingLock"); }
            Assert.That(h.Packing.TryPack().Receipt.SlotIndex, Is.EqualTo(1), "Only a separate later request can choose a different customer.");
        }
    }

    [TestCase("order-permutation")]
    [TestCase("customer")]
    [TestCase("replacement")]
    public void StrictDisplayedLiveSnapshotChangeRejectsUnorderedPacking(string mutation)
    {
        using (var h = new Host(new[] { Order("STAFF01,STAFF02") }))
        {
            h.Fill("RIBBON_YELLOW", "STAFF02", "BOX_PINK", "STAFF01");
            var before = h.Counter.Materials.ToArray();
            if (mutation == "order-permutation") Write(h.Orders[0], "_plushIds", new[] { "STAFF02", "STAFF01" });
            else if (mutation == "customer") Write(h.Orders[0], "_customerId", "CUSTOMER04");
            else h.Orders[0] = Order("STAFF01,STAFF02");
            Assert.That(h.Packing.TryPack().Success, Is.False);
            CollectionAssert.AreEqual(before, h.Counter.Materials);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
        }
    }

    [TestCase("expired")]
    [TestCase("replacement")]
    public void SelectedRecipientInvalidationCannotRetargetTheSameCommit(string mutation)
    {
        using (var h = new Host(ThreeIdentical(), true))
        {
            h.SetRemaining(0, 2); h.SetRemaining(1, 8); h.SetRemaining(2, 10);
            h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            var before = h.Counter.Materials.ToArray();
            Assert.That(Call<bool>(h.Counter, "BeginPackingLock"), Is.True);
            Assert.That(Call<bool>(h.Display, "BeginPackingLock"), Is.True);
            try
            {
                object[] select = { h.Display.QueryExactMatches(h.Counter), null };
                Assert.That(Call<bool>(h.Display, "TrySelectPackingCandidate", select), Is.True);
                var candidate = (PopupStoreExactOrderCandidate)select[1];
                Assert.That(candidate.SlotIndex, Is.Zero);
                if (mutation == "expired") SetProperty(h.Patience.GetTimer(0), "IsExpired", true);
                else h.Orders[0] = Order("STAFF01");
                object[] commit = { h.Counter, candidate, null };
                Assert.That(Call<bool>(h.Display, "TryCommitPacking", commit), Is.False);
                Assert.That(commit[2], Is.Null);
                CollectionAssert.AreEqual(before, h.Counter.Materials);
                Assert.That(h.Orders.Any(x => x.IsPacked), Is.False);
            }
            finally { Call(h.Display, "EndPackingLock"); Call(h.Counter, "EndPackingLock"); }
            Assert.That(h.Packing.IsReady, Is.True);
        }
    }

    [TestCase("zero")]
    [TestCase("paused")]
    [TestCase("hidden")]
    [TestCase("abandoned")]
    [TestCase("unapproved")]
    public void DirectPackingApiRetainsAllBusinessGates(string gate)
    {
        using (var h = new Host(new[] { Order("STAFF01") }, false, gate != "unapproved"))
        {
            // Seed the existing counter while approved; unapproved input itself remains blocked.
            if (gate != "unapproved") h.Fill("RIBBON_YELLOW", "BOX_PINK", "STAFF01");
            if (gate == "zero") h.Clock.AdvanceTime(90);
            else if (gate == "paused") h.Clock.SetManualPaused(true);
            else if (gate == "hidden") h.View.Hide();
            else if (gate == "abandoned") Assert.That(Call<bool>(h.Clock, "TryAbandonSession"), Is.True);
            var before = h.Counter.Materials.ToArray();
            Assert.That(h.Packing.TryPack().Success, Is.False);
            CollectionAssert.AreEqual(before, h.Counter.Materials);
            Assert.That(h.Packing.SuccessfulPackCount, Is.Zero);
        }
    }

    [Test]
    public void ActualProgressIsUnorderedSilentOnExcessClearAndRefresh()
    {
        using (var h = new Host(new[] { Order("STAFF01,STAFF01") }))
        {
            int feedback = 0;
            h.Progress.FeedbackTriggered += () => feedback++;
            h.Fill("RIBBON_YELLOW", "STAFF01");
            CollectionAssert.AreEqual(new[] { true, false, false, true }, h.CircleMask(0, 4));
            Assert.That(feedback, Is.EqualTo(2));
            h.Fill("STAFF01", "STAFF01");
            CollectionAssert.AreEqual(new[] { true, true, false, true }, h.CircleMask(0, 4));
            Assert.That(feedback, Is.EqualTo(3), "A third identical plush adds no new fulfilled quantity.");
            for (int i = 0; i < 3; i++) { h.Progress.Refresh(); h.Packing.Refresh(); }
            Assert.That(feedback, Is.EqualTo(3));
            Assert.That(h.Packing.TryPack().Success, Is.False);
            h.Counter.Clear();
            Assert.That(h.CircleMask(0, 4).Any(x => x), Is.False);
            Assert.That(feedback, Is.EqualTo(3));
            h.Fill("BOX_PINK", "STAFF01", "RIBBON_YELLOW", "STAFF01");
            Assert.That(h.Packing.TryPack().Success, Is.True);
            Assert.That(h.CircleMask(0, 4).Any(x => x), Is.False);
            Assert.That(feedback, Is.EqualTo(7));
        }
    }

    [Test]
    public void AuthoredSignUsesOriginalYoungdeokCrabFontAndIndependentNonBlockingTwoLineText()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var signs = prefab.GetComponentsInChildren<TMP_Text>(true).Where(x => x.gameObject.name == "Popup Store Sign Text").ToArray();
        Assert.That(signs.Length, Is.EqualTo(1));
        var sign = signs[0];
        Assert.That(sign.text, Is.EqualTo("POPUP\nSTORE"));
        Assert.That(sign.transform.parent.name, Is.EqualTo("Approved Clean Sign"));
        Assert.That(sign.raycastTarget, Is.False);
        Assert.That(sign.enableAutoSizing, Is.False);
        Assert.That(sign.fontSize, Is.GreaterThanOrEqualTo(24));
        Assert.That(AssetDatabase.GetAssetPath(sign.font), Is.EqualTo("Assets/TextMesh Pro/Fonts/영덕대게체.asset"));
        Assert.That(sign.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        foreach (char c in "POUSTRE ") Assert.That(sign.font.HasCharacter(c, false, false), Is.True, c.ToString());
        Assert.That(sign.GetComponent<Button>(), Is.Null);
        Assert.That(sign.GetComponent<Image>(), Is.Null);
        Assert.That(sign.color.r, Is.GreaterThan(sign.color.b));
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreOrderDisplay Display;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStoreScore Score;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerPatience Patience;
        internal PopupStoreOrder[] Orders => (PopupStoreOrder[])Read(Display, "_sampleOrders");
        internal Host(PopupStoreOrder[] orders, bool timers = false, bool approve = true)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-unordered-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>(); Display = Root.GetComponent<PopupStoreOrderDisplay>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true); Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>(); Score = Root.GetComponent<PopupStoreScore>();
                Clock = Root.GetComponent<PopupStoreBusinessClock>(); Patience = Root.GetComponent<PopupStoreCustomerPatience>();
                Write(Root.GetComponent<PopupStoreCustomerEntrance>(), "_automaticEntry", false);
                Write(Root.GetComponent<PopupStoreCustomerEntrance>(), "_fixedTestSupply", false);
                Write(Display, "_sampleOrders", orders);
                View.Init(); View.Show();
                if (approve) Assert.That(PopupStoreApprovedTestSession.Start(Root), Is.True);
                Assert.That(Counter.Refresh(), Is.True);
                Assert.That(Display.Refresh(), Is.True);
                Assert.That(Progress.Refresh(), Is.True);
                Assert.That(Packing.Refresh(), Is.True);
                Assert.That(Score.Refresh(), Is.True);
                if (timers) Assert.That(Patience.Refresh(), Is.True);
            }
            catch { Dispose(); throw; }
        }
        internal void Fill(params string[] ids) { foreach (var id in ids) Assert.That(Counter.TryAdd(Entry(id)), Is.True, id); }
        internal void SetRemaining(int slot, double value)
        {
            Assert.That(Patience.GetTimer(slot), Is.Not.Null);
            SetProperty(Patience.GetTimer(slot), "RemainingPatience", value);
        }
        internal bool[] CircleMask(int slot, int count)
        {
            var data = ((Array)Read(Progress, "_slots")).GetValue(slot);
            return ((Image[])Read(data, "_circles")).Take(count).Select(x => x.gameObject.activeSelf).ToArray();
        }
        public void Dispose() { if (Root != null) Object.DestroyImmediate(Root); }
    }

    private static PopupStoreOrder[] ThreeIdentical() => new[] { Order("STAFF01"), Order("STAFF01", customer: "CUSTOMER04"), Order("STAFF01", customer: "CUSTOMER11") };
    private static PopupStoreOrder Order(string plush, string box = "BOX_PINK", string ribbon = "RIBBON_YELLOW", string customer = "CUSTOMER01")
    {
        var value = new PopupStoreOrder();
        Write(value, "_customerId", customer); Write(value, "_plushIds", plush.Split(','));
        Write(value, "_boxId", box); Write(value, "_ribbonId", ribbon); return value;
    }
    private static PopupStoreMaterialCatalog Catalog() => AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
    private static PopupStoreMaterialEntry Entry(string id) => ((PopupStoreMaterialEntry[])Read(Catalog(), "_entries")).Single(x => x.Id == id);
    private static PopupStoreMaterialEntry[] Entries(string csv) => string.IsNullOrEmpty(csv) ? Array.Empty<PopupStoreMaterialEntry>() : csv.Split(',').Select(Entry).ToArray();
    private static string Ids(IEnumerable<PopupStoreMaterialEntry> entries) => string.Join(",", entries.Select(x => x.Id));
    private static PopupStoreMaterialEntry MakeEntry(string id, PopupStoreMaterialKind kind)
    { var entry = new PopupStoreMaterialEntry(); Write(entry, "_id", id); Write(entry, "_kind", kind); Write(entry, "_sprite", Entry("STAFF01").Sprite); return entry; }
    private static IEnumerable<PopupStoreMaterialEntry[]> Permutations(PopupStoreMaterialEntry[] items)
    {
        if (items.Length == 0) { yield return Array.Empty<PopupStoreMaterialEntry>(); yield break; }
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Length; i++)
        {
            if (!used.Add(items[i].Kind + ":" + items[i].Id)) continue;
            foreach (var rest in Permutations(items.Where((_, j) => j != i).ToArray())) yield return new[] { items[i] }.Concat(rest).ToArray();
        }
    }
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    { var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null, name); return field; }
    private static void SetProperty(object target, string name, object value)
        => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static T Call<T>(object target, string name, params object[] args) => (T)Call(target, name, args);
}
