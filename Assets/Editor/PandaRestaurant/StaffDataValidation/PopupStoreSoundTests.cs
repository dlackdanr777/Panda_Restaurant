#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.DataBind;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using GameHost = PopupStorePhasePreviewIntegrationTests.Host;

/// <summary>Actual local sound dispatch and popup transactions; never starts Play or touches SDK/PlayerPrefs.</summary>
public sealed class PopupStoreSoundTests
{
    [Test]
    public void AuthoredPopupCoversEveryCueWithExistingRuntimeClipsAndNoLocalAudioPool()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(GameHost.Prefab);
        Assert.That(root, Is.Not.Null);
        var owners = root.GetComponentsInChildren<PopupStoreSound>(true);
        Assert.That(owners, Has.Length.EqualTo(1));
        Assert.That(owners[0].gameObject, Is.SameAs(root));
        Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty,
            "The popup must use the existing SoundManager pool instead of serialized duplicate sources.");
        var settings = (PopupStoreSound.CueSetting[])Get(owners[0], "_cues");
        CollectionAssert.AreEquivalent(Enum.GetValues(typeof(PopupStoreSoundCue)).Cast<PopupStoreSoundCue>(), settings.Select(x => x.Cue));
        foreach (var setting in settings)
        {
            Assert.That(setting.Clip, Is.Not.Null, setting.Cue.ToString());
            string path = AssetDatabase.GetAssetPath(setting.Clip);
            Assert.That(path.StartsWith("Assets/Sound/", StringComparison.Ordinal)
                || path.StartsWith("Assets/Resources/Audio/", StringComparison.Ordinal), Is.True, path);
            Assert.That(path, Does.Not.Contain("/Editor/"));
            Assert.That(setting.Clip.length, Is.GreaterThan(0), path);
            Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(path), Is.SameAs(setting.Clip));
            Assert.That(setting.Volume, Is.GreaterThan(0).And.LessThanOrEqualTo(1), setting.Cue.ToString());
            Assert.That(float.IsNaN(setting.MinimumInterval) || float.IsInfinity(setting.MinimumInterval), Is.False);
            Assert.That(setting.MinimumInterval, Is.GreaterThanOrEqualTo(0));
        }
        Assert.That((int)Get(owners[0], "_maximumConcurrent"), Is.InRange(1, 8));
        Assert.That(Get(root.GetComponent<PopupStoreOrderProgress>(), "_matchSound"), Is.Null,
            "The former match bell belongs only to an accepted packing-button click now.");
        var packingBell = settings.Single(x => x.Cue == PopupStoreSoundCue.PackingButton);
        Assert.That(packingBell.MaximumDuration, Is.GreaterThan(0).And.LessThanOrEqualTo(1));
        var recipeClip = (AudioClip)Get(owners[0], "_recipeBackground");
        Assert.That(recipeClip, Is.Not.Null);
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(recipeClip)),
            Is.EqualTo("fbc95f0d7d782464ab51023ca2e421c2"), "Reuse the actual recipe MiniGame1._bgAudio.");
        Assert.That((float)Get(owners[0], "_recipeTransitionSeconds"), Is.EqualTo(.5f));
    }

    [TestCase("_masterVolume")]
    [TestCase("_soundEffectVolume")]
    [TestCase("_effectVolume")]
    public void ZeroExistingMixerGainDispatchesNoPopupEffect(string field)
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach();
            Set(f.Audio.Manager, field, 0f);
            Assert.That(Play(sound, PopupStoreSoundCue.PackingAccepted), Is.False);
            Assert.That(f.Audio.Events, Is.Empty);
        }
    }

    [Test]
    public void ExistingPoolRetainsConfiguredGainAndNeverAddsAnAudioSource()
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach();
            var before = f.Audio.Manager.GetComponentsInChildren<AudioSource>(true);
            Set(f.Audio.Manager, "_effectVolume", .5f);
            Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.True);
            var item = f.Audio.Events.Single();
            Assert.That(item.Owner, Is.SameAs(sound));
            Assert.That(item.Source.volume, Is.EqualTo(.3f).Within(.00001f));
            Assert.That(item.Source.pitch, Is.EqualTo(1));
            Assert.That(item.Source.loop, Is.False);
            CollectionAssert.AreEqual(before, f.Audio.Manager.GetComponentsInChildren<AudioSource>(true));
        }
    }

    [TestCase(PopupStoreSoundCue.PackingAccepted)]
    [TestCase(PopupStoreSoundCue.PackingBoom)]
    [TestCase(PopupStoreSoundCue.BoxAppeared)]
    [TestCase(PopupStoreSoundCue.DeliveryStarted)]
    [TestCase(PopupStoreSoundCue.CustomerSatisfied)]
    [TestCase(PopupStoreSoundCue.CustomerExpired)]
    [TestCase(PopupStoreSoundCue.OrderShown)]
    [TestCase(PopupStoreSoundCue.ScoreAdded)]
    [TestCase(PopupStoreSoundCue.ResultShown)]
    [TestCase(PopupStoreSoundCue.RewardAppeared)]
    [TestCase(PopupStoreSoundCue.RewardOpen)]
    [TestCase(PopupStoreSoundCue.DiamondsShown)]
    public void RepeatedReceiptOrResultIdentityIsDispatchedOnlyOnce(PopupStoreSoundCue cue)
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach(); var id = new object();
            Assert.That(Play(sound, cue, id), Is.True);
            f.Advance();
            Assert.That(Play(sound, cue, id), Is.False);
            Assert.That(f.Count(cue), Is.EqualTo(1));
            Assert.That(Play(sound, cue, new object()), Is.True);
            Assert.That(f.Count(cue), Is.EqualTo(2));
        }
    }

    [Test]
    public void CueAndStageDigitMarkersDoNotSuppressDistinctEvents()
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach(); var identity = new object();
            foreach (var marker in new[] { "1:3", "1:2", "1:1", "2:3" })
            {
                Assert.That(Play(sound, PopupStoreSoundCue.Countdown, identity, marker), Is.True);
                f.Advance();
            }
            Assert.That(Play(sound, PopupStoreSoundCue.Countdown, identity, "1:3"), Is.False);
            Assert.That(Play(sound, PopupStoreSoundCue.GameStart, identity, "1:3"), Is.True);
            Assert.That(f.Count(PopupStoreSoundCue.Countdown), Is.EqualTo(4));
            Assert.That(f.Count(PopupStoreSoundCue.GameStart), Is.EqualTo(1));
        }
    }

    [Test]
    public void RapidUnidentifiedInputsRespectIntervalAndOwnerLimitWithoutDelayedReplay()
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach(); Set(sound, "_maximumConcurrent", 1);
            f.Settings(sound).Single(x => x.Cue == PopupStoreSoundCue.MaterialAdded).MinimumInterval = .1f;
            Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.True);
            Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.False);
            f.Audio.Now = .11;
            Assert.That(Play(sound, PopupStoreSoundCue.CounterCleared), Is.False, "The first owned clip is still alive.");
            f.Advance();
            Assert.That(f.Audio.Events.Count, Is.EqualTo(1), "Suppressed requests never queue a later sound.");
            Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.True);
            Assert.That(f.Audio.Events.Count, Is.EqualTo(2));
        }
    }

    [TestCase("OnDisable", null)]
    [TestCase("OnApplicationFocus", false)]
    [TestCase("OnApplicationPause", true)]
    public void LifecycleStopCancelsOnlyThisPopupOwnedAudio(string callback, object argument)
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach(); var other = new object();
            Assert.That(Play(sound, PopupStoreSoundCue.PackingBoom), Is.True);
            var unrelated = f.Audio.Manager.PlayPresentationEffect(EffectType.None, f.Clip(PopupStoreSoundCue.GameStart), owner: other, maxConcurrent: 1);
            Assert.That(unrelated, Is.Not.Null);
            Invoke(sound, callback, argument == null ? Array.Empty<object>() : new[] { argument });
            Assert.That(f.Audio.Stops, Is.EqualTo(1));
            Assert.That(f.Audio.Manager.PlayPresentationEffect(EffectType.None, f.Clip(PopupStoreSoundCue.GameStart), owner: other, maxConcurrent: 1), Is.Null);
            Assert.That(Play(sound, PopupStoreSoundCue.PackingBoom), Is.True, "This owner's slot was released independently.");
        }
    }

    [Test]
    public void HiddenOrDisabledPopupAndMissingClipAreSilentWithoutThrowing()
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach();
            sound.enabled = false; Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.False);
            sound.enabled = true; sound.gameObject.SetActive(false);
            Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.False);
            sound.gameObject.SetActive(true);
            f.Settings(sound).Single(x => x.Cue == PopupStoreSoundCue.MaterialAdded).Clip = null;
            Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.False);
            Assert.DoesNotThrow(() => PopupStoreSound.Play(null, PopupStoreSoundCue.GameOver));
            Assert.That(f.Audio.Events, Is.Empty);
        }
    }

    [Test]
    public void MissingManagerDoesNotCreateAnInstanceOrLoseGameplayControl()
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach(); var singleton = typeof(SoundManager).GetField("_instance", StaticFlags);
            singleton.SetValue(null, null);
            try
            {
                Assert.That(Play(sound, PopupStoreSoundCue.GameStart), Is.False);
                Assert.That(SoundManager.TryGetExistingInstance(), Is.Null);
                Assert.That(f.Audio.Events, Is.Empty);
            }
            finally { singleton.SetValue(null, f.Audio.Manager); }
        }
    }

    [Test]
    public void ThrowingOptionalClockCannotEscapeIntoACommittedGameplayCaller()
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach(); Set(sound, "ClockForTests", (Func<double>)(() => throw new InvalidOperationException("offline audio failure")));
            Assert.That(Play(sound, PopupStoreSoundCue.MaterialAdded), Is.False);
            Assert.That(f.Audio.Events, Is.Empty);
        }
    }

    [Test]
    public void RepeatedButtonBindingKeepsOneInputSoundAndAutomaticOnClickSilent()
    {
        using (var f = new AudioFixture())
        {
            var root = f.MakeRoot("input button", typeof(RectTransform), typeof(Image), typeof(Button));
            var button = root.GetComponent<Button>();
            for (int i = 0; i < 5; i++) PopupStoreSound.BindButton(button);
            Assert.That(root.GetComponents<GachaButtonInputSound>(), Has.Length.EqualTo(1));
            button.onClick.Invoke(); Assert.That(f.Audio.ButtonEvents, Is.Empty);
            var feedback = root.GetComponent<GachaButtonInputSound>();
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left, pointerId = 17 };
            feedback.OnPointerDown(pointer); feedback.OnPointerDown(pointer); feedback.OnPointerUp(pointer);
            Assert.That(f.Audio.ButtonEvents.Count(), Is.EqualTo(1));
            f.Advance(); feedback.OnSubmit(new BaseEventData(null));
            Assert.That(f.Audio.ButtonEvents.Count(), Is.EqualTo(2));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void DisabledOrMutedInputDoesNotDispatchButtonSound(bool muted)
    {
        using (var f = new AudioFixture())
        {
            var root = f.MakeRoot("blocked input", typeof(RectTransform), typeof(Image), typeof(Button));
            var button = root.GetComponent<Button>(); PopupStoreSound.BindButton(button);
            if (muted) f.Audio.Muted = true; else button.interactable = false;
            var feedback = root.GetComponent<GachaButtonInputSound>();
            feedback.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
            feedback.OnSubmit(new BaseEventData(null));
            Assert.That(f.Audio.Events, Is.Empty);
        }
    }

    [Test]
    public void ButtonGetterAutomaticSoundIsRemovedButTheGameplayListenerRemains()
    {
        using (var f = new AudioFixture())
        {
            var root = f.MakeRoot("data-bound input", typeof(RectTransform), typeof(Image), typeof(Button));
            root.SetActive(false); var button = root.GetComponent<Button>();
            var getter = root.AddComponent<ButtonGetter>();
            // Set up the already-bound listener without touching global DataBind or PlayerPrefs.
            int gameplayCalls = 0, automaticSoundCalls = 0;
            UnityAction automatic = () => automaticSoundCalls++;
            Set(getter, "_bindId", "ButtonClickSound"); Set(getter, "_button", button); Set(getter, "_action", automatic);
            Set(getter, "_data", new BindData<UnityAction>());
            button.onClick.AddListener(automatic); button.onClick.AddListener(() => gameplayCalls++);
            PopupStoreSound.BindButton(button); PopupStoreSound.BindButton(button);
            button.onClick.Invoke();
            Assert.That(gameplayCalls, Is.EqualTo(1)); Assert.That(automaticSoundCalls, Is.Zero);
            Assert.That((bool)Get(getter, "_inputOnlySound"), Is.True);
        }
    }

    [Test]
    public void CounterSoundFollowsAcceptedMaterialAdditionAndNotFullOrPausedRejection()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root);
            for (int i = 0; i < 5; i++) { Assert.That(h.Counter.TryAdd(h.Material("STAFF01")), Is.True); f.Advance(); }
            Assert.That(f.Count(PopupStoreSoundCue.MaterialAdded), Is.EqualTo(5));
            Assert.That(h.Counter.TryAdd(h.Material("BOX_PINK")), Is.False);
            Assert.That(h.Counter.TryAdd(null), Is.False);
            Assert.That(f.Count(PopupStoreSoundCue.MaterialAdded), Is.EqualTo(5));
            h.Counter.Clear(); Assert.That(f.Count(PopupStoreSoundCue.CounterCleared), Is.EqualTo(1));
            h.Counter.Clear(); Assert.That(f.Count(PopupStoreSoundCue.CounterCleared), Is.EqualTo(1));
            h.Clock.SetManualPaused(true);
            Assert.That(h.Counter.TryAdd(h.Material("STAFF01")), Is.False);
            Assert.That(f.Count(PopupStoreSoundCue.MaterialAdded), Is.EqualTo(5));
        }
    }

    [Test]
    public void MatchingMaterialsKeepTheirCirclesWithoutPlayingThePackingBell()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root); h.Ready(); f.Advance();
            var progress = h.Root.GetComponent<PopupStoreOrderProgress>();
            Assert.That(Get(progress, "_matchSound"), Is.Null);
            var entries = h.Entries(0);
            int feedbacks = 0; progress.FeedbackTriggered += () => feedbacks++;
            for (int i = 0; i < entries.Length; i++)
            {
                Assert.That(h.Counter.TryAdd(entries[i]), Is.True); f.Advance();
                Assert.That(progress.GetMatchedCount(0), Is.EqualTo(i + 1));
            }
            var slots = (PopupStoreOrderProgressSlot[])Get(progress, "_slots");
            Assert.That(slots[0].Circles.Count(x => x.gameObject.activeSelf), Is.EqualTo(entries.Length));
            Assert.That(feedbacks, Is.EqualTo(entries.Length), "Existing visual feedback notifications stay intact.");
            Assert.That(f.Count(PopupStoreSoundCue.MaterialAdded), Is.EqualTo(entries.Length));
            Assert.That(f.Count(PopupStoreSoundCue.PackingButton), Is.Zero);
            Assert.That(h.Packing.SuccessfulPackCount, Is.Zero);
        }
    }

    [Test]
    public void PackingPointerHasOneBellAfterRepeatedBindingAndNoGenericClickSound()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root); h.Ready(); f.Advance();
            for (int i = 0; i < 5; i++) Assert.That(h.Packing.Refresh(), Is.True);
            var button = (Button)Get(h.Packing, "_packButton");
            Assert.That(button.GetComponents<GachaButtonInputSound>(), Has.Length.EqualTo(1));
            var data = new PointerEventData(null) { button = PointerEventData.InputButton.Left, pointerId = 21 };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.That(f.Count(PopupStoreSoundCue.PackingButton), Is.EqualTo(1));
            Assert.That(f.Audio.ButtonEvents, Is.Empty);
            Assert.That(h.Packing.SuccessfulPackCount, Is.Zero, "An empty tray still rejects packing.");
            f.Advance(); h.Clock.SetManualPaused(true); button.onClick.Invoke();
            Assert.That(f.Count(PopupStoreSoundCue.PackingButton), Is.EqualTo(1));
        }
    }

    [Test]
    public void DirectPackingApiKeepsItsTransactionFeedbackWithoutButtonBell()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root); h.Ready(); f.Advance(); var receipt = h.Pack();
            Assert.That(f.Count(PopupStoreSoundCue.PackingButton), Is.Zero);
            Assert.That(f.Count(PopupStoreSoundCue.PackingAccepted), Is.EqualTo(1));
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
        }
    }

    [Test]
    public void PackingBellBudgetStopsOnlyBellAndDoesNotCutTheDeliveryEffect()
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach();
            f.Settings(sound).Single(x => x.Cue == PopupStoreSoundCue.PackingButton).MaximumDuration = .2f;
            Assert.That(Play(sound, PopupStoreSoundCue.PackingButton), Is.True);
            Assert.That(Play(sound, PopupStoreSoundCue.DeliveryStarted), Is.True);
            f.Audio.Now = .19; Invoke(sound, "TickPackingButtonSound"); Assert.That(f.Audio.Stops, Is.Zero);
            f.Audio.Now = .21; Invoke(sound, "TickPackingButtonSound"); Assert.That(f.Audio.Stops, Is.EqualTo(1));
            var voices = (System.Collections.IDictionary)Get(f.Audio.Manager, "_effectVoices");
            Assert.That(voices.Count, Is.EqualTo(1));
            Assert.That(voices.Contains(f.Audio.Events.Single(e => e.Clip == f.Clip(PopupStoreSoundCue.DeliveryStarted)).Source), Is.True);
        }
    }

    [TestCase(PopupStoreSoundCue.GameOver, false)]
    [TestCase(PopupStoreSoundCue.GameOver, true)]
    [TestCase(PopupStoreSoundCue.StageSuccess, false)]
    [TestCase(PopupStoreSoundCue.StageSuccess, true)]
    public void TerminalCueStopsTheOwnedWarningBeforePlayingEvenWhenEndCueIsMuted(PopupStoreSoundCue terminal, bool muteEnd)
    {
        using (var f = new AudioFixture())
        {
            var sound = f.Attach(); var otherOwner = new object();
            Assert.That(Play(sound, PopupStoreSoundCue.TimeWarning), Is.True);
            var unrelated = f.Audio.Manager.PlayPresentationEffect(EffectType.None, f.Clip(PopupStoreSoundCue.MaterialAdded), owner: otherOwner);
            var warning = f.Audio.Events.Single(x => x.Clip == f.Clip(PopupStoreSoundCue.TimeWarning)).Source;
            bool stoppedBeforeEndDispatch = false;
            f.Audio.Manager.EffectPlaybackRequested += info =>
            { if (info.Clip == f.Clip(terminal)) stoppedBeforeEndDispatch = f.Audio.Stops == 1; };
            if (muteEnd) f.Audio.Muted = true;
            Assert.That(Play(sound, terminal, new object()), Is.EqualTo(!muteEnd));
            Assert.That(f.Audio.Stops, Is.EqualTo(1));
            Assert.That(f.Count(terminal), Is.EqualTo(muteEnd ? 0 : 1));
            if (!muteEnd) Assert.That(stoppedBeforeEndDispatch, Is.True);
            var voices = (System.Collections.IDictionary)Get(f.Audio.Manager, "_effectVoices");
            Assert.That(voices.Contains(unrelated), Is.True, "Other popup owners remain untouched.");
            if (muteEnd) Assert.That(voices.Contains(warning), Is.False);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void TutorialStartsRecipeMusicOnceAndKeepsItThroughEveryCueWithoutChangingUserSettings(bool previouslyMuted)
    {
        using (var f = new AudioFixture()) using (var h = new GameHost(false, false)) using (var fonts = new StaffGachaOfflineFonts())
        {
            var sound = f.Attach(h.Root); var background = f.Background(previouslyMuted);
            var playback = new RecipePlayback(sound, background, f.Clip(PopupStoreSoundCue.StageNotice));
            float originalVolume = background.volume;
            string[] keys = { "MasterVolume", "BackgroundVolume", "SoundEffectVolume" };
            var keyPresence = keys.Select(PlayerPrefs.HasKey).ToArray();
            var values = keys.Select(key => PlayerPrefs.GetFloat(key, -123.25f)).ToArray();
            int sourceCount = f.Audio.Manager.GetComponentsInChildren<AudioSource>(true).Length;
            var view = MakeTutorial(h, fonts); view.RenderTutorial(true, true, "이용권 1/1");
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(background.clip, Is.SameAs(playback.Recipe));
            Assert.That(playback.Clips.Count, Is.EqualTo(1));
            view.RenderTutorial(true, true, "이용권 1/1");
            for (int stage = 1; stage <= 6; stage++)
                foreach (var cue in new[] { "Stage", "3", "2", "1", "GameStart", "Success", "GameOver" })
                {
                    view.RenderCue(cue, stage, 0); f.Advance(); Invoke(sound, "TickRecipeBackground");
                    Assert.That(background.clip, Is.SameAs(playback.Recipe));
                }
            Assert.That(playback.Clips.Count, Is.EqualTo(1));
            Assert.That(playback.Durations.Single(), Is.EqualTo(.5f)); Assert.That(playback.Loops.Single(), Is.True);
            Assert.That(playback.Restores, Is.Zero);
            Assert.That(background.mute, Is.EqualTo(previouslyMuted)); Assert.That(background.volume, Is.EqualTo(originalVolume));
            Assert.That(background.isPlaying, Is.False, "The test dispatch seam suppresses native audio only.");
            Assert.That(f.Audio.Manager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sourceCount));
            CollectionAssert.AreEqual(keyPresence, keys.Select(PlayerPrefs.HasKey).ToArray());
            CollectionAssert.AreEqual(values, keys.Select(key => PlayerPrefs.GetFloat(key, -123.25f)).ToArray());
            Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
        }
    }

    [TestCase(true)] [TestCase(false)]
    public void ClosingTutorialUsesMainMusicRestoreOrCapturedFallbackExactlyOnce(bool hasMainMusic)
    {
        using (var f = new AudioFixture()) using (var h = new GameHost(false, false)) using (var fonts = new StaffGachaOfflineFonts())
        {
            var sound = f.Attach(h.Root); var background = f.Background(false);
            background.loop = false;
            var playback = new RecipePlayback(sound, background, f.Clip(PopupStoreSoundCue.StageNotice)) { MainRestoreAvailable = hasMainMusic };
            var view = MakeTutorial(h, fonts,
                () => { h.Root.SetActive(false); Invoke(sound, "OnDisable"); return true; });
            view.RenderTutorial(true, true, "이용권 1/1");
            Assert.That(view.TryClose(), Is.True);
            Invoke(sound, "RestoreRecipeBackground"); Invoke(sound, "OnDestroy");
            Assert.That(playback.Restores, Is.EqualTo(1));
            Assert.That(playback.Clips.Count, Is.EqualTo(hasMainMusic ? 1 : 2));
            Assert.That(background.clip, Is.SameAs(playback.Previous));
            if (!hasMainMusic) Assert.That(playback.Loops.Last(), Is.False);
            Assert.That(h.Admission.Access.Remaining, Is.EqualTo(1));
            Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(f.Count(PopupStoreSoundCue.GameStart), Is.Zero);
        }
    }

    [Test]
    public void ReenteringTheSameTutorialStartsAndRestoresRecipeMusicOncePerVisitWithoutSpendingTicket()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost(false, false)) using (var fonts = new StaffGachaOfflineFonts())
        {
            var sound = f.Attach(h.Root); var background = f.Background(false);
            var playback = new RecipePlayback(sound, background, f.Clip(PopupStoreSoundCue.StageNotice)) { MainRestoreAvailable = true };
            var view = MakeTutorial(h, fonts,
                () => { h.Root.SetActive(false); Invoke(sound, "OnDisable"); return true; });
            for (int visit = 1; visit <= 2; visit++)
            {
                h.Root.SetActive(true);
                view.RenderTutorial(true, true, "이용권 1/1");
                view.RenderTutorial(true, true, "이용권 1/1");
                Assert.That(background.clip, Is.SameAs(playback.Recipe));
                Assert.That(playback.Clips.Count, Is.EqualTo(visit));
                Assert.That(view.TryClose(), Is.True);
                Assert.That(view.TryClose(), Is.False);
                Invoke(sound, "RestoreRecipeBackground");
                Assert.That(background.clip, Is.SameAs(playback.Previous));
                Assert.That(playback.Restores, Is.EqualTo(visit));
                Assert.That(view.CloseRequestCount, Is.EqualTo(visit));
                Assert.That(h.Admission.Access.Remaining, Is.EqualTo(1));
                Assert.That(h.Clock.State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
                Assert.That(h.Clock.ElapsedSeconds, Is.Zero);
            }
            Assert.That(f.Count(PopupStoreSoundCue.GameStart), Is.Zero);
        }
    }

    [TestCase("manager")] [TestCase("source")] [TestCase("clip")]
    public void MissingRecipeAudioDependenciesStaySilentAndNeverCreateAReplacement(string missing)
    {
        using (var f = new AudioFixture()) using (var h = new GameHost(false, false))
        {
            var sound = f.Attach(h.Root);
            var background = missing == "source" ? null : f.Background(false);
            var playback = new RecipePlayback(sound, background, missing == "clip" ? null : f.Clip(PopupStoreSoundCue.StageNotice));
            var original = f.Audio.Manager.GetComponentsInChildren<AudioSource>(true);
            var singleton = typeof(SoundManager).GetField("_instance", StaticFlags);
            if (missing == "manager") singleton.SetValue(null, null);
            try
            {
                PopupStoreSound.BeginRecipeBackground(h.Root.GetComponent<UIPopupStore>());
                Invoke(sound, "TickRecipeBackground"); Invoke(sound, "RestoreRecipeBackground");
                Assert.That(playback.Clips, Is.Empty); Assert.That(playback.Restores, Is.Zero);
                CollectionAssert.AreEqual(original, f.Audio.Manager.GetComponentsInChildren<AudioSource>(true));
                if (missing == "manager") Assert.That(SoundManager.TryGetExistingInstance(), Is.Null);
            }
            finally { singleton.SetValue(null, f.Audio.Manager); }
        }
    }

    [TestCase("clip")] [TestCase("source")] [TestCase("manager")]
    public void ExternalBackgroundOwnershipChangeIsNeverOverwrittenByRenderOrClose(string change)
    {
        using (var f = new AudioFixture()) using (var h = new GameHost(false, false))
        {
            var sound = f.Attach(h.Root); var background = f.Background(false);
            var playback = new RecipePlayback(sound, background, f.Clip(PopupStoreSoundCue.StageNotice));
            PopupStoreSound.BeginRecipeBackground(h.Root.GetComponent<UIPopupStore>());
            Invoke(sound, "TickRecipeBackground");
            AudioSource external = background; Phase5UiAudioScope other = null;
            try
            {
                if (change == "source")
                { background.gameObject.name = "FormerBackgroundAudio"; external = f.Background(false); }
                else if (change == "manager")
                {
                    other = new Phase5UiAudioScope();
                    var child = new GameObject("BackgroundAudio"); child.transform.SetParent(other.Manager.transform, false);
                    external = child.AddComponent<AudioSource>(); external.playOnAwake = false;
                }
                var externalClip = f.Clip(PopupStoreSoundCue.ResultShown); external.clip = externalClip;
                Invoke(sound, "TickRecipeBackground");
                PopupStoreSound.BeginRecipeBackground(h.Root.GetComponent<UIPopupStore>());
                Invoke(sound, "TickRecipeBackground"); Invoke(sound, "RestoreRecipeBackground");
                Assert.That(external.clip, Is.SameAs(externalClip));
                Assert.That(playback.Clips.Count, Is.EqualTo(1)); Assert.That(playback.Restores, Is.Zero);
            }
            finally { other?.Dispose(); }
        }
    }

    [Test]
    public void ExistingBackgroundFadeCanKeepItsPreviousClipUntilRecipeTrackIsObserved()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost(false, false))
        {
            var sound = f.Attach(h.Root); var background = f.Background(false);
            var playback = new RecipePlayback(sound, background, f.Clip(PopupStoreSoundCue.StageNotice)) { ApplyClipAtDispatch = false };
            PopupStoreSound.BeginRecipeBackground(h.Root.GetComponent<UIPopupStore>());
            Invoke(sound, "TickRecipeBackground");
            Assert.That(background.clip, Is.SameAs(playback.Previous));
            background.clip = playback.Recipe; Invoke(sound, "TickRecipeBackground");
            playback.ApplyClipAtDispatch = true; Invoke(sound, "RestoreRecipeBackground");
            Assert.That(playback.Restores, Is.EqualTo(1)); Assert.That(playback.Clips.Count, Is.EqualTo(2));
            Assert.That(background.clip, Is.SameAs(playback.Previous));
        }
    }

    [Test]
    public void NoPreviousTrackAndNoMainSceneDoesNotInventAnotherBackgroundOnClose()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost(false, false))
        {
            var sound = f.Attach(h.Root); var background = f.Background(false); background.clip = null;
            var playback = new RecipePlayback(sound, background, f.Clip(PopupStoreSoundCue.StageNotice));
            PopupStoreSound.BeginRecipeBackground(h.Root.GetComponent<UIPopupStore>());
            Invoke(sound, "RestoreRecipeBackground"); Invoke(sound, "RestoreRecipeBackground");
            Assert.That(playback.Restores, Is.EqualTo(1)); Assert.That(playback.Clips.Count, Is.EqualTo(1));
            Assert.That(background.clip, Is.SameAs(playback.Recipe));
        }
    }

    [TestCase("drag")] [TestCase("wheel")]
    public void MaterialPointerCancelledByScrollNeverPlaysSelectionSound(string interruption)
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root);
            var item = h.Root.GetComponentsInChildren<PopupStoreMaterialItem>(true).Single(x => x.Material.Id == "STAFF01");
            var data = new PointerEventData(null) { button = PointerEventData.InputButton.Left, pointerId = 9, eligibleForClick = true };
            item.OnPointerDown(data);
            if (interruption == "drag")
            {
                ExecuteEvents.Execute(item.Owner.gameObject, data, ExecuteEvents.beginDragHandler);
                data.dragging = true;
                ExecuteEvents.Execute(item.Owner.gameObject, data, ExecuteEvents.endDragHandler);
            }
            else
            {
                data.scrollDelta = Vector2.down;
                ExecuteEvents.Execute(item.Owner.gameObject, data, ExecuteEvents.scrollHandler);
                data.scrollDelta = Vector2.zero;
            }
            data.dragging = false; item.OnPointerClick(data);
            Assert.That(h.Counter.Count, Is.Zero);
            Assert.That(f.Count(PopupStoreSoundCue.MaterialAdded), Is.Zero);
        }
    }

    [Test]
    public void RealPackingCommitScoresAndSoundsOnceWhileWrongButtonInputOnlyRejects()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root); h.Ready(); f.Advance();
            var pack = (Button)Get(h.Packing, "_packButton");
            pack.onClick.Invoke();
            Assert.That(h.Packing.LastResult.Success, Is.False);
            Assert.That(f.Count(PopupStoreSoundCue.PackingRejected), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.PackingAccepted), Is.Zero);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty);
            f.Advance(); var receipt = h.Pack();
            Assert.That(f.Count(PopupStoreSoundCue.PackingAccepted), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.ScoreAdded), Is.EqualTo(1));
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            f.Advance(); h.Presentation.TickPresentation(0);
            for (int i = 0; i < 500 && h.Presentation.CurrentPhase != PopupStorePackingPresentation.Phase.Idle; i++)
            { f.Audio.Now += .02; h.Presentation.TickPresentation(.02f); }
            Assert.That(h.Presentation.CurrentPhase, Is.EqualTo(PopupStorePackingPresentation.Phase.Idle));
            f.Advance(); h.Entrance.TickEntrance(2);
            Assert.That(f.Count(PopupStoreSoundCue.PackingBoom), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.BoxAppeared), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.DeliveryStarted), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.CustomerSatisfied), Is.EqualTo(1));
            h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(f.Count(PopupStoreSoundCue.PackingAccepted), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.CustomerSatisfied), Is.EqualTo(1));
            h.AssertNoR1();
        }
    }

    [Test]
    public void MasterMutedRealPackingStillScoresDeliversAndReleasesOneReceipt()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root); Set(f.Audio.Manager, "_masterVolume", 0f);
            h.Ready(); var receipt = h.Pack(); h.Deliver(); h.Entrance.TickEntrance(2);
            Assert.That(h.Score.TotalScore, Is.EqualTo(100));
            Assert.That(h.Packing.SuccessfulPackCount, Is.EqualTo(1));
            Assert.That(h.Packing.CommittedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(h.Presentation.VisualOutcomes.Single().Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.VisualCompleted));
            Assert.That(h.Presentation.VisualOutcomes.Single().Receipt, Is.SameAs(receipt));
            Assert.That(h.Entrance.EditorReleasedReceipts.Single(), Is.SameAs(receipt));
            Assert.That(f.Audio.Events, Is.Empty);
            h.AssertNoR1();
        }
    }

    [Test]
    public void DisabledPackingButtonDoesNotPlayFailureOrMutateOrders()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root); h.Ready(); f.Advance(); h.Clock.SetManualPaused(true);
            var button = (Button)Get(h.Packing, "_packButton");
            Assert.That(button.interactable, Is.False);
            button.onClick.Invoke();
            Assert.That(f.Count(PopupStoreSoundCue.PackingRejected), Is.Zero);
            Assert.That(f.Count(PopupStoreSoundCue.PackingAccepted), Is.Zero);
            Assert.That(h.Packing.CommittedReceipts, Is.Empty); Assert.That(h.Score.TotalScore, Is.Zero);
        }
    }

    [TestCase("Packed")]
    [TestCase("Move")]
    public void CancelledDeliveryRecoveryClearsTheOriginalCustomerWithoutSatisfiedSound(string phase)
    {
        using (var f = new AudioFixture())
        {
            // Reuse the existing ordinary-mode lifecycle fixture without changing its
            // visibility or introducing another variant of the delivery implementation.
            var hostType = typeof(PopupStoreCustomerDepartureTests).GetNestedType("Host", BindingFlags.NonPublic);
            Assert.That(hostType, Is.Not.Null);
            using (var host = (IDisposable)Activator.CreateInstance(hostType, true))
            {
                var root = (GameObject)Get(host, "Root"); f.Attach(root);
                var presentation = (PopupStorePackingPresentation)Get(host, "Presentation");
                var entrance = (PopupStoreCustomerEntrance)Get(host, "Entrance");
                var packing = (PopupStorePacking)Get(host, "Packing");
                Invoke(host, "ReadyCustomers"); f.Advance();
                var receipt = (PopupStorePackingReceipt)Invoke(host, "Pack", 0);
                if (phase != "Packed")
                { presentation.TickPresentation(0); Invoke(host, "ToPhase", phase); }
                Assert.That(f.Count(PopupStoreSoundCue.CustomerSatisfied), Is.Zero);
                Invoke(host, "Close");
                var outcome = presentation.VisualOutcomes.Single();
                Assert.That(outcome.Kind, Is.EqualTo(PopupStoreVisualOutcomeKind.Cancelled));
                Assert.That(outcome.Receipt, Is.SameAs(receipt));
                Assert.That(entrance.StartedDepartureCount, Is.Zero);
                f.Advance(); Invoke(host, "Reopen");
                Assert.That(entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Leaving));
                Assert.That(entrance.StartedDepartureCount, Is.EqualTo(1));
                Assert.That(f.Count(PopupStoreSoundCue.CustomerSatisfied), Is.Zero,
                    "Recovery cleanup must not announce a delivery the player never saw complete.");
                entrance.TickEntrance(10); Invoke(host, "Begin"); entrance.TickEntrance(10);
                Assert.That(entrance.GetState(0), Is.EqualTo(PopupStoreCustomerState.Empty));
                Assert.That(f.Count(PopupStoreSoundCue.CustomerSatisfied), Is.Zero);
                Assert.That(packing.SuccessfulPackCount, Is.EqualTo(1));
                Assert.That(packing.CommittedReceipts.Single(), Is.SameAs(receipt));
                Assert.That(presentation.VisualOutcomes.Single(), Is.SameAs(outcome));
            }
        }
    }

    [Test]
    public void FinalResultRefreshDoesNotRepeatAppearanceSoundOrFinalization()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost())
        {
            f.Attach(h.Root);
            h.Clock.AdvanceTime(h.Clock.RemainingSeconds);
            Assert.That(h.Preview.TickCleanup(), Is.True);
            h.ShowResult();
            var result = h.Preview.Session.FinalResult;
            Assert.That(f.Count(PopupStoreSoundCue.ResultShown), Is.EqualTo(1));
            f.Advance(); h.Panel.Refresh(); h.Panel.TickResult(1); h.Panel.TickResult(1);
            Assert.That(f.Count(PopupStoreSoundCue.ResultShown), Is.EqualTo(1));
            Assert.That(h.Preview.Session.FinalizationCount, Is.EqualTo(1));
            Assert.That(h.Preview.Session.FinalResult, Is.SameAs(result));
            h.AssertNoR1();
        }
    }

    [Test]
    public void ActualPhaseCueRefreshHideResumeAndNextStageHaveSingleOwnedSounds()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost()) using (var fonts = new StaffGachaOfflineFonts())
        {
            f.Attach(h.Root);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewViewBuilder.PrefabPath), h.Root.transform, false);
            root.SetActive(false); fonts.BindBeforeActivation(root); root.SetActive(true);
            var view = root.GetComponent<PopupStorePhasePreviewView>();
            view.Bind(() => false, () => false);
            foreach (var cue in new[] { "Stage", "3", "2", "1", "GameStart", "Success", "GameOver" })
            {
                view.RenderCue(cue, 1, .1f); view.RenderCue(cue, 1, .7f);
                view.HideAll(); view.RenderCue(cue, 1, .9f); f.Advance();
            }
            Assert.That(f.Count(PopupStoreSoundCue.StageNotice), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.Countdown), Is.EqualTo(3));
            Assert.That(f.Count(PopupStoreSoundCue.GameStart), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.StageSuccess), Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.GameOver), Is.EqualTo(1));
            view.RenderCue("Stage", 2, 0); f.Advance(); view.RenderCue("3", 2, 0);
            Assert.That(f.Count(PopupStoreSoundCue.StageNotice), Is.EqualTo(2));
            Assert.That(f.Count(PopupStoreSoundCue.Countdown), Is.EqualTo(4));
        }
    }

    [Test]
    public void StageRenderedBeforeAudioContextIsReadyRetriesOnceAfterContextBecomesEligible()
    {
        using (var f = new AudioFixture()) using (var h = new GameHost()) using (var fonts = new StaffGachaOfflineFonts())
        {
            var sound = f.Attach(h.Root); Set(sound, "AllowEditModeForTests", false);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewViewBuilder.PrefabPath), h.Root.transform, false);
            root.SetActive(false); fonts.BindBeforeActivation(root); root.SetActive(true);
            var view = root.GetComponent<PopupStorePhasePreviewView>(); view.Bind(() => false, () => false);
            view.RenderCue("Stage", 1, 0);
            Assert.That(view.CueShowCount, Is.EqualTo(1));
            Assert.That(f.Count(PopupStoreSoundCue.StageNotice), Is.Zero);
            // The real clock commits Running after its initial presentation callback.
            // The EditMode presentation gate models that eligibility boundary without Play.
            Set(sound, "AllowEditModeForTests", true);
            view.RenderCue("Stage", 1, .01f); view.RenderCue("Stage", 1, .1f);
            Assert.That(view.CueShowCount, Is.EqualTo(1), "The visual itself must not restart.");
            Assert.That(f.Count(PopupStoreSoundCue.StageNotice), Is.EqualTo(1));
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(300)]
    public void RewardOpeningAndDiamondRevealSoundOnceAndResumeDoesNotAcquireAgain(int amount)
    {
        using (var f = new AudioFixture()) using (var h = new RewardHost(f))
        {
            Assert.That(h.View.Begin(h.Identity, h.Sprite, amount, RewardHost.Notice, () => true), Is.True);
            Assert.That(h.View.Begin(h.Identity, h.Sprite, amount, RewardHost.Notice, () => true), Is.True);
            Assert.That(f.Count(PopupStoreSoundCue.RewardAppeared), Is.EqualTo(1));
            Assert.That(h.View.TryOpen(), Is.False);
            h.View.Tick(h.View.RiseDuration, true); f.Advance();
            Assert.That(h.View.TryOpen(), Is.True); Assert.That(h.View.TryOpen(), Is.False);
            Assert.That(f.Count(PopupStoreSoundCue.RewardOpen), Is.EqualTo(1));
            f.Advance(); h.View.Tick(h.View.OpeningDuration, true); h.View.Tick(10, true);
            Assert.That(f.Count(PopupStoreSoundCue.DiamondsShown), Is.EqualTo(amount == 0 ? 0 : 1));
            int total = f.Audio.Events.Count;
            h.View.HidePresentation(); Assert.That(h.View.ResumePresentation(), Is.True);
            h.View.Tick(100, true); Assert.That(h.View.TryOpen(), Is.False);
            Assert.That(f.Audio.Events.Count, Is.EqualTo(total));
            Assert.That(h.View.BeginCount, Is.EqualTo(1)); Assert.That(h.View.OpenCount, Is.EqualTo(1));
            Assert.That(h.View.Amount, Is.EqualTo(amount)); h.Game.AssertNoR1();
        }
    }

    [Test]
    public void RecoveredRewardPresentationBoundaryNeverEmitsNewAcquisitionAudio()
    {
        using (var f = new AudioFixture()) using (var h = new RewardHost(f))
        {
            // Exercise the exact shared rendering boundary called only after the recovery
            // owner verifies a saved claim. This fixture does not pretend to validate a server claim.
            Assert.That((bool)Invoke(h.View, "BeginCore", h.Identity, h.Sprite, 3, "보상 수령 완료", (Func<bool>)(() => true), true, true), Is.True);
            h.View.Tick(h.View.RiseDuration, true); Assert.That(h.View.TryOpen(), Is.True);
            h.View.Tick(h.View.OpeningDuration, true);
            Assert.That(h.View.State, Is.EqualTo(PopupStorePhaseRewardVisualState.Revealed));
            Assert.That(f.Count(PopupStoreSoundCue.RewardAppeared), Is.Zero);
            Assert.That(f.Count(PopupStoreSoundCue.RewardOpen), Is.Zero);
            Assert.That(f.Count(PopupStoreSoundCue.DiamondsShown), Is.Zero);
            Assert.That(h.View.Amount, Is.EqualTo(3)); h.Game.AssertNoR1();
        }
    }

    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Get(object owner, string field) => owner.GetType().GetField(field, InstanceFlags).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, InstanceFlags).SetValue(owner, value);
    private static object Invoke(object owner, string method, params object[] arguments)
        => owner.GetType().GetMethod(method, InstanceFlags).Invoke(owner, arguments);
    private static bool Play(PopupStoreSound sound, PopupStoreSoundCue cue, object identity = null, string marker = null)
        => (bool)Invoke(sound, "TryPlay", cue, identity, marker);
    private static PopupStorePhasePreviewView MakeTutorial(GameHost host, StaffGachaOfflineFonts fonts, Func<bool> close = null)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhasePreviewViewBuilder.PrefabPath), host.Root.transform, false);
        root.SetActive(false); fonts.BindBeforeActivation(root); root.SetActive(true);
        var view = root.GetComponent<PopupStorePhasePreviewView>(); view.Bind(() => false, close ?? (() => false)); return view;
    }

    private sealed class RecipePlayback
    {
        internal readonly AudioClip Recipe, Previous;
        internal readonly List<AudioClip> Clips = new List<AudioClip>();
        internal readonly List<float> Durations = new List<float>();
        internal readonly List<bool> Loops = new List<bool>();
        internal bool MainRestoreAvailable, ApplyClipAtDispatch = true;
        internal int Restores;
        internal RecipePlayback(PopupStoreSound sound, AudioSource background, AudioClip recipe)
        {
            Recipe = recipe; Previous = background != null ? background.clip : null;
            Set(sound, "_recipeBackground", recipe); Set(sound, "_recipeTransitionSeconds", .5f);
            Set(sound, "PlayBackgroundForTests", (Action<AudioClip, float, bool>)((clip, seconds, loop) =>
            {
                Clips.Add(clip); Durations.Add(seconds); Loops.Add(loop);
                if (background != null && ApplyClipAtDispatch) { background.clip = clip; background.loop = loop; }
            }));
            Set(sound, "RestoreMainMusicForTests", (Func<bool>)(() =>
            {
                Restores++;
                if (MainRestoreAvailable && background != null) background.clip = Previous;
                return MainRestoreAvailable;
            }));
        }
    }

    private sealed class AudioFixture : IDisposable
    {
        internal readonly Phase5UiAudioScope Audio = new Phase5UiAudioScope();
        private readonly List<GameObject> _ownedRoots = new List<GameObject>();
        private readonly Dictionary<PopupStoreSoundCue, AudioClip> _clips = new Dictionary<PopupStoreSoundCue, AudioClip>();
        internal AudioFixture()
        {
            foreach (PopupStoreSoundCue cue in Enum.GetValues(typeof(PopupStoreSoundCue)))
                _clips.Add(cue, AudioClip.Create("Popup offline " + cue, 22050, 1, 44100, false));
        }
        internal GameObject MakeRoot(string name, params Type[] components)
        { var root = new GameObject(name, components); _ownedRoots.Add(root); return root; }
        internal PopupStoreSound Attach(GameObject root = null)
        {
            if (root == null) root = MakeRoot("popup sound owner");
            var sound = root.GetComponent<PopupStoreSound>() ?? root.AddComponent<PopupStoreSound>();
            Set(sound, "AllowEditModeForTests", true); Set(sound, "ClockForTests", (Func<double>)(() => Audio.Now));
            Set(sound, "_maximumConcurrent", 8);
            Set(sound, "_cues", _clips.Select(pair => new PopupStoreSound.CueSetting
            { Cue = pair.Key, Clip = pair.Value, Volume = .6f, MinimumInterval = 0 }).ToArray());
            return sound;
        }
        internal AudioClip Clip(PopupStoreSoundCue cue) => _clips[cue];
        internal AudioSource Background(bool muted)
        {
            var root = new GameObject("BackgroundAudio"); root.transform.SetParent(Audio.Manager.transform, false);
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.clip = Clip(PopupStoreSoundCue.GameStart); source.volume = .37f; source.timeSamples = 200; source.mute = muted;
            return source;
        }
        internal PopupStoreSound.CueSetting[] Settings(PopupStoreSound owner) => (PopupStoreSound.CueSetting[])Get(owner, "_cues");
        internal int Count(PopupStoreSoundCue cue) => Audio.Events.Count(e => e.Clip == _clips[cue]);
        internal void Advance() => Audio.Now += 1;
        public void Dispose()
        {
            foreach (var root in _ownedRoots) if (root != null) Object.DestroyImmediate(root);
            foreach (var clip in _clips.Values) Object.DestroyImmediate(clip);
            Audio.Dispose();
        }
    }

    private sealed class RewardHost : IDisposable
    {
        internal const string Notice = "검수용 · 미지급 · 실제 계정에 반영되지 않음";
        internal readonly GameHost Game = new GameHost();
        internal readonly PopupStorePhaseRewardPresentation View;
        internal readonly object Identity = new object();
        internal readonly Sprite Sprite;
        private readonly StaffGachaOfflineFonts _fonts = new StaffGachaOfflineFonts();
        internal RewardHost(AudioFixture audio)
        {
            audio.Attach(Game.Root);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PopupStorePhaseRewardViewBuilder.PrefabPath), Game.Root.transform, false);
            root.SetActive(false); _fonts.BindBeforeActivation(root); root.SetActive(true);
            View = root.GetComponent<PopupStorePhaseRewardPresentation>();
            Assert.That(View.BindClock(Game.Clock), Is.True);
            Sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_C_APPROVED.png");
        }
        public void Dispose() { Game.Dispose(); _fonts.Dispose(); }
    }
}
#endif
