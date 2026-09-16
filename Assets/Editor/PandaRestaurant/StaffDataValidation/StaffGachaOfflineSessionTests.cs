#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.Tween;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public sealed class StaffGachaOfflineSessionTests
{
    [Test]
    public void OfflineSession_RegisteredSingleElevenDuplicatesAndUpperRanksCommitOnceAndReuseCalculatedResults()
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        foreach (StaffGachaOfflineCase scenario in Enum.GetValues(typeof(StaffGachaOfflineCase)))
        {
            using (var session = new StaffGachaOfflineSession(catalog))
            {
                Assert.That(session.Owner, Is.Not.Null);
                Assert.That(session.Owner.gameObject.activeSelf, Is.True);
                Assert.That(session.Owner.IsEditorOfflineOwner, Is.True);
                Assert.That(session.MockReads, Is.EqualTo(1));
                string original = JsonConvert.SerializeObject(session.Account);
                Assert.That(session.TryStart(scenario, out string error), Is.True, scenario + ": " + error);
                StaffGachaPurchaseExecution request = session.Request;
                string[] ids = ExpectedIds(scenario, catalog);
                bool allDuplicate = scenario == StaffGachaOfflineCase.SingleDuplicate || scenario == StaffGachaOfflineCase.ElevenDuplicates;
                int[] rewards = scenario == StaffGachaOfflineCase.Eleven ? new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)).ToArray()
                    : allDuplicate ? Enumerable.Repeat(5, ids.Length).ToArray() : new[] { 0 };
                int cost = ids.Length == 1 ? 10 : 100;
                Assert.That(session.DrawCount, Is.EqualTo(ids.Length));
                Assert.That(session.PurchaseWrites, Is.EqualTo(1));
                Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out _), Is.False);
                Assert.That(session.Diamonds, Is.EqualTo(110));
                Assert.That(JsonConvert.SerializeObject(session.Account), Is.EqualTo(original));
                Assert.That(request.IsCompleted, Is.False);
                Assert.That(session.RecordNotifications, Is.Zero);
                Assert.That(session.WalletNotifications, Is.Zero);
                var payload = JObject.Parse(session.PurchasePayload);
                Assert.That(payload.Properties().Select(item => item.Name), Is.EquivalentTo(new[] { "Dia", "StaffAccount" }));
                Assert.That((int)payload["Dia"], Is.EqualTo(110 - cost));
                Assert.That(ReadAccount(payload).PandaTokens, Is.EqualTo(55 + rewards.Sum()));
                string frozenPlan = JsonConvert.SerializeObject(request.Plan);
                Assert.That(session.ReplySuccess(), Is.True);
                Assert.That(request.IsCompleted, Is.True);
                Assert.That(request.CompletionCount, Is.EqualTo(1));
                Assert.That(request.SessionState, Is.EqualTo(StaffGachaRequestState.Succeeded));
                Assert.That(session.Diamonds, Is.EqualTo(110 - cost));
                Assert.That(session.Account.PandaTokens, Is.EqualTo(55 + rewards.Sum()));
                Assert.That(session.Account.Staff.Single(item => item.Id == "STAFF01").Level, Is.EqualTo(2));
                Assert.That(session.Account.Staff.Single(item => item.Id == "STAFF03").Level, Is.EqualTo(4));
                var result = request.Plan.AccountResult.Acquisition;
                Assert.That(result.Items.Select(item => item.StaffId), Is.EqualTo(ids));
                Assert.That(result.Items.Select(item => item.PandaTokenReward), Is.EqualTo(rewards));
                Assert.That(result.Items.Count(item => item.IsNew), Is.EqualTo(allDuplicate ? 0 : 1));
                Assert.That(result.TotalPandaTokens, Is.EqualTo(rewards.Sum()));
                if (!allDuplicate) Assert.That(session.Account.Staff.Single(item => item.Id == ids[0]).Level, Is.EqualTo(1));

                // A fresh display sequence is allowed; it holds the exact acquisition object, not a recalculation.
                for (int reopen = 0; reopen < 2; reopen++)
                {
                    Assert.That(StaffGachaResultSequence.TryCreateFromCalculated(result, request.DrawnStaff,
                        out var sequence, out error), Is.True, error);
                    Assert.That(sequence.Result, Is.SameAs(result));
                    Assert.That(sequence.TryMove(-1), Is.False);
                    for (int index = 0; index < ids.Length; index++)
                    {
                        Assert.That(sequence.CurrentItem, Is.SameAs(result.Items[index]));
                        Assert.That(sequence.CurrentStaff, Is.SameAs(request.DrawnStaff[index]));
                        if (index + 1 < ids.Length) Assert.That(sequence.TryMove(1), Is.True);
                    }
                    Assert.That(sequence.TryMove(1), Is.False);
                }
                Assert.That(session.ReplySuccess(), Is.True, "Only mock delivery occurred; repeated receipt is ignored by the real coordinator");
                Assert.That(session.ReplyIndeterminate(), Is.True);
                Assert.That(request.CompletionCount, Is.EqualTo(1));
                Assert.That(session.RecordNotifications, Is.EqualTo(1));
                Assert.That(session.WalletNotifications, Is.EqualTo(1));
                Assert.That(session.DrawCount, Is.EqualTo(ids.Length));
                Assert.That(session.PurchaseWrites, Is.EqualTo(1));
                Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(frozenPlan));
                witness.AssertUnchanged();
            }
            witness.AssertUnchanged();
        }
    }

    [Test]
    public void OfflineSession_PendingRewardRemainsSeparateFromFixedPayloadAndLatestMemoryAutosave()
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        using (var session = new StaffGachaOfflineSession(catalog))
        {
            Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out string error), Is.True, error);
            string payload = session.PurchasePayload;
            Assert.That(session.AddFiveDiamonds(out error), Is.True, error);
            Assert.That(session.Diamonds, Is.EqualTo(115));
            Assert.That(session.Account.PandaTokens, Is.EqualTo(55));
            Assert.That(session.Account.Staff.Any(item => item.Id == "STAFF23"), Is.False);
            Assert.That(session.Wallet.TrySpend(1, out _), Is.False);
            Assert.That(session.TryStart(StaffGachaOfflineCase.SingleNew, out _), Is.False);
            Assert.That(session.FollowupWrites, Is.Zero);
            Assert.That(session.PurchasePayload, Is.EqualTo(payload));
            Assert.That((int)JObject.Parse(payload)["Dia"], Is.EqualTo(10));
            Assert.That(session.ReplySuccess(), Is.True);
            Assert.That(session.Diamonds, Is.EqualTo(15));
            Assert.That(session.Account.PandaTokens, Is.EqualTo(110));
            Assert.That(session.FollowupWrites, Is.EqualTo(1), "No manual autosave was requested: release must preserve the later reward");
            var latest = JObject.Parse(session.FollowupPayload);
            Assert.That((int)latest["Dia"], Is.EqualTo(15));
            Assert.That(JsonConvert.SerializeObject(ReadAccount(latest)), Is.EqualTo(JsonConvert.SerializeObject(session.Account)));
            Assert.That(session.ReplySuccess(), Is.True);
            Assert.That(session.Diamonds, Is.EqualTo(15));
            Assert.That(session.FollowupWrites, Is.EqualTo(1));
            Assert.That(session.RecordNotifications, Is.EqualTo(1));
            Assert.That(session.DrawCount, Is.EqualTo(11));
            witness.AssertUnchanged();
        }
    }

    [Test]
    public void OfflineSession_UnknownReceiptAndInvalidatedOwnerKeepRequestWithoutRetryOrPartialApplication()
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        foreach (bool invalidate in new[] { false, true })
        using (var session = new StaffGachaOfflineSession(catalog))
        {
            Assert.That(session.TryStart((StaffGachaOfflineCase)999, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(session.Request, Is.Null);
            Assert.That(session.DrawCount, Is.Zero);
            Assert.That(session.PurchaseWrites, Is.Zero);
            Assert.That(session.ReplySuccess(), Is.False);
            Assert.That(session.TryStart(StaffGachaOfflineCase.SingleNew, out error), Is.True, error);
            var request = session.Request;
            string originalAccount = JsonConvert.SerializeObject(session.Account);
            string fixedPlan = JsonConvert.SerializeObject(request.Plan);
            if (invalidate)
            {
                session.Owner.InvalidateGameDataRestore();
                Assert.That(session.ReplySuccess(), Is.True);
            }
            else
            {
                Assert.That(session.HasUnresolvedRequest, Is.True, "No display object is necessary to retain the request");
                Assert.That(session.ReplyIndeterminate(), Is.True);
                Assert.That(request.Status, Is.EqualTo(StaffGachaPurchaseExecutionStatus.Indeterminate));
                Assert.That(JsonConvert.SerializeObject(session.Account), Is.EqualTo(originalAccount));
            }
            Assert.That(session.Request, Is.SameAs(request));
            Assert.That(session.HasUnresolvedRequest, Is.True);
            Assert.That(request.IsCompleted, Is.False);
            Assert.That(session.Owner.LastCompletedStaffPurchaseExecution, Is.Null);
            Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out _), Is.False);
            Assert.That(session.Diamonds, Is.EqualTo(110));
            Assert.That(session.Wallet.TrySpend(1, out _), Is.False);
            Assert.That(session.RecordNotifications, Is.Zero);
            Assert.That(session.WalletNotifications, Is.Zero);
            Assert.That(session.PurchaseWrites, Is.EqualTo(1));
            Assert.That(session.FollowupWrites, Is.Zero);
            Assert.That(session.DrawCount, Is.EqualTo(1));
            Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(fixedPlan));
            if (!invalidate)
            {
                // This is the original injected callback confirming the same request, not a retry.
                // The existing coordinator permits a definite response to resolve an unknown receipt.
                Assert.That(session.ReplySuccess(), Is.True);
                Assert.That(request.IsCompleted, Is.True);
                Assert.That(request.CompletionCount, Is.EqualTo(1));
                Assert.That(session.Diamonds, Is.EqualTo(100));
                Assert.That(session.Account.PandaTokens, Is.EqualTo(55));
                Assert.That(session.Account.Staff.Single(item => item.Id == "STAFF23").Level, Is.EqualTo(1));
                Assert.That(session.RecordNotifications, Is.EqualTo(1));
                Assert.That(session.WalletNotifications, Is.EqualTo(1));
                Assert.That(session.ReplySuccess(), Is.True);
                Assert.That(request.CompletionCount, Is.EqualTo(1));
                Assert.That(session.PurchaseWrites, Is.EqualTo(1));
                Assert.That(session.DrawCount, Is.EqualTo(1));
            }
            witness.AssertUnchanged();
        }
    }

    [Test]
    public void OfflineViewFactory_CopiesOnlyInactiveExistingDisplayAndBindsActualCardsWithoutSceneOrAccountActivation()
    {
        StaffData[] catalog = Catalog();
        Scene previous = SceneManager.GetActiveScene();
        Scene temporary = default;
        GameObject createdRoot = null;
        GameObject fontTemplate = null;
        StaffGachaOfflineFonts offlineFonts = null;
        Hash128 assetHash = AssetDatabase.GetAssetDependencyHash(StaffGachaOfflineViewFactory.SourceScenePath);
        using (var witness = new GlobalWitness(catalog))
        {
            try
            {
                // An explicit preview destination leaves even a nonempty untitled runner scene intact.
                // Never activate the preview scene, save it, or enter Play.
                temporary = EditorSceneManager.NewPreviewScene();
                GameObject root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(temporary, out var view, out var staff);
                createdRoot = root;
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previous));
                Assert.That(root.name, Is.EqualTo(StaffGachaOfflineViewFactory.RootName));
                Assert.That(root.scene, Is.EqualTo(temporary));
                Assert.That(root.activeSelf, Is.False);
                Assert.That(view.gameObject.activeInHierarchy, Is.False);
                Assert.That(root.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                Assert.That(root.GetComponentsInChildren<GachaMachineParent>(true), Is.EqualTo(new[] { staff }));
                Assert.That(root.GetComponentsInChildren<Muks.DataBind.ButtonGetter>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<UIBouncingBall>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<BackendManager>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<UIGachaCard>(true), Is.Not.Empty);
                foreach (UIGachaCard template in root.GetComponentsInChildren<UIGachaCard>(true))
                    Assert.That(template.gameObject.activeSelf, Is.False, "Source sample cards must not appear before a confirmed purchase: " + template.name);
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    Assert.That(animator.fireEvents, Is.False);
                    Assert.That(animator.enabled, Is.EqualTo(animator.gameObject == staff.gameObject));
                }
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);

                // This is the same before-activation path used by AttachView after entering Play.
                // Include a null-font text to exercise the default font without changing TMP_Settings.
                var defaultTextObject = new GameObject("Default font isolation", typeof(RectTransform));
                defaultTextObject.transform.SetParent(root.transform, false);
                TextMeshProUGUI defaultText = defaultTextObject.AddComponent<TextMeshProUGUI>();
                Assert.That(defaultText.font, Is.Null);
                TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
                TMP_FontAsset[] globalFallbacks = TMP_Settings.fallbackFontAssets?.ToArray();
                var originalTextFonts = root.GetComponentsInChildren<TMP_Text>(true)
                    .ToDictionary(text => text, text => text.font != null ? text.font : defaultFont);
                TMP_FontAsset[] sourceFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                    .Where(font => EditorUtility.IsPersistent(font)).ToArray();
                var sourceJson = sourceFonts.ToDictionary(font => font, font => EditorJsonUtility.ToJson(font));
                var sourceAtlases = sourceFonts.SelectMany(font => font.atlasTextures ?? Array.Empty<Texture2D>())
                    .Where(atlas => atlas != null).ToArray();
                var sourceMaterials = sourceFonts.Select(font => font.material).Where(material => material != null).ToArray();
                fontTemplate = Object.Instantiate(root);
                fontTemplate.name = "Inactive font recreation template";
                offlineFonts = new StaffGachaOfflineFonts();
                offlineFonts.BindBeforeActivation(root, fontTemplate);
                foreach (var pair in originalTextFonts)
                {
                    TMP_FontAsset clone = pair.Key.font;
                    Assert.That(clone, Is.Not.SameAs(pair.Value));
                    Assert.That(EditorUtility.IsPersistent(clone), Is.False);
                    Assert.That(clone.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                    Assert.That(clone.atlasWidth, Is.EqualTo(pair.Value.atlasWidth));
                    Assert.That(clone.atlasHeight, Is.EqualTo(pair.Value.atlasHeight));
                    Assert.That(clone.atlasPopulationMode, Is.EqualTo(pair.Value.atlasPopulationMode));
                    Assert.That(clone.material, Is.Not.SameAs(pair.Value.material));
                    Assert.That(EditorUtility.IsPersistent(pair.Key.fontSharedMaterial), Is.False);
                    foreach (Texture2D atlas in clone.atlasTextures.Where(atlas => atlas != null))
                    {
                        Assert.That(sourceAtlases, Has.No.Member(atlas));
                        Assert.That(EditorUtility.IsPersistent(atlas), Is.False);
                    }
                }
                TMP_FontAsset[] isolatedFonts = originalTextFonts.Keys.Select(text => text.font).ToArray();
                Assert.That(fontTemplate.GetComponentsInChildren<TMP_Text>(true).Select(text => text.font),
                    Is.EqualTo(isolatedFonts), "Display recreation must reuse the same session-only font graph");
                // Binding again must not create another font graph or reattach project resources.
                offlineFonts.BindBeforeActivation(root, fontTemplate);
                Assert.That(originalTextFonts.Keys.Select(text => text.font), Is.EqualTo(isolatedFonts));
                var visitedFonts = new HashSet<TMP_FontAsset>();
                var pendingFonts = new Queue<TMP_FontAsset>(isolatedFonts);
                while (pendingFonts.Count > 0)
                {
                    TMP_FontAsset font = pendingFonts.Dequeue();
                    if (font == null || !visitedFonts.Add(font)) continue;
                    Assert.That(EditorUtility.IsPersistent(font), Is.False, "A persistent fallback/weight font escaped isolation");
                    foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable) pendingFonts.Enqueue(fallback);
                    foreach (TMP_FontWeightPair weight in font.fontWeightTable)
                    { pendingFonts.Enqueue(weight.regularTypeface); pendingFonts.Enqueue(weight.italicTypeface); }
                    if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                        font.TryAddCharacters("신규 획득 중복 판다토큰 테스트 미리보기 0123456789", out _);
                }

                using (var session = new StaffGachaOfflineSession(catalog))
                {
                    view.ConfigureEditorOfflineView(staff);
                    staff.ConfigureEditorOffline(session.Owner, view);
                    Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out string error), Is.True, error);
                    Assert.That(session.ReplySuccess(), Is.True);
                    var request = session.Request;
                    UIGachaCard card = Reference<UIGachaCard>(staff, "_gachaCard");
                    TextMeshProUGUI description = Reference<TextMeshProUGUI>(card, "_descriptionText");
                    for (int pass = 0; pass < 2; pass++)
                    foreach (int index in new[] { 0, 1, 0, 10 })
                    {
                        Assert.That(card.TrySetStaffAcquisitionResult(request.DrawnStaff[index],
                            request.Plan.AccountResult.Acquisition.Items[index], true), Is.True);
                        Assert.That(description.text, Does.Contain("테스트 미리보기"));
                        Assert.That(description.text, index == 0 ? Does.Contain("신규 획득") : Does.Contain("중복 획득"));
                        if (index == 0) Assert.That(description.text, Does.Not.Contain("판다토큰"));
                        else Assert.That(description.text, Does.Contain(index == 1 ? "판다토큰 +10" : "판다토큰 +5"));
                    }
                    Assert.That(staff.SingleButton.interactable, Is.False);
                    Assert.That(staff.EditorOfflinePurchaseDisplay, Is.Not.Null);
                    staff.EditorOfflinePurchaseDisplay.Suspend();
                    Assert.That(session.Request, Is.SameAs(request));
                    Assert.That(request.CompletionCount, Is.EqualTo(1));
                    Assert.That(session.Diamonds, Is.EqualTo(10));
                    Assert.That(session.Account.PandaTokens, Is.EqualTo(110));
                    Assert.That(session.DrawCount, Is.EqualTo(11));
                    Assert.That(session.PurchaseWrites, Is.EqualTo(1));
                    Assert.That(root.activeSelf, Is.False, "EditMode never activates or plays the copied machine");
                    witness.AssertUnchanged();
                }
                foreach (var source in sourceJson)
                    Assert.That(EditorJsonUtility.ToJson(source.Key), Is.EqualTo(source.Value), source.Key.name);
                Assert.That(TMP_Settings.defaultFontAsset, Is.SameAs(defaultFont));
                Assert.That(TMP_Settings.fallbackFontAssets?.ToArray(), Is.EqualTo(globalFallbacks));
                Assert.That(AssetDatabase.GetAssetDependencyHash(StaffGachaOfflineViewFactory.SourceScenePath), Is.EqualTo(assetHash));
                Object.DestroyImmediate(createdRoot); createdRoot = null;
                Object.DestroyImmediate(fontTemplate); fontTemplate = null;
                offlineFonts.Dispose();
                foreach (var source in sourceJson)
                {
                    Assert.That(source.Key != null, Is.True, "Cleanup must not destroy a source font");
                    Assert.That(EditorJsonUtility.ToJson(source.Key), Is.EqualTo(source.Value), source.Key.name);
                }
                foreach (Texture2D atlas in sourceAtlases)
                    Assert.That(atlas != null, Is.True, "Cleanup must not destroy a source atlas");
                foreach (Material material in sourceMaterials)
                    Assert.That(material != null, Is.True, "Cleanup must not destroy a source material");
            }
            finally
            {
                if (createdRoot != null) Object.DestroyImmediate(createdRoot);
                if (fontTemplate != null) Object.DestroyImmediate(fontTemplate);
                offlineFonts?.Dispose();
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.ClosePreviewScene(temporary);
            }
            witness.AssertUnchanged();
        }
    }

    [Test]
    public void OfflineNavigationFactory_KeepsOnlyCopiedViewsAndTwoMachinesWithoutGameplayBindings()
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        using (var session = new StaffGachaOfflineSession(catalog))
        using (var ui = new OfflineNavigationView(session.Owner, false))
        {
            Assert.That(ui.Root.activeSelf, Is.False);
            Assert.That(ui.Navigation.Count, Is.Zero);
            Assert.That(ui.Root.GetComponentsInChildren<UIMainCanvas>(true).Length, Is.EqualTo(1));
            Assert.That(ui.Root.GetComponentsInChildren<MobileUINavigation>(true), Is.EqualTo(new[] { ui.Navigation }));
            Assert.That(ui.Root.GetComponentsInChildren<MobileUIView>(true), Is.EquivalentTo(new MobileUIView[]
                { ui.Shop, ui.ShopStaff, ui.View }));
            Assert.That(ui.Root.GetComponentsInChildren<GachaMachineParent>(true), Is.EquivalentTo(new GachaMachineParent[]
                { ui.Item, ui.Staff }));
            Assert.That(ui.Root.GetComponentsInChildren<BackendManager>(true), Is.Empty);
            Assert.That(ui.Root.GetComponentsInChildren<MainScene>(true), Is.Empty);
            Assert.That(ui.Root.GetComponentsInChildren<UIStaffPreview>(true), Is.Empty);
            Assert.That(ui.Root.GetComponentsInChildren<Muks.DataBind.ButtonGetter>(true), Is.Empty);
            Assert.That(ui.Root.GetComponentsInChildren<UIBouncingBall>(true), Is.Empty);
            foreach (Button button in ui.Root.GetComponentsInChildren<Button>(true))
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero, button.name);
            foreach (Component component in ui.Root.GetComponentsInChildren<Component>(true))
            using (var serialized = new SerializedObject(component))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object reference = property.objectReferenceValue;
                    Transform target = reference is GameObject gameObject ? gameObject.transform : (reference as Component)?.transform;
                    Assert.That(target == null || EditorUtility.IsPersistent(reference) || target == ui.Root.transform ||
                        target.IsChildOf(ui.Root.transform), Is.True, component.name + "." + property.propertyPath);
                }
            }
            Assert.That(session.PurchaseWrites, Is.Zero);
            Assert.That(session.DrawCount, Is.Zero);
            witness.AssertUnchanged();
        }
    }

    [Test]
    public void OfflineNavigation_NativeArrowsExitAndShopReentryKeepOneAcknowledgedCompletionAndReplayItsFixedResult()
    {
        StaffData[] catalog = Catalog();
        using (var witness = new GlobalWitness(catalog))
        using (var session = new StaffGachaOfflineSession(catalog))
        using (var ui = new OfflineNavigationView(session.Owner, true))
        {
            Assert.That(ui.Navigation.Count, Is.EqualTo(2));
            Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.ShopStaff));
            Assert.That(ui.Navigation.CheckActiveView("UIGacha"), Is.False);
            CanvasGroup shopGroup = Reference<CanvasGroup>(ui.Shop, "_canvasGroup");
            CanvasGroup staffGroup = Reference<CanvasGroup>(ui.ShopStaff, "_canvasGroup");
            ScrollRect scroll = RuntimeReference<ScrollRect>(ui.ShopStaff, "_staffScrollRect");
            Assert.That(scroll, Is.Not.Null, "The original staff scroll belongs to the preserved shop view");
            scroll.normalizedPosition = new Vector2(0.25f, 0.65f);
            Vector2 beforeScroll = scroll.normalizedPosition;
            object beforeFloor = RuntimeReference<object>(ui.ShopStaff, "_currentFloorType");
            object beforeType = RuntimeReference<object>(ui.ShopStaff, "_currentType");
            shopGroup.alpha = 0.85f;
            staffGroup.alpha = 0.75f;
            ui.ClickEntry();
            Assert.That(ui.Navigation.Count, Is.EqualTo(3));
            Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.View));
            Assert.That(shopGroup.alpha, Is.Zero);
            Assert.That(staffGroup.alpha, Is.Zero);
            Assert.That(shopGroup.interactable || shopGroup.blocksRaycasts || staffGroup.interactable || staffGroup.blocksRaycasts, Is.False);
            Assert.That(ui.CurrentMachine, Is.SameAs(ui.Staff));
            Assert.That(session.TryStart(StaffGachaOfflineCase.Eleven, out string error), Is.True, error);
            session.Owner.EditorStaffPurchaseAdmission = _ => false;
            Assert.That(session.ReplySuccess(), Is.True);
            // The real completion callback requests one latest-value autosave. The memory
            // transport acknowledges it immediately; navigation must add no further writes.
            int completedFollowupWrites = session.FollowupWrites;
            Assert.That(completedFollowupWrites, Is.EqualTo(1), "NotifyStaffPurchaseCompleted requests the latest-value autosave");
            string completedFollowupPayload = session.FollowupPayload;
            StaffGachaPurchaseExecution request = session.Request;
            object acquisition = request.Plan.AccountResult.Acquisition;
            object account = session.Account;
            string fixedPlan = JsonConvert.SerializeObject(request.Plan);
            Assert.That(ui.Display.TryShowCompleted(false, out error), Is.True, error);
            Assert.That(ui.Display.EditorIsResultVisible, Is.True);
            Assert.That(ui.Display.EditorResultCount, Is.EqualTo(11));
            Assert.That(ui.Display.EditorCurrentItem, Is.SameAs(request.Plan.AccountResult.Acquisition.Items[0]));
            ui.ClickResultClose();

            for (int pass = 0; pass < 2; pass++)
            {
                ui.ClickArrow("_leftButton");
                Assert.That(ui.CurrentMachine, Is.SameAs(ui.Item));
                Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.View));
                Assert.That(ui.Display.EditorIsResultVisible || ui.Display.EditorIsAnimating, Is.False);
                ui.ClickArrow("_rightButton");
                Assert.That(ui.CurrentMachine, Is.SameAs(ui.Staff));
                ui.Display.Tick();
                Assert.That(ui.Display.EditorIsResultVisible || ui.Display.EditorIsAnimating, Is.False,
                    "Acknowledged results must stay closed after the real machine arrow round trip");
                Assert.That(ui.View.IsStartGacha, Is.False);
                ui.ClickExit();
                Assert.That(ui.Navigation.Count, Is.EqualTo(2));
                Assert.That(ui.Navigation.FirstView, Is.SameAs(ui.ShopStaff));
                Assert.That(ui.Navigation.CheckActiveView("UIGacha"), Is.False);
                Assert.That(ui.View.gameObject.activeSelf, Is.False);
                Assert.That(shopGroup.alpha, Is.EqualTo(0.85f));
                Assert.That(staffGroup.alpha, Is.EqualTo(0.75f));
                Assert.That(shopGroup.interactable && shopGroup.blocksRaycasts && staffGroup.interactable && staffGroup.blocksRaycasts, Is.True);
                Assert.That(scroll.normalizedPosition, Is.EqualTo(beforeScroll));
                Assert.That(RuntimeReference<object>(ui.ShopStaff, "_currentFloorType"), Is.EqualTo(beforeFloor));
                Assert.That(RuntimeReference<object>(ui.ShopStaff, "_currentType"), Is.EqualTo(beforeType));
                ui.ClickEntry();
                ui.Display.Tick();
                Assert.That(ui.Navigation.Count, Is.EqualTo(3));
                Assert.That(ui.CurrentMachine, Is.SameAs(ui.Staff));
                Assert.That(ui.Display.EditorIsResultVisible || ui.Display.EditorIsAnimating, Is.False);
                Assert.That(ui.View.IsStartGacha, Is.False);
                Button replay = ui.Staff.GetComponentsInChildren<Button>(true).Single(button => button.name == "획득 결과");
                Assert.That(replay.gameObject.activeInHierarchy && replay.interactable, Is.True);
                replay.onClick.Invoke();
                Assert.That(ui.Display.EditorIsResultVisible, Is.True);
                Assert.That(ui.Display.EditorCurrentItem, Is.SameAs(request.Plan.AccountResult.Acquisition.Items[0]));
                Assert.That(ui.Display.EditorResultCount, Is.EqualTo(11));
                ui.ClickResultClose();
                Assert.That(session.Request, Is.SameAs(request));
                Assert.That(session.Owner.LastCompletedStaffPurchaseExecution, Is.SameAs(request));
                Assert.That(request.Plan.AccountResult.Acquisition, Is.SameAs(acquisition));
                Assert.That(session.Account, Is.SameAs(account));
                Assert.That(request.CompletionCount, Is.EqualTo(1));
                Assert.That(session.Diamonds, Is.EqualTo(10));
                Assert.That(session.Account.PandaTokens, Is.EqualTo(110));
                Assert.That(session.DrawCount, Is.EqualTo(11));
                Assert.That(session.PurchaseWrites, Is.EqualTo(1));
                Assert.That(session.FollowupWrites, Is.EqualTo(completedFollowupWrites), "Navigation and result replay must not enqueue another save");
                Assert.That(session.FollowupPayload, Is.EqualTo(completedFollowupPayload));
                Assert.That(session.RecordNotifications, Is.EqualTo(1));
                Assert.That(session.WalletNotifications, Is.EqualTo(1));
                Assert.That(JsonConvert.SerializeObject(request.Plan), Is.EqualTo(fixedPlan));
                witness.AssertUnchanged();
            }
        }
    }

    private sealed class OfflineNavigationView : IDisposable
    {
        private readonly Scene _scene;
        private readonly Hash128 _sourceHash;
        private readonly StaffGachaOfflineFonts _fonts;
        public readonly GameObject Root;
        public readonly UIGacha View;
        public readonly UIStaffGacha Staff;
        public readonly UIItemGacha Item;
        public readonly UIRestaurantAdmin Shop;
        public readonly UIStaff ShopStaff;
        public readonly MobileUINavigation Navigation;
        public StaffGachaPurchaseDisplay Display => Staff == null ? null : Staff.EditorOfflinePurchaseDisplay;
        public GachaMachineParent CurrentMachine => RuntimeReference<GachaMachineParent>(View, "_currentGachaMachine");

        public OfflineNavigationView(BackendManager owner, bool activate)
        {
            _sourceHash = AssetDatabase.GetAssetDependencyHash(StaffGachaOfflineViewFactory.SourceScenePath);
            _scene = EditorSceneManager.NewPreviewScene();
            try
            {
                Root = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(_scene, out View, out Staff, includeNavigation: true);
                _fonts = new StaffGachaOfflineFonts();
                _fonts.BindBeforeActivation(Root);
                StaffGachaOfflineViewFactory.ConfigureNavigation(View, Staff, owner);
                Navigation = Root.GetComponent<MobileUINavigation>();
                Shop = Root.GetComponentInChildren<UIRestaurantAdmin>(true);
                ShopStaff = Root.GetComponentInChildren<UIStaff>(true);
                Item = View.GetComponentInChildren<UIItemGacha>(true);
                if (!activate) return;
                // EditMode has no normal Awake/Start loop. Invoke only the copied native
                // navigation lifecycle; all account-dependent Start/Init routes stay guarded.
                InvokePrivate(Root.GetComponent<UIMainCanvas>(), "Awake");
                InvokePrivate(Navigation, "Start");
                Root.SetActive(true);
                StaffGachaOfflineViewFactory.BeginNavigation(View);
            }
            catch { Dispose(); throw; }
        }

        public void ClickEntry()
        {
            Button button = StaffGachaOfflineViewFactory.NavigationEntry(View);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
            button.onClick.Invoke();
            CompleteNavigationTweens();
        }

        public void ClickArrow(string field)
        {
            Button button = Reference<Button>(View, field);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, field);
            button.onClick.Invoke();
            CompleteNavigationTweens();
        }

        public void ClickExit()
        {
            Button button = View.transform.Find("Anime UI/UI Components/Exit Button").GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
            button.onClick.Invoke();
        }

        public void ClickResultClose()
        {
            Button button = View.transform.Find("Staff Acquisition Results").GetComponentsInChildren<Button>(true)
                .Single(candidate => candidate.name == "닫기");
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
            button.onClick.Invoke();
            Assert.That(Display.EditorIsResultVisible, Is.False);
        }

        private void CompleteNavigationTweens()
        {
            // Exercise the actual queued tween callbacks without waiting for an Editor frame.
            // The tested actions above always originate from the original source buttons.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (TweenData tween in Root.GetComponentsInChildren<TweenData>(true))
            {
                if (!tween.enabled) continue;
                if (typeof(TweenData).GetField("_percentHandler", flags).GetValue(tween) == null)
                    typeof(TweenData).GetMethod("Awake", flags).Invoke(tween, null);
                MethodInfo update = tween.GetType().GetMethod("Update", flags);
                update.Invoke(tween, null);
                typeof(TweenData).GetField("ElapsedDuration", flags).SetValue(tween, float.MaxValue);
                update.Invoke(tween, null);
            }
        }

        public void Dispose()
        {
            try
            {
                if (Root != null)
                {
                    Display?.Dispose();
                    foreach (ScrollingImage image in Root.GetComponentsInChildren<ScrollingImage>(true))
                    {
                        Material material = RuntimeReference<Material>(image, "_material");
                        if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
                    }
                    Object.DestroyImmediate(Root);
                }
                _fonts?.Dispose();
                Assert.That(AssetDatabase.GetAssetDependencyHash(StaffGachaOfflineViewFactory.SourceScenePath), Is.EqualTo(_sourceHash));
            }
            finally { if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene); }
        }
    }

    private static T RuntimeReference<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    private static void InvokePrivate(object owner, string name) =>
        owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);

    private static StaffData[] Catalog()
    {
        StaffData[] catalog = Resources.LoadAll<StaffData>("StaffData");
        Assert.That(catalog, Is.Not.Empty, "Use the project's registered resources, never synthetic employee assets");
        Assert.That(catalog.Count(item => item.Id == "STAFF01"), Is.EqualTo(1));
        Assert.That(catalog.Count(item => item.Id == "STAFF03"), Is.EqualTo(1));
        Assert.That(catalog.Count(item => item.Id == "STAFF23"), Is.EqualTo(1));
        return catalog;
    }

    private static string[] ExpectedIds(StaffGachaOfflineCase scenario, StaffData[] catalog)
    {
        switch (scenario)
        {
            case StaffGachaOfflineCase.SingleNew: return new[] { "STAFF23" };
            case StaffGachaOfflineCase.Eleven: return new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)).ToArray();
            case StaffGachaOfflineCase.SingleDuplicate: return new[] { "STAFF01" };
            case StaffGachaOfflineCase.ElevenDuplicates: return Enumerable.Repeat("STAFF01", 11).ToArray();
            default:
                Rank rank = scenario == StaffGachaOfflineCase.Unique ? Rank.Unique : Rank.Special;
                return new[] { catalog.Where(item => item.Rank == rank).OrderBy(item => item.Id, StringComparer.Ordinal).First().Id };
        }
    }

    private static StaffAccountSaveData ReadAccount(JObject payload)
    {
        var read = StaffAccountSaveConverter.Read((string)payload["StaffAccount"]);
        Assert.That(read.Data, Is.Not.Null, read.Error);
        return read.Data;
    }

    private static T Reference<T>(Object owner, string name) where T : Object
    {
        using (var serialized = new SerializedObject(owner))
            return serialized.FindProperty(name).objectReferenceValue as T;
    }

    private sealed class GlobalWitness : IDisposable
    {
        private readonly StaffData[] _catalog;
        private readonly string[] _resourceJson;
        private readonly Dictionary<FieldInfo, object> _scalars;
        private readonly Dictionary<FieldInfo, object> _singletons;
        private readonly StageInfo[] _stages;
        private readonly StaffStageRuntimeSnapshot[] _stageStaff;
        private readonly string[] _stageEquipment;
        private readonly string _payments, _gachaPayments;
        private readonly bool _saveEnabled;
        private readonly Random.State _random = Random.state;
        private readonly int _wrappers = Resources.FindObjectsOfTypeAll<GachaStaffData>().Length;

        public GlobalWitness(StaffData[] catalog)
        {
            _catalog = catalog;
            _resourceJson = catalog.Select(JsonUtility.ToJson).ToArray();
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            _scalars = typeof(UserInfo).GetFields(flags).Where(field => field.FieldType.IsPrimitive ||
                field.FieldType.IsEnum || field.FieldType == typeof(string) || field.FieldType == typeof(DateTime))
                .ToDictionary(field => field, field => field.GetValue(null));
            _singletons = new[] { typeof(BackendManager), typeof(StaffDataManager), typeof(GameManager),
                typeof(SoundManager), typeof(PopupManager), typeof(ObjectPoolManager) }
                .Select(type => type.GetField("_instance", flags)).Where(field => field != null)
                .ToDictionary(field => field, field => field.GetValue(null));
            _stages = ((StageInfo[])typeof(UserInfo).GetField("_stageInfos", flags).GetValue(null)).ToArray();
            _stageStaff = _stages.Select(stage => stage?.CaptureStaffRuntimeSnapshot()).ToArray();
            _stageEquipment = _stages.Select(Equipment).ToArray();
            _payments = JsonConvert.SerializeObject(PaymentInfo.PaymentDatas);
            _gachaPayments = JsonConvert.SerializeObject(PaymentInfo.GachaPaymentDatas);
            _saveEnabled = BackendManager.IsSaveEnabled;
        }

        public void AssertUnchanged()
        {
            foreach (var pair in _scalars) Assert.That(pair.Key.GetValue(null), Is.EqualTo(pair.Value), "UserInfo." + pair.Key.Name);
            foreach (var pair in _singletons) Assert.That(pair.Key.GetValue(null), Is.SameAs(pair.Value), pair.Key.DeclaringType.Name + " singleton");
            Assert.That(BackendManager.IsSaveEnabled, Is.EqualTo(_saveEnabled));
            var current = (StageInfo[])typeof(UserInfo).GetField("_stageInfos", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(current, Is.EqualTo(_stages));
            for (int index = 0; index < _stages.Length; index++)
            {
                if (_stages[index] == null) continue;
                Assert.That(_stageStaff[index].HasSameState(current[index].CaptureStaffRuntimeSnapshot()), Is.True);
                Assert.That(Equipment(current[index]), Is.EqualTo(_stageEquipment[index]));
            }
            Assert.That(JsonConvert.SerializeObject(PaymentInfo.PaymentDatas), Is.EqualTo(_payments));
            Assert.That(JsonConvert.SerializeObject(PaymentInfo.GachaPaymentDatas), Is.EqualTo(_gachaPayments));
            Assert.That(_catalog.Select(JsonUtility.ToJson), Is.EqualTo(_resourceJson));
            Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(JsonUtility.ToJson(_random)), "Fixed selector inputs and display-only cards must not consume RNG");
        }

        private static string Equipment(StageInfo stage)
        {
            if (stage == null) return null;
            var equipment = (Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>)typeof(StageInfo)
                .GetField("_equipStaffTypeDic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
            return JsonConvert.SerializeObject(equipment.OrderBy(pair => pair.Key).Select(floor => new
            {
                floor.Key,
                Staff = floor.Value?.OrderBy(pair => pair.Key).Select(slot => new { slot.Key, Id = slot.Value == null ? null : slot.Value.Id }).ToArray()
            }));
        }

        public void Dispose()
        {
            try
            {
                AssertUnchanged();
                Assert.That(Resources.FindObjectsOfTypeAll<GachaStaffData>().Length, Is.EqualTo(_wrappers), "Detached owner disposal must release selected display wrappers");
            }
            finally { Random.state = _random; }
        }
    }
}
#endif
