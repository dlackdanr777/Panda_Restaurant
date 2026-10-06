#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Offline evidence only: records accepted production pool dispatches and their stops,
/// exports the original imported AudioClip PCM, and supplies a frame-synchronous clock.
/// The resulting movie reconstructs these events; it is not a speaker/mixer recording.
/// </summary>
public sealed class Phase5AudioEvidence : IDisposable
{
    [Serializable]
    public sealed class ClipRecord
    {
        public string key, assetPath, guid, wavPath;
        public int sampleRate, channels, samples;
        public float duration;
    }

    [Serializable]
    public sealed class PlaybackRecord
    {
        public string clipKey, effectType, ownerType;
        public int sourceId;
        public double time, stopTime = -1;
        public float sourceVolume, effectiveVolume, pitch, startSeconds;
    }

    [Serializable]
    private sealed class EvidenceManifest
    {
        public string evidenceKind = "original-imported-clip-and-accepted-runtime-event-reconstruction";
        public string limitation = "Not native AudioRenderer, device, or speaker capture; mixer effects and spatial filtering are not reconstructed.";
        public int framesPerSecond;
        public double durationSeconds;
        public List<ClipRecord> clips = new List<ClipRecord>();
        public List<PlaybackRecord> events = new List<PlaybackRecord>();
    }

    private readonly SoundManager _manager;
    private readonly string _directory;
    private readonly Func<double> _previousClock;
    private readonly bool _previousSuppression;
    private readonly EvidenceManifest _manifest = new EvidenceManifest();
    private readonly Dictionary<AudioClip, ClipRecord> _clips = new Dictionary<AudioClip, ClipRecord>();
    private readonly Dictionary<int, PlaybackRecord> _active = new Dictionary<int, PlaybackRecord>();
    private bool _disposed;

    public double TimeSeconds { get; private set; }
    public int EventCount => _manifest.events.Count;
    public IReadOnlyList<PlaybackRecord> Events => _manifest.events;
    public string FramesDirectory => Path.Combine(_directory, "frames");

    public Phase5AudioEvidence(SoundManager manager, string outputDirectory,
        int fps = 30, bool suppressNativePlayback = true)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));
        _manager = manager;
        _directory = Path.GetFullPath(outputDirectory);
        _manifest.framesPerSecond = fps;
        Directory.CreateDirectory(FramesDirectory);
        Directory.CreateDirectory(Path.Combine(_directory, "clips"));
        _previousClock = manager.EffectClockForTests;
        _previousSuppression = manager.SuppressNativePlaybackForTests;
        manager.EffectClockForTests = () => TimeSeconds;
        manager.SuppressNativePlaybackForTests = suppressNativePlayback;
        manager.EffectPlaybackRequested += RecordPlayback;
        manager.EffectPlaybackStopped += RecordStop;
    }

    public void BeginFrame(int frameIndex)
    {
        if (frameIndex < 0) throw new ArgumentOutOfRangeException(nameof(frameIndex));
        TimeSeconds = (double)frameIndex / _manifest.framesPerSecond;
    }

    public void SetTime(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        TimeSeconds = seconds;
    }

    public string FramePath(int frameIndex) => Path.Combine(FramesDirectory, frameIndex.ToString("D5") + ".png");

    private void RecordPlayback(SoundManager.EffectPlaybackDiagnostic value)
    {
        if (!_clips.TryGetValue(value.Clip, out var clip))
        {
            string path = AssetDatabase.GetAssetPath(value.Clip);
            string guid = AssetDatabase.AssetPathToGUID(path);
            string key = string.IsNullOrEmpty(guid) ? "runtime-" + _clips.Count.ToString("D3") : guid;
            clip = new ClipRecord { key = key, assetPath = path, guid = guid,
                wavPath = "clips/" + key + ".wav", sampleRate = value.Clip.frequency,
                channels = value.Clip.channels, samples = value.Clip.samples, duration = value.Clip.length };
            _clips.Add(value.Clip, clip);
            _manifest.clips.Add(clip);
        }
        int sourceId = value.Source.GetInstanceID();
        var playback = new PlaybackRecord { clipKey = clip.key, effectType = value.Type.ToString(),
            ownerType = value.Owner == null ? "none" : value.Owner.GetType().Name,
            sourceId = sourceId, time = value.Time, sourceVolume = value.Volume,
            effectiveVolume = value.EffectiveVolume, pitch = value.Pitch, startSeconds = value.StartSeconds };
        _manifest.events.Add(playback);
        _active[sourceId] = playback;
    }

    private void RecordStop(AudioSource source, double time)
    {
        if (source != null && _active.TryGetValue(source.GetInstanceID(), out var record))
        {
            record.stopTime = time;
            _active.Remove(source.GetInstanceID());
        }
    }

    public void Save(double durationSeconds)
    {
        if (durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        _manifest.durationSeconds = durationSeconds;
        foreach (var pair in _clips)
            ExportOriginalClip(pair.Key, Path.Combine(_directory, pair.Value.wavPath));
        File.WriteAllText(Path.Combine(_directory, "audio-timeline.json"), JsonUtility.ToJson(_manifest, true));
    }

    public static void ExportOriginalClip(AudioClip clip, string outputPath)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        var pcm = new float[checked(clip.samples * clip.channels)];
        if (!clip.GetData(pcm, 0))
            throw new InvalidOperationException("Cannot export original imported PCM: " + AssetDatabase.GetAssetPath(clip));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
        using (var writer = new BinaryWriter(File.Create(outputPath)))
        {
            int bytes = checked(pcm.Length * sizeof(short));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)clip.channels); writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * sizeof(short));
            writer.Write((short)(clip.channels * sizeof(short))); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            for (int i = 0; i < pcm.Length; i++)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(pcm[i], -1f, 1f) * short.MaxValue));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_manager != null)
        {
            _manager.EffectPlaybackRequested -= RecordPlayback;
            _manager.EffectPlaybackStopped -= RecordStop;
            _manager.EffectClockForTests = _previousClock;
            _manager.SuppressNativePlaybackForTests = _previousSuppression;
        }
    }
}
#endif
