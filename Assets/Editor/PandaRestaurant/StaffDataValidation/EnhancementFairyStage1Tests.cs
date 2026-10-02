#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Muks.Tween;
using Muks.UI;
using Muks.MobileUI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class EnhancementFairyStage1Tests
{
    [Test]
    public void Stage1_DragRevealPreparesOpaqueFairiesBeforeArrivalAndKeepsTheExistingCap()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var items = host.Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(14).ToArray();
            Assert.That(items.Length, Is.GreaterThan(12));
            host.Restore(items.Select(item => new KeyValuePair<string, int>(item.Id, 1)));
            host.Move(ERestaurantFloorType.Floor2);
            Assert.That(host.Habitat.PooledObjectCount, Is.Zero);
            host.DragUp(.5f);
            Assert.That(host.Controller.CurrentFloor, Is.EqualTo(ERestaurantFloorType.Floor2));
            Assert.That(host.Habitat.ActiveCount, Is.EqualTo(12), "Prepared during the first upward drag, before arrival");
            Assert.That(host.Bodies().All(body => body.color.a == 1f), Is.True, "No late fade-in at reveal");
            var identity = host.Identity();
            var brain = host.Habitat.GetActiveBrain(0);
            float elapsed = brain.ElapsedSeconds;
            host.Advance(20f);
            CollectionAssert.AreEquivalent(identity, host.Identity(), "Slow reveal must not rotate the roster");
            Assert.That(brain.ElapsedSeconds, Is.EqualTo(elapsed));
            float peekY = host.Floor.transform.position.y + host.Habitat.Settings.GroundArea.y - host.Camera.orthographicSize + 1f;
            float remaining = peekY - host.Camera.transform.position.y;
            for (int frame = 0; frame < 18; frame++)
            {
                host.DragUp(remaining / 18f);
                host.Capture("prewarm-frames/frame-" + frame.ToString("D4") + ".png");
            }
            Assert.That(host.Controller.CurrentFloor, Is.EqualTo(ERestaurantFloorType.Floor2));
            Assert.That(host.VisibleBodyCount(), Is.GreaterThan(0), "The first partial Floor3 viewport already contains opaque fairies");
            host.Capture("stage1-prewarm-before-arrival.png");
            host.Move(ERestaurantFloorType.Floor2);
            Assert.That(host.Habitat.ActiveCount, Is.Zero, "Cancelled reveal hides but retains the pool");
            host.BeginMove(ERestaurantFloorType.Floor3);
            Assert.That((bool)EnhancementFairyStage1Host.Get(host.Controller, "_isMoveAction"), Is.True);
            Assert.That(host.Habitat.ActiveCount, Is.EqualTo(12), "Button/tween path also prepares before completion");
            CollectionAssert.AreEquivalent(identity, host.Identity());
            host.CompleteCameraTween();
            CollectionAssert.AreEquivalent(identity, host.Identity());
        }
    }

    [Test]
    public void Stage1_PendingBirthSurvivesSlowAndCancelledPrewarmUntilArrival()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var item = host.Catalog.First(EnhancementFairyCatalog.IsEligible);
            host.Move(ERestaurantFloorType.Floor2);
            host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 1) });
            EnhancementFairyAcquisitionEvents.PublishConfirmed("prewarm-birth", item.Id);
            host.DragUp(.5f);
            host.Advance(2f);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.Zero);
            Assert.That(host.Bodies().Single().color.a, Is.Zero, "A pending birth must not first appear full-size and then shrink");
            host.Move(ERestaurantFloorType.Floor2);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
            host.Move(ERestaurantFloorType.Floor3);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void Stage1_FairyTapReusesReadOnlyCardBlocksDragAndClosesWithoutChangingOwnership()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            foreach (var itemUi in host.SceneComponents<UIItemGacha>()) fonts.BindBeforeActivation(itemUi.gameObject);
            var items = host.Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(8).ToArray();
            host.Restore(items.Select(item => new KeyValuePair<string, int>(item.Id, 3)));
            host.Move(ERestaurantFloorType.Floor3);
            host.Advance(.5f);
            var originalOwnership = UserInfo.GetGiveGachaItemCountDic().ToArray();
            var originalLevels = new Dictionary<string, int>((Dictionary<string, int>)EnhancementFairyStage1Host.Field(typeof(UserInfo), "_giveGachaItemLevelDic", true).GetValue(null));
            var body = host.Bodies().First(value =>
            {
                var p = host.Camera.WorldToViewportPoint(value.bounds.center);
                return p.x > .1f && p.x < .9f && p.y > 0f && p.y < 1f;
            });
            Vector2 position = host.Camera.WorldToScreenPoint(body.bounds.center);
            var selected = items.Single(item => body.transform.parent.name == "Fairy " + item.Id);
            EnhancementFairyStage1Host.Invoke(host.Controller, "BeginWorldTap", position);
            EnhancementFairyStage1Host.Invoke(host.Controller, "TrackWorldTap", position + Vector2.right * 150f);
            Assert.That(EnhancementFairyStage1Host.Invoke(host.Controller, "CompleteWorldTap", position), Is.False, "Out-and-back drag is not a tap");
            Assert.That(host.Habitat.IsItemCardOpen, Is.False);
            EnhancementFairyStage1Host.Invoke(host.Controller, "BeginWorldTap", position);
            Assert.That(EnhancementFairyStage1Host.Invoke(host.Controller, "CompleteWorldTap", position), Is.True);
            Assert.That(host.Habitat.IsItemCardOpen, Is.True);
            Assert.That(host.Controller.IsWorldInputBlocked, Is.True);
            var popup = (GachaResultCardPopup)EnhancementFairyStage1Host.Get(host.Habitat, "_itemPopup");
            Assert.That(((TMPro.TMP_Text)EnhancementFairyStage1Host.Get(popup.Card, "_nameText")).text, Is.EqualTo(selected.Name));
            Assert.That(((TMPro.TMP_Text)EnhancementFairyStage1Host.Get(popup.Card, "_effectText")).text, Is.EqualTo(Utility.GetGachaItemEffectDescription(selected)));
            Assert.That(host.Habitat.TryOpenItemCard(position), Is.False);
            Assert.That(host.Floor.GetComponentsInChildren<UIGachaCard>(true).Length, Is.EqualTo(1));
            var canvas = (GameObject)EnhancementFairyStage1Host.Get(host.Habitat, "_cardCanvas");
            var inspectionCanvas = canvas.GetComponent<Canvas>();
            Canvas.ForceUpdateCanvases();
            Assert.That(inspectionCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            // PreviewScene does not submit overlay canvases to the display. Capture and
            // raycast the same native graphics through a camera here; the separate sanitized
            // ResultPopup PlayerLoop test verifies the actual overlay canvas and top hit.
            int oldMask = host.Camera.cullingMask;
            inspectionCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            inspectionCanvas.worldCamera = host.Camera;
            inspectionCanvas.planeDistance = 1f;
            inspectionCanvas.overrideSorting = true;
            inspectionCanvas.sortingLayerName = "UI";
            host.Camera.cullingMask |= 1 << LayerMask.NameToLayer("UI");
            host.Capture("stage1-tap-item-card.png");
            var eventSystem = host.SceneComponents<EventSystem>().Single();
            var pointer = new PointerEventData(eventSystem) { position = new Vector2(5f, 5f), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            var dismiss = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(dismiss.name, Is.EqualTo("Dismiss Background"), "Top hit on the native inspection graphics");
            ExecuteEvents.Execute(dismiss, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(host.Habitat.IsItemCardOpen, Is.False);
            Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
            CollectionAssert.AreEquivalent(originalOwnership, UserInfo.GetGiveGachaItemCountDic());
            CollectionAssert.AreEquivalent(originalLevels, (Dictionary<string, int>)EnhancementFairyStage1Host.Field(typeof(UserInfo), "_giveGachaItemLevelDic", true).GetValue(null));
            position = host.Camera.WorldToScreenPoint(body.bounds.center);
            Assert.That(host.Habitat.TryOpenItemCard(position), Is.True);
            Assert.That(EnhancementFairyStage1Host.Get(host.Habitat, "_itemPopup"), Is.SameAs(popup));
            host.Camera.cullingMask = oldMask;
            inspectionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            inspectionCanvas.worldCamera = null;
            inspectionCanvas.sortingLayerName = "Default";
            host.VerifyActualGachaOpenClose();
            Assert.That(host.Habitat.IsItemCardOpen, Is.False, "Other navigation closes the inspection first");
            Assert.That(host.Controller.IsWorldInputBlocked, Is.False);
        }
    }

    [Test]
    public void Stage1_ConfirmedBirthBehindGachaPreparesImmediatelyAndPlaysOnceAfterClose()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            host.Move(ERestaurantFloorType.Floor3);
            var item = host.Catalog.First(EnhancementFairyCatalog.IsEligible);
            host.VerifyActualGachaOpenClose(() =>
            {
                host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 1) });
                EnhancementFairyAcquisitionEvents.PublishConfirmed("covered-birth", item.Id);
                Assert.That(host.Habitat.PooledObjectCount, Is.EqualTo(1), "Instantiate on the confirmed event while Floor3 is covered");
                Assert.That(host.Habitat.ActiveCount, Is.Zero);
                Assert.That(host.Habitat.ArrivalPresentationCount, Is.Zero);
                Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
            });
            Assert.That(host.Habitat.ActiveCount, Is.EqualTo(1));
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
            Assert.That(EnhancementFairyAcquisitionEvents.PublishConfirmed("duplicate-birth", item.Id), Is.False);
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void Stage1_ProductionRestoreHallKitchenFloorReturnAndReentryPreserveObjects()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            host.Move(ERestaurantFloorType.Floor3);
            Assert.That(host.Habitat.ActiveCount, Is.Zero, "A: empty production ownership");
            Assert.That(UserInfo.GetUnlockFloor(EStage.Stage1), Is.EqualTo(ERestaurantFloorType.Floor2));
            Assert.That(host.Habitat.IsVisible, Is.True, "The accessible decorative floor must not require a new business-floor unlock.");
            var items = host.Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(8).ToArray();
            host.Restore(items.Select((item, index) => new KeyValuePair<string, int>(item.Id, index == 0 ? 0 : 7)));
            Assert.That(host.Habitat.ActiveCount, Is.EqualTo(items.Length), "B: includes consumed materials and deduplicates quantities");
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.Zero);
            host.Advance(.5f);
            var before = host.Identity();
            var positions = host.Positions();
            host.Capture("stage1-hall.png");
            Assert.That(host.VisibleBodyCount(), Is.GreaterThan(0), "Actual Hall camera frustum contains fairies");
            host.Move(RestaurantType.Kitchen);
            Assert.That(host.Habitat.IsVisible, Is.True, "C: Kitchen is part of the same habitat");
            CollectionAssert.AreEquivalent(before, host.Identity());
            CollectionAssert.AreEqual(positions, host.Positions(), "A camera transition must not teleport fairies");
            Assert.That(host.VisibleBodyCount(), Is.GreaterThan(0), "Actual Kitchen camera frustum contains fairies");
            host.Capture("stage1-kitchen.png");
            host.Move(RestaurantType.Hall);
            CollectionAssert.AreEquivalent(before, host.Identity());
            var brain = host.Habitat.GetActiveBrain(0);
            float elapsed = brain.ElapsedSeconds;
            host.Move(ERestaurantFloorType.Floor1);
            Assert.That(host.Habitat.ActiveCount, Is.Zero);
            Assert.That(host.Habitat.enabled, Is.False);
            host.Advance(1f);
            Assert.That(brain.ElapsedSeconds, Is.EqualTo(elapsed), "D: hidden simulation is suspended");
            host.Move(ERestaurantFloorType.Floor3);
            CollectionAssert.AreEquivalent(before, host.Identity());
            Assert.That(host.Habitat.GetActiveBrain(0), Is.SameAs(brain));
            host.RecordCameraTour();
            Assert.That(EnhancementFairyHabitat.AttachTo(host.Floor), Is.SameAs(host.Habitat));
            Assert.That(host.Floor.GetComponents<EnhancementFairyHabitat>().Length, Is.EqualTo(1));
            host.ReloadStage();
            host.Move(ERestaurantFloorType.Floor3);
            Assert.That(host.Habitat.ActiveCount, Is.EqualTo(items.Length), "F: actual Stage1 reentry restores ownership");
            Assert.That(host.Floor.GetComponents<EnhancementFairyHabitat>().Length, Is.EqualTo(1));
            Assert.That(host.Habitat.ArrivalPresentationCount, Is.Zero);
            Assert.That(UserInfo.GetUnlockFloor(EStage.Stage1), Is.EqualTo(ERestaurantFloorType.Floor2), "Never change progression to make fairies visible");
        }
    }

    [Test]
    public void Stage1_ActiveLockAndClosingUiBlockThenRestoreTheExistingHabitat()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var item = host.Catalog.First(EnhancementFairyCatalog.IsEligible);
            host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 1) });
            host.Move(ERestaurantFloorType.Floor3);
            var before = host.Identity();
            var floorLock = host.Floor.GetComponentsInChildren<FloorLockGroup>(true).Single();
            floorLock.gameObject.SetActive(true);
            host.RefreshVisibility();
            Assert.That(host.Habitat.IsVisible, Is.False, "A real active floor lock still hides fairies");
            floorLock.gameObject.SetActive(false);
            host.RefreshVisibility();
            Assert.That(host.Habitat.IsVisible, Is.True);
            host.VerifyActualGachaOpenClose();
            CollectionAssert.AreEquivalent(before, host.Identity());
            host.Floor.gameObject.SetActive(false);
            Assert.That(host.Habitat.ActiveCount, Is.Zero);
            host.Floor.gameObject.SetActive(true);
            EnhancementFairyStage1Host.Invoke(host.Habitat, "OnEnable");
            CollectionAssert.AreEquivalent(before, host.Identity());
        }
    }
}

