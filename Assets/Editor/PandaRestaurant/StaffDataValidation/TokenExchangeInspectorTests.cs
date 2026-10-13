#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Uses disposable authored-asset copies. Never edits/saves the user's Stage1 or source prefab.</summary>
public sealed class TokenExchangeInspectorTests
{
    internal const string PrefabPath = "Assets/Resources/UI/GachaCollection/TokenExchangeView.prefab";
    internal const string EntryPath = "Assets/Resources/UI/GachaCollection/TokenExchangeEntryButton.prefab";
    private const string TempFolder = "Assets/__TokenExchangeInspectorTest_TEMP";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void ConnectAuthoredAssets(UIGacha owner)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var entry = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPath);
        Assert.That(prefab, Is.Not.Null); Assert.That(entry, Is.Not.Null);
        var view = Object.Instantiate(prefab, owner.transform, false).GetComponent<TokenExchangeView>();
        var button = Object.Instantiate(entry, owner.transform, false).GetComponent<Button>();
        Set(owner, "_collectionExchange", view); Set(owner, "_collectionExchangeButton", button);
    }

    [Test]
    public void AuthoredPrefabAndNativeOfflineCopyUseTheSameBindingsWithoutTouchingSourceScene()
    {
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        byte[] sourceBytes = File.ReadAllBytes(StaffGachaOfflineViewFactory.SourceScenePath);
        Scene source = default, destination = default;
        GameObject copiedRoot = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var asset = prefab.GetComponent<TokenExchangeView>();
            Assert.That(asset.ValidateLayout(out var assetError), Is.True, assetError);
            Assert.That(asset.gameObject.activeSelf, Is.False);
            Assert.That(Cards(asset).Count, Is.EqualTo(6));
            source = EditorSceneManager.OpenPreviewScene(StaffGachaOfflineViewFactory.SourceScenePath);
            var production = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIGacha>(true)).Single();
            var productionExchange = (TokenExchangeView)Get(production, "_collectionExchange");
            var productionEntry = (Button)Get(production, "_collectionExchangeButton");
            Assert.That(production.gameObject.activeSelf, Is.True, "Do not change the developer's saved Gacha UI activation for tests.");
            Assert.That(productionExchange, Is.Not.Null); Assert.That(productionEntry, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(productionExchange), Is.EqualTo(PrefabPath));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(productionEntry), Is.EqualTo(EntryPath));
            Assert.That(productionExchange.transform.IsChildOf(production.transform), Is.True);
            Assert.That(productionExchange.ValidateLayout(out var productionError), Is.True, productionError);
            Vector2 authoredBuyPosition = ((RectTransform)((Button)Get(productionExchange, "_buy")).transform).anchoredPosition;
            destination = EditorSceneManager.NewPreviewScene();
            copiedRoot = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(destination, out var copied, out _);
            Assert.That(copiedRoot.activeSelf || copied.gameObject.activeSelf, Is.False,
                "All display dependencies must be prepared before activation.");
            var copiedExchange = (TokenExchangeView)Get(copied, "_collectionExchange");
            Assert.That(copiedExchange, Is.Not.Null);
            Assert.That(copied.GetComponentsInChildren<TokenExchangeView>(true), Has.Length.EqualTo(1));
            Assert.That(copiedExchange.ValidateLayout(out var copiedError), Is.True, copiedError);
            Assert.That(copiedExchange.transform.IsChildOf(copied.transform), Is.True);
            Assert.That(((RectTransform)((Button)Get(copiedExchange, "_buy")).transform).anchoredPosition, Is.EqualTo(authoredBuyPosition));
            Assert.That(Get(copiedExchange, "_theme"), Is.SameAs(Get(productionExchange, "_theme")));
            Assert.That(((Button)Get(copied, "_collectionExchangeButton")).transform.IsChildOf(copied.transform), Is.True);
            Assert.That(production.gameObject.activeSelf, Is.True);
        }
        finally
        {
            if (copiedRoot != null) Object.DestroyImmediate(copiedRoot);
            if (destination.IsValid()) EditorSceneManager.ClosePreviewScene(destination);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
        }
        Assert.That(File.ReadAllBytes(StaffGachaOfflineViewFactory.SourceScenePath), Is.EqualTo(sourceBytes));
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        witness.AssertUnchanged("authored exchange native/offline inspection");
    }

    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    [TestCase(2560, 1080)]
    public void SavedInspectorEditsSurviveReloadDataRefreshReentryAndInterruptedAnimations(int width, int height)
    {
        var witness = GachaCollectionPreviewSceneWitness.Capture();
        using (var copy = new EditedAssetCopy())
        {
            using (var original = new ViewFixture(PrefabPath, width, height))
            {
                original.Bind(); original.View.SetVisible(true); original.Finish();
                original.Capture("default-" + width + "x" + height);
            }
            using (var f = new ViewFixture(copy.Prefab, width, height))
            {
                f.ActivatePresentation();
                f.Bind(); Assert.That(f.Theme.EntranceSeconds, Is.EqualTo(1.25f));
                AssertEdits(f.View);
                Assert.That(f.View.SetVisible(true), Is.True);
                float opened = (float)Get(f.View, "_openedAt");
                f.View.TickPresentation(opened + 1f);
                Assert.That(f.View.IsEntering, Is.True, "1.25 seconds must not silently clamp to the previous 0.9-second maximum.");
                f.View.TickPresentation(opened + 1.26f);
                Assert.That(f.View.IsEntering, Is.False); AssertEdits(f.View);
                AssertVisiblePartsFit(f, width, height);
                int changedWidth = width == 2560 ? 1280 : 2560;
                int changedHeight = changedWidth == 1280 ? 720 : 1080;
                ((RectTransform)f.Root.transform).sizeDelta = new Vector2(changedWidth, changedHeight);
                f.Finish(); AssertEdits(f.View); AssertVisiblePartsFit(f, changedWidth, changedHeight);
                ((RectTransform)f.Root.transform).sizeDelta = new Vector2(width, height);
                f.Finish(); AssertEdits(f.View);
                f.View.SelectProduct("P1");
                f.Snapshot.PandaTokens = 7654321; f.Products[1].Price = 1234567890; f.View.Refresh();
                Assert.That(((TMP_Text)Get(f.View, "_balance")).text, Does.Contain("7,654,321"));
                AssertEdits(f.View);
                f.Products[1].SoldOut = true; f.Products[1].CanPurchase = false; f.View.Refresh();
                var soldCard = Cards(f.View)[1]; var soldStamp = (RectTransform)Get(soldCard, "Stamp");
                float stampStarted = (float)Get(soldCard, "StampStarted");
                Assert.That(stampStarted, Is.GreaterThanOrEqualTo(0), "An active edited view must actually animate its new SOLD OUT state.");
                f.View.TickPresentation(stampStarted);
                Assert.That(Vector3.Distance(soldStamp.localScale, new Vector3(.81f, .87f, 1) * 1.7f), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(soldStamp.localRotation, Quaternion.Euler(0, 0, -13)), Is.LessThan(.01f));
                Assert.That(((CanvasGroup)Get(soldCard, "StampGroup")).alpha, Is.Zero);
                f.View.TickPresentation(stampStarted + .2f);
                Assert.That(((CanvasGroup)Get(soldCard, "StampGroup")).alpha, Is.EqualTo(.68f).Within(.001f));
                f.View.TickPresentation(stampStarted + .31f);
                AssertEdits(f.View);
                Assert.That(((RectTransform)Get(Cards(f.View)[1], "Stamp")).gameObject.activeSelf, Is.True);
                f.Products[1].SoldOut = false; f.View.Refresh(); f.Products[1].SoldOut = true; f.View.Refresh();
                f.View.TickPresentation((float)Get(soldCard, "StampStarted") + .1f);
                f.View.SetVisible(false); AssertEdits(f.View);
                for (int cycle = 0; cycle < 5; cycle++)
                {
                    f.View.SetVisible(false); AssertEdits(f.View);
                    f.View.SetVisible(true);
                    f.View.TickPresentation((float)Get(f.View, "_openedAt") + .17f);
                    f.View.SetVisible(false); AssertEdits(f.View);
                }
                f.View.SetVisible(true); f.Finish(); AssertEdits(f.View);
                f.Capture("edited-reloaded-reopened-" + width + "x" + height);
                f.View.ResetSession(); AssertEdits(f.View);
                Assert.That(f.Purchases + f.Refreshes, Is.Zero, "Inspector and display refresh experiments never transact.");
            }
        }
        witness.AssertUnchanged("saved copy Inspector edit validation");
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    public void NonPositiveEntranceDurationFinishesWithoutInputLock(float duration)
    {
        using (var f = new ViewFixture(PrefabPath, 1920, 1080))
        {
            var theme = Object.Instantiate(f.Theme);
            try
            {
                theme.EntranceSeconds = duration; f.Bind(theme);
                Assert.That(f.View.SetVisible(true), Is.True);
                f.View.TickPresentation(Time.realtimeSinceStartup);
                Assert.That(f.View.IsEntering, Is.False);
                Assert.That(((Button)Get(f.View, "_buy")).interactable, Is.True);
                f.View.SetVisible(false);
            }
            finally { Object.DestroyImmediate(theme); }
        }
    }

    [Test]
    public void SavedEntryEditsSurviveHudRefreshAndRepeatedBindingAddsOneListener()
    {
        using (var copy = new EditedAssetCopy())
        using (var f = new ViewFixture(copy.Prefab, 1920, 1080))
        {
            var entry = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(copy.Entry), f.Root.transform, false).GetComponent<Button>();
            int opens = 0;
            var hud = GachaCollectionMachineHud.Attach((RectTransform)f.Root.transform, f.Theme, () => opens++, () => { }, entry, false);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                hud.BindExchangeButton(entry, () => opens++);
                hud.Refresh(cycle, cycle, false); hud.SetResultPresentation(true);
                Assert.That(entry.gameObject.activeSelf, Is.False);
                hud.SetResultPresentation(false);
                typeof(GachaCollectionMachineHud).GetMethod("LateUpdate", Fields).Invoke(hud, null);
                Assert.That(entry.gameObject.activeSelf, Is.True);
            }
            var rect = (RectTransform)entry.transform;
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(-37, 51)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(245, 64)));
            var label = entry.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label.fontSize, Is.EqualTo(23)); Assert.That(label.text, Is.EqualTo("교환소 편집 확인"));
            entry.onClick.Invoke(); Assert.That(opens, Is.EqualTo(1));
            Assert.That(entry.GetComponents<GachaButtonInputSound>(), Has.Length.EqualTo(1));
            Assert.That(hud.ExchangeButton, Is.SameAs(entry));
        }
    }

    [TestCase("missing")]
    [TestCase("duplicate")]
    [TestCase("foreign")]
    [TestCase("null-star")]
    public void InvalidBindingsAreDiagnosedWithoutFallbackOrEconomyCallbacks(string kind)
    {
        using (var f = new ViewFixture(PrefabPath, 1920, 1080))
        {
            var foreign = new GameObject("Another screen's button", typeof(RectTransform), typeof(Image), typeof(Button));
            foreign.transform.SetParent(f.Root.transform, false);
            int componentsBefore = f.Root.GetComponentsInChildren<Component>(true).Length;
            string expected;
            if (kind == "missing") { Set(f.View, "_buy", null); expected = "_buy"; }
            else if (kind == "duplicate") { Set(Cards(f.View)[1], "Button", Get(Cards(f.View)[0], "Button")); expected = "Button"; }
            else if (kind == "foreign") { Set(f.View, "_closeButton", foreign.GetComponent<Button>()); expected = "_closeButton"; }
            else { ((Image[])Get(Cards(f.View)[0], "Stars"))[2] = null; expected = "Stars"; }
            Assert.That(f.View.ValidateLayout(out var error), Is.False);
            Assert.That(error, Does.Contain(expected));
            LogAssert.Expect(LogType.Error, new Regex("^TokenExchangeView .*" + Regex.Escape(expected)));
            Assert.That(f.Bind(), Is.False);
            Assert.That(f.View.SetVisible(true), Is.False);
            Assert.That(f.View.IsConfigured || f.View.IsOpen, Is.False);
            Assert.That(f.Reads + f.Purchases + f.Refreshes, Is.Zero);
            Assert.That(f.Snapshot.PandaTokens, Is.EqualTo(10000));
            Assert.That(f.Root.GetComponentsInChildren<TokenExchangeView>(true), Has.Length.EqualTo(1));
            Assert.That(f.Root.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(componentsBefore), "No replacement hierarchy may be generated.");
        }
    }

    [Test]
    public void RenamedObjectsAndEditedLayoutComponentsRemainValid()
    {
        using (var f = new ViewFixture(PrefabPath, 1920, 1080))
        {
            f.View.Board.name = "개발자 변경 판 이름";
            var priceRow = (RectTransform)Get(f.View, "_priceTokenRow");
            Object.DestroyImmediate(priceRow.GetComponent<ContentSizeFitter>());
            Object.DestroyImmediate(priceRow.GetComponent<HorizontalLayoutGroup>());
            Assert.That(f.View.ValidateLayout(out var error), Is.True, error);
            Assert.That(f.Bind(), Is.True);
            Assert.That(f.View.SetVisible(true), Is.True); f.Finish();
            Assert.That(f.View.Board.name, Is.EqualTo("개발자 변경 판 이름"));
            Assert.That(f.Root.GetComponentsInChildren<TokenExchangeView>(true), Has.Length.EqualTo(1));
        }
    }

    [TestCase("_viewport")]
    [TestCase("_content")]
    public void ForeignResponsiveReferencesAreRejectedWithoutScalingAnotherScreen(string field)
    {
        using (var f = new ViewFixture(PrefabPath, 1920, 1080))
        {
            var foreign = new GameObject("Foreign responsive target", typeof(RectTransform));
            foreign.transform.SetParent(f.Root.transform, false);
            var foreignRect = (RectTransform)foreign.transform;
            foreignRect.localScale = new Vector3(.73f, .82f, 1);
            foreignRect.sizeDelta = new Vector2(500, 500);
            var fitter = f.View.GetComponent<TokenExchangeViewportFitter>();
            Assert.That(fitter, Is.Not.Null); Set(fitter, field, foreignRect);
            Assert.That(fitter.ValidateLayout(out var fitError), Is.False);
            Assert.That(fitError, Does.Contain(field));
            Assert.That(f.View.ValidateLayout(out var error), Is.False);
            Assert.That(error, Does.Contain("TokenExchangeViewportFitter." + field));
            fitter.Refresh();
            Assert.That(foreignRect.localScale, Is.EqualTo(new Vector3(.73f, .82f, 1)));
            LogAssert.Expect(LogType.Error, new Regex("^TokenExchangeView .*TokenExchangeViewportFitter\\." + Regex.Escape(field)));
            Assert.That(f.Bind(), Is.False); Assert.That(f.View.SetVisible(true), Is.False);
            Assert.That(f.Reads + f.Purchases + f.Refreshes, Is.Zero);
        }
    }

    [Test]
    public void LatePurchaseConfirmationWhileHiddenPreservesEditedStampPoseAndReopensSilently()
    {
        using (var copy = new EditedAssetCopy())
        using (var audio = new Phase5UiAudioScope())
        using (var f = new ViewFixture(copy.Prefab, 1920, 1080))
        {
            f.DeferPurchase = true; f.Bind(); f.View.SetVisible(true); f.Finish();
            ((Button)Get(f.View, "_buy")).onClick.Invoke();
            Assert.That(f.PendingPurchase, Is.Not.Null); Assert.That(f.Purchases, Is.EqualTo(1));
            f.View.SetVisible(false); AssertEdits(f.View); audio.Events.Clear();
            f.Products[0].SoldOut = true; f.Products[0].CanPurchase = false;
            f.PendingPurchase(true, null); f.PendingPurchase(true, null);
            Assert.That(f.View.IsOpen, Is.False); AssertEdits(f.View);
            f.View.TickPresentation(Time.realtimeSinceStartup + 2f); AssertEdits(f.View);
            Assert.That(audio.Events.Count(item => item.Clip == f.Theme.SoldOutStampSound), Is.Zero);
            f.View.SetVisible(true); f.Finish(); AssertEdits(f.View);
            Assert.That(((RectTransform)Get(Cards(f.View)[0], "Stamp")).gameObject.activeSelf, Is.True);
            Assert.That(audio.Events.Count(item => item.Clip == f.Theme.SoldOutStampSound), Is.Zero,
                "Hidden completion/restored stock is not a second purchase presentation.");
        }
    }

    [Test]
    public void ProductionBindingAndAccountRebindingPreserveTheInspectorTheme()
    {
        using (var audio = new Phase5UiAudioScope())
        using (var f = new ViewFixture(PrefabPath, 1920, 1080))
        {
            var custom = Object.Instantiate(f.Theme);
            try
            {
                custom.EntranceSeconds = 1.7f;
                custom.NormalProductFrame = f.Theme.RareProductFrame;
                custom.Ticket = f.Theme.PandaToken;
                custom.ExchangeChainSound = audio.Click;
                custom.SoldOutStampSound = audio.Exit;
                Set(f.View, "_theme", custom);
                var owner = f.Root.AddComponent<UIGacha>();
                var entry = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EntryPath), f.Root.transform, false).GetComponent<Button>();
                Set(owner, "_collectionExchange", f.View); Set(owner, "_collectionExchangeButton", entry);
                Set(owner, "_editorOfflineConfigured", true); owner.VisibleState = VisibleState.Appeared;
                for (int account = 0; account < 2; account++)
                {
                    var memory = new GachaEconomyMemoryStore(new GachaEconomySnapshot("theme-copy-account-" + account,
                        new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(),
                            1000, GachaEconomySaveData.Empty), 1000));
                    var economy = new GachaEconomyService(memory, Array.Empty<GachaData>(), Resources.Load<GachaEconomySettings>("GachaEconomySettings"));
                    owner.BindCollectionEconomy(economy);
                    Assert.That(owner.CollectionExchange, Is.SameAs(f.View));
                    Assert.That(f.View.Theme, Is.SameAs(custom));
                    Assert.That(f.View.Theme.SoldOutStampSound, Is.SameAs(audio.Exit));
                    audio.Now += 20; audio.Events.Clear(); owner.OpenCollectionExchange();
                    Assert.That(f.View.IsOpen, Is.True);
                    float opened = (float)Get(f.View, "_openedAt");
                    f.View.TickPresentation(opened + 1.1f);
                    Assert.That(f.View.IsEntering, Is.True, "Production binding must use the serialized 1.7-second duration.");
                    f.View.TickPresentation(opened + 1.8f);
                    Assert.That(f.View.IsEntering, Is.False);
                    Assert.That(audio.Events.Count(item => item.Clip == custom.ExchangeChainSound), Is.EqualTo(1),
                        "Account " + account + " must dispatch its serialized chain clip exactly once.");
                    var firstCard = Cards(f.View)[0];
                    Assert.That(Get(firstCard, "Id"), Is.Not.Null.And.Not.Empty);
                    Assert.That(((Image)Get(firstCard, "Frame")).sprite, Is.SameAs(custom.NormalProductFrame));
                    Assert.That(((Image)Get(firstCard, "Artwork")).sprite, Is.SameAs(custom.Ticket),
                        "Production ticket fallback must also read the exchange's Inspector theme.");
                    Assert.That(economy.Snapshot.Account.PandaTokens, Is.EqualTo(1000));
                    Assert.That(memory.CommitCount, Is.EqualTo(1), "Only the free initial in-memory display is saved.");
                    f.View.SetVisible(false);
                    // Plain NUnit EditMode does not run non-ExecuteAlways MonoBehaviour lifecycle messages.
                    typeof(TokenExchangeView).GetMethod("OnDisable", Fields).Invoke(f.View, null);
                }
                Assert.That(f.Root.GetComponentsInChildren<TokenExchangeView>(true), Has.Length.EqualTo(1));
            }
            finally { Object.DestroyImmediate(custom); }
        }
    }

    [Test]
    public void NestedExchangePreservesItsContainersAndRestoresHiddenSiblings()
    {
        using (var f = new ViewFixture(PrefabPath, 1920, 1080))
        {
            var owner = AddNativeOwner(f);
            CanvasGroup Group(string name, Transform parent, float alpha, bool interactable, bool blocks)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)); go.transform.SetParent(parent, false);
                var group = go.GetComponent<CanvasGroup>(); group.alpha = alpha; group.interactable = interactable; group.blocksRaycasts = blocks;
                return group;
            }
            var wrapper = Group("Developer-authored wrapper", f.Root.transform, .8f, true, true);
            ((RectTransform)wrapper.transform).sizeDelta = new Vector2(1920, 1080);
            f.View.transform.SetParent(wrapper.transform, false);
            var nested = Group("Sibling inside wrapper", wrapper.transform, .65f, false, true);
            var outer = Group("Sibling beside wrapper", f.Root.transform, .75f, true, false);
            var memory = Memory("nested-inspector-copy"); var economy = Economy(memory);
            owner.BindCollectionEconomy(economy);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                owner.OpenCollectionExchange();
                Assert.That(f.View.IsOpen, Is.True);
                Assert.That(wrapper.alpha, Is.EqualTo(.8f)); Assert.That(wrapper.interactable && wrapper.blocksRaycasts, Is.True);
                Assert.That(wrapper.gameObject.activeSelf, Is.True);
                Assert.That(nested.alpha, Is.Zero); Assert.That(nested.interactable || nested.blocksRaycasts, Is.False);
                Assert.That(outer.alpha, Is.Zero); Assert.That(outer.interactable || outer.blocksRaycasts, Is.False);
                f.View.SetVisible(false);
                Assert.That(wrapper.alpha, Is.EqualTo(.8f)); Assert.That(wrapper.interactable && wrapper.blocksRaycasts, Is.True);
                Assert.That(nested.alpha, Is.EqualTo(.65f)); Assert.That(nested.interactable, Is.False); Assert.That(nested.blocksRaycasts, Is.True);
                Assert.That(outer.alpha, Is.EqualTo(.75f)); Assert.That(outer.interactable, Is.True); Assert.That(outer.blocksRaycasts, Is.False);
            }
            Assert.That(memory.CommitCount, Is.EqualTo(1)); Assert.That(economy.Snapshot.Account.PandaTokens, Is.EqualTo(1000));
        }
    }

    [Test]
    public void RepairingAnEntryReferenceToTheBuyButtonRestoresNormalPurchasing()
    {
        using (var f = new ViewFixture(PrefabPath, 1920, 1080))
        {
            var owner = AddNativeOwner(f); var entry = (Button)Get(owner, "_collectionExchangeButton");
            var buy = (Button)Get(f.View, "_buy"); Set(owner, "_collectionExchangeButton", buy);
            var memory = Memory("repaired-entry-copy"); var economy = Economy(memory);
            LogAssert.Expect(LogType.Error, new Regex("^토큰 교환소 UI 연결 오류: .*_collectionExchangeButton"));
            owner.BindCollectionEconomy(economy);
            Assert.That(f.View.IsOpen, Is.False); Assert.That(memory.SaveAttempts, Is.Zero);
            Assert.That(buy.gameObject.activeSelf, Is.True, "Invalid entry wiring must never deactivate the real purchase control.");
            Set(owner, "_collectionExchangeButton", entry); owner.BindCollectionEconomy(economy);
            Assert.That(owner.CollectionHud.ExchangeButton, Is.SameAs(entry)); Assert.That(f.View.IsConfigured, Is.True);
            owner.OpenCollectionExchange(); f.Finish();
            Assert.That(f.View.IsOpen, Is.True); Assert.That(buy.gameObject.activeSelf && buy.interactable, Is.True);
            var selected = economy.GetDisplayedProducts(GachaExchangeCategory.Mixed).Single(item => item.Id == f.View.SelectedProductId);
            int committed = memory.CommitCount; long tokens = economy.Snapshot.Account.PandaTokens;
            buy.onClick.Invoke(); buy.onClick.Invoke();
            Assert.That(memory.CommitCount, Is.EqualTo(committed + 1));
            Assert.That(economy.Snapshot.Account.PandaTokens, Is.EqualTo(tokens - selected.Price));
            Assert.That(buy.gameObject.activeSelf, Is.True); Assert.That(buy.interactable, Is.False);
            Assert.That(f.Root.GetComponentsInChildren<TokenExchangeView>(true), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void BrokenNativeExchangeLeavesOriginalLayersAndMemoryWalletUntouched()
    {
        var scene = EditorSceneManager.NewPreviewScene(); GameObject root = null;
        try
        {
            root = new GameObject("Invalid native exchange isolation", typeof(RectTransform));
            root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            var native = new GameObject("Native machine controls", typeof(RectTransform), typeof(CanvasGroup));
            native.transform.SetParent(root.transform, false);
            var group = native.GetComponent<CanvasGroup>(); group.alpha = .75f; group.interactable = false; group.blocksRaycasts = true;
            var owner = root.AddComponent<UIGacha>(); ConnectAuthoredAssets(owner);
            Set(owner, "_editorOfflineConfigured", true); owner.VisibleState = VisibleState.Appeared;
            var exchange = (TokenExchangeView)Get(owner, "_collectionExchange"); Set(exchange, "_buy", null);
            var memory = new GachaEconomyMemoryStore(new GachaEconomySnapshot("invalid-inspector-copy",
                new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(),
                    1000, GachaEconomySaveData.Empty), 1000));
            var economy = new GachaEconomyService(memory, Array.Empty<GachaData>(), Resources.Load<GachaEconomySettings>("GachaEconomySettings"));
            LogAssert.Expect(LogType.Error, new Regex("^토큰 교환소 UI 연결 오류: .*_buy"));
            owner.BindCollectionEconomy(economy); owner.OpenCollectionExchange();
            Assert.That(exchange.IsOpen, Is.False);
            Assert.That(memory.SaveAttempts, Is.Zero); Assert.That(memory.CommitCount, Is.Zero);
            Assert.That(economy.Snapshot.Account.PandaTokens, Is.EqualTo(1000));
            Assert.That(group.alpha, Is.EqualTo(.75f)); Assert.That(group.interactable, Is.False); Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(root.GetComponentsInChildren<TokenExchangeView>(true), Has.Length.EqualTo(1));
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static UIGacha AddNativeOwner(ViewFixture f)
    {
        var owner = f.Root.AddComponent<UIGacha>();
        var entry = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EntryPath), f.Root.transform, false).GetComponent<Button>();
        Set(owner, "_collectionExchange", f.View); Set(owner, "_collectionExchangeButton", entry);
        Set(owner, "_editorOfflineConfigured", true); owner.VisibleState = VisibleState.Appeared;
        return owner;
    }

    private static GachaEconomyMemoryStore Memory(string account) => new GachaEconomyMemoryStore(new GachaEconomySnapshot(account,
        new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(),
            1000, GachaEconomySaveData.Empty), 1000));
    private static GachaEconomyService Economy(GachaEconomyMemoryStore memory) =>
        new GachaEconomyService(memory, Array.Empty<GachaData>(), Resources.Load<GachaEconomySettings>("GachaEconomySettings"));

    private static void AssertEdits(TokenExchangeView view)
    {
        Assert.That(view.Board.anchoredPosition, Is.EqualTo(new Vector2(13, -9)));
        Assert.That(Quaternion.Angle(view.Board.localRotation, Quaternion.Euler(0, 0, 2)), Is.LessThan(.01f));
        Assert.That(view.Board.localScale, Is.EqualTo(new Vector3(.94f, .96f, 1)));
        Assert.That(((RectTransform)((Button)Get(view, "_buy")).transform).anchoredPosition, Is.EqualTo(new Vector2(349, -869)));
        Assert.That(((RectTransform)Get(view, "_balanceFrame")).anchoredPosition, Is.EqualTo(new Vector2(-530, 432)));
        var text = (TMP_Text)Get(view, "_description");
        Assert.That(text.fontSize, Is.EqualTo(21)); Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
        var grid = ((RectTransform)Get(view, "_content")).GetComponent<GridLayoutGroup>();
        Assert.That(grid.spacing, Is.EqualTo(new Vector2(11, 81)));
        Assert.That(new[] { grid.padding.left, grid.padding.right, grid.padding.top, grid.padding.bottom }, Is.EqualTo(new[] { 4, 5, 7, 8 }));
        foreach (var card in Cards(view))
        {
            var stamp = (RectTransform)Get(card, "Stamp");
            Assert.That(stamp.localScale, Is.EqualTo(new Vector3(.81f, .87f, 1)));
            Assert.That(Quaternion.Angle(stamp.localRotation, Quaternion.Euler(0, 0, -13)), Is.LessThan(.01f));
            Assert.That(((CanvasGroup)Get(card, "StampGroup")).alpha, Is.EqualTo(.68f).Within(.001f));
        }
    }

    private static void AssertVisiblePartsFit(ViewFixture f, int width, int height)
    {
        Rect visible = ((RectTransform)f.Root.transform).rect;
        foreach (string field in new[] { "_balanceFrame", "_closeButton", "_refresh", "_buy", "_content", "_description" })
        {
            var component = (Component)Get(f.View, field); var rect = (RectTransform)component.transform;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector3 local = f.Root.transform.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(visible.xMin - 1, visible.xMax + 1), field + " x " + width + "x" + height);
                Assert.That(local.y, Is.InRange(visible.yMin - 1, visible.yMax + 1), field + " y " + width + "x" + height);
            }
        }
        Assert.That(Cards(f.View).Count, Is.EqualTo(6));
    }

    private static IList Cards(TokenExchangeView view) => (IList)Get(view, "_cards");
    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);

    private sealed class EditedAssetCopy : IDisposable
    {
        internal string Prefab => TempFolder + "/EditedTokenExchange.prefab";
        internal string Entry => TempFolder + "/EditedEntry.prefab";
        private readonly byte[] _original = File.ReadAllBytes(PrefabPath);
        private readonly byte[] _originalEntry = File.ReadAllBytes(EntryPath);
        private bool _ownsFolder;
        internal EditedAssetCopy()
        {
            Assert.That(AssetDatabase.IsValidFolder(TempFolder) || Directory.Exists(TempFolder), Is.False,
                "Do not overwrite or delete a pre-existing test folder.");
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", "__TokenExchangeInspectorTest_TEMP"), Is.Not.Empty); _ownsFolder = true;
                Assert.That(AssetDatabase.CopyAsset(PrefabPath, Prefab), Is.True);
                Assert.That(AssetDatabase.CopyAsset(EntryPath, Entry), Is.True);
                var theme = Object.Instantiate(GachaCollectionUiTheme.Load()); theme.EntranceSeconds = 1.25f;
                AssetDatabase.CreateAsset(theme, TempFolder + "/EditedTheme.asset");
                GameObject contents = PrefabUtility.LoadPrefabContents(Prefab);
                try
                {
                    var view = contents.GetComponent<TokenExchangeView>(); Set(view, "_theme", theme);
                    view.Board.anchoredPosition = new Vector2(13, -9); view.Board.localRotation = Quaternion.Euler(0, 0, 2);
                    view.Board.localScale = new Vector3(.94f, .96f, 1);
                    ((RectTransform)((Button)Get(view, "_buy")).transform).anchoredPosition = new Vector2(349, -869);
                    ((RectTransform)Get(view, "_balanceFrame")).anchoredPosition = new Vector2(-530, 432);
                    var text = (TMP_Text)Get(view, "_description"); text.enableAutoSizing = false; text.fontSize = 21; text.textWrappingMode = TextWrappingModes.NoWrap;
                    var grid = ((RectTransform)Get(view, "_content")).GetComponent<GridLayoutGroup>();
                    grid.spacing = new Vector2(11, 81); grid.padding = new RectOffset(4, 5, 7, 8);
                    foreach (var card in Cards(view))
                    {
                        var stamp = (RectTransform)Get(card, "Stamp"); stamp.localScale = new Vector3(.81f, .87f, 1); stamp.localRotation = Quaternion.Euler(0, 0, -13);
                        ((CanvasGroup)Get(card, "StampGroup")).alpha = .68f;
                    }
                    Assert.That(PrefabUtility.SaveAsPrefabAsset(contents, Prefab), Is.Not.Null);
                    AssetDatabase.SaveAssetIfDirty(theme);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                contents = PrefabUtility.LoadPrefabContents(Entry);
                try
                {
                    var rect = (RectTransform)contents.transform; rect.anchoredPosition = new Vector2(-37, 51); rect.sizeDelta = new Vector2(245, 64);
                    var label = contents.GetComponentInChildren<TMP_Text>(true); label.enableAutoSizing = false; label.fontSize = 23; label.text = "교환소 편집 확인";
                    Assert.That(PrefabUtility.SaveAsPrefabAsset(contents, Entry), Is.Not.Null);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(Entry, ImportAssetOptions.ForceSynchronousImport);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (_ownsFolder)
            {
                _ownsFolder = false;
                Assert.That(AssetDatabase.DeleteAsset(TempFolder), Is.True, "Remove only the owned disposable asset copies.");
            }
            Assert.That(File.ReadAllBytes(PrefabPath), Is.EqualTo(_original), "The shipped prefab must remain byte-identical.");
            Assert.That(File.ReadAllBytes(EntryPath), Is.EqualTo(_originalEntry));
        }
    }

    private sealed class ViewFixture : IDisposable
    {
        internal readonly GameObject Root;
        internal readonly TokenExchangeView View;
        internal readonly GachaCollectionUiTheme Theme;
        internal readonly TokenExchangeSnapshot Snapshot;
        internal readonly TokenExchangeProductView[] Products;
        internal int Reads, Purchases, Refreshes;
        internal bool DeferPurchase;
        internal Action<bool, string> PendingPurchase;
        private readonly Scene _scene;
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        private readonly int _width, _height;
        internal ViewFixture(string path, int width, int height)
        {
            _width = width; _height = height; _scene = EditorSceneManager.NewPreviewScene();
            Root = new GameObject("Inspector test viewport", typeof(RectTransform)); Root.SetActive(false);
            SceneManager.MoveGameObjectToScene(Root, _scene); ((RectTransform)Root.transform).sizeDelta = new Vector2(width, height);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null, path);
            View = Object.Instantiate(prefab, Root.transform, false).GetComponent<TokenExchangeView>();
            Theme = (GachaCollectionUiTheme)Get(View, "_theme"); Assert.That(Theme, Is.Not.Null);
            Products = Enumerable.Range(0, 6).Select(index => new TokenExchangeProductView
            {
                Id = "P" + index, Name = "편집 검증 상품 " + (index + 1), Description = "저장한 글자 크기와 줄바꿈 설정을 유지합니다.",
                Category = (TokenExchangeTab)(index / 2), Rank = Rank.Rare, Sprite = Theme.Ticket,
                Quantity = 1, Price = 80 + index * 20, CanPurchase = true, DisplayVersion = 1
            }).ToArray();
            Snapshot = new TokenExchangeSnapshot { PandaTokens = 10000, Products = Products, CanRefresh = true, RemainingRefreshes = 3, DisplayVersion = 1 };
            _fonts.BindBeforeActivation(Root);
        }
        internal bool Bind(GachaCollectionUiTheme theme = null) => View.Bind(theme ?? Theme,
            () => { Reads++; return Snapshot; }, (id, version, reply) => { Purchases++; PendingPurchase = reply; if (!DeferPurchase) reply(false, "isolated"); },
            (tab, version, reply) => { Refreshes++; reply(false, "isolated"); });
        internal void Finish()
        {
            Canvas.ForceUpdateCanvases(); View.TickPresentation((float)Get(View, "_openedAt") + 3f);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Get(View, "_content"));
        }
        internal void ActivatePresentation()
        {
            Assert.That(Root.GetComponent<UIGacha>(), Is.Null, "Only the pure exchange presentation fixture can be activated.");
            var canvas = Root.GetComponent<Canvas>();
            if (canvas == null) canvas = Root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; Root.SetActive(true); Canvas.ForceUpdateCanvases();
        }
        internal void Capture(string name)
        {
            string directory = Environment.GetEnvironmentVariable("PANDA_EXCHANGE_EVIDENCE_DIR");
            if (string.IsNullOrWhiteSpace(directory)) return;
            directory = Path.GetFullPath(directory);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(directory.StartsWith(project, StringComparison.OrdinalIgnoreCase), Is.False, "Evidence belongs outside the project.");
            var canvas = Root.GetComponent<Canvas>();
            if (canvas == null) canvas = Root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var cameraObject = new GameObject("Isolated evidence camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = _height * .5f;
            camera.transform.position = new Vector3(0, 0, -1000); camera.nearClipPlane = 1; camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .17f, .2f);
            camera.scene = _scene; canvas.worldCamera = camera;
            var target = new RenderTexture(_width, _height, 24); var image = new Texture2D(_width, _height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create(); camera.targetTexture = target; camera.aspect = (float)_width / _height;
                Root.SetActive(true); Finish(); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, _width, _height), 0, 0); image.Apply();
                Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                Root.SetActive(false); camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
            }
        }
        public void Dispose()
        {
            Object.DestroyImmediate(Root); _fonts.Dispose(); EditorSceneManager.ClosePreviewScene(_scene);
        }
    }
}
#endif
