#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

public sealed class PresentationSoundTests
{
    [Test]
    public void Presentation_ReusesExistingPoolAndMixerWithIndependentPitchAndVolume()
    {
        using (var f = new Fixture())
        {
            Set(f.Sound, "_effectVolume", .8f);
            var owner = new object();
            var source = f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, .4f, 1.4f, owner, 2);
            Assert.That(source, Is.SameAs(f.Sources[0]));
            Assert.That(source.outputAudioMixerGroup, Is.SameAs(f.Group));
            Assert.That(source.volume, Is.EqualTo(.32f).Within(.0001f));
            Assert.That(source.pitch, Is.EqualTo(1.4f));
            Assert.That(source.loop, Is.False);
            Assert.That(f.Plays.Count, Is.EqualTo(1));
            Assert.That(f.Plays[0].Clip, Is.SameAs(f.Clip));
            Assert.That(f.Plays[0].Owner, Is.SameAs(owner));
            Assert.That(f.Plays[0].Type, Is.EqualTo(EffectType.None));
            Assert.That(f.Host.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(3));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Muted_MasterOrSfxRejectsPresentationAndLegacyBeforeDispatch(bool master)
    {
        using (var f = new Fixture())
        {
            Set(f.Sound, master ? "_masterVolume" : "_soundEffectVolume", 0f);
            Assert.That(f.Sound.IsEffectAudioMuted, Is.True);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip), Is.Null);
            f.Sound.PlayEffectAudio(EffectType.None, f.Clip);
            f.Sound.PlayEffectAudio(EffectType.None, SoundEffectType.ButtonClickSound);
            f.Sound.PlayEffectAudio(EffectType.None, f.Clip, .5f);
            Assert.That(f.Plays.Count, Is.Zero);
            Assert.That(f.Sources[0].clip, Is.Null, "Mute never dispatches to a pooled source");
        }
    }

    [Test]
    public void SettingsVolumeRemainsInMixerAndDiagnosticReflectsTheEffectiveGain()
    {
        using (var f = new Fixture())
        {
            Set(f.Sound, "_masterVolume", .5f);
            Set(f.Sound, "_soundEffectVolume", .4f);
            f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, .35f);
            Assert.That(f.Sources[0].volume, Is.EqualTo(.35f), "Do not apply user settings twice before the existing mixer");
            Assert.That(f.Plays[0].EffectiveVolume, Is.EqualTo(.07f).Within(.0001f));
        }
    }

    [Test]
    public void OwnerLimitDropsExcessAndStopOnlyCancelsThatOwnersPresentation()
    {
        using (var f = new Fixture())
        {
            var voiceOwner = new object();
            var popOwner = new object();
            var first = f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: voiceOwner, maxConcurrent: 1);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: voiceOwner, maxConcurrent: 1), Is.Null);
            var other = f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: popOwner, maxConcurrent: 1);
            f.Sound.PlayEffectAudio(EffectType.None, SoundEffectType.ButtonClickSound);
            Assert.That(f.Plays.Count, Is.EqualTo(3));
            f.Sound.StopPresentationEffects(voiceOwner);
            Assert.That(f.Stops, Is.EqualTo(new[] { first }));
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: popOwner, maxConcurrent: 1), Is.Null);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: voiceOwner, maxConcurrent: 1), Is.SameAs(first));
            Assert.That(other, Is.Not.SameAs(first));
        }
    }

    [Test]
    public void PresentationPoolIsBoundedAndNeverStealsBusyLegacyOrPresentationVoices()
    {
        using (var f = new Fixture())
        {
            for (int i = 0; i < 3; i++)
                Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: new object()), Is.SameAs(f.Sources[i]));
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip), Is.Null);
            Assert.That(f.Plays.Count, Is.EqualTo(3));
            Assert.That(f.Stops.Count, Is.Zero);
            Assert.That(f.Host.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(3));
            f.Clock = .51d;
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip), Is.SameAs(f.Sources[0]));
            Assert.That(f.Plays.Count, Is.EqualTo(4));
        }
    }

    [Test]
    public void PitchAdjustedLifetimeReleasesTheOwnerLimitWithoutAllocatingMoreSources()
    {
        using (var f = new Fixture())
        {
            var owner = new object();
            f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, pitch: 2f, owner: owner, maxConcurrent: 1);
            f.Clock = .24d;
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: owner, maxConcurrent: 1), Is.Null);
            f.Clock = .26d;
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: owner, maxConcurrent: 1), Is.SameAs(f.Sources[0]));
            Assert.That(f.Plays.Count, Is.EqualTo(2));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LegacyPlayResetsPitchVolumeAndLoopAfterPresentationReuse(bool enumClip)
    {
        using (var f = new Fixture())
        {
            var owner = new object();
            var source = f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, .3f, 1.7f, owner, startSeconds: .1f);
            f.Sound.StopPresentationEffects(owner);
            source.loop = true;
            if (enumClip) f.Sound.PlayEffectAudio(EffectType.None, SoundEffectType.ButtonClickSound);
            else f.Sound.PlayEffectAudio(EffectType.None, f.Clip);
            Assert.That(f.Plays.Count, Is.EqualTo(2));
            Assert.That(f.Plays[1].Source, Is.SameAs(source));
            Assert.That(source.pitch, Is.EqualTo(1f));
            Assert.That(source.volume, Is.EqualTo(1f));
            Assert.That(source.loop, Is.False);
            Assert.That(source.time, Is.Zero.Within(.001f));
            Assert.That(f.Plays[1].Owner, Is.Null);
        }
    }

    [Test]
    public void OptionalClipOffsetSkipsLeadingSilenceAndUsesTheShortenedPitchedLifetime()
    {
        using (var f = new Fixture())
        {
            var owner = new object();
            var source = f.Sound.PlayPresentationEffect(EffectType.None, f.Clip,
                pitch: 1.5f, owner: owner, maxConcurrent: 1, startSeconds: .2f);
            Assert.That(source.time, Is.EqualTo(.2f).Within(.001f));
            Assert.That(f.Plays[0].StartSeconds, Is.EqualTo(.2f));
            f.Clock = .19d;
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: owner, maxConcurrent: 1), Is.Null);
            f.Clock = .21d;
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, owner: owner, maxConcurrent: 1), Is.SameAs(source));
            Assert.That(source.time, Is.Zero.Within(.001f));
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, startSeconds: float.NaN), Is.Null);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, startSeconds: f.Clip.length), Is.Null);
        }
    }

    [Test]
    public void GlobalFadeStartsAtEachPooledSourcesGainAndNeverAmplifiesQuietPresentation()
    {
        using (var f = new Fixture())
        {
            var quiet = f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, .35f);
            f.Sound.PlayEffectAudio(EffectType.None, SoundEffectType.ButtonClickSound);
            var full = f.Plays[1].Source;
            var fade = (IEnumerator)Invoke(f.Sound, "IEStopEffectAudio", .1f);
            Assert.That(fade.MoveNext(), Is.True);
            Assert.That(quiet.volume, Is.EqualTo(.28f).Within(.0001f));
            Assert.That(full.volume, Is.EqualTo(.8f).Within(.0001f));
            float previous = quiet.volume;
            while (fade.MoveNext())
            {
                Assert.That(quiet.volume, Is.LessThanOrEqualTo(previous));
                previous = quiet.volume;
            }
            Assert.That(f.Stops.Count, Is.EqualTo(2));
        }
    }

    [Test]
    public void DeferredLegacySoundRechecksMuteAndResetsSourceFieldsAtDispatch()
    {
        using (var f = new Fixture())
        {
            var deferred = (IEnumerator)Invoke(f.Sound, "IEDelayPlayEffectAudio", EffectType.None, f.Clip, .1f);
            Assert.That(deferred.MoveNext(), Is.True);
            Set(f.Sound, "_soundEffectVolume", 0f);
            Assert.That(deferred.MoveNext(), Is.False);
            Assert.That(f.Plays.Count, Is.Zero);
            Set(f.Sound, "_soundEffectVolume", 1f);
            f.Sources[0].pitch = 1.8f; f.Sources[0].volume = .2f; f.Sources[0].loop = true;
            deferred = (IEnumerator)Invoke(f.Sound, "IEDelayPlayEffectAudio", EffectType.None, f.Clip, .1f);
            Assert.That(deferred.MoveNext(), Is.True);
            Assert.That(deferred.MoveNext(), Is.False);
            Assert.That(f.Plays.Count, Is.EqualTo(1));
            Assert.That(f.Sources[0].pitch, Is.EqualTo(1f));
            Assert.That(f.Sources[0].volume, Is.EqualTo(1f));
            Assert.That(f.Sources[0].loop, Is.False);
        }
    }

    [Test]
    public void ExistingManagerAndClipAccessorsDoNotCreateOrLoadAnUninitializedManager()
    {
        var field = typeof(SoundManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            Assert.That(SoundManager.TryGetExistingInstance(), Is.Null);
            using (var f = new Fixture())
            {
                Assert.That(f.Sound.GetEffectClip(SoundEffectType.ButtonClickSound), Is.SameAs(f.Clip));
                Assert.That(f.Sound.GetEffectClip((SoundEffectType)999), Is.Null);
                Set(f.Sound, "_clips", null);
                Assert.That(f.Sound.GetEffectClip(SoundEffectType.ButtonClickSound), Is.Null);
                Assert.That(f.Sound.PlayPresentationEffect(EffectType.Hall1, f.Clip), Is.Null);
                Assert.That(f.Plays.Count, Is.Zero);
            }
        }
        finally { field.SetValue(null, previous); }
    }

    [Test]
    public void ChannelGateAndInvalidValuesDoNotDispatchOrConsumeAnOwnerSlot()
    {
        using (var f = new Fixture())
        {
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.UI, f.Clip), Is.Null);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, null), Is.Null);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, float.NaN), Is.Null);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, 0f), Is.Null);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, pitch: float.PositiveInfinity), Is.Null);
            Assert.That(f.Plays.Count, Is.Zero);
            Assert.That(f.Sound.PlayPresentationEffect(EffectType.None, f.Clip), Is.SameAs(f.Sources[0]));
        }
    }

    [Test]
    public void SoundDispatchNeverChangesUnityGameplayRandomState()
    {
        var original = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(761);
            float expected = UnityEngine.Random.value;
            UnityEngine.Random.InitState(761);
            using (var f = new Fixture())
            {
                f.Sound.PlayPresentationEffect(EffectType.None, f.Clip, .4f, 1.5f);
                Assert.That(UnityEngine.Random.value, Is.EqualTo(expected));
            }
        }
        finally { UnityEngine.Random.state = original; }
    }

    private static void Set(object owner, string name, object value) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static object Invoke(object owner, string name, params object[] args) => owner.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);

    private sealed class Fixture : IDisposable
    {
        internal readonly GameObject Host;
        internal readonly SoundManager Sound;
        internal readonly AudioClip Clip;
        internal readonly AudioMixerGroup Group;
        internal readonly AudioSource[] Sources = new AudioSource[3];
        internal readonly List<SoundManager.EffectPlaybackDiagnostic> Plays = new List<SoundManager.EffectPlaybackDiagnostic>();
        internal readonly List<AudioSource> Stops = new List<AudioSource>();
        internal double Clock;
        private readonly object _previousManager;

        internal Fixture()
        {
            var singleton = typeof(SoundManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            _previousManager = singleton.GetValue(null);
            Host = new GameObject("Isolated presentation sound pool");
            Host.SetActive(false); // Never run Awake/Init, DataBind, PlayerPrefs, coroutines or native audio.
            Sound = Host.AddComponent<SoundManager>();
            Sound.EffectClockForTests = () => Clock;
            Sound.SuppressNativePlaybackForTests = true;
            Sound.EffectPlaybackRequested += info => Plays.Add(info);
            Sound.EffectPlaybackStopped += (source, _) => Stops.Add(source);
            Clip = AudioClip.Create("Owned offline sound fixture", 22050, 1, 44100, false);
            Group = Resources.Load<AudioMixer>("Audio/AudioMixer").FindMatchingGroups("SoundEffect")[0];
            var pool = new List<AudioSource>();
            for (int i = 0; i < Sources.Length; i++)
            {
                var go = new GameObject("Existing pooled source " + i);
                go.transform.SetParent(Host.transform, false);
                Sources[i] = go.AddComponent<AudioSource>();
                Sources[i].playOnAwake = false;
                Sources[i].outputAudioMixerGroup = Group;
                pool.Add(Sources[i]);
            }
            Set(Sound, "_effectAudioDic", new Dictionary<EffectType, List<AudioSource>> { [EffectType.None] = pool });
            Set(Sound, "_clips", new[] { Clip, Clip, Clip, Clip, Clip });
        }

        public void Dispose()
        {
            Object.DestroyImmediate(Host);
            Object.DestroyImmediate(Clip);
            typeof(SoundManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousManager);
        }
    }
}
#endif
