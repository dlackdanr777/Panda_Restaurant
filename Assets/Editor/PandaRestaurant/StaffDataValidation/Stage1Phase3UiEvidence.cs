#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal sealed partial class EnhancementFairyStage1Host
{
    internal void VerifyPhase3UiEvidence()
    {
        var random = UnityEngine.Random.state;
        var view = SceneComponents<UIGacha>().Single();
        var staff = view.GetComponentInChildren<UIStaffGacha>(true);
        var item = view.GetComponentInChildren<UIItemGacha>(true);
        var list = (UIGachaSlotList)Get(view, "_gachaItemList");
        var staffData = Resources.LoadAll<StaffData>("StaffData").Select(GachaStaffData.Create).ToArray();
        var items = new[] {
            Catalog.First(d => d.UpgradeType != UpgradeType.None && d.Rank <= Rank.Normal2),
            Catalog.First(d => EnhancementFairyCatalog.IsEligible(d) && d.Rank >= Rank.Rare),
            Catalog.First(d => d.UpgradeType == UpgradeType.None) };
        Assert.That(items.Length, Is.EqualTo(3));
        GachaData next = items[0];
        var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(), 10000, new GachaEconomySaveData(20, 20, 0, 0));
        var store = new GachaEconomyMemoryStore(new GachaEconomySnapshot("stage1-ui-evidence", account, 1000));
        var economy = new GachaEconomyService(store, Catalog.Cast<GachaData>().Concat(staffData).ToArray(),
            Resources.Load<GachaEconomySettings>("GachaEconomySettings"), (machine, pool, guarantee) => next,
            new PolishEvidenceClock(), new System.Random(20261002));
        using (var fonts = new StaffGachaOfflineFonts())
        {
            try
            {
                // The unopened Stage1 file stores many editor-visible sample views.
                // Hide unrelated canvas roots without running any game/bootstrap lifecycle.
                foreach (var canvas in SceneComponents<Canvas>())
                    if (!view.transform.IsChildOf(canvas.transform) && !canvas.transform.IsChildOf(view.transform))
                        canvas.gameObject.SetActive(false);
                foreach (var floorLock in SceneComponents<FloorLockGroup>()) floorLock.gameObject.SetActive(false);
                foreach (var floorLock in SceneComponents<FloorLocker>()) Invoke(floorLock, "OnChangeFloor");
                view.gameObject.SetActive(false);
                view.transform.parent.gameObject.SetActive(false);
                fonts.BindBeforeActivation(view.transform.parent.gameObject);
                view.transform.parent.gameObject.SetActive(true);
                staff.ConfigureCollectionOffline(view, economy); item.ConfigureCollectionOffline(view, economy);
                view.ConfigureEditorOfflineNavigation(staff, item); view.BindCollectionEconomy(economy);
                list.gameObject.SetActive(true);
                list.Init(Catalog.Cast<GachaData>().OrderByDescending(d => d.Rank).ToList());
                list.UpdateMachineData(Catalog.Cast<GachaData>().OrderByDescending(d => d.Rank).ToList());
                fonts.BindBeforeActivation(view.gameObject);
                view.SetEditorOfflineVisible(true); view.SelectCollectionOfflineMachine(GachaMachineKind.Item);
                Invoke(item, "Update");
                foreach (var canvas in SceneComponents<Canvas>())
                {
                    if (!canvas.isRootCanvas) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera;
                    canvas.planeDistance = 1f; canvas.sortingLayerName = "UI";
                }
                Camera.cullingMask |= 1 << LayerMask.NameToLayer("UI");
                Move(ERestaurantFloorType.Floor2);
                CapturePhase3Ui(view, "item-idle-1920x1080.png", 1920, 1080, true);
                CapturePhase3Ui(view, "item-idle-3840x2160.png", 3840, 2160, true);
                CapturePhase3Ui(view, "item-idle-2560x1080.png", 2560, 1080, true);
                item.OnTenGachaButtonClicked();
                Assert.That(view.CollectionPayment.IsOpen, Is.True);
                CapturePhase3Ui(view, "payment-item-eleven.png", 1920, 1080, false);
                Assert.That(store.SaveAttempts, Is.Zero);
                view.CollectionPayment.Close();
                foreach (var machine in new[] { GachaMachineKind.Item, GachaMachineKind.Staff })
                foreach (bool eleven in new[] { false, true })
                {
                    view.SelectCollectionOfflineMachine(machine);
                    if (machine == GachaMachineKind.Item) { Invoke(item, "Update"); if (eleven) item.OnTenGachaButtonClicked(); else item.OnSingleGachaButtonClicked(); }
                    else { if (eleven) staff.OnTenGachaButtonClicked(); else staff.OnSingleGachaButtonClicked(); }
                    Assert.That(view.CollectionPayment.IsOpen, Is.True);
                    foreach (var resolution in new[] { new Vector2Int(1920,1080), new Vector2Int(3840,2160), new Vector2Int(2560,1080) })
                        CapturePhase3Ui(view, $"payment-{machine}-{(eleven ? "eleven" : "single")}-{resolution.x}x{resolution.y}.png", resolution.x, resolution.y, false);
                    view.CollectionPayment.Close();
                }
                view.SelectCollectionOfflineMachine(GachaMachineKind.Item);
                VerifyPolishExchange(view, economy, store, fonts);
                view.SelectCollectionOfflineMachine(GachaMachineKind.Staff);
                list.gameObject.SetActive(true); list.UpdateMachineData(staffData.Cast<GachaData>().OrderByDescending(d => d.Rank).ToList());
                CapturePhase3Ui(view, "staff-idle-1920x1080.png", 1920, 1080, true);
                staff.OnSingleGachaButtonClicked(); CapturePhase3Ui(view, "payment-staff-single.png", 1920, 1080, false);
                view.CollectionPayment.Close();
                view.SelectCollectionOfflineMachine(GachaMachineKind.Item);
                list.UpdateMachineData(Catalog.Cast<GachaData>().OrderByDescending(d => d.Rank).ToList());
                var animator = (Animator)Get(item, "_gachaMacineAnimator");
                animator.fireEvents = false;
                int beforeDraws = store.CommitCount;
                foreach (var result in items)
                {
                    next = result;
                    Assert.That(economy.TryDraw(GachaMachineKind.Item, GachaPaymentKind.DiamondsSingle, item.PresentCollectionTransaction, out string error), Is.True, error);
                    Assert.That(((List<GachaItemData>)Get(item, "_getItemList")).Single(), Is.SameAs(result));
                    item.SetStep(2);
                    animator.Rebind();
                    animator.Play("Base Layer.Start_Gacha", 0, 1f); animator.Update(0f);
                    animator.Play("Base Layer.Wait_Gacha", 0, 0f); animator.Update(0f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Wait_Gacha"), Is.True);
                    var image = (Image)Get(item, "_getItemImage");
                    Vector2 originalSize = image.rectTransform.sizeDelta, originalPivot = image.rectTransform.pivot;
                    Vector2 originalMin = image.rectTransform.anchorMin, originalMax = image.rectTransform.anchorMax;
                    int originalSibling = image.transform.GetSiblingIndex();
                    item.SetStep(3); Invoke(item, "LateUpdate");
                    Assert.That(image.sprite, Is.SameAs(result.ThumbnailSprite ?? result.Sprite));
                    Assert.That(image.gameObject.activeInHierarchy && image.enabled && image.color.a > 0, Is.True);
                    Assert.That(image.transform.GetSiblingIndex(), Is.LessThan(((Image)Get(item, "_upperCapsule")).transform.GetSiblingIndex()));
                    Assert.That(image.transform.GetSiblingIndex(), Is.LessThan(((Image)Get(item, "_lowerCapsule")).transform.GetSiblingIndex()));
                    Assert.That(view.CollectionHud.IsGaugeVisible, Is.False);
                    Assert.That(image.rectTransform.sizeDelta.x / image.rectTransform.sizeDelta.y,
                        Is.EqualTo(image.sprite.rect.width / image.sprite.rect.height).Within(.001f));
                    string kind = result.UpgradeType == UpgradeType.None ? "recipe" : result == items[0] ? "normal" : "enhancement";
                    CapturePhase3Ui(view, "item-capsule-" + kind + "-wait.png", 1920, 1080, false);
                    Vector2 fitted = image.rectTransform.sizeDelta;
                    foreach (float seconds in new[] { 0f, .27f, .6f })
                    {
                        SamplePolishOpen(animator, seconds);
                        item.SetStep(4); Invoke(item, "LateUpdate");
                        Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(fitted), "Opening halves must not enlarge content");
                        Assert.That(image.transform.GetSiblingIndex(), Is.Zero, "Content stays behind shell and smoke until pop");
                        CapturePhase3Ui(view, "item-capsule-" + kind + "-open-" + Mathf.RoundToInt(seconds * 100) + ".png", 1920, 1080, false);
                    }
                    item.Hide();
                    Assert.That(image.sprite, Is.Null); Assert.That(image.gameObject.activeSelf, Is.False);
                    Assert.That(image.rectTransform.sizeDelta, Is.EqualTo(originalSize));
                    Assert.That(image.rectTransform.pivot, Is.EqualTo(originalPivot));
                    Assert.That(image.rectTransform.anchorMin, Is.EqualTo(originalMin));
                    Assert.That(image.rectTransform.anchorMax, Is.EqualTo(originalMax));
                    Assert.That(image.transform.GetSiblingIndex(), Is.EqualTo(originalSibling));
                    item.Show();
                    Assert.That(view.CollectionHud.IsGaugeVisible, Is.True);
                }
                Assert.That(store.CommitCount, Is.EqualTo(beforeDraws + 3));
                view.SelectCollectionOfflineMachine(GachaMachineKind.Staff);
                next = staffData[0];
                var staffAnimator = (Animator)Get(staff, "_gachaMacineAnimator");
                staffAnimator.Rebind(); staffAnimator.Play("Base Layer.Idle", 0, 0); staffAnimator.Update(0);
                Assert.That(economy.TryDraw(GachaMachineKind.Staff, GachaPaymentKind.DiamondsSingle,
                    tx => staff.PresentCollectionTransaction(economy, tx), out string staffError), Is.True, staffError);
                var display = Get(staff, "_purchaseDisplay");
                Assert.That(Get(display, "_animation"), Is.Not.Null, "Use production staff single presentation for comparison");
                staffAnimator.Play("Base Layer.Start_Gacha", 0, 1); staffAnimator.Update(0);
                staffAnimator.Play("Base Layer.Wait_Gacha", 0, 0); staffAnimator.Update(0);
                Invoke(display, "Tick");
                CapturePhase3Ui(view, "staff-capsule-wait.png", 1920, 1080, false);
                foreach (float seconds in new[] { 0f, .27f, .6f })
                {
                    SamplePolishOpen(staffAnimator, seconds); Invoke(display, "Tick");
                    CapturePhase3Ui(view, "staff-capsule-open-" + Mathf.RoundToInt(seconds * 100) + ".png", 1920, 1080, false);
                }
                staff.Hide();
                Assert.That(UserInfo.GetGiveGachaItemCountDic(), Is.Empty, "Memory transactions never grant the live UserInfo");
            }
            finally
            {
                foreach (var scrolling in view.GetComponentsInChildren<ScrollingImage>(true))
                {
                    var material = (Material)Get(scrolling, "_material"); if (material != null) Object.DestroyImmediate(material);
                }
                foreach (var data in staffData) Object.DestroyImmediate(data);
                UnityEngine.Random.state = random;
            }
        }
    }

    private sealed class PolishEvidenceClock : IGachaExchangeClock
    {
        public bool TryGetUtcNow(out DateTime utc) { utc = new DateTime(2026, 10, 2, 6, 0, 0, DateTimeKind.Utc); return true; }
    }

    private static void SamplePolishOpen(Animator animator, float seconds)
    {
        float length = animator.runtimeAnimatorController.animationClips.First(x => x.name == "Open_Gacha").length;
        animator.Play("Base Layer.Open_Gacha", 0, seconds / length); animator.Update(0);
    }

    private void VerifyPolishExchange(UIGacha view, GachaEconomyService economy, GachaEconomyMemoryStore store, StaffGachaOfflineFonts fonts)
    {
        view.OpenCollectionExchange();
        var exchange = view.CollectionExchange;
        exchange.gameObject.SetActive(false);
        fonts.BindBeforeActivation(exchange.gameObject);
        exchange.gameObject.SetActive(true);
        exchange.TickPresentation((float)Get(exchange, "_openedAt") + 1);
        var offers = economy.GetDisplayedProducts(GachaExchangeCategory.Mixed);
        Assert.That(offers.Count, Is.EqualTo(6));
        Assert.That(offers.Select(x => x.Category).Distinct().Count(), Is.EqualTo(3));
        Assert.That(exchange.GetComponentsInChildren<Button>().Any(x => x.name.StartsWith("Tab ")), Is.False);
        foreach (var resolution in new[] { new Vector2Int(1920,1080), new Vector2Int(3840,2160), new Vector2Int(2560,1080) })
            CapturePhase3Ui(view, $"exchange-mixed-{resolution.x}x{resolution.y}.png", resolution.x, resolution.y, false);
        exchange.SelectProduct(offers.First(x => x.Category == GachaExchangeCategory.Items).Id);
        string id = exchange.SelectedProductId;
        var buy = exchange.GetComponentsInChildren<Button>().Single(x => x.name == "Exchange Selected Product");
        Assert.That(buy.interactable, Is.True);
        int commits = store.CommitCount;
        buy.onClick.Invoke(); buy.onClick.Invoke();
        exchange.Refresh(); // Runtime polls after the synchronous transaction leaves its preparing guard.
        Assert.That(store.CommitCount, Is.EqualTo(commits + 1));
        Assert.That(buy.interactable, Is.False);
        Assert.That(economy.CanExchange(id, 1, out _), Is.False);
        exchange.TickPresentation(Time.realtimeSinceStartup + .31f);
        CapturePhase3Ui(view, "exchange-item-sold-out.png", 1920, 1080, false);
        foreach (var offer in offers.Where(x => x.Id != id))
        {
            exchange.SelectProduct(offer.Id); Assert.That(buy.interactable, Is.True);
            buy.onClick.Invoke(); exchange.Refresh();
        }
        exchange.TickPresentation(Time.realtimeSinceStartup + .31f);
        Assert.That(exchange.GetComponentsInChildren<TMP_Text>().Count(x => x.name == "SOLD OUT"), Is.EqualTo(6));
        CapturePhase3Ui(view, "exchange-all-six-sold-out.png", 1920, 1080, false);
        var refresh = exchange.GetComponentsInChildren<Button>().Single(x => x.name == "Refresh Display");
        var label = exchange.GetComponentsInChildren<TMP_Text>().Single(x => x.name == "Remaining Refreshes");
        for (int remaining = 3; remaining >= 0; remaining--)
        {
            Assert.That(label.text, Is.EqualTo("남은 새로고침 " + remaining + "/3"));
            if (remaining > 0) { Assert.That(refresh.interactable, Is.True); refresh.onClick.Invoke(); exchange.Refresh();
                Assert.That(exchange.GetComponentsInChildren<TMP_Text>().Any(x => x.name == "SOLD OUT"), Is.False);
                Assert.That(economy.GetDisplayedProducts(GachaExchangeCategory.Mixed).Count, Is.EqualTo(6)); }
        }
        Assert.That(refresh.interactable, Is.False);
        CapturePhase3Ui(view, "exchange-refresh-zero.png", 1920, 1080, false);
        exchange.SetVisible(false);
    }

    private void CapturePhase3Ui(UIGacha view, string name, int width, int height, bool verifyCatalog)
    {
        var previous = RenderTexture.active;
        var oldTarget = Camera.targetTexture;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            target.Create(); Camera.targetTexture = target; Camera.aspect = (float)width / height;
            foreach (var label in view.transform.parent.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            Invoke(view.CollectionHud, "RefreshCatalogLayout");
            if (view.CollectionExchange.IsOpen) Invoke(view.CollectionExchange, "Fit");
            Canvas.ForceUpdateCanvases();
            if (view.CollectionPayment != null && view.CollectionPayment.IsOpen)
            {
                Invoke(view.CollectionPayment, "Fit");
                AssertPolishInside((RectTransform)view.CollectionPayment.transform.Find("Payment Frame"), name);
                var paymentFrame = (RectTransform)view.CollectionPayment.transform.Find("Payment Frame");
                Assert.That(paymentFrame.rect.width / paymentFrame.rect.height, Is.InRange(1.95f, 2f), "Keep the native ad frame proportions");
                AssertPolishInside((RectTransform)paymentFrame.Find("Capsule Badge"), name);
                AssertPolishInside((RectTransform)paymentFrame.Find("Left Capsule"), name);
                AssertPolishInside((RectTransform)paymentFrame.Find("Right Capsule"), name);
                foreach (string capsuleName in new[] { "Blue Capsule", "Purple Capsule", "Tiny Gold Capsule", "Tiny Green Capsule" })
                    AssertPolishInside((RectTransform)paymentFrame.Find(capsuleName), name);
                AssertPolishInside((RectTransform)view.CollectionPayment.DiamondButton.transform, name);
                AssertPolishInside((RectTransform)view.CollectionPayment.TicketButton.transform, name);
                var sourceSingle = view.EditorOfflineCurrentMachine is UIStaffGacha
                    ? view.GetComponentInChildren<UIStaffGacha>(true).SingleButton
                    : view.GetComponentInChildren<UIItemGacha>(true).SingleButton;
                var sourceTen = view.EditorOfflineCurrentMachine is UIStaffGacha
                    ? view.GetComponentInChildren<UIStaffGacha>(true).TenButton
                    : view.GetComponentInChildren<UIItemGacha>(true).TenButton;
                Assert.That(sourceSingle.GetComponent<CanvasGroup>().alpha, Is.Zero, "Native draw captions stay hidden behind the chooser");
                Assert.That(sourceTen.GetComponent<CanvasGroup>().alpha, Is.Zero);
                AssertPolishInside((RectTransform)paymentFrame.Find("Close Payment"), name);
                var badge = (RectTransform)paymentFrame.Find("Capsule Badge");
                float badgeTop = view.transform.InverseTransformPoint(badge.TransformPoint(
                    new Vector3(badge.rect.center.x, badge.rect.yMax))).y;
                var gaugeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.transform,
                    view.CollectionHud.transform.Find("Special Guarantee Gauge"));
                Assert.That(badgeTop, Is.LessThanOrEqualTo(gaugeBounds.min.y - 6), "Capsule must not cover the guarantee gauge");
            }
            if (view.CollectionExchange.IsOpen)
            {
                var exchange = view.CollectionExchange;
                var currency = (RectTransform)exchange.transform.Find("External Panda Token Balance");
                var wood = (RectTransform)exchange.Board.Find("Original Exchange Wood Frame");
                AssertPolishInside(currency, name); AssertPolishInside(wood, name);
                var c = RectTransformUtility.CalculateRelativeRectTransformBounds(exchange.transform, currency);
                var w = RectTransformUtility.CalculateRelativeRectTransformBounds(exchange.transform, wood);
                Assert.That(c.min.x, Is.GreaterThan(w.max.x), "Currency sits outside the right frame");
                var labels = exchange.GetComponentsInChildren<TMP_Text>();
                Assert.That(labels.Single(x => x.name == "Title").text, Is.EqualTo("토큰교환소"));
                Assert.That(labels.Single(x => x.name == "Available Panda Tokens").text, Is.EqualTo(view.Economy.Snapshot.Account.PandaTokens.ToString("N0")));
                Assert.That(labels.Any(x => x.text.Contains("반복 교환") || x.text.Contains("공유") || x.text.Contains("고정 판매") || x.text.Contains("저장 상태")), Is.False);
                var footer = labels.Single(x => x.name == "Remaining Refreshes").rectTransform;
                var refresh = (RectTransform)exchange.Board.Find("Refresh Display");
                var f = RectTransformUtility.CalculateRelativeRectTransformBounds(exchange.Board, footer);
                var r = RectTransformUtility.CalculateRelativeRectTransformBounds(exchange.Board, refresh);
                Assert.That(r.min.x - f.max.x, Is.InRange(0f, 25f));
                Assert.That(Mathf.Abs(f.center.y - r.center.y), Is.LessThan(1f));
            }
            if (verifyCatalog)
            {
                var list = (UIGachaSlotList)Get(view, "_gachaItemList");
                // EditMode has no ScrollRect LateUpdate: rebuild bounds after the render target's canvas layout.
                var scroll = (ScrollRect)Get(list, "_scrollRect");
                scroll.Rebuild(CanvasUpdate.PostLayout);
                list.UpdateMachineData((List<GachaData>)Get(list, "_dataList"));
                Canvas.ForceUpdateCanvases();
                var frame = list.transform.Find("Background Image") as RectTransform;
                var button = view.CollectionHud.ExchangeButton.transform as RectTransform;
                var exit = ((GameObject)Get(view, "_uiComponents")).transform.Find("Exit Button");
                Assert.That(RectTransformUtility.CalculateRelativeRectTransformBounds(view.transform, frame)
                    .Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(view.transform, exit)), Is.False, name + " close must not cover catalog");
                Assert.That(button.parent, Is.SameAs(view.CollectionHud.transform));
                Assert.That(view.GetComponentsInChildren<RectTransform>(true).Count(r => r.name == "Open Token Exchange"), Is.EqualTo(1));
                var leftButton = RectTransformUtility.CalculateRelativeRectTransformBounds(view.transform, button);
                var viewRect = (RectTransform)view.transform;
                Assert.That(leftButton.min.x - viewRect.rect.xMin, Is.EqualTo(32).Within(1));
                Assert.That(viewRect.rect.yMax - leftButton.max.y, Is.EqualTo(224).Within(1));
                Assert.That(leftButton.Intersects(RectTransformUtility.CalculateRelativeRectTransformBounds(view.transform, frame)), Is.False);
                var corners = new Vector3[4]; button.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 viewport = Camera.WorldToViewportPoint(corner);
                    Assert.That(viewport.x, Is.InRange(0f, 1f), name);
                    Assert.That(viewport.y, Is.InRange(0f, 1f), name);
                }
                var rows = list.GetComponentsInChildren<UIGachaItemSlot>(true).Where(s => s.gameObject.activeSelf)
                    .OrderByDescending(s => s.RectTransform.localPosition.y).Take(8).ToArray();
                Assert.That(rows.Length, Is.EqualTo(8));
                foreach (var row in rows)
                {
                    row.RectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        Vector3 local = scroll.viewport.InverseTransformPoint(corner);
                        Assert.That(local.y, Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1), name + " four complete rows fit");
                    }
                }
            }
            foreach (var light in SceneComponents<UnityEngine.Rendering.Universal.Light2D>())
                if (light.isActiveAndEnabled) Invoke(light, "LateUpdate");
            Camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory(EvidenceDirectory); File.WriteAllBytes(Path.Combine(EvidenceDirectory, name), image.EncodeToPNG());
            if (name.StartsWith("item-capsule-", StringComparison.Ordinal) && name.EndsWith("-wait.png", StringComparison.Ordinal))
            {
                var content = (Image)Get(view.GetComponentInChildren<UIItemGacha>(true), "_getItemImage");
                var withContent = image.GetPixels32();
                content.enabled = false;
                try
                {
                    Canvas.ForceUpdateCanvases(); Camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    var withoutContent = image.GetPixels32();
                    int visiblePixels = 0;
                    for (int i = 0; i < withContent.Length; i++)
                        if (Math.Abs(withContent[i].r - withoutContent[i].r) > 2 || Math.Abs(withContent[i].g - withoutContent[i].g) > 2 ||
                            Math.Abs(withContent[i].b - withoutContent[i].b) > 2) visiblePixels++;
                    Assert.That(visiblePixels, Is.Zero, name + ": opaque closed shell must cover every content pixel");
                }
                finally { content.enabled = true; }
            }
        }
        finally
        {
            Camera.targetTexture = oldTarget; RenderTexture.active = previous;
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        }
    }

    private void AssertPolishInside(RectTransform rect, string name)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var viewport = Camera.WorldToViewportPoint(corner);
            Assert.That(viewport.x, Is.InRange(0f, 1f), name);
            Assert.That(viewport.y, Is.InRange(0f, 1f), name);
        }
    }
}
#endif
