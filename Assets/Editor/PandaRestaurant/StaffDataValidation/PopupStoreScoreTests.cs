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

/// <summary>Local score accounting against real packing receipts and the authored popup. No account writes or scenes.</summary>
public sealed class PopupStoreScoreTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";

    [Test]
    public void AuthoredScoreUsesOnlyTheExistingOwnedHudAndStartsWithNoAwards()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var authored = prefab.GetComponent<PopupStoreScore>();
        Assert.That(authored, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PopupStoreScore>(true), Has.Length.EqualTo(1));
        Assert.That(Read(authored, "_packing"), Is.SameAs(prefab.GetComponent<PopupStorePacking>()));
        var text = (TMP_Text)Read(authored, "_scoreText");
        Assert.That(text.name, Is.EqualTo("Score"));
        Assert.That(text.GetComponentInParent<UIPopupStore>(true), Is.SameAs(prefab.GetComponent<UIPopupStore>()));
        Assert.That(Read(authored, "_format"), Is.EqualTo("점수 {0}"));
        using (var host = new Host())
        {
            Assert.That(host.Hud.text, Is.EqualTo("점수 0"));
            Assert.That(host.Hud.font, Is.SameAs(text.font));
            Assert.That(host.Hud.rectTransform.sizeDelta, Is.EqualTo(text.rectTransform.sizeDelta));
            Assert.That(host.Hud.rectTransform.anchoredPosition, Is.EqualTo(text.rectTransform.anchoredPosition));
            host.AssertNoScore();
        }
    }

    [TestCase("STAFF01", 100)]
    [TestCase("STAFF01,STAFF02", 140)]
    [TestCase("STAFF03,STAFF05,STAFF02", 180)]
    [TestCase("STAFF01,STAFF01", 140)]
    [TestCase("STAFF01,STAFF01,STAFF01", 180)]
    public void RealSuccessfulPackingAwardsByActualPlushCountBeforeAnyVisual(string plush, int points)
    {
        using (var host = new Host(orders: new[] { Order("CUSTOMER01", plush) }))
        {
            host.StartCustomers(1);
            host.Fill(0);
            int feedback = host.FeedbackCount;
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            Assert.That(host.FeedbackCount, Is.EqualTo(feedback));
            AssertAward(host, result.Receipt, plush.Split(',').Length, points);
            Assert.That(host.Hud.text, Is.EqualTo("점수 " + points));
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True, "Score must not consume delivery's receipt.");
            Assert.That(queued, Is.SameAs(result.Receipt));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [Test]
    public void EveryApprovedPlushAndBoxRibbonColourKeepsTheSameBasePoints()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
        Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Plush, out var plushes, out _), Is.True);
        Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Box, out var boxes, out _), Is.True);
        Assert.That(catalog.TryGetMaterials(PopupStoreMaterialKind.Ribbon, out var ribbons, out _), Is.True);
        Assert.That(plushes.Length, Is.EqualTo(32));
        foreach (var plush in plushes)
            foreach (var box in boxes)
                foreach (var ribbon in ribbons)
                {
                    var receipt = new PopupStorePackingReceipt("calculation-only", 0, "CUSTOMER01", new[] { plush, box, ribbon });
                    Assert.That(PopupStoreScore.TryCalculate(receipt, out int count, out int points, out var error), Is.True, error);
                    Assert.That(count, Is.EqualTo(1), plush.Id + "/" + box.Id + "/" + ribbon.Id);
                    Assert.That(points, Is.EqualTo(100));
                }
    }

    [TestCase("null")]
    [TestCase("receipt-id")]
    [TestCase("order-id")]
    [TestCase("customer-id")]
    [TestCase("negative-slot")]
    [TestCase("fourth-slot")]
    [TestCase("too-short")]
    [TestCase("too-long")]
    [TestCase("wrong-sequence")]
    [TestCase("wrong-kind")]
    [TestCase("unknown-plush")]
    [TestCase("unknown-box")]
    [TestCase("unknown-ribbon")]
    [TestCase("box-contradiction")]
    [TestCase("ribbon-contradiction")]
    public void CalculationRejectsMalformedReceiptIdentityShapeAndMaterialMapping(string defect)
    {
        var materials = new List<PopupStoreMaterialEntry> { Entry("STAFF01"), Entry("BOX_PINK"), Entry("RIBBON_YELLOW") };
        string order = "calculation-only", customer = "CUSTOMER01";
        int slot = 0;
        if (defect == "order-id") order = " ";
        if (defect == "customer-id") customer = null;
        if (defect == "negative-slot") slot = -1;
        if (defect == "fourth-slot") slot = 3;
        if (defect == "too-short") materials.RemoveAt(0);
        if (defect == "too-long") materials.InsertRange(0, new[] { Entry("STAFF02"), Entry("STAFF03"), Entry("STAFF04") });
        if (defect == "wrong-sequence") materials.Reverse();
        if (defect == "wrong-kind") materials[0] = Unregistered("STAFF01", PopupStoreMaterialKind.Box);
        if (defect == "unknown-plush") materials[0] = Unregistered("STAFF33", PopupStoreMaterialKind.Plush);
        if (defect == "unknown-box") materials[1] = Unregistered("BOX_RED", PopupStoreMaterialKind.Box);
        if (defect == "unknown-ribbon") materials[2] = Unregistered("RIBBON_RED", PopupStoreMaterialKind.Ribbon);
        var receipt = defect == "null" ? null : new PopupStorePackingReceipt(order, slot, customer, materials);
        if (defect == "receipt-id") Write(receipt, "<ReceiptId>k__BackingField", "");
        if (defect == "box-contradiction") Write(receipt, "<BoxId>k__BackingField", "BOX_BLUE");
        if (defect == "ribbon-contradiction") Write(receipt, "<RibbonId>k__BackingField", "RIBBON_MINT");
        Assert.That(PopupStoreScore.TryCalculate(receipt, out int count, out int points, out var error), Is.False);
        Assert.That(count, Is.Zero);
        Assert.That(points, Is.Zero);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void AValidShapeIsNotProofOfSaleAndForgedMatchingIdentityCannotReceiveCredit()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var materials = host.Resolve(host.SourceOrders[0]);
            var fake = new PopupStorePackingReceipt(host.SourceOrders[0].RuntimeId, 0, host.SourceOrders[0].CustomerId, materials);
            Assert.That(PopupStoreScore.TryCalculate(fake, out _, out _, out _), Is.True);
            Assert.That(host.Score.TryRecordReceipt(null), Is.False);
            Assert.That(host.Score.TryRecordReceipt(fake), Is.False);
            Assert.That(host.Packing.IsCommittedReceipt(fake), Is.False);
            host.AssertNoScore();
            host.Fill(0);
            var actual = host.Packing.TryPack().Receipt;
            Write(fake, "<ReceiptId>k__BackingField", actual.ReceiptId);
            Assert.That(host.Packing.IsCommittedReceipt(fake), Is.False, "ID equality cannot replace reference authentication.");
            Assert.That(host.Score.TryRecordReceipt(fake), Is.False);
            AssertAward(host, actual, 1, 100);
        }
    }

    [Test]
    public void AnotherPopupAuthenticReceiptAndForeignPackingSourceCannotContaminateThisLedger()
    {
        using (var owner = new Host())
        using (var other = new Host())
        {
            other.StartCustomers();
            other.Fill(0);
            var receipt = other.Packing.TryPack().Receipt;
            Assert.That(other.Packing.IsCommittedReceipt(receipt), Is.True);
            Assert.That(owner.Score.TryRecordReceipt(receipt), Is.False);
            Write(owner.Score, "_packing", other.Packing);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreScore: The packing source must belong to this popup"));
            Assert.That(owner.Score.Refresh(), Is.False);
            owner.AssertNoScore();
            AssertAward(other, receipt, 1, 100);
            Write(owner.Score, "_packing", owner.Packing);
            Assert.That(owner.Score.Refresh(), Is.True);
            owner.AssertNoScore();
        }
    }

    [TestCase("None")]
    [TestCase("Multiple")]
    [TestCase("Stale")]
    [TestCase("NotStarted")]
    [TestCase("Paused")]
    [TestCase("Ended")]
    public void RealRejectedPackingNeverCreatesScoreOrCommittedHistory(string reason)
    {
        var orders = reason == "Multiple"
            ? new[] { Order("CUSTOMER01", "STAFF01"), Order("CUSTOMER04", "STAFF01") }
            : new[] { Order("CUSTOMER01", "STAFF01") };
        using (var host = new Host(orders: orders))
        {
            if (reason != "NotStarted") host.StartCustomers(orders.Length);
            if (reason != "None") host.Fill(0);
            if (reason == "Stale") Write(host.SourceOrders[0], "_plushIds", new[] { "STAFF02" });
            if (reason == "Paused") host.Clock.SetManualPaused(true);
            if (reason == "Ended") host.Clock.AdvanceTime(90);
            var before = host.Counter.Materials.ToArray();
            var result = host.Packing.TryPack();
            var expected = reason == "None" ? PopupStorePackingStatus.None
                : reason == "Multiple" ? PopupStorePackingStatus.Multiple
                : reason == "Stale" ? PopupStorePackingStatus.Stale : PopupStorePackingStatus.Unavailable;
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Receipt, Is.Null);
            Assert.That(host.Packing.SuccessfulPackCount, Is.Zero);
            Assert.That(host.Packing.CommittedReceipts, Is.Empty);
            CollectionAssert.AreEqual(before, host.Counter.Materials);
            host.AssertNoScore();
        }
    }

    [Test]
    public void ReentrantBusyAttemptAndRepeatedPublicationCannotAwardOrConsumeTwice()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            PopupStorePackingResult nested = null;
            int publications = 0;
            host.Packing.SucceededCommitted += receipt =>
            {
                publications++;
                nested = host.Packing.TryPack();
                Assert.That(host.Score.TryRecordReceipt(receipt), Is.False);
                Assert.That(host.Score.Refresh(), Is.True);
                host.Score.RefreshHud();
            };
            var actual = host.Packing.TryPack();
            Assert.That(actual.Success, Is.True);
            Assert.That(nested.Status, Is.EqualTo(PopupStorePackingStatus.Busy));
            Assert.That(publications, Is.EqualTo(1));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Packing.CommittedReceipts.Count, Is.EqualTo(1));
            AssertAward(host, actual.Receipt, 1, 100);
            for (int i = 0; i < 3; i++) Assert.That(host.Packing.TryPack().Success, Is.False);
            Assert.That(host.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void ScoreIsCommittedBeforeExistingCounterPublicationCanEndBusiness()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            int scoreObserved = -1, countObserved = -1;
            host.Counter.Changed += reason =>
            {
                if (reason != PopupStoreSalesCounter.ChangeReason.Packed) return;
                scoreObserved = host.Score.TotalScore;
                countObserved = host.Score.SuccessfulOrderCount;
                host.Clock.AdvanceTime(90);
            };
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(scoreObserved, Is.EqualTo(100));
            Assert.That(countObserved, Is.EqualTo(1));
            Assert.That(host.Clock.State, Is.EqualTo(PopupStoreBusinessState.Ended));
            AssertAward(host, result.Receipt, 1, 100);
            host.CompleteVisual();
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void ExpirationDuringFinalPackingValidationNeverAwardsAStaleTransaction()
    {
        using (var host = new Host(new[] { 1d, 60d, 60d }))
        {
            Write(host.Clock, "_timeSource", new Func<double>(() => 0));
            host.StartCustomers();
            host.Fill(0);
            int reads = 0;
            Write(host.Clock, "_timeSource", new Func<double>(() => ++reads == 1 ? 0 : 1));
            Assert.That(host.Packing.TryPack().Status, Is.EqualTo(PopupStorePackingStatus.Stale));
            Assert.That(host.SourceOrders[0].IsExpired, Is.True);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Packing.CommittedReceipts, Is.Empty);
            Assert.That(host.Counter.Count, Is.EqualTo(3));
            host.AssertNoScore();
        }
    }

    [Test]
    public void ComplaintAddsNoScoreAndDoesNotSubtractAnEarlierSuccessfulSale()
    {
        using (var host = new Host(new[] { 60d, 1d, 60d }))
        {
            host.StartCustomers();
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            host.CompleteVisual();
            host.Clock.AdvanceTime(1);
            Assert.That(host.SourceOrders[1].IsExpired, Is.True);
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            Assert.That(host.Entrance.StartedComplaintCount, Is.EqualTo(1));
            host.Fill(1);
            Assert.That(host.Packing.TryPack().Success, Is.False);
            Assert.That(host.Counter.Count, Is.EqualTo(4));
            AssertAward(host, receipt, 1, 100);
            Assert.That(host.Score.GetSuccessfulCount(2), Is.Zero);
        }
    }

    [TestCase("Paused")]
    [TestCase("Ended")]
    public void TemporarilyDisabledObserverRecoversAuthenticSuccessEvenWhenBusinessNoLongerRuns(string state)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Score.enabled = false;
            Invoke(host.Score, "OnDisable");
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            Assert.That(receipt, Is.Not.Null);
            host.AssertNoScore();
            if (state == "Ended") host.Clock.AdvanceTime(90);
            else host.Clock.SetManualPaused(true);
            host.Score.enabled = true;
            Invoke(host.Score, "OnEnable");
            AssertAward(host, receipt, 1, 100);
            Assert.That(host.Score.Refresh(), Is.True);
            Assert.That(host.Score.TryRecordReceipt(receipt), Is.False);
            Assert.That(host.Packing.TryTakeReceipt(out var queued), Is.True);
            Assert.That(queued, Is.SameAs(receipt));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
        }
    }

    [Test]
    public void CounterEditsAndOrderSlotReplacementCannotChangeAnAlreadyAwardedReceiptSnapshot()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            var signature = string.Join(",", receipt.Materials.Select(value => value.Kind + ":" + value.Id));
            host.CompleteVisual();
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            for (int i = 0; i < 20 && !host.Entrance.IsWaiting(0); i++) host.Entrance.TickEntrance(.5f);
            Assert.That(host.SourceOrders[0].RuntimeId, Is.Not.EqualTo(receipt.OrderId));
            Assert.That(host.Counter.TryAdd(Entry("STAFF32")), Is.True);
            Assert.That(host.Counter.TryAdd(Entry("RIBBON_MINT")), Is.True);
            host.Counter.Clear();
            Assert.That(host.Score.Refresh(), Is.True);
            Assert.That(string.Join(",", receipt.Materials.Select(value => value.Kind + ":" + value.Id)), Is.EqualTo(signature));
            AssertAward(host, receipt, 1, 100);
        }
    }

    [Test]
    public void SameCustomerReturningToSameSlotWithANewReceiptEarnsAnotherAward()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            var first = host.Packing.TryPack().Receipt;
            host.CompleteVisual();
            host.Entrance.TickEntrance(host.ExitDuration + .01f);
            for (int i = 0; i < 20 && !host.Entrance.IsWaiting(0); i++) host.Entrance.TickEntrance(.5f);
            Assert.That(host.Entrance.IsWaiting(0), Is.True);
            host.Fill(0);
            var second = host.Packing.TryPack().Receipt;
            Assert.That(second, Is.Not.Null);
            Assert.That(second.CustomerId, Is.EqualTo(first.CustomerId));
            Assert.That(second.SlotIndex, Is.EqualTo(first.SlotIndex));
            Assert.That(second.OrderId, Is.Not.EqualTo(first.OrderId));
            Assert.That(second.ReceiptId, Is.Not.EqualTo(first.ReceiptId));
            Assert.That(host.Score.TotalScore, Is.EqualTo(200));
            Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(2));
            Assert.That(host.Score.GetSuccessfulCount(1), Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { first.ReceiptId, second.ReceiptId }, host.Score.Entries.Select(value => value.ReceiptId));
        }
    }

    [TestCase("queued")]
    [TestCase("moving")]
    [TestCase("delivered")]
    public void ClosingAndRepeatedReentryPreserveAwardWithoutReplayOrReceiptConsumption(string phase)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            if (phase == "moving")
            {
                host.Presentation.TickPresentation(0);
                for (int i = 0; i < 1000 && host.Presentation.CurrentPhase.ToString() != "Move"; i++) host.Presentation.TickPresentation(.005f);
                Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Move"));
            }
            if (phase == "delivered") host.CompleteVisual();
            int started = host.Presentation.StartedPresentationCount;
            for (int i = 0; i < 3; i++)
            {
                host.Close();
                host.Reopen();
                Assert.That(host.Score.Refresh(), Is.True);
                Assert.That(host.Score.TryRecordReceipt(receipt), Is.False);
            }
            AssertAward(host, receipt, 1, 100);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(started));
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            Assert.That(host.Packing.CommittedReceipts.Count, Is.EqualTo(1));
            host.Fill(1);
            Assert.That(host.Packing.TryPack().Success, Is.True);
            Assert.That(host.Score.TotalScore, Is.EqualTo(240));
            Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void FreshPopupStartsEmptyWhilePreviousInstanceRetainsOnlyItsOwnSessionScore()
    {
        var account = new AccountSnapshot();
        using (var original = new Host())
        {
            original.StartCustomers();
            original.Fill(0);
            var receipt = original.Packing.TryPack().Receipt;
            using (var fresh = new Host())
            {
                fresh.AssertNoScore();
                Assert.That(fresh.Packing.CommittedReceipts, Is.Empty);
                Assert.That(fresh.Score.TryRecordReceipt(receipt), Is.False);
                AssertAward(original, receipt, 1, 100);
                account.AssertUnchanged();
            }
        }
        account.AssertUnchanged();
    }

    [TestCase("missing")]
    [TestCase("bad-format")]
    public void BrokenHudCannotUndoTheSaleLoseScoreOrPreventOriginalDelivery(string defect)
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var hud = host.Hud;
            if (defect == "missing") Write(host.Score, "_scoreText", null);
            else Write(host.Score, "_format", "점수 {");
            host.Fill(0);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreScore: HUD update failed; score is preserved\\."));
            var result = host.Packing.TryPack();
            Assert.That(result.Success, Is.True);
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            AssertAward(host, result.Receipt, 1, 100);
            host.Score.RefreshHud();
            host.CompleteVisual();
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Write(host.Score, "_scoreText", hud);
            Write(host.Score, "_format", "점수 {0}");
            host.Score.RefreshHud();
            Assert.That(hud.text, Is.EqualTo("점수 100"));
        }
    }

    [Test]
    public void ForeignHudIsUntouchedWhileLocalSuccessfulTransactionStillScores()
    {
        using (var host = new Host())
        using (var other = new Host())
        {
            host.StartCustomers();
            var ownHud = host.Hud;
            string foreign = other.Hud.text;
            Write(host.Score, "_scoreText", other.Hud);
            host.Fill(0);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreScore: HUD update failed; score is preserved\\."));
            var receipt = host.Packing.TryPack().Receipt;
            AssertAward(host, receipt, 1, 100);
            Assert.That(other.Hud.text, Is.EqualTo(foreign));
            other.AssertNoScore();
            Write(host.Score, "_scoreText", ownHud);
            host.Score.RefreshHud();
            Assert.That(ownHud.text, Is.EqualTo("점수 100"));
        }
    }

    [Test]
    public void HiddenHudAndCustomInspectorFormatKeepAccountingAndExistingLayoutIntact()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            var hud = host.Hud;
            var font = hud.font;
            float size = hud.fontSize;
            var anchor = hud.rectTransform.anchoredPosition;
            var rectSize = hud.rectTransform.sizeDelta;
            hud.gameObject.SetActive(false);
            Write(host.Score, "_format", "판매 점수: {0:N0}");
            host.Fill(2);
            var receipt = host.Packing.TryPack().Receipt;
            AssertAward(host, receipt, 3, 180);
            hud.gameObject.SetActive(true);
            host.Score.RefreshHud();
            Assert.That(hud.text, Is.EqualTo("판매 점수: 180"));
            Assert.That(hud.font, Is.SameAs(font));
            Assert.That(hud.fontSize, Is.EqualTo(size));
            Assert.That(hud.rectTransform.anchoredPosition, Is.EqualTo(anchor));
            Assert.That(hud.rectTransform.sizeDelta, Is.EqualTo(rectSize));
        }
    }

    [Test]
    public void ThrowingEarlierObserverCannotPreventScoreOrExistingPackingNotifications()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            Invoke(host.Score, "OnDisable");
            Action<PopupStorePackingReceipt> broken = _ => throw new InvalidOperationException("score-test-observer-failure");
            host.Packing.SucceededCommitted += broken;
            Assert.That(host.Score.Refresh(), Is.True);
            int consumed = 0;
            host.Counter.Changed += reason => { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) consumed++; };
            host.Fill(0);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: score-test-observer-failure"));
            var result = host.Packing.TryPack();
            host.Packing.SucceededCommitted -= broken;
            Assert.That(result.Success, Is.True);
            Assert.That(consumed, Is.EqualTo(1));
            AssertAward(host, result.Receipt, 1, 100);
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.False);
            host.CompleteVisual();
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void MalformedCommittedReceiptIsDiagnosedWithoutRollbackAndCanBeReviewedWithoutReconsuming()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Score.enabled = false;
            Invoke(host.Score, "OnDisable");
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            string originalBox = receipt.BoxId;
            // Fault injection is confined to this in-memory receipt; no production or catalog object is edited.
            Write(receipt, "<BoxId>k__BackingField", "BOX_BLUE");
            LogAssert.Expect(LogType.Error, new Regex("^PopupStoreScore: committed receipt was not scored; receipt="));
            Assert.That(host.Score.TryRecordReceipt(receipt), Is.False);
            Assert.That(host.Score.TryRecordReceipt(receipt), Is.False);
            host.AssertNoScore();
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Counter.Count, Is.Zero);
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
            Write(receipt, "<BoxId>k__BackingField", originalBox);
            Assert.That(host.Score.Refresh(), Is.True);
            AssertAward(host, receipt, 1, 100);
            host.CompleteVisual();
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
        }
    }

    [Test]
    public void PublicLedgerHistoryAndReceiptMaterialsCannotBeMutatedByConsumers()
    {
        using (var host = new Host())
        {
            host.StartCustomers();
            host.Fill(0);
            var receipt = host.Packing.TryPack().Receipt;
            var entry = host.Score.Entries[0];
            Assert.Throws<NotSupportedException>(() => ((IList<PopupStoreScoreEntry>)host.Score.Entries).Clear());
            Assert.Throws<NotSupportedException>(() => ((ICollection<string>)host.Score.ProcessedReceiptIds).Remove(receipt.ReceiptId));
            Assert.Throws<NotSupportedException>(() => ((IList<PopupStorePackingReceipt>)host.Packing.CommittedReceipts).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<PopupStorePackedMaterial>)receipt.Materials)[0] = new PopupStorePackedMaterial(PopupStoreMaterialKind.Plush, "STAFF32"));
            foreach (var property in typeof(PopupStoreScoreEntry).GetProperties()) Assert.That(property.GetSetMethod(true), Is.Null, property.Name);
            Assert.That(host.Score.TryGetEntry(null, out _), Is.False);
            Assert.That(host.Score.TryGetEntry("absent", out _), Is.False);
            Assert.That(host.Score.TryGetEntry(receipt.ReceiptId, out var lookedUp), Is.True);
            Assert.That(lookedUp, Is.SameAs(entry));
            Assert.That(host.Score.GetSuccessfulCount(0), Is.Zero);
            Assert.That(host.Score.GetSuccessfulCount(4), Is.Zero);
            AssertAward(host, receipt, 1, 100);
        }
    }

    [Test]
    public void EveryOneOfTheTwentyRealSuppliedOrdersScoresExactly2640WithoutConsumingDeliveryOrChangingAccount()
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            var templates = (PopupStoreOrder[])Read(host.Entrance, "_testOrders");
            Assert.That(templates.Count(value => value.PlushIds.Count == 1), Is.EqualTo(8));
            Assert.That(templates.Count(value => value.PlushIds.Count == 2), Is.EqualTo(8));
            Assert.That(templates.Count(value => value.PlushIds.Count == 3), Is.EqualTo(4));
            int objects = host.Root.GetComponentsInChildren<Transform>(true).Length;
            host.Start();
            int safety = 0;
            while (host.Entrance.ReleasedCount < 20 && safety++ < 2000)
            {
                host.Entrance.TickEntrance(.5f);
                int slot = Enumerable.Range(0, 3).Where(host.Entrance.IsWaiting).DefaultIfEmpty(-1).First();
                if (slot < 0) continue;
                host.Fill(slot);
                var result = host.Packing.TryPack();
                Assert.That(result.Success, Is.True);
                Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(host.Packing.SuccessfulPackCount));
                host.CompleteVisual();
                Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(result.Receipt));
                host.Entrance.TickEntrance(host.ExitDuration + .01f);
            }
            Assert.That(safety, Is.LessThan(2000));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.CompletedOrderCount, Is.EqualTo(20));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(20));
            Assert.That(host.Entrance.RemainingOrderCount, Is.Zero);
            Assert.That(host.Score.TotalScore, Is.EqualTo(2640));
            Assert.That(host.Hud.text, Is.EqualTo("점수 2640"));
            Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(20));
            CollectionAssert.AreEqual(new[] { 8, 8, 4 }, Enumerable.Range(1, 3).Select(host.Score.GetSuccessfulCount));
            Assert.That(host.Score.Entries.Sum(value => value.Points), Is.EqualTo(2640));
            CollectionAssert.AllItemsAreUnique(host.Score.ProcessedReceiptIds);
            CollectionAssert.AreEquivalent(host.Packing.CommittedReceipts.Select(value => value.ReceiptId), host.Score.ProcessedReceiptIds);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(20));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(20));
            Assert.That(host.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(host.Clock.ElapsedSeconds, Is.Zero);
            Assert.That(host.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
            host.Close();
            host.Reopen();
            host.Entrance.TickEntrance(100);
            Assert.That(host.Score.TotalScore, Is.EqualTo(2640));
            Assert.That(host.Entrance.SuppliedOrderCount, Is.EqualTo(20));
            account.AssertUnchanged();
        }
        account.AssertUnchanged();
    }

    private static void AssertAward(Host host, PopupStorePackingReceipt receipt, int plush, int points)
    {
        Assert.That(receipt, Is.Not.Null);
        Assert.That(host.Score.TotalScore, Is.EqualTo(points));
        Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(1));
        Assert.That(host.Score.ProcessedReceiptIds.Count, Is.EqualTo(1));
        Assert.That(host.Score.GetSuccessfulCount(plush), Is.EqualTo(1));
        Assert.That(host.Score.TryGetEntry(receipt.ReceiptId, out var entry), Is.True);
        Assert.That(entry.ReceiptId, Is.EqualTo(receipt.ReceiptId));
        Assert.That(entry.OrderId, Is.EqualTo(receipt.OrderId));
        Assert.That(entry.CustomerId, Is.EqualTo(receipt.CustomerId));
        Assert.That(entry.SlotIndex, Is.EqualTo(receipt.SlotIndex));
        Assert.That(entry.PlushCount, Is.EqualTo(plush));
        Assert.That(entry.Points, Is.EqualTo(points));
    }

    private sealed class Host : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly UIPopupStore View;
        internal readonly PopupStoreBusinessClock Clock;
        internal readonly PopupStoreCustomerEntrance Entrance;
        internal readonly PopupStoreSalesCounter Counter;
        internal readonly PopupStoreOrderDisplay Orders;
        internal readonly PopupStoreOrderProgress Progress;
        internal readonly PopupStorePacking Packing;
        internal readonly PopupStorePackingPresentation Presentation;
        internal readonly PopupStoreCustomerPatience Patience;
        internal readonly PopupStoreScore Score;
        internal int FeedbackCount;
        internal TMP_Text Hud => (TMP_Text)Read(Score, "_scoreText");
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");
        internal float EntryDuration => (float)Read(Entrance, "_entryDuration");
        internal float ExitDuration => (float)Read(Entrance, "_exitDuration");

        internal Host(double[] durations = null, PopupStoreOrder[] orders = null)
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-score-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Clock = Root.GetComponent<PopupStoreBusinessClock>();
                Entrance = Root.GetComponent<PopupStoreCustomerEntrance>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Patience = Root.GetComponent<PopupStoreCustomerPatience>();
                Score = Root.GetComponent<PopupStoreScore>();
                Assert.That(Score, Is.Not.Null);
                Write(Clock, "_autoStartForEditorReview", false);
                if (durations != null)
                {
                    Write(Patience, "_onePlushSeconds", durations[0]);
                    Write(Patience, "_twoPlushSeconds", durations[1]);
                    Write(Patience, "_threePlushSeconds", durations[2]);
                }
                if (orders != null)
                {
                    Write(Entrance, "_fixedTestSupply", false);
                    Write(Orders, "_sampleOrders", orders);
                }
                View.Init();
                View.Show();
                RefreshExisting();
                Assert.That(Entrance.BeginVisibleSession(), Is.True);
                Assert.That(Presentation.BeginVisibleSession(), Is.True);
                Assert.That(Patience.Refresh(), Is.True);
                Assert.That(Score.Refresh(), Is.True);
                Progress.FeedbackTriggered += CountFeedback;
            }
            catch { Dispose(); throw; }
        }
        internal void Start(double seconds = 90)
        {
            Write(Clock, "_initialSeconds", seconds);
            Assert.That(Clock.TryStartSession(), Is.True);
        }
        internal void StartCustomers(int count = 3)
        {
            Start();
            Entrance.TickEntrance(0);
            for (int i = 1; i < count; i++) Entrance.TickEntrance(4);
            Entrance.TickEntrance(EntryDuration + .01f);
            for (int i = 0; i < count; i++) Assert.That(Entrance.IsWaiting(i), Is.True, "arrival " + i);
        }
        internal PopupStoreMaterialEntry[] Resolve(PopupStoreOrder order)
        {
            Assert.That(order.TryResolve((PopupStoreMaterialCatalog)Read(Orders, "_catalog"), out var materials, out var error), Is.True, error);
            return materials;
        }
        internal void Fill(int slot)
        {
            foreach (var material in Resolve(SourceOrders[slot])) Assert.That(Counter.TryAdd(material), Is.True);
        }
        internal void AssertNoScore()
        {
            Assert.That(Score.TotalScore, Is.Zero);
            Assert.That(Score.SuccessfulOrderCount, Is.Zero);
            Assert.That(Score.Entries, Is.Empty);
            Assert.That(Score.ProcessedReceiptIds, Is.Empty);
            for (int i = 1; i <= 3; i++) Assert.That(Score.GetSuccessfulCount(i), Is.Zero);
        }
        internal void CompleteVisual()
        {
            Presentation.TickPresentation(0);
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != "Idle"; i++) Presentation.TickPresentation(.005f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo("Idle"));
        }
        internal void Close()
        {
            if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
            View.Hide();
            Entrance.PauseVisibleSession();
            Presentation.EndVisibleSession();
            Invoke(Patience, "OnDisable");
            Invoke(Packing, "OnDisable");
            Invoke(Progress, "OnDisable");
            Invoke(Counter, "OnDisable");
            Invoke(Orders, "OnDisable");
            Invoke(Score, "OnDisable");
        }
        internal void Reopen()
        {
            View.Show();
            RefreshExisting();
            Assert.That(Entrance.BeginVisibleSession(), Is.True);
            Assert.That(Presentation.BeginVisibleSession(), Is.True);
            Assert.That(Patience.Refresh(), Is.True);
            Assert.That(Score.Refresh(), Is.True);
        }
        private void RefreshExisting()
        {
            Assert.That(Counter.Refresh(), Is.True);
            foreach (var list in Root.GetComponentsInChildren<PopupStoreMaterialList>(true)) Assert.That(list.Refresh(), Is.True);
            Assert.That(Orders.Refresh(), Is.True);
            Assert.That(Progress.Refresh(), Is.True);
            Assert.That(Packing.Refresh(), Is.True);
        }
        private void CountFeedback() => FeedbackCount++;
        public void Dispose()
        {
            if (Progress != null) Progress.FeedbackTriggered -= CountFeedback;
            if (Entrance != null) Entrance.PauseVisibleSession();
            if (Presentation != null)
            {
                if (Presentation.CurrentReceipt != null) LogAssert.Expect(LogType.Warning, new Regex("^POPUP_STORE_DELIVERY_CANCELLED"));
                Presentation.EndVisibleSession();
            }
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }

    private sealed class AccountSnapshot
    {
        private readonly long _money = UserInfo.Money, _context = UserInfo.CustomerEncounterContextGeneration;
        private readonly int _dia = UserInfo.Dia, _visits = UserInfo.TotalCumulativeCustomerCount;
        private readonly object _score = typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private readonly float _timeScale = Time.timeScale;
        internal void AssertUnchanged()
        {
            Assert.That(UserInfo.Money, Is.EqualTo(_money));
            Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(_visits));
            Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(typeof(UserInfo).GetField("_score", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.EqualTo(_score));
            Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        }
    }
    private static PopupStoreOrder Order(string customer, string plush)
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", plush.Split(','));
        Write(order, "_boxId", "BOX_PINK");
        Write(order, "_ribbonId", "RIBBON_YELLOW");
        return order;
    }
    private static PopupStoreMaterialEntry Entry(string id)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath);
        foreach (PopupStoreMaterialKind kind in Enum.GetValues(typeof(PopupStoreMaterialKind)))
        {
            Assert.That(catalog.TryGetMaterials(kind, out var materials, out var error), Is.True, error);
            var result = materials.FirstOrDefault(value => value.Id == id);
            if (result != null) return result;
        }
        throw new ArgumentException("Not an approved material: " + id);
    }
    private static PopupStoreMaterialEntry Unregistered(string id, PopupStoreMaterialKind kind)
    {
        var entry = new PopupStoreMaterialEntry();
        Write(entry, "_id", id);
        Write(entry, "_kind", kind);
        return entry;
    }
    private static void Invoke(object target, string name)
    {
        var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(target, null);
    }
    private static object Read(object target, string name) => Field(target, name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static FieldInfo Field(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
        return field;
    }
}
