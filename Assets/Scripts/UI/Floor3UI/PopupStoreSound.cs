using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public enum PopupStoreSoundCue
{
    MaterialAdded, CounterCleared, PackingAccepted, PackingRejected, PackingBoom,
    BoxAppeared, DeliveryStarted, CustomerSatisfied, CustomerExpired, OrderShown,
    ScoreAdded, StageNotice, Countdown, GameStart, StageSuccess, GameOver,
    TimeWarning, ResultShown, RewardAppeared, RewardOpen, DiamondsShown, PackingButton
}

/// <summary>Optional popup-local presentation adapter. No gameplay, economy or mixer ownership.</summary>
[DisallowMultipleComponent]
public sealed partial class PopupStoreSound : MonoBehaviour
{
    [Serializable]
    public sealed class CueSetting
    {
        public PopupStoreSoundCue Cue;
        public AudioClip Clip;
        [Range(0, 1)] public float Volume = .6f;
        [Min(0)] public float MinimumInterval = .08f;
        [Min(0)] public float MaximumDuration;
    }

    [Tooltip("Existing project clips only. Missing entries are deliberately silent.")]
    [SerializeField] private CueSetting[] _cues = Array.Empty<CueSetting>();
    [SerializeField, Range(1, 8)] private int _maximumConcurrent = 4;
    private readonly HashSet<EventKey> _observed = new HashSet<EventKey>();
    private readonly Dictionary<PopupStoreSoundCue, double> _lastPlayed = new Dictionary<PopupStoreSoundCue, double>();
    private SoundManager _manager;
    private PopupStoreBusinessClock _clock;
    private readonly object _packingButtonVoiceOwner = new object();
    private SoundManager _packingButtonManager;
    private double _packingButtonStopAt;

#if UNITY_EDITOR
    // SDK-free fixtures use the real SoundManager pool with native playback suppressed.
    internal bool AllowEditModeForTests;
    internal Func<double> ClockForTests;
#endif

    public static void Play(Component source, PopupStoreSoundCue cue, object identity = null, string marker = null)
    {
        // Optional audio must never unwind a committed transaction or a UI callback.
        try
        {
            if (source == null || !source.gameObject.activeInHierarchy) return;
            var popup = source.GetComponentInParent<UIPopupStore>(true);
            if (popup != null) popup.GetComponent<PopupStoreSound>()?.TryPlay(cue, identity, marker);
        }
        catch (Exception) { /* Audio is best effort; gameplay has already decided the event. */ }
    }

    public static void BindButton(Button button, SoundEffectType sound = SoundEffectType.ButtonClickSound)
    {
        if (button == null) return;
        try
        {
            // The existing input adapter suppresses ButtonGetter's automatic onClick binding.
            // Material cards never use this: they sound only after a successful counter addition.
            GachaButtonInputSound.Bind(button, sound);
        }
        catch (Exception) { /* Missing optional feedback cannot disable a button. */ }
    }

    internal bool TryPlay(PopupStoreSoundCue cue, object identity = null, string marker = null)
    {
        try
        {
            if (!isActiveAndEnabled || !CanPresent()) return false;
            if (identity != null && !_observed.Add(new EventKey(cue, identity, marker))) return false;
            if (cue == PopupStoreSoundCue.StageNotice || cue == PopupStoreSoundCue.StageSuccess
                || cue == PopupStoreSoundCue.GameOver) StopOwnedSounds();
            CueSetting setting = null;
            foreach (var candidate in _cues)
                if (candidate != null && candidate.Cue == cue) { setting = candidate; break; }
            if (setting == null || setting.Clip == null || !Finite(setting.Volume) || setting.Volume <= 0
                || !Finite(setting.MinimumInterval) || setting.MinimumInterval < 0) return false;
            double now = GetAudioTime();
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            if (_lastPlayed.TryGetValue(cue, out double previous) && now - previous < setting.MinimumInterval) return false;
            var manager = SoundManager.TryGetExistingInstance();
            if (manager == null || manager.IsEffectAudioMuted) return false;
            // No delayed coroutine, new AudioSource, global volume or BGM change.
            bool packingButton = cue == PopupStoreSoundCue.PackingButton;
            if (packingButton) StopPackingButtonSound();
            var voice = manager.PlayPresentationEffect(EffectType.None, setting.Clip,
                Mathf.Clamp01(setting.Volume), 1f, packingButton ? _packingButtonVoiceOwner : this,
                packingButton ? 1 : Mathf.Clamp(_maximumConcurrent, 1, 8));
            if (voice == null) return false;
            if (packingButton)
            {
                _packingButtonManager = manager;
                float duration = Finite(setting.MaximumDuration) && setting.MaximumDuration > 0
                    ? Mathf.Min(setting.MaximumDuration, setting.Clip.length) : setting.Clip.length;
                _packingButtonStopAt = now + duration;
            }
            _manager = manager;
            _lastPlayed[cue] = now;
            return true;
        }
        catch (Exception) { return false; }
    }

