using Muks.DataBind;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum AudioType
{
    Master,
    BackgroundAudio,
    EffectAudio,
    Count,
}

public enum SoundEffectType
{
    ButtonClickSound,
    ButtonExitSound,
    BuySound,
    GoldSound,
    DiaSound,
    Length
}

public enum EffectType
{
    None,
    Hall1,
    Hall2,
    Hall3,
    Kitchen1,
    Kitchen2,
    Kitchen3,
    Restaurant,
    
    UI
}

public class SoundManager : MonoBehaviour
{
    private const int _poolSize = 10;

    public static SoundManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("SoundManager");
                _instance = obj.AddComponent<SoundManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static SoundManager _instance;

    // Presentation adapters must not create a persistent manager during teardown or previews.
    public static SoundManager TryGetExistingInstance() => _instance != null ? _instance : null;
    private float _masterVolume = 1f;
    private float _soundEffectVolume = 1f;
    public bool IsEffectAudioMuted => _masterVolume <= 0f || _soundEffectVolume <= 0f || _effectVolume <= 0f;

    private struct EffectVoice
    {
        public object Owner;
        public EffectType Type;
        public bool Presentation;
        public double EndsAt;
    }
    private readonly Dictionary<AudioSource, EffectVoice> _effectVoices = new Dictionary<AudioSource, EffectVoice>();
    private readonly List<AudioSource> _finishedEffects = new List<AudioSource>();

#if UNITY_EDITOR
    public readonly struct EffectPlaybackDiagnostic
    {
        public readonly AudioSource Source;
        public readonly AudioClip Clip;
        public readonly EffectType Type;
        public readonly object Owner;
        public readonly float Volume, EffectiveVolume, Pitch, StartSeconds;
        public readonly double Time;
        public EffectPlaybackDiagnostic(AudioSource source, AudioClip clip, EffectType type,
            object owner, float volume, float effectiveVolume, float pitch, float startSeconds, double time)
        { Source = source; Clip = clip; Type = type; Owner = owner; Volume = volume;
            EffectiveVolume = effectiveVolume; Pitch = pitch; StartSeconds = startSeconds; Time = time; }
    }
    // Accepted pool dispatches, including legacy button audio. Native playback can be
    // suppressed in SDK-free tests; a diagnostic is not evidence of speaker capture.
    public event Action<EffectPlaybackDiagnostic> EffectPlaybackRequested;
    public event Action<AudioSource, double> EffectPlaybackStopped;
    public Func<double> EffectClockForTests { get; set; }
    public bool SuppressNativePlaybackForTests { get; set; }
#endif

    private double EffectClock
    {
        get
        {
#if UNITY_EDITOR
            if (EffectClockForTests != null) return EffectClockForTests();
#endif
            return AudioSettings.dspTime;
        }
    }

    public AudioClip GetEffectClip(SoundEffectType type)
    {
        int index = (int)type;
        return _clips != null && index >= 0 && index < _clips.Length ? _clips[index] : null;
    }

    public AudioSource PlayPresentationEffect(EffectType type, AudioClip clip,
        float volumeScale = 1f, float pitch = 1f, object owner = null, int maxConcurrent = 0, float startSeconds = 0f)
    {
        if (clip == null || IsEffectAudioMuted || !CanPlayEffectType(type) || volumeScale <= 0f
            || float.IsNaN(volumeScale) || float.IsInfinity(volumeScale)
            || float.IsNaN(pitch) || float.IsInfinity(pitch)
            || float.IsNaN(startSeconds) || float.IsInfinity(startSeconds)) return null;
        startSeconds = Mathf.Clamp(startSeconds, 0f, clip.length);
        if (startSeconds >= clip.length || !_effectAudioDic.ContainsKey(type)) return null;
        RetireFinishedEffects();
        if (maxConcurrent > 0)
        {
            int count = 0;
            foreach (var voice in _effectVoices.Values)
                if (voice.Presentation && voice.Type == type && ReferenceEquals(voice.Owner, owner)) count++;
            if (count >= maxConcurrent) return null;
        }
        // Reuse the existing channel pool; never allocate or steal a busy voice here.
        AudioSource source = GetAvailableAudioSource(type, false);
        if (source == null) return null;
        EnsureClipLoaded(clip);
        PlayPooledEffect(source, type, clip, volumeScale, pitch, owner, true, startSeconds);
        return source;
    }

    public void StopPresentationEffects(object owner)
    {
        _finishedEffects.Clear();
        foreach (var pair in _effectVoices)
            if (pair.Value.Presentation && ReferenceEquals(pair.Value.Owner, owner)) _finishedEffects.Add(pair.Key);
        foreach (var source in _finishedEffects) StopPooledEffect(source);
        _finishedEffects.Clear();
    }

    private bool CanPlayEffectType(EffectType type) => type == EffectType.None || type == _effectType
        || (IsRestaurantAreaType(type) && IsRestaurantAreaType(_effectType));

    private void RetireFinishedEffects()
    {
        double now = EffectClock;
        _finishedEffects.Clear();
        foreach (var pair in _effectVoices)
            if (pair.Key == null || pair.Value.EndsAt <= now) _finishedEffects.Add(pair.Key);
        foreach (var source in _finishedEffects) _effectVoices.Remove(source);
        _finishedEffects.Clear();
    }

