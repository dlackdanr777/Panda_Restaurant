#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class Phase5UiSoundTests
{
    [TestCase("Choose Diamonds")]
    [TestCase("Choose Machine Tickets")]
    [TestCase("Close Payment")]
    [TestCase("Open Token Exchange")]
    [TestCase("Close Exchange")]
    [TestCase("Product P0")]
    [TestCase("Exchange Selected Product")]
    [TestCase("Refresh Display")]
    public void Buttons_TenPointerAndSubmitInputsEachAfterFiveReentries_PlayOnceOnly(string name)
    {
        using (var fixture = new Phase5UiFixture())
        {
            for (int reopen = 0; reopen < 5; reopen++) { fixture.OpenAll(); fixture.CloseAll(); }
            for (int i = 0; i < 10; i++)
            {
                fixture.OpenAll(); var button = fixture.Button(name);
                GachaButtonInputSound.Bind(button, name.StartsWith("Close") ? SoundEffectType.ButtonExitSound : SoundEffectType.ButtonClickSound);
                fixture.Audio.Events.Clear(); fixture.Pointer(button);
                Assert.That(fixture.Audio.ButtonEvents.Count(), Is.EqualTo(1), name + " pointer " + i);
                fixture.OpenAll(); button = fixture.Button(name); fixture.Audio.Events.Clear();
                fixture.Submit(button);
                Assert.That(fixture.Audio.ButtonEvents.Count(), Is.EqualTo(1), name + " submit " + i);
            }
            fixture.OpenAll(); var target = fixture.Button(name);
            fixture.Audio.Events.Clear(); target.onClick.Invoke();
            Assert.That(fixture.Audio.ButtonEvents.Count(), Is.Zero, "Automatic onClick has no input sound");
            fixture.OpenAll(); target.interactable = false; fixture.Audio.Events.Clear();
            fixture.Pointer(target); fixture.Submit(target);
            Assert.That(fixture.Audio.ButtonEvents.Count(), Is.Zero, "Disabled");
            target.interactable = true; fixture.Audio.Muted = true; fixture.Pointer(target); fixture.Submit(target);
            Assert.That(fixture.Audio.Events.Count, Is.Zero, "Muted");
        }
    }

    [TestCase(GachaMachineKind.Item)] [TestCase(GachaMachineKind.Staff)]
    public void NativeMachineButtons_TenInputsAndReentriesHaveOneSoundAndAutomaticZero(GachaMachineKind machine)
    {
        using (var audio = new Phase5UiAudioScope())
        using (var events = new Phase5UiEvents())
        using (var host = new Phase3PaymentHost(machine, 100, 10))
        {
            var single = machine == GachaMachineKind.Item ? host.Item.SingleButton : host.Staff.SingleButton;
            var multi = machine == GachaMachineKind.Item ? host.Item.TenButton : host.Staff.TenButton;
            foreach (var button in new[] { single, multi })
            {
                Assert.That(button.GetComponents<GachaButtonInputSound>().Length, Is.EqualTo(1));
                for (int i = 0; i < 10; i++)
                {
                    EnhancementFairyStage1Host.Invoke(host.Machine, "Update");
                    audio.Now += 2; audio.Events.Clear(); events.Pointer(button);
                    Assert.That(host.View.IsChoosingPayment, Is.True);
                    Assert.That(audio.ButtonEvents.Count(), Is.EqualTo(1)); host.View.CollectionPayment.Close();
                    EnhancementFairyStage1Host.Invoke(host.Machine, "Update");
                    audio.Now += 2; audio.Events.Clear(); events.Submit(button);
                    Assert.That(host.View.IsChoosingPayment, Is.True);
                    Assert.That(audio.ButtonEvents.Count(), Is.EqualTo(1), "Native submit must remain audible"); host.View.CollectionPayment.Close();
                }
                audio.Events.Clear(); button.onClick.Invoke(); host.View.CollectionPayment.Close();
                Assert.That(audio.Events.Count, Is.Zero);
            }
        }
    }

    [Test]
    public void PaymentOutsideClose_TenRealInputsUseExitAndProgrammaticCloseIsSilent()
    {
        using (var f = new Phase5UiFixture())
        {
            for (int i = 0; i < 10; i++)
            {
                f.ShowPayment(); f.Audio.Events.Clear();
                f.Payment.OnPointerClick(new PointerEventData(f.Events.System)
                { button = PointerEventData.InputButton.Left, pointerCurrentRaycast = new RaycastResult { gameObject = f.Payment.gameObject } });
                Assert.That(f.Payment.IsOpen, Is.False);
                Assert.That(f.Audio.ButtonEvents.Single().Clip, Is.SameAs(f.Audio.Exit));
            }
            f.ShowPayment(); f.Audio.Events.Clear(); f.Payment.Close(); Assert.That(f.Audio.Events.Count, Is.Zero);
            f.ShowPayment(); f.Audio.Muted = true;
            f.Payment.OnPointerClick(new PointerEventData(f.Events.System)
            { pointerCurrentRaycast = new RaycastResult { gameObject = f.Payment.gameObject } });
            Assert.That(f.Audio.Events.Count, Is.Zero);
        }
    }

    [Test]
    public void ExchangeChain_TenRepeatedOpensOneDispatchCloseStopsAndReentryRestarts()
    {
        using (var f = new Phase5UiFixture())
        {
            Assert.That(f.Theme.ExchangeChainSound, Is.Not.Null, "Production theme must reference a project-owned clip");
            f.Audio.Events.Clear();
            for (int i = 0; i < 10; i++) f.Exchange.SetVisible(true);
            Assert.That(f.Audio.Events.Count(x => x.Clip == f.Theme.ExchangeChainSound), Is.EqualTo(1));
            f.Exchange.SetVisible(false); Assert.That(f.Audio.Stops, Is.EqualTo(1));
            f.Exchange.SetVisible(true); Assert.That(f.Audio.Events.Count(x => x.Clip == f.Theme.ExchangeChainSound), Is.EqualTo(2));
            f.FinishEntrance(); f.Exchange.SelectProduct("P1"); f.Exchange.Refresh();
            Assert.That(f.Audio.Events.Count(x => x.Clip == f.Theme.ExchangeChainSound), Is.EqualTo(2));
            f.Exchange.SetVisible(false); f.Audio.Muted = true; f.Exchange.SetVisible(true);
            Assert.That(f.Audio.Events.Count(x => x.Clip == f.Theme.ExchangeChainSound), Is.EqualTo(2));
        }
    }

    [Test]
    public void SoldOut_OnlySuccessfulCurrentPurchasePlaysAttendanceClipAtImpactOnce()
    {
        using (var f = new Phase5UiFixture())
        {
            Assert.That(AssetDatabase.GetAssetPath(f.Theme.SoldOutStampSound), Is.EqualTo("Assets/Sound/출석/오늘의 보상_도장.mp3"));
            f.OpenAll(); f.DeferPurchase = true;
            f.Button("Exchange Selected Product").onClick.Invoke();
            f.Exchange.TickPresentation(Time.realtimeSinceStartup + .5f);
            Assert.That(f.Stamps, Is.Zero, "No save confirmation, no stamp sound");
            f.Products[0].SoldOut = true; f.Products[0].CanPurchase = false;
            var reply = f.PendingPurchase; reply(true, null); reply(true, null);
            float started = Time.realtimeSinceStartup;
            f.Exchange.TickPresentation(started + .1f); Assert.That(f.Stamps, Is.Zero);
            f.Exchange.TickPresentation(started + .21f); Assert.That(f.Stamps, Is.EqualTo(1));
            f.Exchange.TickPresentation(started + .6f); f.Exchange.Refresh(); Assert.That(f.Stamps, Is.EqualTo(1));
            f.Audio.Events.Clear(); f.Pointer(f.Button("Product P0"));
            Assert.That(f.Audio.ButtonEvents.Count(), Is.Zero, "Already SOLD OUT card is silent");
            f.Exchange.SetVisible(false); f.Exchange.SetVisible(true); f.FinishEntrance(); Assert.That(f.Stamps, Is.Zero);
            f.Products[0].DisplayVersion++; f.Products[0].SoldOut = false; f.Products[0].CanPurchase = true;
            f.Exchange.Refresh(); f.Button("Exchange Selected Product").onClick.Invoke();
            f.Products[0].SoldOut = true; f.Products[0].CanPurchase = false; f.PendingPurchase(true, null);
            f.Exchange.TickPresentation(Time.realtimeSinceStartup + .22f); Assert.That(f.Stamps, Is.EqualTo(1));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void SoldOut_FailureOrHiddenConfirmationAndReopenAreSilent(bool closeBeforeReply)
    {
        using (var f = new Phase5UiFixture())
        {
            f.OpenAll(); f.DeferPurchase = true; f.Button("Exchange Selected Product").onClick.Invoke();
            if (closeBeforeReply)
            { f.Exchange.SetVisible(false); f.Products[0].SoldOut = true; f.Products[0].CanPurchase = false; }
            f.PendingPurchase(closeBeforeReply, "unconfirmed");
            f.Exchange.SetVisible(true); f.FinishEntrance(); f.Exchange.TickPresentation(Time.realtimeSinceStartup + 3);
            Assert.That(f.Stamps, Is.Zero);
        }
    }
}

internal sealed class Phase5UiFixture : IDisposable
{
    internal readonly Phase5UiAudioScope Audio = new Phase5UiAudioScope();
    internal readonly Phase5UiEvents Events = new Phase5UiEvents();
    internal readonly GachaCollectionUiTheme Theme;
    internal readonly TokenExchangeView Exchange;
    internal readonly GachaPaymentChoiceView Payment;
    internal readonly TokenExchangeProductView[] Products;
    internal bool DeferPurchase;
    internal Action<bool, string> PendingPurchase;
    private readonly GameObject _root;
    private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
    internal int Stamps => Audio.Events.Count(x => x.Clip == Theme.SoldOutStampSound);
    internal Phase5UiFixture()
    {
        Theme = GachaCollectionUiTheme.Load();
        _root = new GameObject("Phase5 production UI sound isolation", typeof(RectTransform)); _root.SetActive(false);
        ((RectTransform)_root.transform).sizeDelta = new Vector2(1920, 1080);
        Products = Enumerable.Range(0, 6).Select(i => new TokenExchangeProductView
        { Id = "P" + i, Name = "상품 " + i, CanPurchase = true, Quantity = 1, Price = 10, DisplayVersion = 1 }).ToArray();
        Exchange = TokenExchangeView.Attach(_root.transform, Theme,
            () => new TokenExchangeSnapshot { Products = Products, CanRefresh = true, RemainingRefreshes = 3 },
            (id, version, reply) => { PendingPurchase = reply; if (!DeferPurchase) reply(false, "isolated cancelled purchase"); },
            (tab, version, reply) => reply(false, "isolated cancelled refresh"));
        Payment = GachaPaymentChoiceView.Attach(_root.transform, Theme);
        GachaCollectionMachineHud.Attach((RectTransform)_root.transform, Theme, () => Exchange.SetVisible(true), () => { });
        _fonts.BindBeforeActivation(_root); _root.SetActive(true);
    }
    internal Button Button(string name) => _root.GetComponentsInChildren<Button>(true).Single(x => x.name == name);
    internal void ShowPayment() { Audio.Now += 2; Payment.Show(GachaMachineKind.Item, false, 100, 10, null, true, _ => { }); }
    internal void FinishEntrance() => Exchange.TickPresentation(Time.realtimeSinceStartup + 1);
    internal void OpenAll() { Audio.Now += 2; Exchange.SetVisible(true); FinishEntrance(); ShowPayment(); }
    internal void CloseAll() { Payment.Close(); Exchange.SetVisible(false); }
    internal void Pointer(Button button) { Audio.Now += 2; Events.Pointer(button); }
    internal void Submit(Button button) { Audio.Now += 2; Events.Submit(button); }
    public void Dispose() { Object.DestroyImmediate(_root); _fonts.Dispose(); Events.Dispose(); Audio.Dispose(); }
}

internal sealed class Phase5UiEvents : IDisposable
{
    private readonly GameObject _root;
    private readonly EventSystem _previous;
    internal readonly EventSystem System;
    internal Phase5UiEvents()
    { _previous = EventSystem.current; _root = new GameObject("Phase5 isolated EventSystem"); System = _root.AddComponent<EventSystem>(); EventSystem.current = System; }
    internal void Pointer(Button button)
    {
        var data = new PointerEventData(System) { button = PointerEventData.InputButton.Left, pointerId = 0 };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    internal void Submit(Button button) => ExecuteEvents.Execute(button.gameObject, new BaseEventData(System), ExecuteEvents.submitHandler);
    public void Dispose() { Object.DestroyImmediate(_root); EventSystem.current = _previous; }
}

internal sealed class Phase5UiAudioScope : IDisposable
{
    private readonly GameObject _root;
    private readonly SoundManager _previous;
    internal readonly SoundManager Manager;
    internal readonly AudioClip Click, Exit;
    internal readonly List<SoundManager.EffectPlaybackDiagnostic> Events = new List<SoundManager.EffectPlaybackDiagnostic>();
    internal IEnumerable<SoundManager.EffectPlaybackDiagnostic> ButtonEvents => Events.Where(x => x.Clip == Click || x.Clip == Exit);
    internal double Now;
    internal int Stops;
    internal bool Muted { set => Set(Manager, "_soundEffectVolume", value ? 0f : 1f); }
    internal Phase5UiAudioScope()
    {
        _previous = SoundManager.TryGetExistingInstance();
        _root = new GameObject("Phase5 isolated SoundManager pool"); _root.SetActive(false);
        Manager = _root.AddComponent<SoundManager>();
        typeof(SoundManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Manager);
        var clips = Enumerable.Range(0, (int)SoundEffectType.Length).Select(i => Resources.Load<AudioClip>("Audio/" + (SoundEffectType)i)).ToArray();
        Click = clips[(int)SoundEffectType.ButtonClickSound]; Exit = clips[(int)SoundEffectType.ButtonExitSound]; Set(Manager, "_clips", clips);
        var pool = new List<AudioSource>();
        for (int i = 0; i < 10; i++) { var child = new GameObject("Existing pool " + i); child.transform.SetParent(_root.transform); pool.Add(child.AddComponent<AudioSource>()); }
        Set(Manager, "_effectAudioDic", new Dictionary<EffectType, List<AudioSource>> { [EffectType.None] = pool });
        Manager.SuppressNativePlaybackForTests = true; Manager.EffectClockForTests = () => Now;
        Manager.EffectPlaybackRequested += Events.Add; Manager.EffectPlaybackStopped += (_, __) => Stops++;
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    public void Dispose()
    { Object.DestroyImmediate(_root); typeof(SoundManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previous); }
}
#endif
