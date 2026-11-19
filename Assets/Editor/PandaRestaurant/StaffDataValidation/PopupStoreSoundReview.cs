#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in observation of an existing original memory Play. Never starts Play, changes audio or calls a server.</summary>
[InitializeOnLoad]
public static class PopupStoreSoundReview
{
    private const string Folder = "Temp/PopupStoreSoundReview";
    private const string ArmedKey = "Panda.PopupStore.SoundReview.ArmedUtc";
    private const string OriginalAssets = "C:/Users/MOONAIN/Downloads/CookingProject01/Panda_Restaurant/Assets";
    private const int MaximumEntries = 1000;
    private static SoundManager _manager;
    private static Report _report;
    private static string _lastBlockedReason;
    private static AudioSource _background;
    private static bool? _backgroundMute;
    private static AudioClip _backgroundClip;
    private static readonly Dictionary<int, string> DirectAudioStates = new Dictionary<int, string>();

    [Serializable]
    public sealed class Entry
    {
        public string clip, effectType, ownerType, utc;
        public float volume, effectiveVolume, clipSeconds;
        public double dspTime;
        public int frame;
    }

    [Serializable]
    public sealed class BackgroundObservation
    {
        public string utc, clip;
        public bool muted;
        public double dspTime;
    }

    [Serializable]
    public sealed class StoppedEffect
    {
        public string clip, utc;
        public double dspTime;
    }

    [Serializable]
    public sealed class DirectAudioObservation
    {
        public string utc, scene, sourcePath, clip, clipPath, clipGuid, mixer;
        public string ownerType, ownerPath, ownerVisibleState, popupStates;
        public int sourceId, frame, timeSamples;
        public bool present, activeSelf, activeInHierarchy, enabled, playing, loop, playOnAwake, muted;
        public float volume;
        public double dspTime;
    }

    [Serializable]
    public sealed class Report
    {
        public string scope = "Existing SoundManager effect dispatches, background source and direct scene AudioSource state changes; not speaker/audio capture or proof of audibility. AudioSource.clip does not identify PlayOneShot clips.";
        public string workspace = "Original", status, armedUtc, startedUtc, finishedUtc, reason;
        public bool sdkInitializedObserved, loginObserved, nativePlaybackSuppressed, truncated;
        public int acceptedDispatches;
        public List<Entry> entries = new List<Entry>();
        public List<BackgroundObservation> backgroundMuteChanges = new List<BackgroundObservation>();
        public List<StoppedEffect> stoppedEffects = new List<StoppedEffect>();
        public List<DirectAudioObservation> directAudioChanges = new List<DirectAudioObservation>();
    }