    private void StopPooledEffect(AudioSource source)
    {
        bool tracked = _effectVoices.Remove(source);
        if (source != null) source.Stop();
#if UNITY_EDITOR
        if (tracked) EffectPlaybackStopped?.Invoke(source, EffectClock);
#endif
    }

    private void PlayPooledEffect(AudioSource source, EffectType type, AudioClip clip,
        float volumeScale = 1f, float pitch = 1f, object owner = null, bool presentation = false, float startSeconds = 0f)
    {
        StopPooledEffect(source);
        source.clip = clip;
        source.timeSamples = Mathf.Clamp(Mathf.RoundToInt(startSeconds * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
        source.loop = false;
        source.pitch = Mathf.Clamp(pitch, .1f, 3f);
        source.volume = Mathf.Clamp01(_effectVolume * volumeScale);
        double now = EffectClock;
        _effectVoices[source] = new EffectVoice { Owner = owner, Type = type, Presentation = presentation,
            EndsAt = now + (clip.length - startSeconds) / source.pitch };
#if UNITY_EDITOR
        if (!SuppressNativePlaybackForTests) source.Play();
        EffectPlaybackRequested?.Invoke(new EffectPlaybackDiagnostic(source, clip, type, owner,
            source.volume, source.volume * _masterVolume * _soundEffectVolume, source.pitch, startSeconds, now));
#else
        source.Play();
#endif
    }

    public event Action<float, AudioType> OnVolumeChangedHandler;

    private AudioMixer _audioMixer;
    private float _backgroundVolume = 0.9f;
    private float _effectVolume = 1f;

    private AudioSource[] _audios;


    private Dictionary<EffectType, List<AudioSource>> _effectAudioDic = new Dictionary<EffectType, List<AudioSource>>();
    private AudioClip[] _clips;
    private AudioClip _currentBackgroundClip;

    private float _backgroundVolumeMul;
    public float BackgroundVolumeMul => _backgroundVolumeMul;

    private float _effectVolumeMul;
    public float EffectVolumeMul => _effectVolumeMul;

    private bool _isVibration = false;
    public bool IsVibration => _isVibration;

    //배경 음악 변경시 볼륨 업, 다운 기능을 위한 변수
    private Coroutine _changeAudioRoutine;
    private Coroutine _delayPlayEffectAudioRoutine;
    private Coroutine _stopBackgroundAudioRoutine;
    private Coroutine _stopEffectAudioRoutine;
    private Coroutine _changeEffectTypeRoutine;
    private Coroutine _periodicVolumeResetRoutine;
    private const float VolumeResetInterval = 60f;

    private EffectType _effectType = EffectType.Hall1;
    private EffectType _savedEffectType = EffectType.Hall1;
    public EffectType EffectType => _effectType;

    // Helper methods for new EffectType categories
    private bool IsKitchenType(EffectType type)
    {
        return type == EffectType.Kitchen1 || type == EffectType.Kitchen2 || type == EffectType.Kitchen3;
    }

    private bool IsHallType(EffectType type)
    {
        return type == EffectType.Hall1 || type == EffectType.Hall2 || type == EffectType.Hall3;
    }

    private bool IsRestaurantAreaType(EffectType type)
    {
        return IsHallType(type) || IsKitchenType(type) || type == EffectType.Restaurant;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
        LoadSoundData();
        _periodicVolumeResetRoutine = StartCoroutine(IEPeriodicVolumeReset());
    }

    private void Init()
    {
        _effectAudioDic.Clear();
        _audioMixer = Resources.Load<AudioMixer>("Audio/AudioMixer");

        if (_audioMixer == null)
        {
            Debug.LogError("[SoundManager] AudioMixer not found: Audio/AudioMixer");
            return;
        }

        _clips = new AudioClip[(int)SoundEffectType.Length];
        
        for (int i = 0, cnt = (int)SoundEffectType.Length; i < cnt; ++i)
        {
            int index = i;
            _clips[i] = Resources.Load<AudioClip>("Audio/" + ((SoundEffectType)i).ToString());
            DataBind.SetUnityActionValue(((SoundEffectType)index).ToString(), () => PlayEffectAudio(EffectType.None, (SoundEffectType)index));
        }

        _audios = new AudioSource[(int)AudioType.Count];

        for (int i = (int)AudioType.BackgroundAudio, count = (int)AudioType.EffectAudio; i < count; i++)
        {
            GameObject obj = new GameObject(Enum.GetName(typeof(AudioType), i));
            obj.transform.parent = transform;
            _audios[i] = obj.AddComponent<AudioSource>();
        }

        _backgroundVolumeMul = 1;
        _effectVolumeMul = 1;

        _audios[(int)AudioType.BackgroundAudio].loop = true;
        _audios[(int)AudioType.BackgroundAudio].playOnAwake = true;
        _audios[(int)AudioType.BackgroundAudio].volume = _backgroundVolume;
        _audios[(int)AudioType.BackgroundAudio].dopplerLevel = 0;
        _audios[(int)AudioType.BackgroundAudio].reverbZoneMix = 0;

        var bgGroups = _audioMixer.FindMatchingGroups("Background");
        if (bgGroups == null || bgGroups.Length == 0)
        {
            Debug.LogError("[SoundManager] AudioMixer group not found: Background");
            return;
        }
        _audios[(int)AudioType.BackgroundAudio].outputAudioMixerGroup = bgGroups[0];

        for (int i = (int)EffectType.None, cnt = (int)EffectType.UI + 1; i < cnt; ++i)
        {
            EffectType effectType = (EffectType)i;
            string effectTypeName = effectType == EffectType.None ? "SoundEffect" : Enum.GetName(typeof(EffectType), effectType);
            GameObject parent = new GameObject(effectTypeName + "Parent");
            parent.transform.SetParent(transform);
            _effectAudioDic.Add(effectType, new List<AudioSource>());
            
            for(int j = 0; j < _poolSize; ++j)
            {
                GameObject obj = new GameObject(effectTypeName);
                obj.transform.parent = parent.transform;
                AudioSource audioSource = obj.AddComponent<AudioSource>();

                // 기본 오디오 소스 설정
                audioSource.loop = false;
                audioSource.playOnAwake = false;
                audioSource.volume = _effectVolume;
                audioSource.dopplerLevel = 0;
                audioSource.reverbZoneMix = 0;

                // ? AudioMixer 그룹 설정 수정
                AudioMixerGroup targetGroup = GetAudioMixerGroup(effectType);
                audioSource.outputAudioMixerGroup = targetGroup;

                _effectAudioDic[effectType].Add(audioSource);
            }        
        }
    }


    public EffectType GetHallEffectType(ERestaurantFloorType floor, RestaurantType type)
    {
        int floorNumber = (int)floor; // Floor1=0 -> 1, Floor2=1 -> 2, Floor3=2 -> 3
        
        switch (type)
        {
            case RestaurantType.Hall:
                return (EffectType)((int)EffectType.Hall1 + floorNumber);
                
            case RestaurantType.Kitchen:
                return (EffectType)((int)EffectType.Kitchen1 + floorNumber);
                
            default:
                throw new ArgumentException("Invalid RestaurantType: " + type);
        }
    }

    public void LoadSoundData()
    {
        if (_audioMixer == null)
            return;

        float masterVolume = PlayerPrefs.HasKey("MasterVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("MasterVolume"), 0, 1) : 1;
        float backgroundVolume = PlayerPrefs.HasKey("BackgroundVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("BackgroundVolume"), 0, 1) : 1;
        float soundEffectVolume = PlayerPrefs.HasKey("SoundEffectVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("SoundEffectVolume"), 0, 1) : 1;
        bool isVibration = PlayerPrefs.GetInt("IsVibration", 0) == 1;

        // ? dB 변환 후 AudioMixer에 적용
        float masterDB = masterVolume != 0 ? Mathf.Log10(masterVolume) * 20 : -80;
        float backgroundDB = backgroundVolume != 0 ? Mathf.Log10(backgroundVolume) * 20 : -80;
        float soundEffectDB = soundEffectVolume != 0 ? Mathf.Log10(soundEffectVolume) * 20 : -80;

        _audioMixer.SetFloat("Master", masterDB);
        _audioMixer.SetFloat("Background", backgroundDB);
        _audioMixer.SetFloat("SoundEffect", soundEffectDB);

        _masterVolume = masterVolume;
        _soundEffectVolume = soundEffectVolume;
        _isVibration = isVibration;
    }

    public float GetVolume(AudioType audioType)
    {
        float volume = 0;
        switch (audioType)
        {
            case AudioType.Master:
                volume = PlayerPrefs.HasKey("MasterVolume") ? PlayerPrefs.GetFloat("MasterVolume") : 1;
                break;

            case AudioType.BackgroundAudio:
                volume = PlayerPrefs.HasKey("BackgroundVolume") ? PlayerPrefs.GetFloat("BackgroundVolume") : 1;
                break;

            case AudioType.EffectAudio:
                volume = PlayerPrefs.HasKey("SoundEffectVolume") ? PlayerPrefs.GetFloat("SoundEffectVolume") : 1;
                break;
        }

        return volume;
    }

    public void SaveSoundData(string name, float value)
    {
        PlayerPrefs.SetFloat(name + "Volume", value);
    }

    public void SetVibration(bool value)
    {
        _isVibration = value;
        PlayerPrefs.SetInt("IsVibration", _isVibration ? 1 : 0);
    }

    public void PlayBackgroundAudio(AudioClip clip, float duration = 0, bool isLoop = true)
    {
        if (clip == _currentBackgroundClip)
        {
            DebugLog.Log("현재 클립과 같은 이름의 클립을 재생 시도 했습니다: " + clip.name);
            return;
        }
        _currentBackgroundClip = clip;

        if (_changeAudioRoutine != null)
            StopCoroutine(_changeAudioRoutine);
        _audios[(int)AudioType.BackgroundAudio].loop = isLoop;
        if (duration == 0)
        {
            _audios[(int)AudioType.BackgroundAudio].volume = _backgroundVolume;
            _audios[(int)AudioType.BackgroundAudio].clip = clip;
            _audios[(int)AudioType.BackgroundAudio].Play();
            return;
        }

        _changeAudioRoutine = StartCoroutine(IEChangeBackgroundAudio(clip, duration));
    }


    public void ChangePlayEffectType(EffectType type)
    {
        _savedEffectType = type;

        if (_audioMixer == null)
            return;

        if (_changeEffectTypeRoutine != null)
            StopCoroutine(_changeEffectTypeRoutine);

        _effectType = type;

        // 효과음이 꺼진 상태면 모든 그룹 음소거 유지
        float effectVolume = PlayerPrefs.HasKey("SoundEffectVolume") ? PlayerPrefs.GetFloat("SoundEffectVolume") : 1;
        if (effectVolume <= 0)
        {
            _audioMixer.SetFloat("Restaurant", -80f);
            _audioMixer.SetFloat("Hall1", -80f);
            _audioMixer.SetFloat("Hall2", -80f);
            _audioMixer.SetFloat("Hall3", -80f);
            _audioMixer.SetFloat("Kitchen1", -80f);
            _audioMixer.SetFloat("Kitchen2", -80f);
            _audioMixer.SetFloat("Kitchen3", -80f);
            _audioMixer.SetFloat("UI", -80f);
            return;
        }

        // Restaurant 그룹 볼륨 설정 (UI 또는 기타일 경우 0, Hall/Kitchen/Restaurant일 경우 1)
        bool isRestaurantArea = IsRestaurantAreaType(type);
        _audioMixer.SetFloat("Restaurant", isRestaurantArea ? 0f : -80f);

        // UI 그룹 볼륨 설정 (UI일 경우 1, 그 외 0)
        _audioMixer.SetFloat("UI", type == EffectType.UI ? 0f : -80f);

        // 세부 그룹 설정
        if (type == EffectType.Restaurant)
        {
            // Restaurant 타입이면 모든 하위 그룹 활성화
            _audioMixer.SetFloat("Hall1", 0f);
            _audioMixer.SetFloat("Hall2", 0f);
            _audioMixer.SetFloat("Hall3", 0f);
            _audioMixer.SetFloat("Kitchen1", 0f);
            _audioMixer.SetFloat("Kitchen2", 0f);
            _audioMixer.SetFloat("Kitchen3", 0f);
            _audioMixer.SetFloat("Restaurant", 0f);
        }
        else if (IsHallType(type) || IsKitchenType(type))
        {
            // 특정 구역만 활성화, 다른 구역은 음소거
            // Restaurant 부모 그룹은 이미 위에서 0f로 설정됨 - 여기서 덮어쓰지 않음
            _audioMixer.SetFloat("Hall1", type == EffectType.Hall1 ? 0f : -80f);
            _audioMixer.SetFloat("Hall2", type == EffectType.Hall2 ? 0f : -80f);
            _audioMixer.SetFloat("Hall3", type == EffectType.Hall3 ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen1", type == EffectType.Kitchen1 ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen2", type == EffectType.Kitchen2 ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen3", type == EffectType.Kitchen3 ? 0f : -80f);
        }
    }

    public void ChangePlayEffectType(EffectType type, float duration = 0.02f)
    {
        _savedEffectType = type;

        if (_effectType == type)
        {
            DebugLog.Log("현재 재생중인 효과음 타입과 동일한 타입을 재생 시도 했습니다: " + type.ToString());
            return;
        }

        duration = Mathf.Max(0.02f, duration);

        if (_changeEffectTypeRoutine != null)
            StopCoroutine(_changeEffectTypeRoutine);

        EffectType prevType = _effectType;
        _effectType = type;
        _changeEffectTypeRoutine = StartCoroutine(IEChangeEffectType(type, prevType, duration));
    }

    public void PlayEffectAudio(EffectType type, AudioClip clip, float waitTime = 0)
    {
        if (IsEffectAudioMuted) return;
        if (clip == null)
        {
            DebugLog.LogError("재생할 효과음이 없습니다: " + type.ToString());
            return;
        }

        // Play() 시점의 동기 디코딩 비용을 줄이기 위해 대기 시간 동안 미리 로드를 시작한다
        EnsureClipLoaded(clip);

        if (waitTime == 0)
        {
            bool isRestaurantRelated = IsRestaurantAreaType(type);
            bool isCurrentRestaurantRelated = IsRestaurantAreaType(_effectType);

            // 다음 조건 중 하나만 충족하면 재생 가능:
            // 1. type이 None (모든 곳에서 재생 가능한 기본 효과음)
            // 2. type이 현재 활성화된 타입과 일치
            // 3. type과 현재 타입 모두 레스토랑 관련 타입(Restaurant, Hall, Kitchen)
            if (type != EffectType.None && type != _effectType && !(isRestaurantRelated && isCurrentRestaurantRelated))
            {
                DebugLog.Log("현재 재생 가능한 타입이 아닙니다: (현재 타입: " + _effectType + ", 요청 타입: " + type + ")");
                return;
            }

            // 해당 타입의 오디오 소스 풀에서 재생 가능한 소스 찾기
            AudioSource availableSource = GetAvailableAudioSource(type);
            if (availableSource != null)
            {
                PlayPooledEffect(availableSource, type, clip);
            }
        }
        else
        {
            // ? 지연 재생 시에도 동일한 검증 로직 적용
            bool isRestaurantRelated = IsRestaurantAreaType(type);
            bool isCurrentRestaurantRelated = IsRestaurantAreaType(_effectType);

            if (type != EffectType.None && type != _effectType && !(isRestaurantRelated && isCurrentRestaurantRelated))
            {
                DebugLog.Log("현재 재생 가능한 타입이 아닙니다: (현재 타입: " + _effectType + ", 요청 타입: " + type + ")");
                return;
            }

            StartCoroutine(IEDelayPlayEffectAudio(type, clip, waitTime));
        }
    }

    private AudioSource GetAvailableAudioSource(EffectType type, bool allowSteal = true)
    {
        RetireFinishedEffects();
        if (!_effectAudioDic.ContainsKey(type))
        {
            DebugLog.LogError("등록되지 않은 EffectType입니다: " + type.ToString());
            return null;
        }

        // 해당 타입의 오디오 소스 풀에서 현재 재생 중이지 않은 소스 찾기   
        foreach (AudioSource source in _effectAudioDic[type])
        {
            if (source != null && !source.isPlaying && !_effectVoices.ContainsKey(source))
                return source;
        }

        // 모든 소스가 재생 중이면 가장 오래된 소스 선택 (첫 번째 소스 반환)
        return allowSteal && _effectAudioDic[type].Count > 0 ? _effectAudioDic[type][0] : null;
    }

    // 로딩 화면 등에서 미리 호출해 Play() 시점의 동기 로딩 비용을 없앤다
    public void PreloadAudioClip(AudioClip clip)
    {
        EnsureClipLoaded(clip);
    }

    private void EnsureClipLoaded(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    public void PlayEffectAudio(EffectType type, SoundEffectType soundEffectType)
    {
        if (IsEffectAudioMuted) return;
        // ? 레스토랑 관련 타입 간 재생 허용 로직 추가
        bool isRestaurantRelated = IsRestaurantAreaType(type);
        bool isCurrentRestaurantRelated = IsRestaurantAreaType(_effectType);

        if (type != EffectType.None && type != _effectType && !(isRestaurantRelated && isCurrentRestaurantRelated))
        {
            DebugLog.Log("현재 재생 가능한 타입이 아닙니다: (현재 타입: " + _effectType + ", 요청 타입: " + type + ")");
            return;
        }

        AudioClip clip = GetEffectClip(soundEffectType);
        if (clip == null)
        {
            DebugLog.LogError("재생할 효과음이 없습니다: " + soundEffectType.ToString());
            return;
        }

        EnsureClipLoaded(clip);

        // 해당 타입의 오디오 소스 풀에서 재생 가능한 소스 찾기
        AudioSource availableSource = GetAvailableAudioSource(type);
        if (availableSource != null)
        {
            PlayPooledEffect(availableSource, type, clip);
        }
    }

    public void StopBackgroundAudio(float duration = 0)
    {
        if (_stopBackgroundAudioRoutine != null)
            StopCoroutine(_stopBackgroundAudioRoutine);

        if (duration == 0)
        {
            _audios[(int)AudioType.BackgroundAudio].Stop();
            return;
        }

        _stopBackgroundAudioRoutine = StartCoroutine(IEStopBackgroundAudio(duration));
    }

    public void StopEffectAudio(float duration = 0)
    {
        if (_stopEffectAudioRoutine != null)
            StopCoroutine(_stopEffectAudioRoutine);

        if (duration == 0)
        {
            foreach (var pair in _effectAudioDic)
                foreach (var source in pair.Value)
                    StopPooledEffect(source);
            return;
        }

        _stopEffectAudioRoutine = StartCoroutine(IEStopEffectAudio(duration));
    }

    public void SetVolume(float value, AudioType type)
    {
        value = Mathf.Clamp(value, 0.0f, 1.0f);
        float volume = value != 0 ? Mathf.Log10(value) * 20 : -80;

        switch (type)
        {
            case AudioType.Master:
                _masterVolume = value;
                _audioMixer.SetFloat("Master", volume);
                SaveSoundData("Master", value);
                break;

            case AudioType.BackgroundAudio:
                _audioMixer.SetFloat("Background", volume);
                SaveSoundData("Background", value);
                break;

            case AudioType.EffectAudio:
                _soundEffectVolume = value;
                _audioMixer.SetFloat("SoundEffect", volume);
                SaveSoundData("SoundEffect", value);
                if (value <= 0)
                {
                    // 모든 효과음 관련 믹서 그룹 음소거
                    _audioMixer.SetFloat("Restaurant", -80f);
                    _audioMixer.SetFloat("Hall1", -80f);
                    _audioMixer.SetFloat("Hall2", -80f);
                    _audioMixer.SetFloat("Hall3", -80f);
                    _audioMixer.SetFloat("Kitchen1", -80f);
                    _audioMixer.SetFloat("Kitchen2", -80f);
                    _audioMixer.SetFloat("Kitchen3", -80f);
                    _audioMixer.SetFloat("UI", -80f);
                }
                else
                {
                    // 현재 effectType에 맞게 믹서 그룹 복원
                    if (_changeEffectTypeRoutine != null)
                    {
                        StopCoroutine(_changeEffectTypeRoutine);
                        _changeEffectTypeRoutine = null;
                    }
                    ChangePlayEffectType(_effectType);
                }
                break;
        }

        OnVolumeChangedHandler?.Invoke(volume, type);
    }

    private IEnumerator IEStopBackgroundAudio(float duration)
    {
        float maxVolume = _audios[(int)AudioType.BackgroundAudio].volume;
        float changeDuration = duration;
        float timer = 0;

        while (timer < changeDuration)
        {
            timer += 0.02f;
            _audios[(int)AudioType.BackgroundAudio].volume = Mathf.Lerp(maxVolume, 0, timer / changeDuration);

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _audios[(int)AudioType.BackgroundAudio].Stop();
    }

    private IEnumerator IEChangeBackgroundAudio(AudioClip clip, float duration)
    {
        float maxVolume = _backgroundVolume;
        float changeDuration = duration * 0.5f;
        float timer = 0;

        while (timer < changeDuration)
        {
            timer += 0.02f;
            _audios[(int)AudioType.BackgroundAudio].volume = Mathf.Lerp(maxVolume, 0, timer / changeDuration);

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _audios[(int)AudioType.BackgroundAudio].clip = clip;
        _audios[(int)AudioType.BackgroundAudio].volume = 0;
        _audios[(int)AudioType.BackgroundAudio].Play();

        timer = 0;
        while (timer < changeDuration)
        {
            timer += 0.02f;
            _audios[(int)AudioType.BackgroundAudio].volume = Mathf.Lerp(0, maxVolume, timer / changeDuration);

            yield return YieldCache.WaitForSeconds(0.02f);
        }
    }

    private IEnumerator IEDelayPlayEffectAudio(EffectType type, AudioClip clip, float waitTime)
    {
        yield return YieldCache.WaitForSeconds(waitTime);
        if (IsEffectAudioMuted) yield break;

        // ? 지연 후 다시 한 번 타입 검증 (지연 중 타입이 변경될 수 있음)
        bool isRestaurantRelated = IsRestaurantAreaType(type);
        bool isCurrentRestaurantRelated = IsRestaurantAreaType(_effectType);

        // 다음 조건 중 하나만 충족하면 재생 가능:
        // 1. type이 None (모든 곳에서 재생 가능한 기본 효과음)
        // 2. type이 현재 활성화된 타입과 일치
        // 3. type과 현재 타입 모두 레스토랑 관련 타입(Restaurant, Hall, Kitchen)
        if (type != EffectType.None && type != _effectType && !(isRestaurantRelated && isCurrentRestaurantRelated))
        {
            DebugLog.Log("지연 재생 중 타입이 변경되어 재생할 수 없습니다: (현재 타입: " + _effectType + ", 요청 타입: " + type + ")");
            yield break;
        }

        // 지연 후 재생 가능한 오디오 소스 찾기
        AudioSource availableSource = GetAvailableAudioSource(type);
        if (availableSource != null)
        {
            PlayPooledEffect(availableSource, type, clip);
        }
    }

    private IEnumerator IEChangeEffectType(EffectType type, EffectType prevType, float duration)
    {
        float changeDuration = duration * 0.5f;
        float timer = 0;

        // Restaurant 카테고리가 활성화될지 여부
        bool willRestaurantBeActive = IsRestaurantAreaType(type);
        bool willUIBeActive = (type == EffectType.UI);

        // 이전 타입이 레스토랑 관련 타입인지 확인 (prevType 사용 - _effectType은 이미 새 타입으로 변경됨)
        bool isCurrentRestaurantRelated = IsRestaurantAreaType(prevType);

        // 레스토랑 관련 타입 간 전환인지 확인 (prevType != type이 보장됨)
        bool isRestaurantAreaTransition = isCurrentRestaurantRelated && willRestaurantBeActive && (prevType != type);

        // 이전 믹서 그룹 페이드 아웃
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;

            // 이전 타입에 따른 페이드 아웃 (prevType 사용)
            if (prevType == EffectType.Restaurant)
            {
                // Restaurant 타입에서 변경 시 하위 믹서도 함께 페이드 아웃 (레스토랑 타입 간 이동이 아닐 경우에만)
                if (!willRestaurantBeActive)
                {
                    float dbValue = Mathf.Lerp(0, -80, t);
                    _audioMixer.SetFloat("Restaurant", dbValue);
                    _audioMixer.SetFloat("Hall1", dbValue);
                    _audioMixer.SetFloat("Hall2", dbValue);
                    _audioMixer.SetFloat("Hall3", dbValue);
                    _audioMixer.SetFloat("Kitchen1", dbValue);
                    _audioMixer.SetFloat("Kitchen2", dbValue);
                    _audioMixer.SetFloat("Kitchen3", dbValue);
                }
            }
            else if (IsHallType(prevType) || IsKitchenType(prevType))
            {
                if (isRestaurantAreaTransition)
                {
                    // 레스토랑 관련 타입 간 이동 시 Restaurant 그룹은 유지, 이전 개별 그룹만 페이드 아웃
                    float dbValue = Mathf.Lerp(0, -80, t);
                    if (prevType == EffectType.Hall1)
                        _audioMixer.SetFloat("Hall1", dbValue);
                    else if (prevType == EffectType.Hall2)
                        _audioMixer.SetFloat("Hall2", dbValue);
                    else if (prevType == EffectType.Hall3)
                        _audioMixer.SetFloat("Hall3", dbValue);
                    else if (prevType == EffectType.Kitchen1)
                        _audioMixer.SetFloat("Kitchen1", dbValue);
                    else if (prevType == EffectType.Kitchen2)
                        _audioMixer.SetFloat("Kitchen2", dbValue);
                    else if (prevType == EffectType.Kitchen3)
                        _audioMixer.SetFloat("Kitchen3", dbValue);
                }
                else if (!willRestaurantBeActive)
                {
                    // 레스토랑 관련 타입에서 벗어날 경우 모든 레스토랑 그룹 페이드 아웃
                    float dbValue = Mathf.Lerp(0, -80, t);
                    _audioMixer.SetFloat("Restaurant", dbValue);
                    _audioMixer.SetFloat("Hall1", dbValue);
                    _audioMixer.SetFloat("Hall2", dbValue);
                    _audioMixer.SetFloat("Hall3", dbValue);
                    _audioMixer.SetFloat("Kitchen1", dbValue);
                    _audioMixer.SetFloat("Kitchen2", dbValue);
                    _audioMixer.SetFloat("Kitchen3", dbValue);
                }
            }
            else if (prevType == EffectType.UI && !willUIBeActive)
            {
                float dbValue = Mathf.Lerp(0, -80, t);
                _audioMixer.SetFloat("UI", dbValue);
            }

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        // 페이드 아웃 후 초기화 (다음 페이드인 준비)
        if (isRestaurantAreaTransition)
        {
            // 레스토랑 관련 타입 간 전환 시: Restaurant 그룹은 유지하고, 이전 타입(prevType)만 -80dB로 설정
            if (prevType == EffectType.Hall1)
                _audioMixer.SetFloat("Hall1", -80f);
            else if (prevType == EffectType.Hall2)
                _audioMixer.SetFloat("Hall2", -80f);
            else if (prevType == EffectType.Hall3)
                _audioMixer.SetFloat("Hall3", -80f);
            else if (prevType == EffectType.Kitchen1)
                _audioMixer.SetFloat("Kitchen1", -80f);
            else if (prevType == EffectType.Kitchen2)
                _audioMixer.SetFloat("Kitchen2", -80f);
            else if (prevType == EffectType.Kitchen3)
                _audioMixer.SetFloat("Kitchen3", -80f);
            
            _audioMixer.SetFloat("UI", -80f);
        }
        else
        {
            // 레스토랑 타입이 아니거나 레스토랑에서 벗어날 경우: 모든 그룹 초기화
            _audioMixer.SetFloat("Restaurant", -80f);
            _audioMixer.SetFloat("Hall1", -80f);
            _audioMixer.SetFloat("Hall2", -80f);
            _audioMixer.SetFloat("Hall3", -80f);
            _audioMixer.SetFloat("Kitchen1", -80f);
            _audioMixer.SetFloat("Kitchen2", -80f);
            _audioMixer.SetFloat("Kitchen3", -80f);
            _audioMixer.SetFloat("UI", -80f);
        }

        // 새 타입 페이드 인 부분 수정
        timer = 0;
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;
            float dbValue = Mathf.Lerp(-80, 0, t);

            // 새 타입에 따른 믹서 그룹 페이드 인
            if (IsRestaurantAreaType(type))
            {
                // Restaurant 그룹 페이드 인 (레스토랑 관련 타입 간 전환이 아닐 경우에만)
                if (!isRestaurantAreaTransition)
                {
                    _audioMixer.SetFloat("Restaurant", dbValue);
                }
                else
                {
                    // 레스토랑 관련 타입 간 전환 시에도 레스토랑 볼륨을 유지하도록 설정
                    _audioMixer.SetFloat("Restaurant", 0f);
                }

                // 세부 타입별 설정
                if (type == EffectType.Hall1)
                {
                    _audioMixer.SetFloat("Hall1", dbValue);
                }
                else if (type == EffectType.Hall2)
                {
                    _audioMixer.SetFloat("Hall2", dbValue);
                }
                else if (type == EffectType.Hall3)
                {
                    _audioMixer.SetFloat("Hall3", dbValue);
                }
                else if (type == EffectType.Kitchen1)
                {
                    _audioMixer.SetFloat("Kitchen1", dbValue);
                }
                else if (type == EffectType.Kitchen2)
                {
                    _audioMixer.SetFloat("Kitchen2", dbValue);
                }
                else if (type == EffectType.Kitchen3)
                {
                    _audioMixer.SetFloat("Kitchen3", dbValue);
                }
                else if (type == EffectType.Restaurant)
                {
                    _audioMixer.SetFloat("Restaurant", dbValue);
                }
            }
            else if (type == EffectType.UI)
            {
                _audioMixer.SetFloat("UI", dbValue);
            }

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        // 최종 믹서 그룹 설정
        if (IsRestaurantAreaType(type))
        {
            // Restaurant 믹서는 항상 활성화 (레스토랑 관련 타입일 때)
            _audioMixer.SetFloat("Restaurant", 0f);

            _audioMixer.SetFloat("Hall1", type == EffectType.Hall1 ? 0f : -80f);
            _audioMixer.SetFloat("Hall2", type == EffectType.Hall2 ? 0f : -80f);
            _audioMixer.SetFloat("Hall3", type == EffectType.Hall3 ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen1", type == EffectType.Kitchen1 ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen2", type == EffectType.Kitchen2 ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen3", type == EffectType.Kitchen3 ? 0f : -80f);

            _audioMixer.SetFloat("UI", -80f);
        }
        else if (type == EffectType.UI)
        {
            _audioMixer.SetFloat("Restaurant", -80f);
            _audioMixer.SetFloat("Hall1", -80f);
            _audioMixer.SetFloat("Hall2", -80f);
            _audioMixer.SetFloat("Hall3", -80f);
            _audioMixer.SetFloat("Kitchen1", -80f);
            _audioMixer.SetFloat("Kitchen2", -80f);
            _audioMixer.SetFloat("Kitchen3", -80f);
            _audioMixer.SetFloat("UI", 0);
        }
    }

    private IEnumerator IEStopEffectAudio(float duration)
    {
        float changeDuration = duration;
        float timer = 0;
        RetireFinishedEffects();
        var startingVolumes = new Dictionary<AudioSource, float>();
        foreach (var pair in _effectAudioDic)
            foreach (var source in pair.Value)
                if (source != null && (source.isPlaying || _effectVoices.ContainsKey(source)))
                    startingVolumes[source] = source.volume;

        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;
            foreach (var pair in startingVolumes)
                if (pair.Key != null)
                    pair.Key.volume = Mathf.Min(pair.Key.volume, Mathf.Lerp(pair.Value, 0, t));

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        foreach (var pair in _effectAudioDic)
        {
            foreach (var source in pair.Value)
            {
                StopPooledEffect(source);
                source.volume = _effectVolume;
            }
        }
    }

    // ? 각 EffectType에 맞는 AudioMixerGroup 반환
    private AudioMixerGroup GetAudioMixerGroup(EffectType effectType)
    {
        string groupName = "";
        
        switch (effectType)
        {
            case EffectType.None:
                groupName = "SoundEffect";
                break;
                
            case EffectType.Hall1:
                groupName = "Hall1";
                break;
                
            case EffectType.Hall2:
                groupName = "Hall2";
                break;
                
            case EffectType.Hall3:
                groupName = "Hall3";
                break;
                
            case EffectType.Kitchen1:
                groupName = "Kitchen1";
                break;
                
            case EffectType.Kitchen2:
                groupName = "Kitchen2";
                break;
                
            case EffectType.Kitchen3:
                groupName = "Kitchen3";
                break;
                
            case EffectType.Restaurant:
                groupName = "Restaurant";
                break;
                
            case EffectType.UI:
                groupName = "UI";
                break;
                
            default:
                groupName = "SoundEffect";
                break;
        }

        // ? FindMatchingGroups는 그룹 이름만으로 검색 가능
        AudioMixerGroup[] groups = _audioMixer.FindMatchingGroups(groupName);
        
        if (groups != null && groups.Length > 0)
        {
            return groups[0];
        }
        else
        {
            return _audioMixer.FindMatchingGroups("SoundEffect")[0];
        }
    }

    private IEnumerator IEPeriodicVolumeReset()
    {
        while (true)
        {
            yield return YieldCache.WaitForSeconds(VolumeResetInterval);
            ForceReapplyMixerState();
        }
    }

    // 60초 주기 리셋용: 재생 중인 효과음이 있으면 보류
    private void ForceReapplyMixerState()
    {
        foreach (var pair in _effectAudioDic)
            foreach (var source in pair.Value)
                if (source.isPlaying)
                    return;

        if (_changeEffectTypeRoutine != null)
        {
            StopCoroutine(_changeEffectTypeRoutine);
            _changeEffectTypeRoutine = null;
        }

        DebugLog.Log("[SoundManager] AudioMixer 주기 재설정: " + _savedEffectType);
        _effectType = _savedEffectType;
        LoadSoundData();
        ChangePlayEffectType(_savedEffectType);
    }

    // 포커스/pause 복귀용: 믹서가 깨진 상태이므로 재생 중 여부 무관하게 즉시 적용
    private void ForceReapplyMixerStateImmediate()
    {
        DebugLog.Log("[SoundManager] AudioMixer 즉시 강제 재설정: " + _savedEffectType);

        if (_changeEffectTypeRoutine != null)
        {
            StopCoroutine(_changeEffectTypeRoutine);
            _changeEffectTypeRoutine = null;
        }

        _effectType = _savedEffectType;
        LoadSoundData();
        ChangePlayEffectType(_savedEffectType);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ForceReapplyMixerStateImmediate();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            ForceReapplyMixerStateImmediate();
    }
}

