#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

public sealed class FairyBirthSoundTests
{
    [Test]
    public void ConfiguredVoicesHaveFiveDistinctShortVariantsAndQuieterPop()
    {
        using (var host = new EnhancementFairyStage1Host())
        {
            var settings = host.Habitat.Settings;
            Assert.That(settings.BirthPopClip, Is.Not.Null);
            Assert.That(settings.BirthPopVolume, Is.InRange(.3f, .45f));
            Assert.That(settings.BirthVoiceVariants.Length, Is.EqualTo(5));
            Assert.That(settings.BirthVoiceVariants.Select(v => v.Clip.GetInstanceID() + ":" + v.Pitch).Distinct().Count(), Is.EqualTo(5));
            foreach (var voice in settings.BirthVoiceVariants)
            {
                Assert.That(voice.Clip, Is.Not.Null);
                Assert.That((voice.Clip.length - settings.VoiceStartSeconds) / voice.Pitch, Is.InRange(.15f, .6f));
                Assert.That(settings.VoiceVolume * voice.Volume, Is.InRange(0f, 1f),
                    "Per-clip source gains differ because the owned voice recording is much quieter than the pop");
            }
            Assert.That(settings.VoiceDelay, Is.InRange(.05f, .18f));
            Assert.That(settings.MaxConcurrentVoices, Is.EqualTo(2));
        }
    }