    static PopupStoreSoundReview()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += BeforeQuit;
    }

    private static bool Armed => !string.IsNullOrEmpty(SessionState.GetString(ArmedKey, ""));

    private static void Update()
    {
        try
        {
            if (File.Exists(Folder + "/stop.txt"))
            {
                File.Delete(Folder + "/stop.txt");
                if (Armed) Finish("STOPPED", "Explicit diagnostic stop request.");
                return;
            }
            if (!Armed)
            {
                if (!File.Exists(Folder + "/arm.txt")) return;
                string blocked = ArmBlockReason();
                if (blocked == null && File.ReadAllText(Folder + "/arm.txt").Trim() != "arm")
                    blocked = "The explicit request must contain exactly arm.";
                if (blocked != null)
                {
                    if (_lastBlockedReason != blocked)
                    {
                        _lastBlockedReason = blocked;
                        Save(new Report { status = "BLOCKED", finishedUtc = DateTime.UtcNow.ToString("O"), reason = blocked });
                    }
                    return; // Do not consume an unsafe request or attach it to an existing Play.
                }
                File.Delete(Folder + "/arm.txt");
                string utc = DateTime.UtcNow.ToString("O");
                SessionState.SetString(ArmedKey, utc);
                _lastBlockedReason = null;
                DirectAudioStates.Clear();
                _report = new Report { status = "ARMED", armedUtc = utc };
                Save(_report);
            }
            if (_report == null)
                _report = new Report { status = "ARMED", armedUtc = SessionState.GetString(ArmedKey, "") };
            if (BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin || !AccountModeOff() || !OriginalWorkspace())
            { ObserveSafety(); Finish("BLOCKED", "SDK, account mode or original workspace safety changed."); return; }
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false))
            { Finish("BLOCKED", "This Play is not the explicit original memory review."); return; }
            if (!SafeMemoryOwner())
            {
                if (_manager != null) Finish("BLOCKED", "The original memory owner became unavailable.");
                return; // Allow the already-owned manual bootstrap to finish; never create an owner.
            }
            var manager = SoundManager.TryGetExistingInstance();
            if (_manager != null && manager != _manager)
            { Finish("BLOCKED", "The observed SoundManager changed during this review."); return; }
            if (_manager != null) { ObserveBackground(); ObserveDirectAudio(); return; }
            if (manager == null) return;
            _manager = manager;
            _manager.EffectPlaybackRequested += OnEffect;
            _manager.EffectPlaybackStopped += OnEffectStopped;
            _report.startedUtc = DateTime.UtcNow.ToString("O");
            _report.status = "RECORDING";
            ObserveSafety();
            ObserveBackground();
            ObserveDirectAudio();
            Save(_report);
        }
        catch (Exception exception)
        {
            if (Armed) Finish("ERROR", exception.GetType().Name);
            else Debug.LogWarning("POPUP_STORE_SOUND_REVIEW: " + exception.GetType().Name);
        }
    }

    private static string ArmBlockReason()
    {
        if (!OriginalWorkspace()) return "Only the original Panda_Restaurant workspace is accepted.";
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorUtility.scriptCompilationFailed) return "A clean, compiled Play OFF Editor is required.";
        if (BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin || CustomerPartyManualPlayBootstrap.Active
            || SessionState.GetBool(CustomerPartyManualPlayBootstrap.SessionKey, false)
            || Muks.BackEnd.BackendManager.ExistingInstance != null) return "An SDK, login or memory owner is already present.";
        if (!AccountModeOff()) return "The existing account configuration must remain OFF and clean.";
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "Close the prefab editing stage before arming.";
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) return "An open scene has unsaved changes.";
        return null;
    }

    private static bool OriginalWorkspace() => string.Equals(Application.dataPath.Replace('\\', '/').TrimEnd('/'),
        OriginalAssets, StringComparison.OrdinalIgnoreCase);

    private static bool AccountModeOff()
    {
        var configuration = Resources.Load<PopupStoreAccountPhaseConfiguration>(PopupStoreAccountPhaseBootstrap.ConfigurationResource);
        return configuration != null && !configuration.EnableAccountMode && !EditorUtility.IsDirty(configuration);
    }

    private static bool SafeMemoryOwner()
    {
        if (!CustomerPartyManualPlayBootstrap.Active
            || !SceneManager.GetSceneByPath(CustomerPartyStage1Preview.ScenePath).isLoaded) return false;
        var memory = typeof(CustomerPartyStage1Preview).GetField("memory", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null) as CustomerPartyOwnedMemory;
        var owner = Muks.BackEnd.BackendManager.ExistingInstance;
        return memory != null && memory.LoggedIn && owner != null && owner.IsEditorOfflineOwner
            && ReferenceEquals(owner, memory.Owner);
    }

    private static void OnEffect(SoundManager.EffectPlaybackDiagnostic diagnostic)
    {
        try
        {
            if (!Armed || _report == null || _manager == null) return;
            ObserveSafety();
            if (_report.sdkInitializedObserved || _report.loginObserved || !AccountModeOff() || !SafeMemoryOwner())
            { Finish("BLOCKED", "The accepted dispatch was outside the isolated memory owner."); return; }
            _report.acceptedDispatches++;
            if (_report.entries.Count >= MaximumEntries) { _report.truncated = true; return; }
            _report.entries.Add(new Entry {
                clip = diagnostic.Clip != null ? diagnostic.Clip.name : "missing",
                effectType = diagnostic.Type.ToString(), ownerType = diagnostic.Owner?.GetType().FullName ?? "legacy",
                volume = diagnostic.Volume, effectiveVolume = diagnostic.EffectiveVolume,
                clipSeconds = diagnostic.Clip != null ? diagnostic.Clip.length : 0,
                dspTime = diagnostic.Time, utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount
            });
        }
        catch (Exception exception) { Finish("ERROR", exception.GetType().Name); }
    }

    private static void ObserveSafety()
    {
        if (_report == null) return;
        _report.sdkInitializedObserved |= BackEnd.Backend.IsInitialized;
        _report.loginObserved |= BackEnd.Backend.IsLogin;
        _report.nativePlaybackSuppressed |= _manager != null && _manager.SuppressNativePlaybackForTests;
    }

    private static void OnEffectStopped(AudioSource source, double time)
    {
        if (_report == null || _report.stoppedEffects.Count >= MaximumEntries) return;
        _report.stoppedEffects.Add(new StoppedEffect {
            clip = source != null && source.clip != null ? source.clip.name : "missing",
            utc = DateTime.UtcNow.ToString("O"), dspTime = time
        });
    }

    private static void ObserveBackground()
    {
        if (_report == null || _manager == null) return;
        var source = _manager.transform.Find("BackgroundAudio")?.GetComponent<AudioSource>();
        if (source == null || (source == _background && _backgroundMute == source.mute && _backgroundClip == source.clip)) return;
        _background = source; _backgroundMute = source.mute; _backgroundClip = source.clip;
        if (_report.backgroundMuteChanges.Count < MaximumEntries)
            _report.backgroundMuteChanges.Add(new BackgroundObservation {
                utc = DateTime.UtcNow.ToString("O"), muted = source.mute, dspTime = AudioSettings.dspTime,
                clip = source.clip != null ? source.clip.name : "missing"
            });
    }

    // Observe the recipe jar's direct loop sources as well as other scene-owned audio.
    // Native playback and all source properties remain untouched. The manager pool is
    // already observed above, including its actual PlayOneShot-equivalent dispatch clip.
    private static void ObserveDirectAudio()
    {
        if (_report == null || _manager == null) return;
        if (_report.directAudioChanges.Count >= MaximumEntries) { _report.truncated = true; return; }
        var popups = new List<string>();
        foreach (var popup in Resources.FindObjectsOfTypeAll<UIPopupStore>())
            if (!EditorUtility.IsPersistent(popup) && popup.gameObject.scene.IsValid() && popup.gameObject.scene.isLoaded)
                popups.Add(popup.GetInstanceID() + ":" + ObjectPath(popup.transform) + ":"
                    + popup.VisibleState + ":active=" + popup.gameObject.activeInHierarchy);
        popups.Sort(StringComparer.Ordinal);
        string popupStates = string.Join("; ", popups);
        var seen = new HashSet<int>();
        foreach (var source in Resources.FindObjectsOfTypeAll<AudioSource>())
        {
            if (EditorUtility.IsPersistent(source) || !source.gameObject.scene.IsValid()
                || !source.gameObject.scene.isLoaded || source.GetComponentInParent<SoundManager>(true) != null) continue;
            int id = source.GetInstanceID();
            seen.Add(id);
            var owner = source.GetComponentInParent<Muks.UI.UIView>(true);
            string clipPath = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : string.Empty;
            var observation = new DirectAudioObservation {
                present = true, sourceId = id, scene = source.gameObject.scene.path,
                sourcePath = ObjectPath(source.transform), clip = source.clip != null ? source.clip.name : "missing",
                clipPath = clipPath, clipGuid = string.IsNullOrEmpty(clipPath) ? string.Empty : AssetDatabase.AssetPathToGUID(clipPath),
                mixer = source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : "none",
                ownerType = owner != null ? owner.GetType().FullName : "none",
                ownerPath = owner != null ? ObjectPath(owner.transform) : string.Empty,
                ownerVisibleState = owner != null ? owner.VisibleState.ToString() : string.Empty,
                popupStates = popupStates, activeSelf = source.gameObject.activeSelf,
                activeInHierarchy = source.gameObject.activeInHierarchy, enabled = source.enabled,
                playing = source.isPlaying, loop = source.loop, playOnAwake = source.playOnAwake,
                muted = source.mute, volume = source.volume
            };
            // Exclude the advancing clock and playback cursor from the state fingerprint.
            string state = JsonUtility.ToJson(observation);
            if (DirectAudioStates.TryGetValue(id, out string previous) && previous == state) continue;
            DirectAudioStates[id] = state;
            observation.timeSamples = source.clip != null ? source.timeSamples : 0;
            RecordDirectAudio(observation);
        }
        foreach (int id in new List<int>(DirectAudioStates.Keys))
            if (!seen.Contains(id))
            {
                DirectAudioStates.Remove(id);
                RecordDirectAudio(new DirectAudioObservation { sourceId = id, present = false, popupStates = popupStates });
            }
    }

    private static string ObjectPath(Transform item)
    {
        string path = item.name;
        while (item.parent != null) { item = item.parent; path = item.name + "/" + path; }
        return path;
    }

    private static void RecordDirectAudio(DirectAudioObservation observation)
    {
        if (_report.directAudioChanges.Count >= MaximumEntries) { _report.truncated = true; return; }
        observation.utc = DateTime.UtcNow.ToString("O");
        observation.dspTime = AudioSettings.dspTime;
        observation.frame = Time.frameCount;
        _report.directAudioChanges.Add(observation);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!Armed) return;
        if (state == PlayModeStateChange.ExitingPlayMode)
            Finish(_manager != null ? "COMPLETED" : "NOT_RUN", "Existing Play ended; diagnostic subscription removed.");
        else if (state == PlayModeStateChange.EnteredEditMode)
            Finish("NOT_RUN", "The expected isolated Play did not reach sound observation.");
    }

    private static void BeforeReload()
    {
        // Keep the explicit arm across the normal pre-Play domain reload; never keep a live subscription.
        if (_manager != null) Finish("INTERRUPTED", "Assembly reload interrupted sound observation.");
    }
    private static void BeforeQuit() { if (Armed) Finish("INTERRUPTED", "Editor shutdown ended sound observation."); }

    private static void Finish(string status, string reason)
    {
        if (_manager != null)
        {
            _manager.EffectPlaybackRequested -= OnEffect;
            _manager.EffectPlaybackStopped -= OnEffectStopped;
        }
        ObserveSafety();
        _manager = null;
        _background = null; _backgroundMute = null; _backgroundClip = null;
        DirectAudioStates.Clear();
        var report = _report ?? new Report { armedUtc = SessionState.GetString(ArmedKey, "") };
        SessionState.EraseString(ArmedKey);
        _report = null;
        report.status = status; report.reason = reason; report.finishedUtc = DateTime.UtcNow.ToString("O");
        try { Save(report); }
        catch (Exception exception) { Debug.LogWarning("POPUP_STORE_SOUND_REVIEW_SAVE: " + exception.GetType().Name); }
    }

    private static void Save(Report report)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/result.json", JsonUtility.ToJson(report, true));
    }
}
#endif