/// <summary>
/// The actual Stage1 scene in an owned PreviewScene container, NOT the renderer-copy demo.
/// Executes Floor3.Start, runtime habitat/catalog/UserInfo adapter and Camera.MoveCamera/tweens.
/// Only these audited callbacks run in Edit Mode; login, MainScene.Start, autosave and SDK never run.
/// All global references and queue/listener contents are restored. No scene/asset/PlayerPrefs save.
/// </summary>
internal sealed partial class EnhancementFairyStage1Host : IDisposable
{
    internal static string EvidenceDirectory = "Logs/FairyStage1Phase1";
    private readonly Dictionary<FieldInfo, object> _statics = new Dictionary<FieldInfo, object>();
    private readonly string[] _seen, _pending;
    private readonly GachaCollectionPreviewSceneWitness _witness;
    private readonly GameObject _services;
    private readonly SoundManager _sound;
    private Scene _scene;
    internal Floor3Controller Floor { get; private set; }
    internal CameraController Controller { get; private set; }
    internal EnhancementFairyHabitat Habitat => Floor.GetComponent<EnhancementFairyHabitat>();
    internal Camera Camera => Controller.GetComponent<Camera>();
    internal IReadOnlyList<GachaItemData> Catalog { get; }

    internal EnhancementFairyStage1Host()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin)
            throw new InvalidOperationException("Stage1 fixture requires SDK-free Edit Mode.");
        _witness = GachaCollectionPreviewSceneWitness.Capture();
        _seen = QueueSet("_seenItems").ToArray();
        _pending = QueueSet("_pendingItems").ToArray();
        try
        {
        Replace(typeof(EnhancementFairyAcquisitionEvents), "Changed", null);
        EnhancementFairyAcquisitionEvents.Queue.Clear();
        Replace(typeof(UserInfo), "_giveGachaItemCountDic", new Dictionary<string, int>());
        Replace(typeof(UserInfo), "_giveGachaItemLevelDic", new Dictionary<string, int>());
        Replace(typeof(UserInfo), "_currentStage", EStage.Stage1);
        Replace(typeof(UserInfo), "_stageInfos", new[] { new StageInfo(), new StageInfo(), new StageInfo() });
        Replace(typeof(UserInfo), "OnGiveGachaItemHandler", null);
        Replace(typeof(UserInfo), "OnChangeFloorHandler", null);
        _services = new GameObject("Isolated Stage1 fairy services");
        _services.SetActive(false);
        var manager = _services.AddComponent<ItemManager>();
        _sound = _services.AddComponent<SoundManager>();
        Replace(typeof(ItemManager), "_instance", manager);
        Replace(typeof(SoundManager), "_instance", _sound);
        foreach (string name in new[] { "_gachaItemDataList", "_gachaItemDataDic", "_upgradeTypeGachaItemDataDic", "_upgradeTypeSpriteDic" })
        {
            var field = Field(typeof(ItemManager), name, true);
            Replace(typeof(ItemManager), name, Activator.CreateInstance(field.FieldType));
        }
            Invoke(manager, "InitCsv"); // Same synchronous Production CSV/sprite loader, without singleton lifecycle/persistence.
            Catalog = manager.GetGachaItemDataList().ToArray();
            Assert.That(Catalog.Count, Is.GreaterThan(0));
            ReloadStage();
        }
        catch { Dispose(); throw; }
    }

    internal void ReloadStage()
    {
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        _scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Stage1.unity");
        Floor = SceneComponents<Floor3Controller>().Single();
        Controller = SceneComponents<CameraController>().Single();
        Camera.scene = _scene;
        Camera.aspect = 1400f / 600f;
        // Preserve actual serialized links/coordinates. Avoid Camera.Awake's unrelated UICamera
        // DataBind registration and account UI lifecycle in this isolated callback host.
        var targets = new Dictionary<ERestaurantFloorType, Dictionary<RestaurantType, Vector3>>();
        for (int i = 0; i < 3; i++)
        {
            float y = (float)Get(Controller, "_floor" + (i + 1) + "Pos_Y");
            targets[(ERestaurantFloorType)i] = new Dictionary<RestaurantType, Vector3>
            {
                [RestaurantType.Hall] = new Vector3((float)Get(Controller, "_hallPos_X"), y, Camera.transform.position.z),
                [RestaurantType.Kitchen] = new Vector3((float)Get(Controller, "_kitchenPos_X"), y, Camera.transform.position.z)
            };
        }
        Set(Controller, "_targetPosDic", targets);
        Invoke(Floor, "Start");
        Assert.That(Get(Habitat, "_cameraController"), Is.SameAs(Controller), "Production scene-local discovery");
        Assert.That(Get(Habitat, "_navigation"), Is.SameAs(SceneComponents<UINavigationCoordinator>().Single()));
        Assert.That(Floor.isActiveAndEnabled, Is.True);
    }

    internal T[] SceneComponents<T>() where T : Component
        => _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    internal void Restore(IEnumerable<KeyValuePair<string, int>> ownership)
    {
        Field(typeof(UserInfo), "_giveGachaItemCountDic", true).SetValue(null, ownership.ToDictionary(x => x.Key, x => x.Value));
        ((Action)Field(typeof(UserInfo), "OnGiveGachaItemHandler", true).GetValue(null))?.Invoke();
    }

    internal void RefreshVisibility() => Invoke(Habitat, "RefreshRuntimeVisibility");
    internal void Move(ERestaurantFloorType floor)
    {
        Set(_sound, "_effectType", _sound.GetHallEffectType(floor, Controller.CurrentRestaurant));
        Controller.MoveCamera(floor);
        CompleteCameraTween();
    }
    internal void Move(RestaurantType restaurant)
    {
        Set(_sound, "_effectType", _sound.GetHallEffectType(Controller.CurrentFloor, restaurant));
        Controller.MoveCamera(restaurant);
        CompleteCameraTween();
    }
    internal void BeginMove(ERestaurantFloorType floor)
    {
        Set(_sound, "_effectType", _sound.GetHallEffectType(floor, Controller.CurrentRestaurant));
        Controller.MoveCamera(floor);
    }
    internal void CompleteCameraTween()
    {
        CompleteTweens(Camera.gameObject);
        Assert.That((bool)Get(Controller, "_isMoveAction"), Is.False, "Actual camera tween completed and dispatched its event");
    }
    private static void CompleteTweens(GameObject root)
    {
        for (int pass = 0; pass < 3; pass++)
        foreach (var tween in root.GetComponentsInChildren<TweenData>(true))
        {
            if (!tween.enabled) continue;
            if (Get(tween, "_percentHandler") == null) Invoke(tween, "Awake");
            Invoke(tween, "Update");
            Set(tween, "ElapsedDuration", float.MaxValue);
            Invoke(tween, "Update");
        }
    }
    internal void VerifyActualGachaOpenClose(Action whileOpen = null)
    {
        var view = SceneComponents<UIGacha>().Single();
        var nav = view.GetComponentInParent<MobileUINavigation>(true);
        var coordinator = SceneComponents<UINavigationCoordinator>().Single();
        view.gameObject.SetActive(false);
        using (var fonts = new StaffGachaOfflineFonts())
        {
            fonts.BindBeforeActivation(view.gameObject);
            var staff = view.GetComponentInChildren<UIStaffGacha>(true);
            var item = view.GetComponentInChildren<UIItemGacha>(true);
            var account = new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion,
                Array.Empty<StaffAccountStaffRecord>(), 0, new GachaEconomySaveData());
            var store = new GachaEconomyMemoryStore(new GachaEconomySnapshot("stage1-ui-only", account, 0));
            var economy = new GachaEconomyService(store, Catalog.Cast<GachaData>().ToArray(), Resources.Load<GachaEconomySettings>("GachaEconomySettings"));
            staff.ConfigureCollectionOffline(view, economy);
            item.ConfigureCollectionOffline(view, economy);
            view.ConfigureEditorOfflineNavigation(staff, item);
            view.PrepareItemMachine();
            view.ViewInit(nav); // The existing offline adapter skips account-dependent Init only.
            Set(nav, "_viewDic", new Dictionary<string, MobileUIView> { ["UIGacha"] = view });
            Invoke(coordinator, "Init"); // Actual Stage1 nav events and open-view count.
            nav.Push("UIGacha");
            Assert.That(coordinator.GetOpenViewCount(), Is.EqualTo(1));
            Assert.That(Habitat.IsVisible, Is.False, "Production OnShow event hides the habitat");
            CompleteTweens(view.gameObject);
            Assert.That(view.VisibleState, Is.EqualTo(VisibleState.Appeared));
            var group = (CanvasGroup)Get(view, "_canvasGroup");
            Assert.That(group.blocksRaycasts, Is.True);
            whileOpen?.Invoke();
            nav.Pop();
            Assert.That(coordinator.GetOpenViewCount(), Is.Zero);
            Assert.That((bool)Get(Habitat, "_waitingForUiHide"), Is.True, "Production OnHide event schedules restoration");
            Assert.That(group.blocksRaycasts || group.interactable, Is.False);
            Assert.That(view.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true).All(raycaster => !raycaster.isActiveAndEnabled), Is.True);
            // Edit Mode does not advance MonoBehaviour coroutines. Drive the exact runtime
            // IEnumerator, retaining its frame barrier and all of its closing-view checks.
            var resume = (IEnumerator)Invoke(Habitat, "ResumeAfterClosingViews");
            Assert.That(resume.MoveNext(), Is.True);
            Assert.That(Habitat.IsVisible, Is.False);
            Assert.That(resume.MoveNext(), Is.False);
            Assert.That(Habitat.IsVisible, Is.True);
            foreach (var scrolling in view.GetComponentsInChildren<ScrollingImage>(true))
            {
                var material = (Material)Get(scrolling, "_material");
                if (material != null) Object.DestroyImmediate(material);
            }
        }
    }
    internal void Advance(float seconds)
    { for (int i = 0; i < Mathf.CeilToInt(seconds * 30); i++) Habitat.AdvancePreview(1f / 30f); }
    internal SpriteRenderer[] Bodies() => Habitat.GetComponentsInChildren<SpriteRenderer>().Where(body => body.name == "Item sprite").ToArray();
    internal void DragUp(float worldDistance)
    {
        Set(Controller, "_startTouchPos", Vector2.zero);
        Set(Controller, "_isDraggingEnabled", true);
        Set(Controller, "_moveHorizontally", false);
        float pixels = worldDistance / (Time.fixedDeltaTime * (float)Get(Controller, "_dragSpeed"));
        Invoke(Controller, "ProcessDrag", new Vector2(0f, -pixels));
        Set(Controller, "_isDraggingEnabled", false);
    }
    internal string[] Identity() => Enumerable.Range(0, Habitat.ActiveCount).Select(i =>
        Habitat.GetActiveBrain(i).ItemId + ":" + Floor.transform.Find("Fairy " + Habitat.GetActiveBrain(i).ItemId).GetInstanceID()).ToArray();
    internal Vector2[] Positions() => Enumerable.Range(0, Habitat.ActiveCount).Select(i => Habitat.GetActiveBrain(i).GroundPosition).ToArray();
    internal int VisibleBodyCount()
    {
        Camera.aspect = 1400f / 600f;
        var planes = GeometryUtility.CalculateFrustumPlanes(Camera);
        return Habitat.GetComponentsInChildren<SpriteRenderer>().Count(renderer => renderer.name == "Item sprite"
            && renderer.enabled && renderer.color.a > 0 && GeometryUtility.TestPlanesAABB(planes, renderer.bounds));
    }
    internal void Capture(string name)
    {
        Directory.CreateDirectory(EvidenceDirectory);
        var target = new RenderTexture(1400, 600, 24);
        var previous = RenderTexture.active;
        var oldTarget = Camera.targetTexture;
        var image = new Texture2D(1400, 600, TextureFormat.RGB24, false);
        try
        {
            // OpenPreviewScene + synchronous tests have not had an editor LateUpdate.
            // Run the authored lights' own mesh/bounds update, without replacing materials
            // or light settings. Normal Stage1 Play does this automatically every frame.
            foreach (var light in SceneComponents<UnityEngine.Rendering.Universal.Light2D>())
                if (light.isActiveAndEnabled) Invoke(light, "LateUpdate");
            target.Create(); Camera.targetTexture = target; Camera.aspect = 1400f / 600f;
            foreach (var label in Habitat.GetComponentsInChildren<TMPro.TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            if (Habitat.IsItemCardOpen)
            {
                ((GachaResultCardPopup)Get(Habitat, "_itemPopup")).BringToFront();
                Canvas.ForceUpdateCanvases();
            }
            Camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1400, 600), 0, 0); image.Apply();
            string path = Path.Combine(EvidenceDirectory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            File.AppendAllText(Path.Combine(EvidenceDirectory, "stage1-evidence.txt"), name + ": actual Stage1 in isolated PreviewScene container; "
                + "Production Floor3/Camera/ItemManager/UserInfo adapter; no Play, SDK, login, persistence. "
                + "floor=" + Controller.CurrentFloor + "; restaurant=" + Controller.CurrentRestaurant
                + "; progression=" + UserInfo.GetUnlockFloor(EStage.Stage1) + "; active=" + Habitat.ActiveCount
                + "; inCamera=" + VisibleBodyCount() + "; IDs=" + string.Join(",", Identity()) + "\n");
        }
        finally
        { Camera.targetTexture = oldTarget; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
    }
    internal void RecordCameraTour()
    {
        const int fps = 12;
        int frame = 0;
        var identity = Identity();
        void CaptureFrame()
        {
            Habitat.AdvancePreview(1f / fps);
            Capture("tour-frames/frame-" + (frame++).ToString("D4") + ".png");
        }
        void Hold() { for (int i = 0; i < fps; i++) CaptureFrame(); }
        void Transition(Action begin)
        {
            begin();
            foreach (var tween in Camera.GetComponents<TweenData>())
            {
                if (!tween.enabled) continue;
                if (Get(tween, "_percentHandler") == null) Invoke(tween, "Awake");
                Invoke(tween, "Update");
                float duration = (float)Get(tween, "TotalDuration");
                int steps = Mathf.Max(1, Mathf.CeilToInt(duration * fps));
                for (int i = 1; i <= steps; i++)
                {
                    Set(tween, "ElapsedDuration", Mathf.Max(0, Mathf.Min(duration, i / (float)fps) - Time.deltaTime));
                    Invoke(tween, "Update"); CaptureFrame();
                }
                if ((bool)Get(Controller, "_isMoveAction"))
                { Set(tween, "ElapsedDuration", float.MaxValue); Invoke(tween, "Update"); }
            }
        }
        Hold();
        Set(_sound, "_effectType", EffectType.Kitchen3);
        Transition(() => Controller.MoveCamera(RestaurantType.Kitchen)); Hold();
        CollectionAssert.AreEquivalent(identity, Identity());
        Set(_sound, "_effectType", EffectType.Hall3);
        Transition(() => Controller.MoveCamera(RestaurantType.Hall)); Hold();
        Set(_sound, "_effectType", EffectType.Hall1);
        Transition(() => Controller.MoveCamera(ERestaurantFloorType.Floor1)); Hold();
        Assert.That(Habitat.ActiveCount, Is.Zero);
        Set(_sound, "_effectType", EffectType.Hall3);
        Transition(() => Controller.MoveCamera(ERestaurantFloorType.Floor3)); Hold();
        CollectionAssert.AreEquivalent(identity, Identity());
        File.WriteAllText(Path.Combine(EvidenceDirectory, "tour-metadata.txt"),
            "Actual Stage1 asset in an isolated PreviewScene container; Production Floor3Controller, CameraController.MoveCamera and tween callbacks, Habitat, ItemManager catalog and UserInfo ownership adapter.\n"
            + "Saved FPS=12; deterministic Editor callback simulation; NOT mobile/Play performance evidence.\n"
            + "No SDK, server, login, PlayerPrefs writes, persisted account or real purchases. Frames=" + frame + "\n");
    }
    internal void RecordBirth()
    {
        var brain = Habitat.GetActiveBrain(0);
        var position = brain.GroundPosition;
        var body = Bodies().Single();
        bool airborne = false;
        for (int frame = 0; frame < 24; frame++)
        {
            Habitat.AdvancePreview(1f / 20f);
            if (frame < 10)
            {
                Assert.That(brain.GroundPosition, Is.EqualTo(position));
                airborne |= EnhancementFairyTests.MeshBottom(body) > body.transform.parent.position.y + .2f;
            }
            Capture("birth-frames/frame-" + frame.ToString("D4") + ".png");
            if (frame == 5) Capture("stage1-birth-peak.png");
            if (frame == 5)
            {
                var puff = body.transform.parent.Find("Arrival puff").GetComponent<SpriteRenderer>();
                Assert.That(puff.sprite, Is.Not.Null, "The actual production puff asset must be connected");
                Assert.That(puff.enabled, Is.True);
                Vector3 oldPosition = Camera.transform.position;
                float oldSize = Camera.orthographicSize;
                try
                {
                    Camera.orthographicSize = 2.5f;
                    Camera.transform.position = new Vector3(body.bounds.center.x, body.transform.parent.position.y + .9f, oldPosition.z);
                    Capture("stage1-birth-closeup.png");
                    File.WriteAllText(Path.Combine(EvidenceDirectory, "birth-visual-state.txt"),
                        "puff=" + puff.sprite.name + "; bounds=" + puff.bounds + "; color=" + puff.color
                        + "; body=" + body.bounds + "; ground=" + body.transform.parent.position);
                }
                finally { Camera.orthographicSize = oldSize; Camera.transform.position = oldPosition; }
            }
        }
        Assert.That(airborne, Is.True, "Production confirmed acquisition must visibly hop, not just scale");
        Assert.That(brain.ElapsedSeconds, Is.GreaterThan(0f), "Ordinary behavior resumes after the birth");
    }
    private static HashSet<string> QueueSet(string name) => (HashSet<string>)Get(EnhancementFairyAcquisitionEvents.Queue, name);
    private void Replace(Type type, string name, object value)
    { var field = Field(type, name, true); _statics.Add(field, field.GetValue(null)); field.SetValue(null, value); }
    internal static FieldInfo Field(Type type, string name, bool isStatic = false)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    internal static object Get(object owner, string name) => Field(owner.GetType(), name).GetValue(owner);
    internal static void Set(object owner, string name, object value) => Field(owner.GetType(), name).SetValue(owner, value);
    internal static object Invoke(object owner, string name, params object[] arguments)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (method != null) return method.Invoke(owner, arguments);
        }
        throw new MissingMethodException(name);
    }
    public void Dispose()
    {
        if (_scene.IsValid()) { EditorSceneManager.ClosePreviewScene(_scene); _scene = default; }
        if (Catalog != null) foreach (var item in Catalog) if (item != null) Object.DestroyImmediate(item);
        if (_services != null) Object.DestroyImmediate(_services);
        foreach (var pair in _statics) pair.Key.SetValue(null, pair.Value);
        EnhancementFairyAcquisitionEvents.Queue.Clear();
        foreach (string id in _seen) QueueSet("_seenItems").Add(id);
        foreach (string id in _pending) QueueSet("_pendingItems").Add(id);
        _witness.AssertUnchanged("Stage1 production fixture disposed");
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
    }
}
#endif