    [Test]
    public void FirstBirthPopMatchesRevealAndOneVoiceFollowsAfterTheConfiguredDelay()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1);
            sound.StartFloor3();
            double start = sound.Clock;
            float reveal = host.Habitat.Settings.TrailSeconds + host.Habitat.Settings.GatherSeconds;
            sound.Advance(reveal - .04f);
            Assert.That(sound.Plays.Count, Is.Zero, "Travel/gather does not fire the pop early");
            sound.Advance(.06f);
            var pop = sound.Pops.Single();
            Assert.That(pop.Time - start, Is.EqualTo(reveal).Within(.025d));
            Assert.That(pop.Volume, Is.EqualTo(host.Habitat.Settings.BirthPopVolume).Within(.0001f));
            Assert.That(host.Habitat.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Arrival puff" && r.enabled), Is.True);
            Assert.That(sound.Voices.Length, Is.Zero);
            sound.Advance(host.Habitat.Settings.VoiceDelay + .04f);
            var voice = sound.Voices.Single();
            Assert.That(voice.Time - pop.Time, Is.EqualTo(host.Habitat.Settings.VoiceDelay).Within(.025d));
            var selectedVariant = host.Habitat.Settings.BirthVoiceVariants.Single(v => v.Clip == voice.Clip && v.Pitch == voice.Pitch);
            Assert.That(voice.Volume, Is.EqualTo(host.Habitat.Settings.VoiceVolume * selectedVariant.Volume).Within(.0001f));
            Assert.That(host.Habitat.Settings.BirthVoiceVariants.Any(v => v.Clip == voice.Clip && v.Pitch == voice.Pitch), Is.True);
            sound.Advance(host.Habitat.Settings.BirthDuration);
            Assert.That(sound.Pops.Length, Is.EqualTo(1));
            Assert.That(sound.Voices.Length, Is.EqualTo(1));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void MutedBirthDispatchesNothingAndUnmutingDoesNotReplayItsClaimedSounds()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            EnhancementFairyStage1Host.Set(sound.Sound, "_soundEffectVolume", 0f);
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(1.7f);
            Assert.That(sound.Plays.Count, Is.Zero);
            EnhancementFairyStage1Host.Set(sound.Sound, "_soundEffectVolume", 1f);
            sound.Advance(3f);
            Assert.That(sound.Plays.Count, Is.Zero, "Unmute does not replay an earlier visual cue");
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void OwnershipRestoreDuplicateAndConsumedSceneReentryRemainSilent()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            var item = sound.Items[0];
            host.Restore(new[] { new KeyValuePair<string, int>(item.Id, 3) });
            sound.StartFloor3(); sound.Advance(4f);
            Assert.That(sound.Plays.Count, Is.Zero, "Loading ownership never creates a birth sound");
            EnhancementFairyAcquisitionEvents.PublishConfirmed("first-confirmed", item.Id);
            sound.Advance(4f);
            Assert.That(sound.Pops.Length, Is.EqualTo(1));
            Assert.That(sound.Voices.Length, Is.EqualTo(1));
            Assert.That(EnhancementFairyAcquisitionEvents.PublishConfirmed("duplicate", item.Id), Is.False);
            host.ReloadStage(); sound.BindHabitat(); sound.StartFloor3(); sound.Advance(4f);
            Assert.That(sound.Pops.Length, Is.EqualTo(1));
            Assert.That(sound.Voices.Length, Is.EqualTo(1));
        }
    }

    [TestCase("floor")]
    [TestCase("scene")]
    [TestCase("ui")]
    public void InterruptedBirthStopsItsVoicesAndDoesNotReplayAlreadyPresentedSounds(string reason)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(1.42f);
            Assert.That(sound.Pops.Length, Is.EqualTo(1));
            Assert.That(sound.Voices.Length, Is.EqualTo(1));
            if (reason == "floor") host.Move(ERestaurantFloorType.Floor2);
            if (reason == "scene") { host.ReloadStage(); sound.BindHabitat(); }
            if (reason == "ui") host.VerifyActualGachaOpenClose(() =>
            {
                sound.Advance(.2f);
                Assert.That(sound.Stops.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.EqualTo(1));
            });
            Assert.That(sound.Stops.Count, Is.GreaterThanOrEqualTo(2));
            sound.StartFloor3(); sound.Advance(4f);
            Assert.That(sound.Pops.Length, Is.EqualTo(1));
            Assert.That(sound.Voices.Length, Is.EqualTo(1));
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void InterruptingBetweenPopAndVoiceDoesNotReplayEitherCueOnSceneReentry()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3();
            sound.Advance(host.Habitat.Settings.TrailSeconds + host.Habitat.Settings.GatherSeconds + .025f);
            Assert.That(sound.Pops.Length, Is.EqualTo(1));
            Assert.That(sound.Voices.Length, Is.Zero);
            host.ReloadStage(); sound.BindHabitat(); sound.StartFloor3(); sound.Advance(4f);
            Assert.That(sound.Pops.Length, Is.EqualTo(1));
            Assert.That(sound.Voices.Length, Is.Zero, "The interrupted cue is not replayed in a recreated scene");
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
        }
    }

    [Test]
    public void AccountResetStopsActiveBirthSoundsAndClearsBothClaimsForTheNewAccount()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(1); sound.StartFloor3(); sound.Advance(1.42f);
            Assert.That(sound.Plays.Count, Is.EqualTo(2));
            EnhancementFairyAcquisitionEvents.ResetSession("different-offline-account");
            Assert.That(sound.Stops.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(host.Habitat.ActiveBirthCount, Is.Zero);
            sound.Confirm(1); sound.Advance(4f);
            Assert.That(sound.Pops.Length, Is.EqualTo(2));
            Assert.That(sound.Voices.Length, Is.EqualTo(2));
        }
    }

    [Test]
    public void ElevenBirthsLimitConcurrentAudioAndAvoidConsecutiveAcceptedVoiceVariants()
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            sound.Confirm(11); sound.StartFloor3(); sound.Advance(12f);
            Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            Assert.That(sound.Pops.Length, Is.InRange(1, 11));
            Assert.That(sound.Voices.Length, Is.InRange(2, 11));
            var voices = sound.Voices;
            for (int i = 1; i < voices.Length; i++)
                Assert.That(voices[i].Clip != voices[i - 1].Clip || voices[i].Pitch != voices[i - 1].Pitch, Is.True);
            foreach (var current in sound.Plays)
            {
                int concurrent = sound.Plays.Count(previous => ReferenceEquals(previous.Owner, current.Owner)
                    && previous.Time <= current.Time && previous.Time + (previous.Clip.length - previous.StartSeconds) / previous.Pitch > current.Time + .00001d);
                Assert.That(concurrent, Is.LessThanOrEqualTo(2), "Shared owner limits prevent a loud pileup");
            }
            Assert.That(sound.Sound.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(10));
        }
    }

    [Test]
    public void SoundChoiceDoesNotConsumeUnityRandomOrAlterSeededBirthPositions()
    {
        var original = UnityEngine.Random.state;
        try
        {
            var withSound = CapturePositions(false);
            var muted = CapturePositions(true);
            CollectionAssert.AreEqual(withSound.Keys, muted.Keys);
            foreach (string id in withSound.Keys) Assert.That(muted[id], Is.EqualTo(withSound[id]));
        }
        finally { UnityEngine.Random.state = original; }
    }

    private static Dictionary<string, Vector2> CapturePositions(bool muted)
    {
        using (var host = new EnhancementFairyStage1Host())
        using (var sound = new FairyBirthSoundScope(host))
        {
            EnhancementFairyStage1Host.Set(sound.Sound, "_soundEffectVolume", muted ? 0f : 1f);
            UnityEngine.Random.InitState(531);
            float expected = UnityEngine.Random.value;
            UnityEngine.Random.InitState(531);
            sound.Confirm(5); sound.StartFloor3();
            var positions = new Dictionary<string, Vector2>();
            for (int i = 0; i < 300; i++)
            {
                sound.Advance(1f / 60f);
                foreach (string id in host.Habitat.ActiveBirthItemIds)
                    if (!positions.ContainsKey(id)) positions[id] = host.Habitat.GetBirthPosition(id);
            }
            Assert.That(positions.Count, Is.EqualTo(5));
            Assert.That(UnityEngine.Random.value, Is.EqualTo(expected));
            return positions;
        }
    }
}

