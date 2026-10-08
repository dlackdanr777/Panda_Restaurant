#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Muks.MobileUI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Phase4AdUiEvidenceTests
{
    [Test]
    public void Stage1_DiamondAdSlotAndNativePopupDisplayThreeWithoutRequestingAnAd()
    {
        using (var host = new EnhancementFairyStage1Host()) host.VerifyPhase4DiamondAdUi();
    }
}

internal sealed partial class EnhancementFairyStage1Host
{
    internal void VerifyPhase4DiamondAdUi()
    {
        Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        bool hadAdManager = AdManager.HasInstance;
        int beforeDiamonds = UserInfo.Dia;
        Replace(typeof(UserInfo), "_dailyAdDiaRewardCount", 0);
        var payment = SceneComponents<UIPayment>().Single();
        var dia = (UIPaymentDia)Get(payment, "_diaUI");
        var gold = (UIPaymentGold)Get(payment, "_goldUI");
        var slot = (UIPaymentAdSlot)Get(dia, "_dia01Slot");
        var watch = (WatchAdButton)Get(slot, "_watchAdButton");
        var popup = (UIAdPopup)Get(watch, "_adPopup");
        var nav = payment.GetComponentInParent<MobileUINavigation>(true);
        Assert.That(nav, Is.Not.Null);
        Assert.That(popup, Is.Not.Null, "Use the original Stage1 reward slot's popup reference");
        payment.gameObject.SetActive(false);
        popup.gameObject.SetActive(false);
        // The test remains in EditMode. In particular, WatchAdButton.Awake and the
        // advertisement request button are never invoked: no SDK object is prepared.
        watch.enabled = false;
        foreach (var canvas in SceneComponents<Canvas>())
        {
            bool paymentLayer = payment.transform.IsChildOf(canvas.transform) || canvas.transform.IsChildOf(payment.transform);
            bool popupLayer = popup.transform.IsChildOf(canvas.transform) || canvas.transform.IsChildOf(popup.transform);
            if (!paymentLayer && !popupLayer) canvas.gameObject.SetActive(false);
        }
        foreach (var floorLock in SceneComponents<FloorLockGroup>()) floorLock.gameObject.SetActive(false);
        using (var fonts = new StaffGachaOfflineFonts())
        {
            fonts.BindBeforeActivation(payment.gameObject, popup.gameObject);
            slot.Init(payment, MoneyType.Dia); // Native reward amount and remaining-count labels.
            Assert.That(((TMP_Text)Get(slot, "_valueText")).text, Is.EqualTo("3"));
            Assert.That(((TMP_Text)Get(slot, "_countText")).text, Is.EqualTo("3/3"));
            payment.gameObject.SetActive(true);
            ((GameObject)Get(payment, "_animeUI")).transform.localScale = Vector3.one;
            ((GameObject)Get(payment, "_dontTouchArea")).SetActive(false);
            gold.SetActive(false); dia.SetActive(true);
            payment.VisibleState = VisibleState.Appeared;
            payment.OnValidate(); popup.OnValidate();
            // Only the popup is registered. Do not run UIPayment.Init, other purchases,
            // WatchAdButton lifecycle, or the scene's account/bootstrap components.
            popup.ViewInit(nav);
            Set(nav, "_viewDic", new Dictionary<string, MobileUIView> { { "UIAd", popup } });
            foreach (var canvas in SceneComponents<Canvas>())
                if (canvas.isRootCanvas)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = 1f; canvas.sortingLayerName = "UI"; }
            Camera.cullingMask |= 1 << LayerMask.NameToLayer("UI");
            Move(ERestaurantFloorType.Floor1);
            payment.Canvas.overrideSorting = true;
            payment.Canvas.sortingOrder = 50;
            CapturePhase4AdUi("phase4-diamond-ad-slot-3.png", payment.gameObject);
            popup.ShowDiaPopup(watch);
            CompleteTweens(popup.gameObject);
            Assert.That(popup.VisibleState, Is.EqualTo(VisibleState.Appeared));
            Assert.That(((TMP_Text)Get(popup, "_text")).text, Does.Contain("다이아 3개"));
            Assert.That(((TMP_Text)Get(popup, "_adCountText")).text, Is.EqualTo("3/3"));
            Assert.That(((GameObject)Get(popup, "_diaLayout")).activeSelf, Is.True);
            popup.Canvas.overrideSorting = true;
            popup.Canvas.sortingOrder = 60;
            CapturePhase4AdUi("phase4-diamond-ad-popup-3.png", popup.gameObject);
            Assert.That(UserInfo.Dia, Is.EqualTo(beforeDiamonds));
            Assert.That(UserInfo.DailyAdDiaRewardCount, Is.Zero);
            Assert.That(AdManager.HasInstance, Is.EqualTo(hadAdManager));
            Assert.That(AdManager.IsAdPlaying, Is.False);
            Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
            File.WriteAllText(Path.Combine(EvidenceDirectory, "phase4-diamond-ad-ui-evidence.txt"),
                "Actual Stage1 asset in isolated PreviewScene. Native UIPaymentAdSlot.Init(Dia) and UIAdPopup.ShowDiaPopup.\n"
                + "Slot reward=3; slot count=3/3; popup reward=다이아 3개; popup count=3/3.\n"
                + "1920x1080 render. Fonts cloned before activation. Render-only native Canvas sorting configured for the capture.\n"
                + "No Play, WatchAdButton.Awake, OnClickAd, reward callback, SDK preparation, login, account write or currency use.\n");
            nav.PopNoAnime("UIAd");
        }
    }

    private void CapturePhase4AdUi(string name, GameObject visibleUi)
    {
        var previous = RenderTexture.active;
        var previousTarget = Camera.targetTexture;
        var target = new RenderTexture(1920, 1080, 24);
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            target.Create(); Camera.targetTexture = target; Camera.aspect = 1920f / 1080f;
            Canvas.ForceUpdateCanvases();
            foreach (var label in visibleUi.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            foreach (var light in SceneComponents<UnityEngine.Rendering.Universal.Light2D>())
                if (light.isActiveAndEnabled) Invoke(light, "LateUpdate");
            Camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            Directory.CreateDirectory(EvidenceDirectory);
            File.WriteAllBytes(Path.Combine(EvidenceDirectory, name), image.EncodeToPNG());
        }
        finally
        {
            Camera.targetTexture = previousTarget; RenderTexture.active = previous;
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        }
    }
}
#endif
