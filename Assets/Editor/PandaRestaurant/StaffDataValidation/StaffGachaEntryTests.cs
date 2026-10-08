#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class StaffGachaEntryTests
{
    [Test]
    public void Phase4Gacha_ActualStage1StaffShopFirstEntryHasVisibleButtonsBeforeArrows()
    {
        using (var host = new EnhancementFairyStage1Host()) host.VerifyPhase4StaffShopEntry();
    }
}

internal sealed partial class EnhancementFairyStage1Host
{
    internal void VerifyPhase4StaffShopEntry()
    {
        var view = SceneComponents<UIGacha>().Single();
        var main = SceneComponents<UIMainCanvas>().Single();
        var shop = SceneComponents<UIRestaurantAdmin>().Single();
        var shopStaff = (UIStaff)Get(shop, "_staffUI");
        var nav = main.GetComponent<MobileUINavigation>();
        var staff = view.GetComponentInChildren<UIStaffGacha>(true);
        var item = view.GetComponentInChildren<UIItemGacha>(true);
        var preview = shopStaff.GetComponentInChildren<UIStaffPreview>(true);
        var entry = ((UIButtonAndText)Get(preview, "_buyButton")).GetComponentInChildren<Button>(true);
        var exit = view.transform.Find("Anime UI/UI Components/Exit Button").GetComponent<Button>();
        var staffData = Resources.LoadAll<StaffData>("StaffData").Select(GachaStaffData.Create).ToArray();
        var store = new GachaEconomyMemoryStore(new GachaEconomySnapshot("phase4-stage1-entry",
            new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
                Array.Empty<StaffAccountStaffRecord>(), 1000, new GachaEconomySaveData(20, 20, 0, 0)), 1000));
        var economy = new GachaEconomyService(store, Catalog.Cast<GachaData>().Concat(staffData).ToArray(),
            Resources.Load<GachaEconomySettings>("GachaEconomySettings"));
        using (var fonts = new StaffGachaOfflineFonts())
        {
            try
            {
                main.gameObject.SetActive(false);
                foreach (var canvas in SceneComponents<Canvas>())
                    if (!view.transform.IsChildOf(canvas.transform) && !canvas.transform.IsChildOf(view.transform))
                        canvas.gameObject.SetActive(false);
                view.gameObject.SetActive(false); shop.gameObject.SetActive(false); shopStaff.gameObject.SetActive(false);
                fonts.BindBeforeActivation(main.gameObject);
                staff.ConfigureCollectionOffline(view, economy); item.ConfigureCollectionOffline(view, economy);
                view.ConfigureEditorOfflineNavigation(staff, item); view.BindCollectionEconomy(economy);
                shop.ConfigureEditorOfflineNavigation(shopStaff); shopStaff.ConfigureEditorOfflineNavigation(shop);
                main.ConfigureEditorOfflineNavigation(view, shop, entry, exit);
                foreach (var ui in new MobileUIView[] { view, shop, shopStaff })
                { ui.OnValidate(); ui.ViewInit(nav); ui.VisibleState = VisibleState.Disappeared; }
                Set(nav, "_viewDic", new Dictionary<string, MobileUIView>
                { { "UIGacha", view }, { "RestaurantAdminUI", shop }, { "UIStaff", shopStaff } });
                Invoke(main, "Awake");
                foreach (var button in new[] { staff.SingleButton, staff.TenButton })
                    button.GetComponent<ButtonPressEffect>()?.Awake();
                var list = (UIGachaSlotList)Get(view, "_gachaItemList");
                list.gameObject.SetActive(true);
                list.Init(staffData.Cast<GachaData>().OrderByDescending(data => data.Rank).ToList());
                fonts.BindBeforeActivation(main.gameObject);
                main.gameObject.SetActive(true);
                nav.PushNoAnime("RestaurantAdminUI"); nav.PushNoAnime("UIStaff");
                for (var current = entry.transform; current != shopStaff.transform; current = current.parent)
                    current.gameObject.SetActive(true);
                entry.interactable = true;
                entry.onClick.Invoke();
                Assert.That(view.VisibleState, Is.EqualTo(VisibleState.Appearing));
                Assert.That(Get(view, "_currentGachaMachine"), Is.SameAs(staff));
                Assert.That(staff.SingleButton.IsInteractable(), Is.False);
                CompleteTweens(view.gameObject);
                Assert.That(view.VisibleState, Is.EqualTo(VisibleState.Appeared));
                foreach (var button in new[] { staff.SingleButton, staff.TenButton })
                {
                    Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True);
                    Assert.That(button.transform.localScale.x, Is.GreaterThan(.1f));
                }
                foreach (var canvas in SceneComponents<Canvas>())
                    if (canvas.isRootCanvas)
                    { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = 1f; canvas.sortingLayerName = "UI"; }
                Camera.cullingMask |= 1 << LayerMask.NameToLayer("UI");
                Move(ERestaurantFloorType.Floor1);
                Invoke(view, "UpdateCollectionUI");
                foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
                    CapturePhase3Ui(view, $"phase4-staff-shop-first-entry-{resolution.x}x{resolution.y}.png",
                        resolution.x, resolution.y, false);
                Assert.That(store.SaveAttempts, Is.Zero);
                exit.onClick.Invoke();
                Assert.That(nav.FirstView, Is.SameAs(shopStaff));
            }
            finally
            {
                foreach (var scrolling in view.GetComponentsInChildren<ScrollingImage>(true))
                { var material = (Material)Get(scrolling, "_material"); if (material != null) Object.DestroyImmediate(material); }
                foreach (var data in staffData) Object.DestroyImmediate(data);
            }
        }
    }
}
#endif
