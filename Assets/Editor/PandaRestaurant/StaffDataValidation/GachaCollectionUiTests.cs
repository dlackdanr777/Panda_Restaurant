#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class GachaCollectionUiTests
{
    private GameObject _root, _slotRoot;
    private UIGachaCardSlot _slot;
    private UIGachaCard _card;
    private GachaCollectionUiTestData _normal, _unique, _special;
    private UnityEngine.Random.State _random;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        _random = UnityEngine.Random.state;
        _root = new GameObject("Collection UI isolation", typeof(RectTransform));
        _root.SetActive(false);
        ((RectTransform)_root.transform).sizeDelta = new Vector2(1920,1080);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/UIGacha/GachaCard Slot.prefab");
        Assert.That(prefab, Is.Not.Null);
        _slotRoot = Object.Instantiate(prefab, _root.transform, false);
        _slot = _slotRoot.GetComponent<UIGachaCardSlot>();
        _card = _slotRoot.AddComponent<UIGachaCard>();
        foreach (string field in new[] { "_star1Frame", "_star3Frame", "_star4Frame", "_star5Frame", "_skinImage",
            "_nameText", "_descriptionText", "_effectText", "_typeText", "_itemStar" })
            typeof(UIGachaCard).GetField(field, Fields).SetValue(_card, typeof(UIGachaCardSlot).GetField(field, Fields).GetValue(_slot));
        typeof(UIGachaCard).GetField("_rectTransform", Fields).SetValue(_card, (RectTransform)_card.transform);
        _normal = Data(Rank.Normal2); _unique = Data(Rank.Unique); _special = Data(Rank.Special);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_normal); Object.DestroyImmediate(_unique); Object.DestroyImmediate(_special);
        Assert.That(UnityEngine.Random.state, Is.EqualTo(_random));
    }

    [Test]
    public void RetainedNewFlag_SlotAndInspectionReuseNeverInfersOwnership()
    {
        _slot.SetData(_normal, true); AssertBadge(true);
        _slot.SetData(_normal, false); AssertBadge(false);
        _slot.SetData(_unique, true); AssertBadge(true);
        _slot.SetData(_unique); AssertBadge(false);
        _card.SetData(_special, true); AssertBadge(true);
        _card.SetData(_special, false); AssertBadge(false);
        _card.SetData(_normal, true); AssertBadge(true);
        _card.SetData(_normal); AssertBadge(false);
    }

    [Test]
    public void Badge_IsSingleUnmaskedNonRaycastGraphicOverCardTopRight()
    {
        for (int i = 0; i < 5; i++) _slot.SetData(_normal, true);
        var badges = _slotRoot.GetComponentsInChildren<GachaAcquisitionBadge>(true);
        Assert.That(badges, Has.Length.EqualTo(1));
        var badge = badges[0]; var rect = (RectTransform)badge.transform;
        Assert.That(badge.GetComponent<GachaRoundedPanel>().canvasRenderer, Is.Not.Null,
            "A custom Graphic must supply its renderer, just like Unity's native Image");
        _slot.InitPresentation(); _card.InitPresentation();
        Assert.That(badge.IsNew && badge.gameObject.activeSelf, Is.True, "Repeated pool initialization preserves an already bound new result");
        Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(rect.anchoredPosition.x + rect.rect.xMax, Is.GreaterThan(0), "Badge overlaps card edge");
        float frameTop = _slot.transform.InverseTransformPoint(Frame("_star1Frame").rectTransform
            .TransformPoint(Frame("_star1Frame").rectTransform.rect.max)).y;
        float badgeBottom = _slot.transform.InverseTransformPoint(rect.TransformPoint(rect.rect.min)).y;
        Assert.That(badgeBottom, Is.GreaterThanOrEqualTo(frameTop - 10),
            "Compact badge follows the visible frame edge, not the shorter invisible slot hit area");
        Assert.That(badge.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
        Assert.That(badge.GetComponentsInChildren<MaskableGraphic>(true).All(g => !g.maskable), Is.True);
        Assert.That(badge.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("신규!"));
    }

    [Test]
    public void NativeHalo_SpecialUniqueSpecialReuseLeavesFramesAndStarsIntact()
    {
        foreach (var data in new[] { _special, _unique, _normal, _special, _unique })
        {
            _slot.SetData(data, true);
            var unique = Frame("_star4Frame"); var special = Frame("_star5Frame");
            Assert.That(unique.gameObject.activeSelf, Is.EqualTo(data.Rank == Rank.Unique));
            Assert.That(special.gameObject.activeSelf, Is.EqualTo(data.Rank == Rank.Special));
            foreach (var halo in unique.GetComponentsInChildren<RotationGameObject>(true))
                Assert.That(halo.gameObject.activeSelf || halo.enabled, Is.False);
            foreach (var halo in special.GetComponentsInChildren<RotationGameObject>(true))
            {
                Assert.That(halo.gameObject.activeSelf, Is.EqualTo(data.Rank == Rank.Special));
                Assert.That(halo.transform.localRotation, Is.EqualTo(Quaternion.identity));
            }
            Assert.That(unique.GetComponentsInChildren<RotationGameObject>(true), Is.Not.Empty, "Test native authored halo");
        }
    }

    [TestCase(Rank.Normal1, false)]
    [TestCase(Rank.Normal2, false)]
    [TestCase(Rank.Rare, false)]
    [TestCase(Rank.Unique, false)]
    [TestCase(Rank.Special, true)]
    public void HaloPolicy_IsExplicitSpecialOnly(Rank rank, bool halo)
        => Assert.That(GachaCardHaloPolicy.HasHalo(rank), Is.EqualTo(halo));

    [Test]
    public void Theme_UsesActualPandaTokenAndNewReferenceArt()
    {
        var theme = GachaCollectionUiTheme.Load();
        Assert.That(theme, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(theme.PandaToken),
            Is.EqualTo("Assets/Sprites/UI/UI공통 이미지/UI_상점_공통_가챠 토큰.png"));
        Assert.That(AssetDatabase.GetAssetPath(theme.WoodFrame), Is.EqualTo("Assets/Resources/GachaCollection/Art/exchange-base.png"));
        Assert.That(theme.Ticket, Is.Not.Null.And.Not.EqualTo(theme.PandaToken));
        Assert.That(theme.ExchangeClose, Is.Not.Null); Assert.That(theme.ExchangeBuy, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(theme.LeftChain), Is.EqualTo("Assets/Resources/GachaCollection/Art/exchange-chain-left.png"));
        Assert.That(theme.RightChain, Is.Not.Null); Assert.That(theme.ExchangeRefresh, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(theme.DetailFrame), Is.EqualTo("Assets/Resources/GachaCollection/Art/exchange-detail-frame.png"));
        Assert.That(theme.DetailFrame.border, Is.EqualTo(new Vector4(115, 115, 115, 115)),
            "Native frame copy has its own import-time slicing; runtime must not crop an atlas using an unpacked sprite rect");
        foreach (Rank rank in new[] { Rank.Normal1, Rank.Rare, Rank.Unique, Rank.Special })
            Assert.That(AssetDatabase.GetAssetPath(theme.ProductFrame(rank)), Does.StartWith("Assets/Sprites/UI/RestaurantAdminUI/StaffRE02/"));
    }

    [Test]
    public void Hud_RapidSummaryPreservesBoundaryQueueAndSkipUsesCommittedCounter()
    {
        var hud = GachaCollectionMachineHud.Attach((RectTransform)_root.transform, GachaCollectionUiTheme.Load(), () => {}, () => {});
        hud.Refresh(1, 2, false);
        hud.PresentResult(98, 99, false); hud.PresentResult(99, 0, true); hud.PresentResult(0, 1, false);
        Assert.That(hud.PendingProgressCount, Is.EqualTo(3));
        hud.Refresh(1, 2, false);
        Assert.That(hud.PendingProgressCount, Is.EqualTo(3), "Authoritative refresh must not cancel visual sequence");
        hud.SnapToCommitted();
        Assert.That(hud.PendingProgressCount, Is.Zero);
        Assert.That(hud.DisplayedProgress, Is.EqualTo(.01f).Within(.001));
        hud.Refresh(99, 0, false);
        Assert.That(hud.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("다음 뽑기 확정")), Is.True);
        Assert.That(hud.GetComponentsInChildren<Button>(true).Single(b => b.name == "Use One Machine Ticket").interactable, Is.False);
        hud.SetResultPresentation(true);
        Assert.That(hud.Rect.anchoredPosition.y, Is.EqualTo(-45));
        Assert.That(hud.GetComponentsInChildren<Button>(true).All(button => !button.gameObject.activeSelf), Is.True,
            "Purchasing controls must not overlap result cards or badges");
        hud.SetResultPresentation(false);
        Assert.That(hud.Rect.anchoredPosition.y, Is.EqualTo(-105));
    }

    [Test]
    public void Exchange_UsesProductIdOnlyBlocksDoubleClickAndCloseLeavesNoInput()
    {
        int calls = 0, reads = 0, closed = 0; Action<bool,string> reply = null;
        var snapshot = new TokenExchangeSnapshot { PandaTokens = 500, Products = new[] {
            new TokenExchangeProductView { Id = "ticket:item", Name = "아이템 뽑기권", Description = "One result", Quantity = 1,
                Price = 80, CanPurchase = true, Category = TokenExchangeTab.Tickets },
            new TokenExchangeProductView { Id = "staff:owned", Name = "보유 직원", Quantity = 1, Price = 40, CanPurchase = false,
                Status = "이미 보유", Category = TokenExchangeTab.Staff }
        } };
        var view = TokenExchangeView.Attach(_root.transform, GachaCollectionUiTheme.Load(), () => { reads++; return snapshot; },
            (id, callback) => { Assert.That(id, Is.EqualTo("ticket:item")); calls++; reply = callback; }, () => closed++);
        view.SetVisible(true);
        SettleExchange(view);
        var board = (RectTransform)view.transform.Find("Wooden Exchange Board");
        Assert.That(board.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(board.rect.width * board.localScale.x, Is.LessThanOrEqualTo(1920));
        Assert.That(board.rect.height * board.localScale.y, Is.LessThanOrEqualTo(1080));
        Button originalProductCard = view.GetComponentsInChildren<Button>(true).Single(b => b.name == "Product ticket:item");
        Assert.That(originalProductCard.targetGraphic.canvasRenderer, Is.Not.Null,
            "Scroll clipping requires a renderer on the actual product panel");
        view.Refresh();
        Assert.That(view.GetComponentsInChildren<Button>(true).Single(b => b.name == "Product ticket:item"), Is.SameAs(originalProductCard),
            "Unchanged products reuse cards during balance refresh");
        Button buy = view.GetComponentsInChildren<Button>(true).Single(b => b.name == "Exchange Selected Product");
        buy.onClick.Invoke(); buy.onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
        int before = reads; reply(true, "완료"); reply(true, "중복 콜백");
        Assert.That(reads, Is.EqualTo(before + 1));
        Assert.That(snapshot.PandaTokens, Is.EqualTo(500), "View must never mutate a wallet");
        view.SelectTab(TokenExchangeTab.Staff); Assert.That(buy.interactable, Is.False);
        Assert.That(view.transform.Find("Wooden Exchange Board/Product Grid/Product staff:owned/Availability")
            .GetComponent<TMP_Text>().text,
            Is.EqualTo("보유중"));
        Assert.That(view.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "Exchange Status").text,
            Is.EqualTo("이미 보유"), "Only the small product-card status is shortened");
        view.SetVisible(false); Assert.That(view.IsOpen, Is.False); Assert.That(closed, Is.EqualTo(1));
        view.SetVisible(false); Assert.That(closed, Is.EqualTo(1));
    }

    [Test]
    public void Hud_GuaranteeActuallyFillsThenResetsAndContinuesRemainingResult()
    {
        var hud = GachaCollectionMachineHud.Attach((RectTransform)_root.transform, GachaCollectionUiTheme.Load(), () => {}, () => {});
        hud.Refresh(1, 1, false);
        hud.PresentResult(99, 0, true); hud.PresentResult(0, 1, false);
        SetHudTime(hud, .3f, .5f);
        Assert.That(hud.DisplayedProgress, Is.EqualTo(1f));
        Assert.That(hud.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("100 / 100")), Is.True);
        SetHudTime(hud, .6f, .2f);
        Assert.That(hud.DisplayedProgress, Is.Zero);
        SetHudTime(hud, .9f, -.1f);
        Assert.That(hud.PendingProgressCount, Is.EqualTo(1));
        SetHudTime(hud, .18f, -.01f);
        Assert.That(hud.PendingProgressCount, Is.Zero);
        Assert.That(hud.DisplayedProgress, Is.EqualTo(.01f).Within(.001));
        Assert.That(hud.CommittedCounter, Is.EqualTo(1));
    }

    private static void SetHudTime(GachaCollectionMachineHud hud, float elapsed, float until)
    {
        typeof(GachaCollectionMachineHud).GetField("_start", Fields).SetValue(hud, Time.unscaledTime - elapsed);
        typeof(GachaCollectionMachineHud).GetField("_until", Fields).SetValue(hud, Time.unscaledTime + until);
        typeof(GachaCollectionMachineHud).GetMethod("Update", Fields).Invoke(hud, null);
    }

    [Test]
    public void WholeView_ServiceRebindKeepsNewAccountRequestWhenOldCallbackArrives()
    {
        var settings = Resources.Load<GachaEconomySettings>("GachaEconomySettings");
        Assert.That(settings, Is.Not.Null);
        GachaEconomyMemoryStore MakeStore(string id) => new GachaEconomyMemoryStore(new GachaEconomySnapshot(id,
            new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(), 1000,
                GachaEconomySaveData.Empty), 1000));
        var firstStore = MakeStore("ui-account-A");
        var secondStore = MakeStore("ui-account-B");
        var first = new GachaEconomyService(firstStore, Array.Empty<GachaData>(), settings);
        var second = new GachaEconomyService(secondStore, Array.Empty<GachaData>(), settings);
        Assert.That(first.TryEnsureExchangeCatalog(null, out string firstError), Is.True, firstError);
        Assert.That(second.TryEnsureExchangeCatalog(null, out string secondError), Is.True, secondError);
        firstStore.DeferNextSave = secondStore.DeferNextSave = true;
        var viewObject = new GameObject("Whole native view collection binding", typeof(RectTransform));
        viewObject.transform.SetParent(_root.transform, false);
        ((RectTransform)viewObject.transform).sizeDelta = new Vector2(1920,1080);
        var view = viewObject.AddComponent<UIGacha>();
        typeof(UIGacha).GetField("_editorOfflineConfigured", Fields).SetValue(view, true);
        view.VisibleState = VisibleState.Appeared;
        view.BindCollectionEconomy(first);
        view.OpenCollectionExchange();
        SettleExchange(view.CollectionExchange);
        Button buy = view.CollectionExchange.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == "Exchange Selected Product");
        buy.onClick.Invoke();
        Assert.That(first.LastTransaction.Status, Is.EqualTo(GachaTransactionStatus.Sending));
        Assert.That(firstStore.CommitCount, Is.EqualTo(1), "Only the free initial catalog is committed");
        var originalExchange = view.CollectionExchange;
        var originalHud = view.CollectionHud;

        view.BindCollectionEconomy(second);
        Assert.That(view.CollectionExchange, Is.SameAs(originalExchange), "Session replacement must reuse one modal");
        Assert.That(view.CollectionHud, Is.SameAs(originalHud));
        Assert.That(view.CollectionExchange.IsOpen, Is.False);
        view.OpenCollectionExchange();
        SettleExchange(view.CollectionExchange);
        Assert.That(buy.interactable, Is.True, "Old session's requesting flag must not lock the new account");
        buy.onClick.Invoke();
        Assert.That(second.LastTransaction.Status, Is.EqualTo(GachaTransactionStatus.Sending));
        Assert.That(buy.interactable, Is.False);

        firstStore.CompletePending(GachaStoreResult.Confirmed);
        Assert.That(firstStore.CommitCount, Is.EqualTo(2), "A hidden view does not cancel its transaction");
        Assert.That(secondStore.CommitCount, Is.EqualTo(1), "The new account still has only its initial catalog");
        Assert.That(second.Snapshot.Account.PandaTokens, Is.EqualTo(1000));
        Assert.That(typeof(UIGacha).GetField("_pendingExchangeReply", Fields).GetValue(view), Is.Not.Null,
            "A stale completion must not erase the new account's failure observer");
        Assert.That(buy.interactable, Is.False);

        secondStore.CompletePending(GachaStoreResult.RejectedBeforeCommit);
        typeof(UIGacha).GetMethod("UpdateCollectionUI", Fields).Invoke(view, null);
        Assert.That(buy.interactable, Is.True, "A rejected current request must release its own view latch");
        Assert.That(second.Snapshot.Account.PandaTokens, Is.EqualTo(1000));
        Assert.That(view.GetComponentsInChildren<TokenExchangeView>(true), Has.Length.EqualTo(1));
        Assert.That(view.GetComponentsInChildren<GachaCollectionMachineHud>(true), Has.Length.EqualTo(1));
    }

    [Test]
    public void Exchange_ResetSessionIgnoresOldCallbackWithoutCancellingCurrentRequest()
    {
        var callbacks = new List<Action<bool,string>>();
        var snapshot = new TokenExchangeSnapshot { PandaTokens = 1000, Products = new[] {
            new TokenExchangeProductView { Id = "ticket:item", Name = "아이템 뽑기권", Description = "1회",
                Quantity = 1, Price = 100, CanPurchase = true, Category = TokenExchangeTab.Tickets }
        } };
        var view = TokenExchangeView.Attach(_root.transform, GachaCollectionUiTheme.Load(), () => snapshot,
            (id, callback) => callbacks.Add(callback));
        view.SetVisible(true);
        SettleExchange(view);
        Button buy = view.GetComponentsInChildren<Button>(true).Single(button => button.name == "Exchange Selected Product");
        buy.onClick.Invoke();
        view.ResetSession();
        view.SetVisible(true);
        SettleExchange(view);
        Assert.That(buy.interactable, Is.True);
        buy.onClick.Invoke();
        callbacks[0](true, "old-account-confirmed");
        Assert.That(buy.interactable, Is.False);
        Assert.That(view.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text.Contains("old-account-confirmed")), Is.False);
        callbacks[1](true, "current-account-confirmed");
        Assert.That(buy.interactable, Is.True);
        Assert.That(view.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text.Contains("current-account-confirmed")), Is.True);
    }

    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    public void Exchange_GuideUsesSixFixedNativeFramesAndTwoDistinctTickets(int width, int height)
    {
        ((RectTransform)_root.transform).sizeDelta = new Vector2(width, height);
        var theme = GachaCollectionUiTheme.Load();
        var products = new List<TokenExchangeProductView>();
        foreach (TokenExchangeTab tab in new[] { TokenExchangeTab.Tickets, TokenExchangeTab.Staff, TokenExchangeTab.Items })
            for (int i = 0; i < (tab == TokenExchangeTab.Tickets ? 2 : 6); i++)
                products.Add(new TokenExchangeProductView { Id = tab + ":" + i, Category = tab,
                    Name = "프레임 내부에 표시하는 아주 긴 상품 이름 " + i, Rank = (Rank)(i % 5), Price = 100,
                    Quantity = 1, CanPurchase = true, Sprite = theme.Ticket });
        var snapshot = new TokenExchangeSnapshot { PandaTokens = 999, Products = products };
        var view = TokenExchangeView.Attach(_root.transform, theme, () => snapshot, (id, reply) => {});
        view.SetVisible(true); SettleExchange(view);
        int ActiveProducts() => view.GetComponentsInChildren<Button>(true)
            .Count(button => button.gameObject.activeSelf && button.name.StartsWith("Product "));
        Assert.That(ActiveProducts(), Is.EqualTo(2), "Fixed tickets must not be duplicated to fill the grid");
        Assert.That(view.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
        Assert.That(view.GetComponentsInChildren<Button>(true).Count(b => b.name == "Close Exchange"), Is.EqualTo(1));
        foreach (TokenExchangeTab tab in new[] { TokenExchangeTab.Staff, TokenExchangeTab.Items })
        {
            view.SelectTab(tab);
            Assert.That(ActiveProducts(), Is.EqualTo(6));
            var grid = (RectTransform)view.Board.Find("Product Grid");
            foreach (Button button in grid.GetComponentsInChildren<Button>(true))
            {
                var frame = button.GetComponent<Image>();
                int index = int.Parse(button.name.Substring(button.name.LastIndexOf(':') + 1));
                Assert.That(frame.sprite, Is.SameAs(theme.ProductFrame((Rank)(index % 5))));
                RectTransform rect = frame.rectTransform;
                Assert.That(rect.anchoredPosition.x + rect.rect.width, Is.LessThanOrEqualTo(grid.rect.width));
                Assert.That(-rect.anchoredPosition.y + 224, Is.LessThanOrEqualTo(grid.rect.height), "Price stays within the fixed grid");
                var name = button.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Product Name");
                Assert.That(name.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
                Assert.That(-name.rectTransform.anchoredPosition.y + name.rectTransform.rect.height,
                    Is.LessThanOrEqualTo(rect.rect.height));
            }
        }
        var chain = view.Board.Find("Left Hanging Chain").GetComponent<Image>();
        Assert.That(chain.preserveAspect, Is.True);
        Assert.That(chain.rectTransform.rect.width / chain.rectTransform.rect.height,
            Is.EqualTo(chain.sprite.rect.width / chain.sprite.rect.height).Within(.001));
        var wood = view.Board.Find("Original Exchange Wood Frame").GetComponent<Image>();
        Assert.That(wood.preserveAspect, Is.True);
        Assert.That(wood.rectTransform.rect.width / wood.rectTransform.rect.height,
            Is.EqualTo(wood.sprite.rect.width / wood.sprite.rect.height).Within(.001));
        Assert.That(view.Board.rect.width * view.Board.localScale.x, Is.LessThanOrEqualTo(width));
        Assert.That(view.Board.rect.height * view.Board.localScale.y, Is.LessThanOrEqualTo(height));
    }

    [Test]
    public void Exchange_EntranceAndPendingRequestBlockRepeatedInputAndRetainDisplayVersion()
    {
        int purchases = 0, refreshes = 0; Action<bool, string> pending = null;
        var snapshot = new TokenExchangeSnapshot { StaffVersion = 7, RemainingRefreshes = 3, CanRefreshStaff = true,
            Products = new[] { new TokenExchangeProductView { Id = "staff:7", Category = TokenExchangeTab.Staff,
                Name = "직원", Rank = Rank.Rare, Quantity = 1, Price = 100, CanPurchase = true, DisplayVersion = 7 } } };
        var view = TokenExchangeView.Attach(_root.transform, GachaCollectionUiTheme.Load(), () => snapshot,
            (id, version, reply) => { Assert.That(id, Is.EqualTo("staff:7")); Assert.That(version, Is.EqualTo(7)); purchases++; pending = reply; },
            (tab, version, reply) => { Assert.That(tab, Is.EqualTo(TokenExchangeTab.Staff)); Assert.That(version, Is.EqualTo(7)); refreshes++; pending = reply; });
        view.SelectTab(TokenExchangeTab.Staff); view.SetVisible(true);
        Button Button(string name) => view.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        var buy = Button("Exchange Selected Product"); var refresh = Button("Refresh Display");
        Assert.That(view.IsEntering, Is.True);
        var entrance = view.Board.anchoredPosition;
        view.SetVisible(true);
        Assert.That(view.Board.anchoredPosition, Is.EqualTo(entrance), "Repeated open must not create or restart a tween");
        buy.onClick.Invoke(); refresh.onClick.Invoke(); Button("Tab Items").onClick.Invoke();
        Assert.That(purchases + refreshes, Is.Zero); Assert.That(view.CurrentTab, Is.EqualTo(TokenExchangeTab.Staff));
        view.SetVisible(false);
        Assert.That(view.Board.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(view.Board.localRotation, Is.EqualTo(Quaternion.identity));
        view.SetVisible(true); SettleExchange(view);
        Assert.That(view.IsEntering, Is.False); Assert.That(buy.interactable, Is.True);
        buy.onClick.Invoke(); buy.onClick.Invoke(); refresh.onClick.Invoke();
        Assert.That(purchases, Is.EqualTo(1)); Assert.That(refreshes, Is.Zero);
        pending(true, "확정");
        refresh.onClick.Invoke(); refresh.onClick.Invoke(); buy.onClick.Invoke();
        Assert.That(refreshes, Is.EqualTo(1)); Assert.That(purchases, Is.EqualTo(1));
        pending(false, "저장 실패");
        Assert.That(buy.interactable, Is.True); Assert.That(refresh.interactable, Is.True);
        Assert.That(snapshot.RemainingRefreshes, Is.EqualTo(3), "The view cannot spend a refresh before the service commits it");
    }

    [Test]
    public void WholeView_ExchangeHidesNativeLayersAndRapidCloseRestoresPriorInputState()
    {
        var native = new GameObject("Native machine close and controls", typeof(RectTransform), typeof(CanvasGroup));
        native.transform.SetParent(_root.transform, false);
        var original = native.GetComponent<CanvasGroup>();
        original.alpha = .75f; original.interactable = false; original.blocksRaycasts = true;
        var view = _root.AddComponent<UIGacha>();
        typeof(UIGacha).GetField("_editorOfflineConfigured", Fields).SetValue(view, true);
        view.VisibleState = VisibleState.Appeared;
        var memory = new GachaEconomyMemoryStore(new GachaEconomySnapshot("hide-machine",
            new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(),
                1000, GachaEconomySaveData.Empty), 1000));
        var economy = new GachaEconomyService(memory, Array.Empty<GachaData>(), Resources.Load<GachaEconomySettings>("GachaEconomySettings"));
        view.BindCollectionEconomy(economy);
        view.OpenCollectionExchange();
        Assert.That(original.alpha, Is.Zero); Assert.That(original.interactable || original.blocksRaycasts, Is.False);
        Assert.That(view.CollectionHud.GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(memory.CommitCount, Is.EqualTo(1), "First opening persists a free initial display");
        view.CollectionExchange.SetVisible(false);
        Assert.That(original.alpha, Is.EqualTo(.75f)); Assert.That(original.interactable, Is.False);
        Assert.That(original.blocksRaycasts, Is.True);
        view.OpenCollectionExchange(); view.CollectionExchange.SetVisible(false); view.OpenCollectionExchange();
        Assert.That(original.alpha, Is.Zero); Assert.That(original.blocksRaycasts, Is.False);
        Assert.That(memory.CommitCount, Is.EqualTo(1), "Reopening cannot roll or charge a display again");
        view.CollectionExchange.SetVisible(false);
        Assert.That(original.alpha, Is.EqualTo(.75f)); Assert.That(original.blocksRaycasts, Is.True);
    }

    private static void SettleExchange(TokenExchangeView view)
    {
        float openedAt = (float)typeof(TokenExchangeView).GetField("_openedAt", Fields).GetValue(view);
        view.TickPresentation(openedAt + 1f);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeStaff_SynchronousDrawStartsBeforeSecondPaidOrTicketInput(bool ticket)
    {
        var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject nativeRoot = null;
        var fonts = new StaffGachaOfflineFonts();
        var source = Resources.Load<StaffData>("StaffData/STAFF01");
        Assert.That(source, Is.Not.Null, "Use the existing common staff fixture asset");
        var wrapper = GachaStaffData.Create(source);
        try
        {
            var memory = new GachaEconomyMemoryStore(new GachaEconomySnapshot("native-ui-reentry",
                new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
                    Array.Empty<StaffAccountStaffRecord>(), 1000, new GachaEconomySaveData(3, 3, 0, 0)), 1000));
            var store = new CountedUiStore(memory);
            var economy = new GachaEconomyService(store, new GachaData[] { wrapper },
                Resources.Load<GachaEconomySettings>("GachaEconomySettings"), (machine, eligible, guaranteed) => wrapper);
            nativeRoot = StaffGachaOfflineViewFactory.BuildInEmptyEditorScene(scene, out var view, out var staff);
            view.ConfigureEditorOfflineView(staff);
            staff.ConfigureCollectionOffline(view, economy);
            view.BindCollectionEconomy(economy);
            fonts.BindBeforeActivation(nativeRoot);
            nativeRoot.SetActive(true);
            view.SetEditorOfflineVisible(true);
            var animator = (Animator)typeof(UIStaffGacha).GetField("_gachaMacineAnimator", Fields).GetValue(staff);
            animator.fireEvents = false;
            animator.Rebind(); animator.Play("Base Layer.Idle", 0, 0); animator.Update(0);

            if (ticket) view.DrawCollectionTicket(); else staff.OnSingleGachaButtonClicked();
            Assert.That(memory.CommitCount, Is.EqualTo(1));
            Assert.That(view.IsStartGacha, Is.True, "The committed result must claim the native presentation in the same call");
            staff.OnSingleGachaButtonClicked();
            view.DrawCollectionTicket();
            view.OpenCollectionExchange();
            Assert.That(memory.CommitCount, Is.EqualTo(1), "Immediate cross-button input must not make another purchase");
            Assert.That(view.CollectionExchange.IsOpen, Is.False);
            Assert.That(economy.Snapshot.Diamonds, Is.EqualTo(ticket ? 1000 : 990));
            Assert.That(economy.Snapshot.Account.GachaEconomy.StaffTickets, Is.EqualTo(ticket ? 2 : 3));

            var display = (StaffGachaPurchaseDisplay)typeof(UIStaffGacha).GetField("_purchaseDisplay", Fields).GetValue(staff);
            display.Close();
            Assert.That(display.TryShowCompleted(false, out string error), Is.True, error);
            display.AcknowledgeResult();
            Assert.That(view.IsStartGacha, Is.False, "Acknowledging restores a usable idle machine");
            int captures = store.Captures;
            for (int i = 0; i < 100; i++) display.Tick();
            Assert.That(store.Captures, Is.EqualTo(captures), "An unchanged acknowledged result must not clone account state each frame");
            Assert.That(memory.CommitCount, Is.EqualTo(1));
        }
        finally
        {
            if (nativeRoot != null)
            {
                foreach (var scrolling in nativeRoot.GetComponentsInChildren<ScrollingImage>(true))
                {
                    var material = (Material)typeof(ScrollingImage).GetField("_material", Fields).GetValue(scrolling);
                    if (material != null && !EditorUtility.IsPersistent(material)) Object.DestroyImmediate(material);
                }
                Object.DestroyImmediate(nativeRoot);
            }
            fonts.Dispose(); Object.DestroyImmediate(wrapper);
            if (previousScene.IsValid() && previousScene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private sealed class CountedUiStore : IGachaEconomyStore
    {
        private readonly GachaEconomyMemoryStore _inner;
        public int Captures { get; private set; }
        public CountedUiStore(GachaEconomyMemoryStore inner) { _inner = inner; }
        public GachaEconomySnapshot Capture() { Captures++; return _inner.Capture(); }
        public bool CanStart(out string error) => _inner.CanStart(out error);
        public void Save(GachaEconomyTransaction transaction, Action<GachaStoreResult, string> completed)
            => _inner.Save(transaction, completed);
    }

    private void AssertBadge(bool expected)
    {
        var badge = _slotRoot.GetComponentInChildren<GachaAcquisitionBadge>(true);
        Assert.That(badge, Is.Not.Null); Assert.That(badge.IsNew, Is.EqualTo(expected));
        Assert.That(badge.gameObject.activeSelf, Is.EqualTo(expected));
    }
    private Image Frame(string field) => (Image)typeof(UIGachaCardSlot).GetField(field, Fields).GetValue(_slot);
    private static GachaCollectionUiTestData Data(Rank rank)
    { var data = ScriptableObject.CreateInstance<GachaCollectionUiTestData>(); data.Initialize(rank); return data; }
}

public sealed class GachaCollectionUiTestData : GachaData
{
    public void Initialize(Rank rank) { _id = "ui:" + rank; _rank = rank; _name = "검증 상품"; _description = "검증"; }
}
#endif
