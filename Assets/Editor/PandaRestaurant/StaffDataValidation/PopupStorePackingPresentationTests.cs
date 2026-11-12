using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>Actual packing receipts and presentation state in memory; no Scene, account setup or PlayerLoop simulation.</summary>
public sealed class PopupStorePackingPresentationTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string CatalogPath = "Assets/Resources/UI/PopupStore/PopupStoreMaterials.asset";
    private const string FinishedPath = "Assets/Sprites/PopupStore/FinishedBoxes/";

    [TestCase("PINK", "PINK", "Box_Pink_Ribbon_Pink.png")]
    [TestCase("PINK", "YELLOW", "Box_Pink_Ribbon_Yellow.png")]
    [TestCase("PINK", "MINT", "Box_Pink_Ribbon_Mint.png")]
    [TestCase("BLUE", "PINK", "Box_Blue_Ribbon_Pink.png")]
    [TestCase("BLUE", "YELLOW", "Box_Blue_Ribbon_Yellow.png")]
    [TestCase("BLUE", "MINT", "Box_Blue_Ribbon_Mint.png")]
    [TestCase("MINT", "PINK", "Box_Mint_Ribbon_Pink.png")]
    [TestCase("MINT", "YELLOW", "Box_Mint_Ribbon_Yellow.png")]
    [TestCase("MINT", "MINT", "Box_Mint_Ribbon_Mint.png")]
    public void SuccessfulPackingPresentsTheApprovedCombinationOnceWithoutAnotherTransaction(string box, string ribbon, string filename)
    {
        var account = new AccountSnapshot();
        using (var host = new Host())
        {
            string boxId = "BOX_" + box, ribbonId = "RIBBON_" + ribbon;
            host.SetOrders(MakeOrder("CUSTOMER01", boxId, ribbonId), MakeOrder("CUSTOMER04", "BOX_BLUE", "RIBBON_PINK", "STAFF02"));
            string otherOrderId = host.Orders.GetDisplayedOrderId(1);
            var otherMaterials = host.Orders.GetDisplayedMaterials(1).ToArray();
            host.Begin();
            var receipt = host.Pack(0);
            var transaction = new TransactionSnapshot(host);
            host.Presentation.TickPresentation(0);
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Boom"));
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
            Assert.That(host.Boom.gameObject.activeSelf, Is.True);
            Assert.That(host.Result.gameObject.activeSelf, Is.False);
            host.AdvanceTo("Grow");
            Assert.That(host.Result.gameObject.activeSelf, Is.True);
            Assert.That(host.Result.preserveAspect, Is.True);
            Assert.That(host.Result.raycastTarget, Is.False);
            Assert.That(AssetDatabase.GetAssetPath(host.Result.sprite), Is.EqualTo(FinishedPath + filename));
            Assert.That(host.Boom.gameObject.activeSelf, Is.False);
            host.FinishCurrent();
            for (int i = 0; i < 8; i++) host.Presentation.TickPresentation(1);
            AssertHidden(host);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts, Has.Count.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts[0], Is.SameAs(receipt));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
            transaction.AssertUnchanged(host);
            Assert.That(host.Orders.Slots[0].CustomerImage.gameObject.activeSelf, Is.True);
            Assert.That(host.Orders.Slots[0].OrderFrame.gameObject.activeSelf, Is.False);
            Assert.That(host.Orders.GetDisplayedOrderId(1), Is.EqualTo(otherOrderId));
            CollectionAssert.AreEqual(otherMaterials, host.Orders.GetDisplayedMaterials(1).ToArray());
            Assert.That(host.Orders.Slots[1].OrderFrame.gameObject.activeSelf, Is.True);
        }
        account.AssertUnchanged();
    }

    [Test]
    public void AuthoredNineResultSpritesAreDistinctTransparentAndSeparateFromTheThirtyEightMaterials()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var presentation = prefab.GetComponent<PopupStorePackingPresentation>();
        Assert.That(presentation, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PopupStorePackingPresentation>(true), Has.Length.EqualTo(1));
        Assert.That(Read(presentation, "_packing"), Is.SameAs(prefab.GetComponent<PopupStorePacking>()));
        var entries = (PopupStoreFinishedBox[])Read(presentation, "_finishedBoxes");
        Assert.That(entries, Has.Length.EqualTo(9));
        CollectionAssert.AllItemsAreUnique(entries.Select(entry => entry.Sprite.GetInstanceID()).ToArray());
        var materials = (PopupStoreMaterialEntry[])Read(AssetDatabase.LoadAssetAtPath<PopupStoreMaterialCatalog>(CatalogPath), "_entries");
        Assert.That(materials, Has.Length.EqualTo(38));
        var importedTextures = new List<string>();
        var measuredTextures = new HashSet<int>();
        long totalRuntimeBytes = 0;
        foreach (var entry in entries)
        {
            string path = AssetDatabase.GetAssetPath(entry.Sprite);
            Assert.That(path, Does.StartWith(FinishedPath));
            Assert.That(materials.Any(material => material.Sprite == entry.Sprite), Is.False);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.GreaterThan(256));
            byte[] png = File.ReadAllBytes(path);
            Assert.That(ReadBigEndian(png, 16), Is.EqualTo(1254), path);
            Assert.That(ReadBigEndian(png, 20), Is.EqualTo(1254), path);
            var texture = entry.Sprite.texture;
            long runtimeBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            if (measuredTextures.Add(texture.GetInstanceID())) totalRuntimeBytes += runtimeBytes;
            importedTextures.Add(entry.BoxId + "/" + entry.RibbonId
                + " sprite=" + entry.Sprite.rect.width + "x" + entry.Sprite.rect.height
                + " texture=" + texture.width + "x" + texture.height
                + " format=" + texture.format + " runtimeBytes=" + runtimeBytes);
        }
        foreach (string box in new[] { "PINK", "BLUE", "MINT" })
            foreach (string ribbon in new[] { "PINK", "YELLOW", "MINT" })
                Assert.That(entries.Count(entry => entry.BoxId == "BOX_" + box && entry.RibbonId == "RIBBON_" + ribbon), Is.EqualTo(1));
        Debug.Log("POPUP_STORE_FINISHED_BOX_IMPORT count=" + entries.Length
            + " uniqueTextures=" + measuredTextures.Count + " totalRuntimeBytes=" + totalRuntimeBytes
            + " [" + string.Join("; ", importedTextures) + "]");
    }

    [Test]
    public void PresentationReusesTheExistingRecipeBoomControllerClipAndSevenSpriteFrames()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var presentation = prefab.GetComponent<PopupStorePackingPresentation>();
        var animator = (Animator)Read(presentation, "_boomAnimator");
        var result = (Image)Read(presentation, "_resultImage");
        Assert.That(animator.GetComponentInParent<UIPopupStore>(true), Is.SameAs(prefab.GetComponent<UIPopupStore>()));
        Assert.That(result.GetComponentInParent<UIPopupStore>(true), Is.SameAs(prefab.GetComponent<UIPopupStore>()));
        Assert.That(animator.gameObject.activeSelf, Is.False);
        Assert.That(result.gameObject.activeSelf, Is.False);
        Assert.That(animator.GetComponent<Image>().raycastTarget, Is.False);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/Boom/Boom.controller");
        Assert.That(animator.runtimeAnimatorController, Is.EqualTo(controller));
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/Boom/Boom_Play.anim");
        CollectionAssert.Contains(controller.animationClips, clip);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(item => item.propertyName == "m_Sprite");
        var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        string[] expected = { "6b6316f1fecc37f4fb50b44d94948556", "260f58fd3efa7e14899d72b7a2e01f92", "073018270b487384dbeebf1d4fd39b22", "b09d99bccf366ed4d8e38f6a412e1fd3", "c18e604c27c0e3847b73fce1685048ae", "c09cb447bd47fbc479c73c2eaf9a0a42", "0b358b4a2529d104d966ef753b1d7684" };
        CollectionAssert.AreEqual(expected, frames.Select(frame => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(frame.value))).ToArray());
    }

    [TestCase("None")]
    [TestCase("Multiple")]
    [TestCase("Stale")]
    public void RejectedPackingDoesNotPresentAndMultipleCandidatesDeliverOnlyOneReceipt(string status)
    {
        using (var host = new Host())
        {
            host.SetOrders(MakeOrder("CUSTOMER01", "BOX_PINK", "RIBBON_YELLOW"));
            host.Begin();
            if (status != "None") host.Fill(0);
            if (status == "Multiple") host.SetOrders(MakeOrder("CUSTOMER01", "BOX_PINK", "RIBBON_YELLOW"), MakeOrder("CUSTOMER04", "BOX_PINK", "RIBBON_YELLOW"));
            if (status == "Stale") Write(host.SourceOrders[0], "_ribbonId", "RIBBON_MINT");
            var before = new TransactionSnapshot(host);
            var result = host.Packing.TryPack();
            if (status == "Multiple")
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Receipt.SlotIndex, Is.Zero);
                host.Presentation.TickPresentation(0);
                host.FinishCurrent();
                host.Presentation.TickPresentation(1);
                Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
                Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(result.Receipt));
                Assert.That(host.SourceOrders.Count(order => order.IsPacked), Is.EqualTo(1));
                Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
                return;
            }
            Assert.That(result.Status.ToString(), Is.EqualTo(status));
            host.Presentation.TickPresentation(10);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            Assert.That(host.Presentation.ReceivedReceipts, Is.Empty);
            AssertHidden(host);
            before.AssertUnchanged(host);
        }
    }

    [Test]
    public void BusyNestedPackingCreatesNoExtraPresentationBeyondTheCommittedReceipt()
    {
        using (var host = new Host())
        {
            host.Begin();
            var nested = PopupStorePackingStatus.Unavailable;
            host.Counter.Changed += reason => { if (reason == PopupStoreSalesCounter.ChangeReason.Packed) nested = host.Packing.TryPack().Status; };
            var receipt = host.Pack(0);
            Assert.That(nested, Is.EqualTo(PopupStorePackingStatus.Busy));
            host.Presentation.TickPresentation(0);
            host.FinishCurrent();
            host.Presentation.TickPresentation(1);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
        }
    }

    [TestCase("BOX_UNKNOWN", "RIBBON_PINK")]
    [TestCase("BOX_PINK", "RIBBON_UNKNOWN")]
    [TestCase("box_pink", "RIBBON_PINK")]
    public void UnknownOrWrongCaseCombinationDoesNotFallBackToAnotherGift(string box, string ribbon)
    {
        using (var host = new Host())
        {
            host.Begin();
            Assert.That(host.Presentation.TryResolveSprite(box, ribbon, out var sprite), Is.False);
            Assert.That(sprite, Is.Null);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            AssertHidden(host);
        }
    }

    [Test]
    public void ARepeatedReceiptIdIsConsumedWithoutASecondBoomOrHistoryEntry()
    {
        using (var host = new Host())
        {
            host.Begin();
            var receipt = host.Pack(0);
            host.Presentation.TickPresentation(0);
            host.FinishCurrent();
            // Inject duplicate delivery at the actual queue boundary; no packing rule is duplicated here.
            ((Queue<PopupStorePackingReceipt>)Read(host.Packing, "_receipts")).Enqueue(receipt);
            for (int i = 0; i < 5; i++) host.Presentation.TickPresentation(1);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts, Has.Count.EqualTo(1));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
            AssertHidden(host);
        }
    }

    [Test]
    public void ThreeSuccessfulReceiptsArePresentedInOrderWithoutOverlapLossOrReconsumption()
    {
        using (var host = new Host())
        {
            host.Begin();
            var first = host.Pack(0);
            host.Presentation.TickPresentation(0);
            var second = host.Pack(1);
            var third = host.Pack(2);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(first));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            var transaction = new TransactionSnapshot(host);
            foreach (var receipt in new[] { first, second, third })
            {
                Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
                host.FinishCurrent();
                AssertHidden(host);
                host.Presentation.TickPresentation(0);
            }
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { first, second, third }, host.Presentation.ReceivedReceipts.ToArray());
            Assert.That(host.Packing.TryTakeReceipt(out _), Is.False);
            transaction.AssertUnchanged(host);
            AssertHidden(host);
        }
    }

    [TestCase("Boom")]
    [TestCase("Grow")]
    [TestCase("Settle")]
    [TestCase("Move")]
    public void ClosingAtAnyPhaseCleansUpWithoutRollbackOrReplayOnReentry(string phase)
    {
        using (var host = new Host())
        {
            host.Begin();
            var receipt = host.Pack(0);
            host.Presentation.TickPresentation(0);
            host.AdvanceTo(phase);
            var transaction = new TransactionSnapshot(host);
            host.Close();
            Assert.That(host.Presentation.IsReady, Is.False);
            AssertHidden(host);
            host.Presentation.TickPresentation(10);
            host.Reopen();
            host.Presentation.TickPresentation(10);
            AssertHidden(host);
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(receipt));
            transaction.AssertUnchanged(host);
            Assert.That(host.SourceOrders[0].IsPacked, Is.True);
        }
    }

    [TestCase("Idle", false)]
    [TestCase("Boom", false)]
    [TestCase("Move", false)]
    [TestCase("Move", true)]
    public void NativeParentDeactivationDefersSiblingRestoreAndReentryKeepsOneReceiptOutcome(string phase, bool ancestor)
    {
        using (var host = new Host())
        {
            var parent = ancestor ? new GameObject("presentation-deactivation-ancestor") : null;
            UIRefreshActivationProbe probe = null;
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) => {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                if (parent != null) host.Root.transform.SetParent(parent.transform, false);
                host.Boom.transform.SetSiblingIndex(3);
                host.Result.transform.SetSiblingIndex(4);
                int boomIndex = host.Boom.transform.GetSiblingIndex(), resultIndex = host.Result.transform.GetSiblingIndex();
                host.Begin();
                PopupStorePackingReceipt receipt = null;
                if (phase != "Idle")
                {
                    receipt = host.Pack(0);
                    host.Presentation.TickPresentation(0);
                    host.AdvanceTo(phase);
                }
                var transaction = new TransactionSnapshot(host);
                int disabled = 0;
                // Ordinary MonoBehaviours do not receive EditMode callbacks. This existing
                // ExecuteAlways test probe invokes the real cleanup callback *inside*
                // Unity's parent SetActive transition, where native sibling validation runs.
                probe = host.Root.AddComponent<UIRefreshActivationProbe>();
                probe.OnDeactivated = () => { disabled++; InvokeLifecycle(host.Presentation, "OnDisable"); };
                if (parent != null) parent.SetActive(false);
                host.View.Hide();
                Assert.That(disabled, Is.EqualTo(1));
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
                AssertHidden(host);
                Assert.That(host.Presentation.IsReady, Is.False);
                Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(receipt == null ? 0 : 1));
                if (receipt != null)
                {
                    Assert.That(host.Presentation.VisualOutcomes[0].Receipt, Is.SameAs(receipt));
                    Assert.That(host.Presentation.VisualOutcomes[0].Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
                }
                // A later duplicate cleanup while hidden must retain the pending original order.
                host.Presentation.EndVisibleSession();
                if (parent != null) parent.SetActive(true);
                host.Reopen();
                Assert.That(Read(host.Presentation, "_originalBoomSibling"), Is.EqualTo(boomIndex));
                Assert.That(Read(host.Presentation, "_originalResultSibling"), Is.EqualTo(resultIndex));
                host.Presentation.TickPresentation(10);
                AssertHidden(host);
                Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(receipt == null ? 0 : 1));
                Assert.That(host.Presentation.VisualOutcomes.Count, Is.EqualTo(receipt == null ? 0 : 1));
                transaction.AssertUnchanged(host);
                host.Presentation.EndVisibleSession();
                Assert.That(host.Boom.transform.GetSiblingIndex(), Is.EqualTo(boomIndex));
                Assert.That(host.Result.transform.GetSiblingIndex(), Is.EqualTo(resultIndex));
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
            }
            finally
            {
                if (probe != null) probe.OnDeactivated = null;
                Application.logMessageReceived -= capture;
                if (parent != null)
                {
                    if (host.Root != null) host.Root.transform.SetParent(null, false);
                    Object.DestroyImmediate(parent);
                }
            }
        }
    }

    [Test]
    public void ExplicitCleanupWhileParentIsStableRestoresAuthoredSiblingOrderImmediately()
    {
        using (var host = new Host())
        {
            host.Result.transform.SetSiblingIndex(3);
            host.Boom.transform.SetSiblingIndex(4);
            int boomIndex = host.Boom.transform.GetSiblingIndex(), resultIndex = host.Result.transform.GetSiblingIndex();
            host.Begin();
            host.Presentation.EndVisibleSession();
            host.Presentation.EndVisibleSession();
            Assert.That(host.Boom.transform.GetSiblingIndex(), Is.EqualTo(boomIndex));
            Assert.That(host.Result.transform.GetSiblingIndex(), Is.EqualTo(resultIndex));
            AssertHidden(host);
            Assert.That(host.Presentation.VisualOutcomes, Is.Empty);
        }
    }

    [Test]
    public void BeginningASessionKeepsOldQueuedReceiptsForDeliveryButDoesNotReplayThem()
    {
        using (var host = new Host())
        {
            var old = host.Pack(0);
            host.Begin();
            host.Presentation.TickPresentation(10);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(old));
            AssertHidden(host);
            var fresh = host.Pack(1);
            host.Presentation.TickPresentation(0);
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(fresh));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { old, fresh }, host.Presentation.ReceivedReceipts.ToArray());
        }
    }

    [Test]
    public void ClosingPreservesQueuedAndCurrentReceiptInformationWithoutReplayingEither()
    {
        using (var host = new Host())
        {
            host.Begin();
            var first = host.Pack(0);
            host.Presentation.TickPresentation(0);
            var queued = host.Pack(1);
            host.Close();
            host.Reopen();
            host.Presentation.TickPresentation(10);
            CollectionAssert.AreEqual(new[] { first, queued }, host.Presentation.ReceivedReceipts.ToArray());
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(2));
            AssertHidden(host);
        }
    }

    [Test]
    public void ClosingBeforeTheFirstTickDoesNotReplayTheUnpresentedSuccess()
    {
        using (var host = new Host())
        {
            host.Begin();
            var receipt = host.Pack(0);
            host.Close();
            host.Reopen();
            host.Presentation.TickPresentation(10);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(host.SourceOrders[0].PackingReceipt, Is.SameAs(receipt));
            AssertHidden(host);
        }
    }

    [Test]
    public void PresentationHistoryRetainsImmutableOrderAndCustomerIdentityForTheNextDeliveryStep()
    {
        using (var host = new Host())
        {
            host.Begin();
            var receipt = host.Pack(1);
            host.Presentation.TickPresentation(0);
            host.FinishCurrent();
            var history = host.Presentation.ReceivedReceipts as IList<PopupStorePackingReceipt>;
            Assert.That(history, Is.Not.Null);
            Assert.That(history.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => history.Clear());
            Assert.That(history[0].ReceiptId, Is.EqualTo(receipt.ReceiptId));
            Assert.That(history[0].OrderId, Is.EqualTo(host.SourceOrders[1].RuntimeId));
            Assert.That(history[0].SlotIndex, Is.EqualTo(1));
            Assert.That(history[0].CustomerId, Is.EqualTo("CUSTOMER04"));
            Assert.That(history[0].BoxId, Is.EqualTo("BOX_BLUE"));
            Assert.That(history[0].RibbonId, Is.EqualTo("RIBBON_PINK"));
            CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF01", "BOX_BLUE", "RIBBON_PINK" }, history[0].Materials.Select(item => item.Id).ToArray());
        }
    }

    [Test]
    public void InspectorScaleAndTimingProduceBoomGrowSettleDeliveryThenRestoreTheAuthoredScale()
    {
        using (var host = new Host())
        {
            var originalScale = new Vector3(2, 3, 1);
            host.Result.rectTransform.localScale = originalScale;
            Write(host.Presentation, "_startScale", .4f);
            Write(host.Presentation, "_maxScale", 1.3f);
            Write(host.Presentation, "_growCurve", AnimationCurve.Linear(0, 0, 1, 1));
            Write(host.Presentation, "_settleCurve", AnimationCurve.Linear(0, 0, 1, 1));
            host.Begin();
            host.Pack(0);
            host.Presentation.TickPresentation(0);
            Assert.That(host.Result.rectTransform.localScale, Is.EqualTo(originalScale * .4f));
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_boomDuration"));
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Grow"));
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_growDuration"));
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Settle"));
            Assert.That(host.Result.rectTransform.localScale, Is.EqualTo(originalScale * 1.3f));
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_settleDuration"));
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Move"));
            Assert.That(host.Result.rectTransform.localScale, Is.EqualTo(originalScale));
            // DEV-03C-3B extends the existing presentation before hiding the result.
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_moveDuration"));
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Arrive"));
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_arrivalDuration"));
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Fade"));
            host.Presentation.TickPresentation((float)Read(host.Presentation, "_fadeDuration"));
            AssertHidden(host);
            Assert.That(host.Result.rectTransform.localScale, Is.EqualTo(originalScale));
            Assert.That(host.Result.sprite, Is.Null);
        }
    }

    [TestCase("missing-pair")]
    [TestCase("duplicate-pair")]
    [TestCase("missing-sprite")]
    public void InvalidInspectorMappingsRejectTheSessionWithoutTouchingAPackedTransaction(string defect)
    {
        using (var host = new Host())
        {
            var receipt = host.Pack(0);
            var transaction = new TransactionSnapshot(host);
            var entries = (PopupStoreFinishedBox[])Read(host.Presentation, "_finishedBoxes");
            if (defect == "missing-pair") Write(host.Presentation, "_finishedBoxes", entries.Take(8).ToArray());
            else if (defect == "duplicate-pair")
            {
                Write(entries[1], "_boxId", entries[0].BoxId);
                Write(entries[1], "_ribbonId", entries[0].RibbonId);
            }
            else Write(entries[0], "_sprite", null);
            LogAssert.Expect(LogType.Error, new Regex("^PopupStorePackingPresentation: "));
            Assert.That(host.Presentation.BeginVisibleSession(), Is.False);
            host.Presentation.TickPresentation(10);
            Assert.That(host.Presentation.IsReady, Is.False);
            Assert.That(host.Presentation.StartedPresentationCount, Is.Zero);
            Assert.That(host.Presentation.ReceivedReceipts.Single(), Is.SameAs(receipt));
            AssertHidden(host);
            transaction.AssertUnchanged(host);
        }
    }

    [Test]
    public void RepeatedBeginDuringAnActiveSessionDoesNotRestartOrArchiveTheCurrentAnimation()
    {
        using (var host = new Host())
        {
            host.Begin();
            var receipt = host.Pack(0);
            host.Presentation.TickPresentation(0);
            host.AdvanceTo("Grow");
            Vector3 scale = host.Result.rectTransform.localScale;
            for (int i = 0; i < 5; i++) host.Begin();
            Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Grow"));
            Assert.That(host.Presentation.CurrentReceipt, Is.SameAs(receipt));
            Assert.That(host.Result.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(host.Presentation.StartedPresentationCount, Is.EqualTo(1));
            Assert.That(host.Presentation.ReceivedReceipts, Has.Count.EqualTo(1));
        }
    }

    private static void AssertHidden(Host host)
    {
        Assert.That(host.Presentation.CurrentPhase.ToString(), Is.EqualTo("Idle"));
        Assert.That(host.Presentation.CurrentReceipt, Is.Null);
        Assert.That(host.Boom.gameObject.activeSelf, Is.False);
        Assert.That(host.Result.gameObject.activeSelf, Is.False);
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
        internal readonly Animator Boom;
        internal readonly Image Result;
        internal int PackedEvents, FeedbackEvents;
        internal PopupStoreOrder[] SourceOrders => (PopupStoreOrder[])Read(Orders, "_sampleOrders");

        internal Host()
        {
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                Root.name = "popup-store-presentation-memory-test";
                Root.GetComponent<CanvasScaler>().enabled = false;
                Root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
                View = Root.GetComponent<UIPopupStore>();
                Counter = Root.GetComponentInChildren<PopupStoreSalesCounter>(true);
                Orders = Root.GetComponent<PopupStoreOrderDisplay>();
                Progress = Root.GetComponent<PopupStoreOrderProgress>();
                Packing = Root.GetComponent<PopupStorePacking>();
                Presentation = Root.GetComponent<PopupStorePackingPresentation>();
                Assert.That(Presentation, Is.Not.Null);
                Boom = (Animator)Read(Presentation, "_boomAnimator");
                Result = (Image)Read(Presentation, "_resultImage");
                View.Init();
                View.Show();
                // Explicit session start keeps these regression fixtures on the real runtime clock path.
                Assert.That(PopupStoreApprovedTestSession.Start(Root), Is.True);
                RefreshExisting();
                Counter.Changed += OnCounterChanged;
                Progress.FeedbackTriggered += OnFeedback;
            }
            catch { Dispose(); throw; }
        }
        internal void Begin() => Assert.That(Presentation.BeginVisibleSession(), Is.True);
        internal void SetOrders(params PopupStoreOrder[] orders)
        {
            Write(Orders, "_sampleOrders", orders);
            Assert.That(Orders.Refresh(), Is.True);
        }
        internal void Fill(int slot)
        {
            foreach (var entry in Orders.GetDisplayedMaterials(slot)) Assert.That(Counter.TryAdd(entry), Is.True);
        }
        internal PopupStorePackingReceipt Pack(int slot)
        {
            Fill(slot);
            var button = (Button)Read(Packing, "_packButton");
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(ExecuteEvents.Execute(button.gameObject, new PointerEventData(null) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
            Assert.That(Packing.LastResult.Status, Is.EqualTo(PopupStorePackingStatus.Succeeded));
            return Packing.LastResult.Receipt;
        }
        internal void AdvanceTo(string phase)
        {
            for (int i = 0; i < 3000 && Presentation.CurrentPhase.ToString() != phase; i++) Presentation.TickPresentation(.01f);
            Assert.That(Presentation.CurrentPhase.ToString(), Is.EqualTo(phase));
        }
        internal void FinishCurrent() => AdvanceTo("Idle");
        internal void Close()
        {
            View.Hide();
            // The actual lifecycle is not automatic for non-ExecuteAlways behaviours in EditMode.
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
            if (Presentation != null) Presentation.EndVisibleSession();
            if (Counter != null) Counter.Changed -= OnCounterChanged;
            if (Progress != null) Progress.FeedbackTriggered -= OnFeedback;
            if (Root != null) Object.DestroyImmediate(Root);
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
            _successes = host.Packing.SuccessfulPackCount;
            _events = host.PackedEvents;
            _feedback = host.FeedbackEvents;
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
        private readonly long _money = UserInfo.Money;
        private readonly int _dia = UserInfo.Dia, _visits = UserInfo.TotalCumulativeCustomerCount;
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
    private static PopupStoreOrder MakeOrder(string customer, string box, string ribbon, string plush = "STAFF01")
    {
        var order = new PopupStoreOrder();
        Write(order, "_customerId", customer);
        Write(order, "_plushIds", new[] { plush });
        Write(order, "_boxId", box);
        Write(order, "_ribbonId", ribbon);
        return order;
    }
    private static int ReadBigEndian(byte[] bytes, int offset)
        => bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
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