    private bool CanPresent()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return AllowEditModeForTests;
#endif
        var popup = GetComponent<UIPopupStore>();
        var clock = GetComponent<PopupStoreBusinessClock>();
        return Application.isPlaying && popup != null && popup.VisibleState == VisibleState.Appeared
            && clock != null && clock.CanAdvanceVisuals;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        TickRecipeBackground();
        TickPackingButtonSound();
        if (!CanPresent()) { StopOwnedSounds(); return; }
        var clock = GetComponent<PopupStoreBusinessClock>();
        if (clock.State != PopupStoreBusinessState.Running || clock.RemainingSeconds <= 0
            || clock.RemainingSeconds > 5) return;
        var preview = GetComponent<PopupStorePhasePreview>();
        if (preview != null && preview.IsInitialized)
        {
            if (preview.CanPlay)
                TryPlay(PopupStoreSoundCue.TimeWarning, preview.Session,
                    preview.Session.CurrentPhase.Grade.ToString());
        }
        else TryPlay(PopupStoreSoundCue.TimeWarning, clock);
    }

    private void StopOwnedSounds()
    {
        try { if (_manager != null) _manager.StopPresentationEffects(this); }
        catch (Exception) { }
        _manager = null;
        StopPackingButtonSound();
    }
    private double GetAudioTime()
    {
#if UNITY_EDITOR
        if (ClockForTests != null) return ClockForTests();
#endif
        return Time.unscaledTimeAsDouble;
    }
    private void TickPackingButtonSound()
    {
        if (_packingButtonManager != null && GetAudioTime() >= _packingButtonStopAt) StopPackingButtonSound();
    }
    private void StopPackingButtonSound()
    {
        try { if (_packingButtonManager != null) _packingButtonManager.StopPresentationEffects(_packingButtonVoiceOwner); }
        catch (Exception) { }
        _packingButtonManager = null; _packingButtonStopAt = 0;
    }
    private void OnEnable()
    {
        _clock = GetComponent<PopupStoreBusinessClock>();
        if (_clock != null)
        {
            _clock.StateChanged += OnClockChanged;
            _clock.ContextChanged += OnClockChanged;
        }
    }
    private void OnClockChanged()
    {
        if (!CanPresent()) { StopOwnedSounds(); return; }
        if (GetComponent<PopupStorePhasePreview>()?.IsInitialized == true || _clock == null) return;
        if (_clock.State == PopupStoreBusinessState.Running) TryPlay(PopupStoreSoundCue.GameStart, _clock);
        else if (_clock.State == PopupStoreBusinessState.Ended) TryPlay(PopupStoreSoundCue.GameOver, _clock);
    }
    private void OnDisable()
    {
        if (_clock != null)
        {
            _clock.StateChanged -= OnClockChanged;
            _clock.ContextChanged -= OnClockChanged;
        }
        _clock = null;
        StopOwnedSounds();
        RestoreRecipeBackground();
    }
    private void OnDestroy() { StopOwnedSounds(); RestoreRecipeBackground(); }
    private void OnApplicationFocus(bool focused) { if (!focused) StopOwnedSounds(); }
    private void OnApplicationPause(bool paused) { if (paused) StopOwnedSounds(); }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private readonly struct EventKey : IEquatable<EventKey>
    {
        private readonly PopupStoreSoundCue _cue;
        private readonly object _identity;
        private readonly string _marker;
        internal EventKey(PopupStoreSoundCue cue, object identity, string marker)
        { _cue = cue; _identity = identity; _marker = marker; }
        public bool Equals(EventKey other) => _cue == other._cue && ReferenceEquals(_identity, other._identity)
            && string.Equals(_marker, other._marker, StringComparison.Ordinal);
        public override bool Equals(object value) => value is EventKey other && Equals(other);
        public override int GetHashCode() => ((int)_cue * 397 ^ RuntimeHelpers.GetHashCode(_identity)) * 397
            ^ (_marker == null ? 0 : StringComparer.Ordinal.GetHashCode(_marker));
    }
}
