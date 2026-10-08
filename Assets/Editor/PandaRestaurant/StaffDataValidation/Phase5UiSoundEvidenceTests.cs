#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class Phase5UiSoundEvidenceTests
{
    [Test]
    public void Stage1_ExchangeEntranceAndConfirmedStamp_RecordProductionAudioTimeline()
    { using (var host = new EnhancementFairyStage1Host()) host.RecordPhase5ExchangeSound(); }
}

internal sealed partial class EnhancementFairyStage1Host
{
    internal void RecordPhase5ExchangeSound()
    {
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        var view = SceneComponents<UIGacha>().Single();
        var staff = view.GetComponentInChildren<UIStaffGacha>(true);
        var item = view.GetComponentInChildren<UIItemGacha>(true);
        var list = (UIGachaSlotList)Get(view, "_gachaItemList");
        var staffData = Resources.LoadAll<StaffData>("StaffData").Select(GachaStaffData.Create).ToArray();
        var store = new GachaEconomyMemoryStore(new GachaEconomySnapshot("phase5-stage1-sound",
            new StaffAccountSaveData(StaffAccountSaveConverter.CurrentVersion, Array.Empty<StaffAccountStaffRecord>(),
                10000, new GachaEconomySaveData(20, 20, 0, 0)), 1000));
        var economy = new GachaEconomyService(store, Catalog.Cast<GachaData>().Concat(staffData).ToArray(),
            Resources.Load<GachaEconomySettings>("GachaEconomySettings"),
            displayRandom: new System.Random(20261002));
        using (var audio = new Phase5UiAudioScope())
        using (var events = new Phase5UiEvents())
        using (var fonts = new StaffGachaOfflineFonts())
        {
            try
            {
                foreach (var canvas in SceneComponents<Canvas>())
                    if (!view.transform.IsChildOf(canvas.transform) && !canvas.transform.IsChildOf(view.transform)) canvas.gameObject.SetActive(false);
                foreach (var floorLock in SceneComponents<FloorLockGroup>()) floorLock.gameObject.SetActive(false);
                view.gameObject.SetActive(false); view.transform.parent.gameObject.SetActive(false);
                fonts.BindBeforeActivation(view.transform.parent.gameObject); view.transform.parent.gameObject.SetActive(true);
                staff.ConfigureCollectionOffline(view, economy); item.ConfigureCollectionOffline(view, economy);
                view.ConfigureEditorOfflineNavigation(staff, item); view.BindCollectionEconomy(economy);
                list.gameObject.SetActive(true); list.Init(Catalog.Cast<GachaData>().OrderByDescending(x => x.Rank).ToList());
                list.UpdateMachineData(Catalog.Cast<GachaData>().OrderByDescending(x => x.Rank).ToList());
                fonts.BindBeforeActivation(view.gameObject);
                view.SetEditorOfflineVisible(true); view.SelectCollectionOfflineMachine(GachaMachineKind.Item); Invoke(item, "Update");
                foreach (var canvas in SceneComponents<Canvas>()) if (canvas.isRootCanvas)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = 1; canvas.sortingLayerName = "UI"; }
                Camera.cullingMask |= 1 << LayerMask.NameToLayer("UI"); Move(ERestaurantFloorType.Floor1);
                var exchange = view.CollectionExchange;
                string directory = Path.Combine(EvidenceDirectory, "exchange-sound");
                using (var recording = new Phase5AudioEvidence(audio.Manager, directory, 30))
                {
                    for (int frame = 0; frame < 90; frame++)
                    {
                        recording.BeginFrame(frame);
                        if (frame == 0)
                        {
                            events.Pointer(view.CollectionHud.ExchangeButton);
                            Assert.That(exchange.IsOpen, Is.True); Set(exchange, "_openedAt", 0f);
                        }
                        if (frame == 36)
                        {
                            store.DeferNextSave = true;
                            events.Pointer(exchange.GetComponentsInChildren<Button>().Single(x => x.name == "Exchange Selected Product"));
                        }
                        if (frame == 40)
                        {
                            Assert.That(audio.Events.Count(x => x.Clip == GachaCollectionUiTheme.Load().SoldOutStampSound), Is.Zero);
                            store.CompletePending(GachaStoreResult.Confirmed);
                            // TickPresentation already supports an external unscaled clock. Align the
                            // just-started stamp to that capture clock without altering production timing.
                            foreach (var card in (IEnumerable)Get(exchange, "_cards"))
                                if ((float)Get(card, "StampStarted") >= 0) Set(card, "StampStarted", frame / 30f);
                        }
                        exchange.TickPresentation(frame / 30f);
                        CapturePhase4AdUi(recording.FramePath(frame), view.gameObject);
                    }
                    recording.Save(3);
                }
                var theme = GachaCollectionUiTheme.Load();
                Assert.That(audio.Events.Count(x => x.Clip == theme.ExchangeChainSound), Is.EqualTo(1));
                Assert.That(audio.Events.Count(x => x.Clip == theme.SoldOutStampSound), Is.EqualTo(1));
                Assert.That(audio.ButtonEvents.Count(), Is.EqualTo(2));
                Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
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
