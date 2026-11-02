using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Muks.MobileUI;
using Muks.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Approved source bytes and real local final results. No disk assets are created by these tests.</summary>
public sealed class PopupStoreRewardBoxTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string SpriteDirectory = "Assets/Sprites/PopupStore/RewardBoxes/";

    [TestCase("C", "cc9616ed1e83240d35f887318ac3929fc3c348375355a1a95fd5a529e3070e4f")]
    [TestCase("B", "c1eec03652e78d364ffd16bfb1f85bb44d88469d8242e47bdae286c70bd6f2c4")]
    [TestCase("A", "478fb0697068b66cd6200cabc9ae986acb1c39a0e5509955f3b66e7798bc075d")]
    [TestCase("S", "838682baef5be77959867c109e13c8dc2e3091b869456807616ff6b89ae13a35")]
    [TestCase("SS", "06504877e9d5187dcc6a2f6ae9b7ffe31489be6fe06642de993ec5229d1967db")]
    [TestCase("SSS", "d0f0617049c18507ad0abe4c8084635addf009209422de6f77eb7e8a798b30f5")]
    public void ApprovedPngRetainsManifestShaRgbaDimensionsTransparencyAndSingleSpriteImport(string grade, string sha)
    {
        string path = SpritePath(grade);
        byte[] bytes = File.ReadAllBytes(path);
        // Expected SHA-256 values are pinned to USER_APPROVED_2026-11-02/manifest.json, not regenerated from imports.
        using (var digest = SHA256.Create())
            Assert.That(BitConverter.ToString(digest.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(), Is.EqualTo(sha));
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray());
        Assert.That(ReadBigEndian(bytes, 16), Is.EqualTo(1254));
        Assert.That(ReadBigEndian(bytes, 20), Is.EqualTo(1254));
        Assert.That(bytes[24], Is.EqualTo(8), "PNG bit depth");
        Assert.That(bytes[25], Is.EqualTo(6), "PNG colour type must retain RGBA.");
        Texture2D decoded = null;
        try
        {
            decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(ImageConversion.LoadImage(decoded, bytes, false), Is.True);
            Assert.That(decoded.width, Is.EqualTo(1254)); Assert.That(decoded.height, Is.EqualTo(1254));
            var pixels = decoded.GetPixels32();
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True, "Transparent source pixels remain present.");
            Assert.That(pixels.Any(pixel => pixel.a == 255), Is.True, "The approved artwork remains visible.");
        }
        finally { if (decoded != null) Object.DestroyImmediate(decoded); }
        Assert.That(File.Exists(path + ".meta"), Is.True);
        string guid = AssetDatabase.AssetPathToGUID(path);
        Assert.That(guid, Has.Length.EqualTo(32));
        Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(path));
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
        Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.isReadable, Is.False);
        Assert.That(importer.maxTextureSize, Is.EqualTo(512));
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite.name, Is.EqualTo("PopupRewardBox_" + grade + "_APPROVED"));
        Assert.That(sprite.rect.width, Is.EqualTo(sprite.texture.width));
        Assert.That(sprite.rect.height, Is.EqualTo(sprite.texture.height));
        Assert.That(sprite.texture.width, Is.LessThanOrEqualTo(512));
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void SixImportedTexturesStayWithinTheDisplayMemoryBudgetWithoutReadableCopiesOrMipmaps()
    {
        var textures = new HashSet<Texture2D>();
        long bytes = 0;
        foreach (var tier in GradedTiers())
        {
            var sprite = Approved(tier);
            Assert.That(sprite, Is.Not.Null);
            var texture = sprite.texture;
            Assert.That(textures.Add(texture), Is.True, "Each approved image has its own imported texture.");
            Assert.That(texture.width, Is.LessThanOrEqualTo(512));
            Assert.That(texture.height, Is.LessThanOrEqualTo(512));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));
            Assert.That(texture.isReadable, Is.False);
            long size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            Assert.That(size, Is.GreaterThan(0), tier.ToString());
            bytes += size;
        }
        Assert.That(textures.Count, Is.EqualTo(6));
        Debug.Log("POPUP_STORE_REWARD_TEXTURE_MEMORY bytes=" + bytes + " assets=" + textures.Count);
        // Six uncompressed 512x512 RGBA textures need 6 MiB; 8 MiB includes conservative native overhead.
        Assert.That(bytes, Is.LessThanOrEqualTo(8L * 1024 * 1024));
    }

    [Test]
    public void ActualPrefabOwnsOneNonInteractiveBoxAndSixDistinctApprovedMappings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var box = prefab.GetComponent<PopupStoreRewardBox>();
        var result = prefab.GetComponent<PopupStoreResultPanel>();
        Assert.That(prefab.GetComponentsInChildren<PopupStoreRewardBox>(true), Has.Length.EqualTo(1));
        Assert.That(Read(result, "_rewardBox"), Is.SameAs(box));
        Assert.That(box.BoxImage.name, Is.EqualTo("Result Reward Box"));
        Assert.That(box.BoxImage.transform.IsChildOf((RectTransform)Read(result, "_panel")), Is.True);
        Assert.That(box.BoxImage.raycastTarget, Is.False);
        Assert.That(box.BoxImage.preserveAspect, Is.True);
        Assert.That(box.BoxImage.rectTransform.sizeDelta.x, Is.EqualTo(box.BoxImage.rectTransform.sizeDelta.y));
        Assert.That(box.ValidateMapping(out string error), Is.True, error);
        var sprites = new HashSet<Sprite>();
        foreach (var tier in GradedTiers())
        {
            var actual = (Sprite)Read(box, SpriteField(tier));
            Assert.That(actual, Is.SameAs(Approved(tier)));
            Assert.That(sprites.Add(actual), Is.True, tier.ToString());
        }
        Assert.That(prefab.GetComponentsInChildren<Image>(true).Count(image => image.name == "Result Reward Box"), Is.EqualTo(1));
    }

    [Test]
    public void ActualRewardAtPeakScaleStaysInsideItsPanelAndClearOfAllResultTextAndConfirmation()
    {
        using (var host = new Host())
        {
            host.Finish(PopupStoreGradeTier.C); host.Show();
            host.Result.TickResult((float)Read(host.Box, "_duration") * .4f);
            Assert.That(host.Image.rectTransform.localScale.x, Is.EqualTo((float)Read(host.Box, "_peakScale")).Within(.0001f));
            var panel = (RectTransform)Read(host.Result, "_panel");
            Rect reward = BoundsWithin(host.Image.rectTransform, panel);
            Assert.That(reward.xMin, Is.GreaterThanOrEqualTo(panel.rect.xMin));
            Assert.That(reward.xMax, Is.LessThanOrEqualTo(panel.rect.xMax));
            Assert.That(reward.yMin, Is.GreaterThanOrEqualTo(panel.rect.yMin));
            Assert.That(reward.yMax, Is.LessThanOrEqualTo(panel.rect.yMax));
            foreach (string name in new[] { "_titleText", "_scoreText", "_gradeText", "_countText" })
            {
                var text = (TMP_Text)Read(host.Result, name);
                Assert.That(reward.Overlaps(BoundsWithin(text.rectTransform, panel)), Is.False, name + " must remain outside even the full transparent image rectangle at peak scale.");
            }
            var button = (Button)Read(host.Result, "_confirmButton");
            Assert.That(reward.Overlaps(BoundsWithin((RectTransform)button.transform, panel)), Is.False, "confirmation");
        }
    }

    [TestCase(PopupStoreGradeTier.None)]
    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void EachActualFinalGradeShowsOnlyItsApprovedSpriteWithoutChangingAnyTransaction(PopupStoreGradeTier tier)
    {
        using (var host = new Host())
        {
            host.Finish(tier);
            var final = host.Grade.FinalResult;
            var snapshot = new Transactions(host);
            host.Show();
            Assert.That(host.Result.ShownResult, Is.SameAs(final));
            Assert.That(host.Box.IsVisible, Is.EqualTo(tier != PopupStoreGradeTier.None));
            Assert.That(host.Box.DisplayedSprite, Is.SameAs(tier == PopupStoreGradeTier.None ? null : Approved(tier)));
            Assert.That(host.Image.raycastTarget, Is.False);
            Assert.That(host.Root.GetComponentsInChildren<Image>(true).Count(image => image.name == "Result Reward Box"), Is.EqualTo(1));
            if (tier != PopupStoreGradeTier.None) Assert.That(host.Box.ShownResult, Is.SameAs(final));
            for (int i = 0; i < 3; i++) { Assert.That(host.Result.Refresh(), Is.True); host.Result.TickResult(1); }
            snapshot.AssertUnchanged(host);
            Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
            Assert.That(host.Box.AnimationCount, Is.EqualTo(tier == PopupStoreGradeTier.None ? 0 : 1));
        }
    }

    [TestCase(PopupStoreGradeTier.C, PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.B, PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.A, PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.S, PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SS, PopupStoreGradeTier.SSS)]
    [TestCase(PopupStoreGradeTier.SSS, PopupStoreGradeTier.C)]
    public void SwappedUniqueSpritesHideOnlyRewardInsteadOfShowingAnotherGrade(PopupStoreGradeTier first, PopupStoreGradeTier second)
    {
        using (var host = new Host())
        {
            host.Finish(first);
            object a = Read(host.Box, SpriteField(first)), b = Read(host.Box, SpriteField(second));
            Write(host.Box, SpriteField(first), b); Write(host.Box, SpriteField(second), a);
            Assert.That(host.Box.ValidateMapping(out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            ExpectRewardError(); host.Show();
            AssertOnlyRewardUnavailable(host);
        }
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void MissingGradeSpriteCannotSubstituteAnotherBoxOrBlockResultConfirmation(PopupStoreGradeTier tier)
    {
        using (var host = new Host())
        {
            host.Finish(tier); Write(host.Box, SpriteField(tier), null);
            Assert.That(host.Box.ValidateMapping(out _), Is.False);
            ExpectRewardError(); host.Show();
            AssertOnlyRewardUnavailable(host);
            host.ConfirmAndDeliverDisable();
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor));
            Assert.That(host.Result.ConfirmationCount, Is.EqualTo(1));
        }
    }

    [TestCase(PopupStoreGradeTier.C, PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A, PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS, PopupStoreGradeTier.SSS)]
    public void DuplicateSpriteAssignmentFailsClosedForTheImageOnly(PopupStoreGradeTier duplicate, PopupStoreGradeTier target)
    {
        using (var host = new Host())
        {
            host.Finish(target); Write(host.Box, SpriteField(target), Read(host.Box, SpriteField(duplicate)));
            Assert.That(host.Box.ValidateMapping(out _), Is.False);
            ExpectRewardError(); host.Show(); AssertOnlyRewardUnavailable(host);
        }
    }

    [Test]
    public void MissingImageDoesNotSuppressFinalTextOrConfirmation()
    {
        using (var host = new Host())
        {
            host.Finish(PopupStoreGradeTier.C); Write(host.Box, "_image", null);
            ExpectRewardError(); host.Show(); AssertOnlyRewardUnavailable(host);
            host.ConfirmAndDeliverDisable();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InitiallyEnabledInspectorSpriteIsActuallyHiddenWhenTheMappingIsInvalid(bool missing)
    {
        using (var host = new Host())
        {
            host.Finish(PopupStoreGradeTier.C);
            Image image = host.Image;
            image.enabled = true; image.sprite = Approved(PopupStoreGradeTier.SSS);
            Write(host.Box, "_cSprite", missing ? null : Approved(PopupStoreGradeTier.B));
            var snapshot = new Transactions(host);
            ExpectRewardError(); host.Show();
            AssertOnlyRewardUnavailable(host);
            Assert.That(image.gameObject.activeInHierarchy, Is.True, "The visible panel exposes the Inspector-enabled image unless the component disables it.");
            Assert.That(image.enabled, Is.False); Assert.That(image.sprite, Is.Null);
            snapshot.AssertUnchanged(host);
            host.ConfirmAndDeliverDisable();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NoneOrImageOptOutHidesAnInitiallyEnabledInspectorSpriteWithoutAnError(bool none)
    {
        using (var host = new Host())
        {
            host.Finish(none ? PopupStoreGradeTier.None : PopupStoreGradeTier.C);
            Image image = host.Image;
            image.enabled = true; image.sprite = Approved(PopupStoreGradeTier.SSS);
            if (!none) Write(host.Box, "_showImage", false);
            var snapshot = new Transactions(host);
            host.Show();
            Assert.That(host.Result.IsVisible, Is.True); Assert.That(host.Box.IsVisible, Is.False);
            Assert.That(image.enabled, Is.False); Assert.That(image.sprite, Is.Null);
            Assert.That(host.Box.AnimationCount, Is.Zero); Assert.That(host.Box.IsAnimating, Is.False);
            Assert.That(((TMP_Text)Read(host.Result, "_gradeText")).text, Is.EqualTo(none ? "최종 등급 없음" : "최종 등급 C"));
            snapshot.AssertUnchanged(host);
            host.ConfirmAndDeliverDisable();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FrameOrConfirmGraphicCannotBecomeARewardOrBeClearedByItsFailure(bool confirmationGraphic)
    {
        using (var host = new Host())
        {
            host.Finish(PopupStoreGradeTier.C);
            var button = (Button)Read(host.Result, "_confirmButton");
            var image = confirmationGraphic ? (Image)button.targetGraphic : (Image)Read(host.Result, "_frame");
            bool enabled = image.enabled, active = image.gameObject.activeSelf, raycasts = image.raycastTarget;
            Sprite sprite = image.sprite; Color color = image.color; Vector3 scale = image.rectTransform.localScale;
            Write(host.Box, "_image", image);
            ExpectRewardError(); host.Show();
            AssertOnlyRewardUnavailable(host);
            Assert.That(image.enabled, Is.EqualTo(enabled)); Assert.That(image.gameObject.activeSelf, Is.EqualTo(active));
            Assert.That(image.raycastTarget, Is.EqualTo(raycasts)); Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.color, Is.EqualTo(color)); Assert.That(image.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(button.IsInteractable(), Is.True);
            host.ConfirmAndDeliverDisable();
            Assert.That(image.enabled, Is.EqualTo(enabled)); Assert.That(image.sprite, Is.SameAs(sprite));
        }
    }

    [Test]
    public void ForeignRewardComponentIsNotPresentedTickedOrHiddenByAnotherPanel()
    {
        using (var host = new Host())
        using (var other = new Host())
        {
            host.Finish(PopupStoreGradeTier.C); other.Finish(PopupStoreGradeTier.B); other.Show();
            Assert.That(other.Box.IsAnimating, Is.True);
            Vector3 scale = other.Image.rectTransform.localScale;
            Sprite sprite = other.Box.DisplayedSprite;
            var snapshot = new Transactions(other);
            Write(host.Result, "_rewardBox", other.Box);
            host.Show(); host.Result.TickResult(.14f);
            Assert.That(host.Result.IsVisible, Is.True); Assert.That(host.Box.IsVisible, Is.False);
            Assert.That(other.Image.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(other.Box.IsAnimating, Is.True); Assert.That(other.Box.DisplayedSprite, Is.SameAs(sprite));
            host.ConfirmAndDeliverDisable();
            Assert.That(other.Box.IsVisible, Is.True); Assert.That(other.Box.IsAnimating, Is.True);
            Assert.That(other.Box.DisplayedSprite, Is.SameAs(sprite)); snapshot.AssertUnchanged(other);
            // The actual owning panel still advances its own curve normally.
            other.Result.TickResult(.14f);
            Assert.That(other.Image.rectTransform.localScale.x, Is.GreaterThan(scale.x));
        }
    }

    [Test]
    public void ForeignImageCannotBeShownOrHiddenByThisResult()
    {
        using (var host = new Host())
        using (var other = new Host())
        {
            host.Finish(PopupStoreGradeTier.C); other.Finish(PopupStoreGradeTier.B); other.Show();
            bool enabled = other.Image.enabled, active = other.Image.gameObject.activeSelf;
            Sprite sprite = other.Image.sprite; Vector3 scale = other.Image.rectTransform.localScale;
            Write(host.Box, "_image", other.Image);
            ExpectRewardError(); host.Show(); AssertOnlyRewardUnavailable(host);
            Assert.That(other.Image.enabled, Is.EqualTo(enabled)); Assert.That(other.Image.gameObject.activeSelf, Is.EqualTo(active));
            Assert.That(other.Image.sprite, Is.SameAs(sprite)); Assert.That(other.Image.rectTransform.localScale, Is.EqualTo(scale));
            Assert.That(other.Box.IsVisible, Is.True);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PresentRejectsAHiddenOwnerOrForeignFinalResult(bool foreign)
    {
        using (var host = new Host())
        using (var other = new Host())
        {
            host.Finish(PopupStoreGradeTier.C); other.Finish(PopupStoreGradeTier.C);
            if (foreign) host.Show();
            var snapshot = new Transactions(host);
            ExpectRewardError();
            Assert.That(host.Box.Present(foreign ? other.Grade.FinalResult : host.Grade.FinalResult), Is.False);
            Assert.That(host.Box.IsVisible, Is.False);
            Assert.That(host.Box.IsAnimating, Is.False);
            Assert.That(host.Result.IsVisible, Is.EqualTo(foreign));
            snapshot.AssertUnchanged(host);
        }
    }

    [Test]
    public void EditableScaleCurveGrowsThenReturnsToAuthoredBaseWithoutAdvancingBusiness()
    {
        using (var host = new Host())
        {
            Write(host.Box, "_baseScale", .9f); Write(host.Box, "_peakScale", 1.2f); Write(host.Box, "_duration", .5f);
            Write(host.Box, "_scaleCurve", new AnimationCurve(new Keyframe(0, 0), new Keyframe(.4f, 1), new Keyframe(1, 0)));
            host.Finish(PopupStoreGradeTier.C);
            var snapshot = new Transactions(host);
            host.Result.TickResult(0); host.Result.TickResult(.2f);
            Assert.That(host.Box.IsVisible, Is.True); Assert.That(host.Box.IsAnimating, Is.True);
            Assert.That(host.Image.rectTransform.localScale.x, Is.EqualTo(.9f).Within(.0001f));
            // Drive the production result tick so this also verifies its connection to the reward pulse.
            host.Result.TickResult(.2f);
            Assert.That(host.Box.IsAnimating, Is.True);
            Assert.That(host.Image.rectTransform.localScale.x, Is.EqualTo(1.2f).Within(.0001f));
            host.Result.TickResult(.3f);
            Assert.That(host.Box.IsAnimating, Is.False);
            Assert.That(host.Image.rectTransform.localScale.x, Is.EqualTo(.9f).Within(.0001f));
            Assert.That(host.Image.rectTransform.localScale.y, Is.EqualTo(.9f).Within(.0001f));
            Assert.That(host.Box.AnimationCount, Is.EqualTo(1)); snapshot.AssertUnchanged(host);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisabledAnimationOrZeroDurationShowsOneStableBoxImmediately(bool zeroDuration)
    {
        using (var host = new Host())
        {
            if (zeroDuration) Write(host.Box, "_duration", 0f); else Write(host.Box, "_animate", false);
            host.Finish(PopupStoreGradeTier.C); host.Show();
            Assert.That(host.Box.IsVisible, Is.True); Assert.That(host.Box.IsAnimating, Is.False);
            Assert.That(host.Image.rectTransform.localScale.x, Is.EqualTo((float)Read(host.Box, "_baseScale")));
            Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(PopupStoreGradeTier.C)));
        }
    }

    [TestCase(0f)]
    [TestCase(.15f)]
    [TestCase(1f)]
    public void ClosingAndReopeningKeepsOneSpriteAndNeverRestartsTheSameAnimation(float elapsed)
    {
        using (var host = new Host())
        {
            host.Finish(PopupStoreGradeTier.S); host.Result.TickResult(0); host.Result.TickResult(.2f);
            host.Box.TickPresentation(elapsed);
            Sprite sprite = host.Box.DisplayedSprite; var final = host.Box.ShownResult;
            int animations = host.Box.AnimationCount;
            var snapshot = new Transactions(host);
            host.Close();
            Assert.That(host.Box.IsVisible, Is.False); Assert.That(host.Box.IsAnimating, Is.False);
            for (int i = 0; i < 3; i++)
            {
                host.Reopen(); host.Result.TickResult(0);
                Assert.That(host.Box.IsVisible, Is.True); Assert.That(host.Box.IsAnimating, Is.False);
                Assert.That(host.Box.DisplayedSprite, Is.SameAs(sprite)); Assert.That(host.Box.ShownResult, Is.SameAs(final));
                Assert.That(host.Box.AnimationCount, Is.EqualTo(animations));
                Assert.That(host.Root.GetComponentsInChildren<Image>(true).Count(image => image.name == "Result Reward Box"), Is.EqualTo(1));
                host.Close();
            }
            Assert.That(animations, Is.EqualTo(1)); snapshot.AssertUnchanged(host);
        }
    }

    [Test]
    public void InspectorImageOptOutPreservesFinalTextAndTheExistingReturnPath()
    {
        using (var host = new Host())
        {
            Write(host.Box, "_showImage", false); host.Finish(PopupStoreGradeTier.C); host.Show();
            AssertOnlyRewardUnavailable(host); Assert.That(host.Box.AnimationCount, Is.Zero);
            host.ConfirmAndDeliverDisable();
        }
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidAnimationSettingsCannotBreakTheResultOrLeaveAnUnboundedTween(float duration)
    {
        using (var host = new Host())
        {
            Write(host.Box, "_duration", duration); host.Finish(PopupStoreGradeTier.C);
            ExpectRewardError(); host.Show(); AssertOnlyRewardUnavailable(host);
            Assert.That(host.Box.IsAnimating, Is.False);
        }
    }

    [Test]
    public void RewardWaitsForQueuedDeliveryAndRecipientDepartureWithoutConsumingThem()
    {
        using (var host = new Host())
        {
            host.StartCustomers(); var receipt = host.Pack(0); host.End();
            host.Result.TickResult(1);
            Assert.That(host.Box.IsVisible, Is.False); Assert.That(host.Result.IsVisible, Is.False);
            Assert.That(host.Queue.Single(), Is.SameAs(receipt)); Assert.That(host.Box.AnimationCount, Is.Zero);
            host.CompleteVisual(); host.Result.TickResult(1);
            Assert.That(host.Entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving));
            Assert.That(host.Box.IsVisible, Is.False);
            host.FinishDepartures(); host.Show();
            Assert.That(host.Box.DisplayedSprite, Is.SameAs(Approved(PopupStoreGradeTier.C)));
            Assert.That(host.Presentation.LastDeliveredReceipt, Is.SameAs(receipt));
            Assert.That(host.Presentation.CompletedDeliveryCount, Is.EqualTo(1));
            Assert.That(host.Entrance.ReleasedCount, Is.EqualTo(1)); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void ConfirmWhileBoxAnimatesCancelsOnlyItsVisualAndReturnsToFloor3ExactlyOnce()
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        using (var host = new Host())
        {
            Write(host.Box, "_duration", 2f); host.Finish(PopupStoreGradeTier.C);
            var snapshot = new Transactions(host);
            host.Result.TickResult(0); host.Result.TickResult(.2f);
            Assert.That(host.Box.IsAnimating, Is.True);
            Assert.That(host.Result.IsAppearing, Is.False);
            host.ConfirmAndDeliverDisable();
            for (int i = 0; i < 5; i++) Assert.That(host.Result.TryConfirm(), Is.False);
            Assert.That(host.Box.IsAnimating, Is.False); Assert.That(host.Box.IsVisible, Is.False);
            Assert.That(host.Navigation.FirstView, Is.SameAs(host.Floor)); Assert.That(host.Navigation.Count, Is.EqualTo(1));
            Assert.That(host.Result.ConfirmationCount, Is.EqualTo(1)); snapshot.AssertUnchanged(host);
        }
        witness.AssertUnchanged("popup-store reward memory tests");
    }

    private static void AssertOnlyRewardUnavailable(Host host)
    {
        Assert.That(host.Box.IsVisible, Is.False); Assert.That(host.Box.DisplayedSprite, Is.Null);
        Assert.That(host.Result.IsVisible, Is.True); Assert.That(host.Result.IsAppearing, Is.False);
        Assert.That(host.Result.ShownResult, Is.SameAs(host.Grade.FinalResult));
        Assert.That(host.Grade.FinalizationCount, Is.EqualTo(1));
        Assert.That(((TMP_Text)Read(host.Result, "_scoreText")).text, Is.EqualTo("최종 점수 " + host.Score.TotalScore));
        Assert.That(((TMP_Text)Read(host.Result, "_gradeText")).text, Is.EqualTo("최종 등급 " + host.Grade.FinalResult.Grade));
        Assert.That(((Button)Read(host.Result, "_confirmButton")).IsInteractable(), Is.True);
        Assert.That(host.Score.TotalScore, Is.EqualTo(host.Grade.FinalResult.Score));
        Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(host.Grade.FinalResult.SuccessfulOrders));
    }

    // Reuse the already-verified actual-prefab/navigation memory host without widening its API or duplicating setup.
    // Reflection reaches test helpers only; every transaction and visual still runs its production implementation.
    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        internal Host()
        {
            Type type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, CultureInfo.InvariantCulture);
            Assert.That(Box, Is.Not.Null);
        }
        internal GameObject Root => (GameObject)Read(_inner, "Root");
        internal PopupStoreResultPanel Result => (PopupStoreResultPanel)Read(_inner, "Result");
        internal PopupStoreRewardBox Box => Root.GetComponent<PopupStoreRewardBox>();
        internal Image Image => Box.BoxImage;
        internal PopupStoreGradeSession Grade => (PopupStoreGradeSession)Read(_inner, "Grade");
        internal PopupStoreScore Score => (PopupStoreScore)Read(_inner, "Score");
        internal PopupStorePacking Packing => (PopupStorePacking)Read(_inner, "Packing");
        internal PopupStoreCustomerEntrance Entrance => (PopupStoreCustomerEntrance)Read(_inner, "Entrance");
        internal PopupStorePackingPresentation Presentation => (PopupStorePackingPresentation)Read(_inner, "Presentation");
        internal MobileUINavigation Navigation => (MobileUINavigation)Read(_inner, "Navigation");
        internal UIFloor3 Floor => (UIFloor3)Read(_inner, "Floor");
        internal Queue<PopupStorePackingReceipt> Queue => (Queue<PopupStorePackingReceipt>)Read(Packing, "_receipts");
        internal void Finish(PopupStoreGradeTier tier)
        {
            Call("Start", 90d); if (tier != PopupStoreGradeTier.None) Call("SellUntil", tier); End();
            Assert.That(Grade.FinalResult.Grade, Is.EqualTo(tier));
        }
        internal void StartCustomers() => Call("StartCustomers", 3, 90d);
        internal PopupStorePackingReceipt Pack(int slot) => (PopupStorePackingReceipt)Call("Pack", slot);
        internal void End() => Call("End");
        internal void Show() => Call("ShowResult");
        internal void Close() => Call("Close");
        internal void Reopen() => Call("Reopen");
        internal void CompleteVisual() => Call("CompleteVisual", true);
        internal void FinishDepartures() => Call("FinishDepartures");
        internal void ConfirmAndDeliverDisable()
        {
            Assert.That(Result.TryConfirm(), Is.True);
            Assert.That(Root.activeSelf, Is.False);
            Assert.That(((UIPopupStore)Read(_inner, "View")).VisibleState, Is.EqualTo(VisibleState.Disappeared));
            Call("DisableObservers"); // Existing synchronous EditMode lifecycle convention, after actual navigation succeeds.
        }
        private object Call(string name, params object[] values)
        {
            try { return _inner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(_inner, values); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
        public void Dispose() => ((IDisposable)_inner).Dispose();
    }

    private sealed class Transactions
    {
        private readonly PopupStoreGradeResult _final;
        private readonly int _score, _orders, _packs, _finalizations;
        private readonly PopupStorePackingReceipt[] _committed, _queue;
        private readonly string[] _processed;
        internal Transactions(Host host)
        {
            _final = host.Grade.FinalResult; _score = host.Score.TotalScore; _orders = host.Score.SuccessfulOrderCount;
            _packs = host.Packing.SuccessfulPackCount; _finalizations = host.Grade.FinalizationCount;
            _committed = host.Packing.CommittedReceipts.ToArray(); _queue = host.Queue.ToArray(); _processed = host.Score.ProcessedReceiptIds.ToArray();
        }
        internal void AssertUnchanged(Host host)
        {
            Assert.That(host.Grade.FinalResult, Is.SameAs(_final)); Assert.That(host.Grade.FinalizationCount, Is.EqualTo(_finalizations));
            Assert.That(host.Score.TotalScore, Is.EqualTo(_score)); Assert.That(host.Score.SuccessfulOrderCount, Is.EqualTo(_orders));
            Assert.That(host.Packing.SuccessfulPackCount, Is.EqualTo(_packs));
            CollectionAssert.AreEqual(_committed, host.Packing.CommittedReceipts.ToArray()); CollectionAssert.AreEqual(_queue, host.Queue.ToArray());
            CollectionAssert.AreEquivalent(_processed, host.Score.ProcessedReceiptIds.ToArray());
        }
    }
    private static IEnumerable<PopupStoreGradeTier> GradedTiers() => Enum.GetValues(typeof(PopupStoreGradeTier)).Cast<PopupStoreGradeTier>().Where(tier => tier != PopupStoreGradeTier.None);
    private static string SpriteField(PopupStoreGradeTier tier) => "_" + tier.ToString().ToLowerInvariant() + "Sprite";
    private static string SpritePath(string grade) => SpriteDirectory + "PopupRewardBox_" + grade + "_APPROVED.png";
    private static Sprite Approved(PopupStoreGradeTier tier) => AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(tier.ToString()));
    private static Rect BoundsWithin(RectTransform rectangle, RectTransform parent)
    {
        var corners = new Vector3[4]; rectangle.GetWorldCorners(corners);
        Vector2 min = parent.InverseTransformPoint(corners[0]), max = min;
        foreach (var corner in corners)
        {
            Vector2 local = parent.InverseTransformPoint(corner);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        return new Rect(min, max - min);
    }
    private static int ReadBigEndian(byte[] bytes, int offset) => bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
    private static void ExpectRewardError() => LogAssert.Expect(LogType.Error, new Regex("^PopupStoreRewardBox:"));
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
}
