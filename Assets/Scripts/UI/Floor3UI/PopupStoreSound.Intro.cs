using System;
using UnityEngine;

public sealed partial class PopupStoreSound
{
    [Header("Existing recipe minigame background")]
    [SerializeField] private AudioClip _recipeBackground;
    [SerializeField, Min(0)] private float _recipeTransitionSeconds = .5f;

    private SoundManager _recipeManager;
    private AudioSource _recipeSource;
    private AudioClip _recipePreviousClip;
    private bool _recipePreviousLoop, _recipeRequested, _recipeStarted, _recipeClipObserved, _recipeRetired;

#if UNITY_EDITOR
    internal Action<AudioClip, float, bool> PlayBackgroundForTests;
    internal Func<bool> RestoreMainMusicForTests;
#endif

    /// <summary>Uses the recipe game's existing music from tutorial until the popup closes.</summary>
    public static void BeginRecipeBackground(Component source)
    {
        try
        {
            if (source == null || !source.gameObject.activeInHierarchy) return;
            var popup = source.GetComponentInParent<UIPopupStore>(true);
            var sound = popup != null ? popup.GetComponent<PopupStoreSound>() : null;
            if (sound == null || sound._recipeRetired || !sound.CanBeginRecipeBackground()) return;
            sound._recipeRequested = true;
            sound.TickRecipeBackground();
        }
        catch (Exception) { /* Optional audio must never block the tutorial. */ }
    }

    private bool CanBeginRecipeBackground()
    {
        if (!isActiveAndEnabled) return false;
#if UNITY_EDITOR
        return Application.isPlaying || AllowEditModeForTests;
#else
        return Application.isPlaying;
#endif
    }

    internal void TickRecipeBackground()
    {
        if (!_recipeRequested || !CanBeginRecipeBackground()) return;
        try
        {
            if (_recipeStarted)
            {
                if (!OwnsRecipeBackground()) ClearRecipeOwnership(true);
                return;
            }
            if (_recipeBackground == null || !Finite(_recipeTransitionSeconds) || _recipeTransitionSeconds < 0) return;
            var manager = SoundManager.TryGetExistingInstance();
            var source = ExistingBackgroundSource(manager);
            if (manager == null || source == null) return;
            _recipeManager = manager;
            _recipeSource = source;
            _recipePreviousClip = source.clip;
            _recipePreviousLoop = source.loop;
            _recipeClipObserved = source.clip == _recipeBackground;
            _recipeStarted = true;
            // Reuse the manager's normal transition/mixer policy. Never alter mute,
            // volume, playback position or preferences, and never create an audio source.
            PlayRecipeBackground(_recipeBackground, _recipeTransitionSeconds, true);
        }
        catch (Exception) { ClearRecipeOwnership(true); }
    }

    internal void RestoreRecipeBackground()
    {
        try
        {
            if (!_recipeStarted || !OwnsRecipeBackground()) return;
            bool restored = false;
#if UNITY_EDITOR
            if (RestoreMainMusicForTests != null) restored = RestoreMainMusicForTests();
            else
#endif
            {
                var scene = UnityEngine.Object.FindObjectOfType<MainScene>();
                if (scene != null)
                {
                    try { scene.PlayMainMusic(); restored = true; }
                    catch (Exception) { /* An incomplete offline fixture may have no scene music bindings. */ }
                }
            }
            if (!restored && _recipePreviousClip != null)
                PlayRecipeBackground(_recipePreviousClip, _recipeTransitionSeconds, _recipePreviousLoop);
        }
        catch (Exception) { /* Teardown must remain safe if the manager disappears. */ }
        finally { ClearRecipeOwnership(); }
    }

    private bool OwnsRecipeBackground()
    {
        var manager = SoundManager.TryGetExistingInstance();
        if (manager == null || manager != _recipeManager || _recipeSource == null
            || ExistingBackgroundSource(manager) != _recipeSource) return false;
        if (_recipeSource.clip == _recipeBackground)
        { _recipeClipObserved = true; return true; }
        // The manager's fade keeps the previous clip until its midpoint. Once the
        // recipe track was observed, any other clip belongs to an external request.
        return !_recipeClipObserved && _recipeSource.clip == _recipePreviousClip;
    }

    private static AudioSource ExistingBackgroundSource(SoundManager manager)
    {
        var child = manager != null ? manager.transform.Find("BackgroundAudio") : null;
        return child != null ? child.GetComponent<AudioSource>() : null;
    }

    private void PlayRecipeBackground(AudioClip clip, float duration, bool loop)
    {
#if UNITY_EDITOR
        if (PlayBackgroundForTests != null)
        { PlayBackgroundForTests(clip, duration, loop); return; }
#endif
        if (_recipeManager != null) _recipeManager.PlayBackgroundAudio(clip, duration, loop);
    }

    private void ClearRecipeOwnership(bool retired = false)
    {
        _recipeManager = null;
        _recipeSource = null;
        _recipePreviousClip = null;
        _recipeRequested = _recipeStarted = _recipeClipObserved = false;
        _recipeRetired = retired;
    }
}