/// <summary>Uses the host's existing manager and mixer channel pool without Awake, preferences or native audio.</summary>
internal sealed class FairyBirthSoundScope : IDisposable
{
    internal readonly EnhancementFairyStage1Host Host;
    internal readonly SoundManager Sound;
    internal readonly GachaItemData[] Items;
    internal readonly List<SoundManager.EffectPlaybackDiagnostic> Plays = new List<SoundManager.EffectPlaybackDiagnostic>();
    internal readonly List<AudioSource> Stops = new List<AudioSource>();
    internal double Clock;
    private readonly object _oldPool;
    private readonly List<GameObject> _sources = new List<GameObject>();
    internal SoundManager.EffectPlaybackDiagnostic[] Pops => Plays.Where(p => p.Clip == Host.Habitat.Settings.BirthPopClip).ToArray();
    internal SoundManager.EffectPlaybackDiagnostic[] Voices => Plays.Where(p => p.Clip != Host.Habitat.Settings.BirthPopClip).ToArray();

    internal FairyBirthSoundScope(EnhancementFairyStage1Host host)
    {
        Host = host;
        Sound = SoundManager.TryGetExistingInstance();
        Assert.That(Sound, Is.Not.Null);
        _oldPool = EnhancementFairyStage1Host.Get(Sound, "_effectAudioDic");
        var group = Resources.Load<AudioMixer>("Audio/AudioMixer").FindMatchingGroups("SoundEffect")[0];
        var sources = new List<AudioSource>();
        for (int i = 0; i < 10; i++)
        {
            var go = new GameObject("Isolated existing SFX pool " + i);
            go.transform.SetParent(Sound.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.outputAudioMixerGroup = group;
            sources.Add(source); _sources.Add(go);
        }
        EnhancementFairyStage1Host.Set(Sound, "_effectAudioDic", new Dictionary<EffectType, List<AudioSource>> { [EffectType.None] = sources });
        Sound.SuppressNativePlaybackForTests = true;
        Sound.EffectClockForTests = () => Clock;
        Sound.EffectPlaybackRequested += OnPlay;
        Sound.EffectPlaybackStopped += OnStop;
        Items = Host.Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(11).ToArray();
        Assert.That(Items.Length, Is.EqualTo(11));
        BindHabitat();
    }
    internal void BindHabitat()
    {
        EnhancementFairyStage1Host.Set(Host.Habitat, "_birthRandom", new System.Random(84));
        EnhancementFairyStage1Host.Set(Host.Habitat, "_birthSoundRandom", new System.Random(193));
    }
    private void OnPlay(SoundManager.EffectPlaybackDiagnostic info) => Plays.Add(info);
    private void OnStop(AudioSource source, double time) => Stops.Add(source);
    internal void Confirm(int count)
    {
        Host.Restore(Items.Take(count).Select(item => new KeyValuePair<string, int>(item.Id, 1)));
        foreach (var item in Items.Take(count)) EnhancementFairyAcquisitionEvents.PublishConfirmed("offline-saved-" + item.Id, item.Id);
    }
    internal void StartFloor3() { Host.Move(ERestaurantFloorType.Floor3); Advance(1f / 60f); }
    internal void Advance(float seconds)
    {
        for (int i = 0; i < Mathf.CeilToInt(seconds * 60f); i++)
        { Clock += 1d / 60d; Host.Habitat.AdvancePreview(1f / 60f); }
    }
    public void Dispose()
    {
        EnhancementFairyStage1Host.Invoke(Host.Habitat, "OnDisable");
        Sound.EffectPlaybackRequested -= OnPlay;
        Sound.EffectPlaybackStopped -= OnStop;
        foreach (var go in _sources) Object.DestroyImmediate(go);
        EnhancementFairyStage1Host.Set(Sound, "_effectAudioDic", _oldPool);
        Sound.EffectClockForTests = null;
        Sound.SuppressNativePlaybackForTests = false;
    }
}
#endif
