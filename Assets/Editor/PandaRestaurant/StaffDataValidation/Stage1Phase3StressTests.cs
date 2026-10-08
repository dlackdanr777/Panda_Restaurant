#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Muks.MobileUI;
using Muks.BackEnd;
using Muks.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public sealed class Stage1Phase3StressTests
{
    [Test]
    public void Stage1_ActualShopFloor3FaultAndTwentyCyclesLeaveNoInputLock()
    {
        using (var host = new EnhancementFairyStage1Host()) host.VerifyPhase3Transitions();
    }
    [TestCase(false, ERestaurantFloorType.Floor1)]
    [TestCase(true, ERestaurantFloorType.Floor2)]
    public void Shop_ContextFallbackDoesNotInventThirdFloorCatalog(bool vip, ERestaurantFloorType expected)
        => Assert.That(UIRestaurantAdmin.ResolveShopFloor(ERestaurantFloorType.Floor3, vip), Is.EqualTo(expected));
    [Test]
    public void Stage1_PaymentLayoutAndNativeSingleItemCapsuleEvidence()
    {
        using (var host = new EnhancementFairyStage1Host()) host.VerifyPhase3UiEvidence();
    }
}

internal sealed partial class EnhancementFairyStage1Host
{
    internal AttendanceData PreparePhase3Attendance(StaffGachaOfflineFonts fonts, DateTime now, int day)
    {
        Replace(typeof(Muks.DataBind.DataBind), "_textBindDic", new Muks.DataBind.DataBindContainer<string>());
        Replace(typeof(UserInfo), "_lastAttendanceTime", day == 1 ? "" : now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"));
        Replace(typeof(UserInfo), "_totalAttendanceDays", day - 1);
        foreach (string name in new[] { "_money", "_totalAddMoney", "_dailyAddMoney", "_weeklyAddMoney" })
            Replace(typeof(UserInfo), name, 100L);
        foreach (string name in new[] { "OnChangeMoneyHandler", "OnUpdateAttendanceDataHandler" })
            Replace(typeof(UserInfo), name, null);
        var data = _services.AddComponent<AttendanceDataManager>();
        Replace(typeof(AttendanceDataManager), "_instance", data);
        Set(data, "_attendanceDic", Invoke(data, "SetData", "AttendanceREWARD"));
        var attendance = SceneComponents<UIAttendance>().Single();
        attendance.gameObject.SetActive(false);
        var slots = (List<UIAttendanceSlot>)Get(attendance, "_slotList");
        for (int i = 0; i < 7; i++) slots.Add(Object.Instantiate((UIAttendanceSlot)Get(attendance, "_slotPrefab"), (RectTransform)Get(attendance, "_slotParent")));
        fonts.BindBeforeActivation(attendance.gameObject);
        return data.GetRewardDic()[day];
    }

    internal void BindAndAssertPhase3Attendance(int day, bool due, bool canClaim)
    {
        var view = SceneComponents<UIAttendance>().Single();
        Invoke(view, "BindConfirmedState", day, due, canClaim);
        AssertPhase3Attendance(day, !due);
        Assert.That(((UIButtonAndPressEffect)Get(view, "_attendanceButton")).interactable, Is.EqualTo(due && canClaim));
    }

    internal void AssertPhase3Attendance(int day, bool claimed)
    {
        var view = SceneComponents<UIAttendance>().Single();
        var bar = (UILoadingBar)Get(view, "_loadingBar");
        float fill = AttendanceProgress.Fill(day, claimed);
        Assert.That(((Image)Get(bar, "_gaugeBar")).fillAmount, Is.EqualTo(fill).Within(.001f));
        Assert.That(((Image)Get(bar, "_iconImage")).rectTransform.anchoredPosition.x,
            Is.EqualTo(((RectTransform)Get(bar, "_loadingBar")).rect.width * fill).Within(.01f));
        var slot = ((List<UIAttendanceSlot>)Get(view, "_slotList"))[(day - 1) % 7];
        Assert.That(((GameObject)Get(slot, "_checkImage")).activeSelf, Is.EqualTo(claimed));
        Assert.That(((GameObject)Get(slot, "_todayArrow")).activeSelf, Is.EqualTo(!claimed));
        Assert.That(((GameObject)Get(slot, "_notCheckImage")).activeSelf, Is.False);
    }

    internal void VerifyPhase3Transitions()
    {
        var log = new List<string>();
        var view = SceneComponents<UIGacha>().Single();
        var shop = SceneComponents<UIRestaurantAdmin>().Single();
        var attendance = SceneComponents<UIAttendance>().Single();
        var nav = view.GetComponentInParent<MobileUINavigation>(true);
        var coordinator = SceneComponents<UINavigationCoordinator>().Single();
        var main = (MainScene)Get(shop, "_mainScene");
        var services = _services.AddComponent<GameManager>();
        Replace(typeof(GameManager), "_instance", services);
        Replace(typeof(UserInfo), "_lastAttendanceTime", "");
        Replace(typeof(UserInfo), "_totalAttendanceDays", 0);
        var attendanceData = _services.AddComponent<AttendanceDataManager>();
        Replace(typeof(AttendanceDataManager), "_instance", attendanceData);
        Set(attendanceData, "_attendanceDic", Invoke(attendanceData, "SetData", "AttendanceREWARD"));
        var authoredBackgrounds = (ScrollingImage[])Get(shop, "_scrollImages");
        Assert.That(authoredBackgrounds.Length, Is.EqualTo(2), "Actual Stage1 has only two business-floor backgrounds");
        foreach (var background in authoredBackgrounds) background.Init();
        var legacyFault = Assert.Throws<TargetInvocationException>(() => Invoke(shop, "SetBackgroundImageImmediate", ERestaurantFloorType.Floor3));
        Assert.That(legacyFault.InnerException, Is.TypeOf<IndexOutOfRangeException>(), "Prove the exact pre-fix Floor3 background access");
        log.Add("Legacy Stage1 Floor3 background access reproduced IndexOutOfRangeException (2 backgrounds, index 2).");
        shop.gameObject.SetActive(false);
        var shopStaff = (UIStaff)Get(shop, "_staffUI"); shopStaff.gameObject.SetActive(false);
        shop.ConfigureEditorOfflineNavigation(shopStaff);
        Set(shop, "_tabs", new[] { (UIRestaurantAdminTab)Get(shop, "_furnitureTab"), (UIRestaurantAdminTab)Get(shop, "_staffTab"),
            (UIRestaurantAdminTab)Get(shop, "_recipeTab"), (UIRestaurantAdminTab)Get(shop, "_kitchenTab") });
        Set(shop, "_tmpScale", ((GameObject)Get(shop, "_mainUI")).transform.localScale);
        shop.ViewInit(nav);
        view.gameObject.SetActive(false);
        attendance.gameObject.SetActive(false);
        var slots = (List<UIAttendanceSlot>)Get(attendance, "_slotList");
        for (int i = 0; i < 7; i++) slots.Add(Object.Instantiate((UIAttendanceSlot)Get(attendance, "_slotPrefab"), (RectTransform)Get(attendance, "_slotParent")));
        Set(attendance, "_uiNav", nav);
        var staff = view.GetComponentInChildren<UIStaffGacha>(true);
        var item = view.GetComponentInChildren<UIItemGacha>(true);
        var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
            Array.Empty<StaffAccountStaffRecord>(), 0, new GachaEconomySaveData());
        var economy = new GachaEconomyService(new GachaEconomyMemoryStore(new GachaEconomySnapshot("phase3-stage1", account, 0)),
            Catalog.Cast<GachaData>().ToArray(), Resources.Load<GachaEconomySettings>("GachaEconomySettings"));
        using (var fonts = new StaffGachaOfflineFonts())
        {
            fonts.BindBeforeActivation(view.gameObject); fonts.BindBeforeActivation(shop.gameObject); fonts.BindBeforeActivation(attendance.gameObject);
            staff.ConfigureCollectionOffline(view, economy); item.ConfigureCollectionOffline(view, economy);
            view.ConfigureEditorOfflineNavigation(staff, item); view.BindCollectionEconomy(economy);
            view.PrepareItemMachine(); view.ViewInit(nav); fonts.BindBeforeActivation(view.gameObject);
            var faultRoot = new GameObject("Failed opening probe", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(faultRoot, _scene);
            faultRoot.SetActive(false);
            var fault = faultRoot.AddComponent<Phase3FailedOpeningProbe>();
            fault.Failure = () => Invoke(shop, "SetBackgroundImageImmediate", ERestaurantFloorType.Floor3);
            var registered = ((MobileViewDicStruct[])Get(nav, "_uiViews")).ToDictionary(entry => entry.Name, entry => entry.UIView);
            registered["Fault"] = fault;
            Set(nav, "_viewDic", registered);
            Invoke(coordinator, "Init");
            Assert.Throws<TargetInvocationException>(() => nav.Push("Fault"));
            Assert.That(nav.Count, Is.Zero); Assert.That(nav.ViewsVisibleStateCheck(), Is.True);
            Assert.That(fault.gameObject.activeSelf, Is.False); Assert.That(fault.VisibleState, Is.EqualTo(VisibleState.Disappeared));
            log.Add("The same throwing background path in navigation rolls back only the failed view; count=0, transition gate clear.");
            Restore(Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(8).Select(fairy => new KeyValuePair<string, int>(fairy.Id, 1)));
            for (int cycle = 0; cycle < 20; cycle++)
            {
                Move(ERestaurantFloorType.Floor2); BeginMove(ERestaurantFloorType.Floor3); CompleteCameraTween();
                Move(cycle % 2 == 0 ? RestaurantType.Hall : RestaurantType.Kitchen);
                Set(main, "_currentFloor", ERestaurantFloorType.Floor3);
                Advance(.1f);
                var body = Bodies().First(candidate =>
                {
                    var point = Camera.WorldToViewportPoint(candidate.bounds.center);
                    return point.x > .1f && point.x < .9f && point.y > 0f && point.y < 1f;
                });
                Vector2 tap = Camera.WorldToScreenPoint(body.bounds.center);
                Invoke(Controller, "BeginWorldTap", tap);
                Invoke(Controller, "TrackWorldTap", tap + Vector2.right * 150);
                Assert.That(Invoke(Controller, "CompleteWorldTap", tap), Is.False, "Drag across a fairy is not a tap");
                Invoke(Controller, "BeginWorldTap", tap);
                Assert.That(Invoke(Controller, "CompleteWorldTap", tap), Is.True, "cycle " + cycle);
                Assert.That(Controller.IsWorldInputBlocked, Is.True);
                var cardRoot = (GameObject)Get(Habitat, "_cardCanvas");
                var canvas = cardRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera;
                canvas.planeDistance = 1f; canvas.overrideSorting = true; canvas.sortingLayerName = "UI";
                Camera.cullingMask |= 1 << LayerMask.NameToLayer("UI");
                Capture("stress-tap-frames/tap-" + cycle.ToString("D2") + ".png");
                Canvas.ForceUpdateCanvases();
                ((GachaResultCardPopup)Get(Habitat, "_itemPopup")).BringToFront();
                var pointer = new PointerEventData(SceneComponents<EventSystem>().Single()) { position = new Vector2(5, 5), button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); cardRoot.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
                Assert.That(hits.Count, Is.GreaterThan(0));
                var dismiss = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(dismiss.name, Is.EqualTo("Dismiss Background"));
                ExecuteEvents.Execute(dismiss, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(dismiss, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(dismiss, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(Habitat.IsItemCardOpen, Is.False);
                Habitat.CloseItemCard();
                Assert.That(Controller.IsWorldInputBlocked, Is.False);
                Set(_sound, "_currentBackgroundClip", Get(shop, "_shopMusic"));
                nav.Push("RestaurantAdminUI"); nav.Push("RestaurantAdminUI"); nav.Push("UIGacha");
                CompleteTweens(shop.gameObject);
                Assert.That(shop.IsReadyForDetail, Is.True, "Real Shop.Show after Floor3 entry, cycle " + cycle);
                Assert.That(Get(shop, "_floorType"), Is.Not.EqualTo(ERestaurantFloorType.Floor3));
                Assert.That(nav.Count, Is.EqualTo(1));
                Set(_sound, "_currentBackgroundClip", Get(main, "_mainSceneMusic"));
                nav.Pop(); CompleteTweens(shop.gameObject);
                ResumePhase3Habitat();
                nav.Push("UIGacha"); nav.Push("UIGacha"); CompleteTweens(view.gameObject);
                Assert.That(view.VisibleState, Is.EqualTo(VisibleState.Appeared));
                view.OpenCollectionExchange(); Assert.That(view.CollectionExchange.IsOpen, Is.True);
                view.CollectionExchange.SetVisible(false);
                Assert.That(view.CollectionExchange.IsOpen, Is.False);
                Assert.That(view.GetComponentsInChildren<CanvasGroup>(true).Where(g => g.alpha == 0).All(g => !g.blocksRaycasts || !g.gameObject.activeInHierarchy), Is.True);
                nav.Pop(); ResumePhase3Habitat();
                nav.Push("UIAttendance"); CompleteTweens(attendance.gameObject);
                Assert.That(attendance.VisibleState, Is.EqualTo(VisibleState.Appeared));
                nav.Pop(); CompleteTweens(attendance.gameObject); ResumePhase3Habitat();
                Assert.That(coordinator.GetOpenViewCount(), Is.Zero, "cycle " + cycle);
                Assert.That(nav.ViewsVisibleStateCheck(), Is.True);
                Assert.That(Controller.IsWorldInputBlocked, Is.False);
                Assert.That((bool)Get(Controller, "_isMoveAction"), Is.False);
                Invoke(Controller, "Update");
                Assert.That((bool)Get(Controller, "_isStopAction"), Is.False);
                Assert.That(new[] { view.gameObject, shop.gameObject, attendance.gameObject }.All(root => !root.activeInHierarchy), Is.True);
                Move(ERestaurantFloorType.Floor2);
                float beforeDrag = Camera.transform.position.y;
                DragUp(.5f);
                Assert.That(Camera.transform.position.y, Is.GreaterThan(beforeDrag), "Functional floor drag, cycle " + cycle);
                Invoke(Controller, "HandleCameraSnapBackOrMove"); CompleteCameraTween();
                Move(ERestaurantFloorType.Floor3);
                log.Add($"Cycle {cycle + 1}: fairy/shop/gacha/exchange/attendance/hall-kitchen/floors PASS; views=0; moving=false; stop=false; worldLock=false; drag moved.");
            }
            foreach (var scrolling in view.GetComponentsInChildren<ScrollingImage>(true).Concat(authoredBackgrounds))
            {
                var material = (Material)Get(scrolling, "_material");
                if (material != null) Object.DestroyImmediate(material);
            }
            Object.DestroyImmediate(faultRoot);
        }
        Directory.CreateDirectory(EvidenceDirectory);
        File.WriteAllLines(Path.Combine(EvidenceDirectory, "stage1-input-stress.txt"), log);
    }

    private void ResumePhase3Habitat()
    {
        var routine = (IEnumerator)Invoke(Habitat, "ResumeAfterClosingViews");
        int limit = 0; while (routine.MoveNext() && limit++ < 4) { }
        Assert.That(limit, Is.LessThan(4));
        Assert.That(Habitat.IsVisible, Is.True);
    }
}

public sealed class Phase3FailedOpeningProbe : MobileUIView
{
    internal Action Failure;
    public override void Init() { }
    public override void Show() { VisibleState = VisibleState.Appearing; Failure(); }
    public override void Hide() { VisibleState = VisibleState.Disappeared; gameObject.SetActive(false); }
}
#endif
