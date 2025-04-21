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
    Hall,
    Kitchen,
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

    private EffectType _effectType = EffectType.Hall;
    public EffectType EffectType => _effectType;

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
        LoadSoundData();
    }

    private void Init()
    {
        _effectAudioDic.Clear();
        _audioMixer = Resources.Load<AudioMixer>("Audio/AudioMixer");
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
        _audios[(int)AudioType.BackgroundAudio].outputAudioMixerGroup = _audioMixer.FindMatchingGroups("Master")[1];

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

                AudioMixerGroup[] groups = _audioMixer.FindMatchingGroups("SoundEffect/" + effectTypeName);

                if (groups != null && groups.Length > 0)
                {
                    audioSource.outputAudioMixerGroup = groups[0];
                }
                else
                {
                    audioSource.outputAudioMixerGroup = _audioMixer.FindMatchingGroups("SoundEffect")[0];
                }

                _effectAudioDic[effectType].Add(audioSource);
            }        
        }
    }

    public void LoadSoundData()
    {
        float backgroundVolume = PlayerPrefs.HasKey("BackgroundVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("BackgroundVolume"), 0, 1) : 1;
        float soundEffectVolume = PlayerPrefs.HasKey("SoundEffectVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("SoundEffectVolume"), 0, 1) : 1;
        bool isVibration = PlayerPrefs.GetInt("IsVibration", 0) == 1;

        _audioMixer.SetFloat("Master", 1);
        _audioMixer.SetFloat("Background", backgroundVolume);
        _audioMixer.SetFloat("SoundEffect", soundEffectVolume);

        SetVolume(1, AudioType.Master);
        SetVolume(backgroundVolume, AudioType.BackgroundAudio);
        SetVolume(soundEffectVolume, AudioType.EffectAudio);
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
        PlayerPrefs.SetInt("IsVibration", value ? 1 : 0);
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
        if (_changeEffectTypeRoutine != null)
            StopCoroutine(_changeEffectTypeRoutine);

        _effectType = type;

        // Restaurant 그룹 볼륨 설정 (UI 또는 기타일 경우 0, Hall/Kitchen/Restaurant일 경우 1)
        bool isRestaurantArea = (type == EffectType.Hall || type == EffectType.Kitchen || type == EffectType.Restaurant);
        _audioMixer.SetFloat("Restaurant", isRestaurantArea ? 0f : -80f);

        // UI 그룹 볼륨 설정 (UI일 경우 1, 그 외 0)
        _audioMixer.SetFloat("UI", type == EffectType.UI ? 0f : -80f);

        // 세부 그룹 설정
        if (type == EffectType.Restaurant)
        {
            // Restaurant 타입이면 모든 하위 그룹 활성화
            _audioMixer.SetFloat("Hall", 0f);
            _audioMixer.SetFloat("Kitchen", 0f);
        }
        else if (type == EffectType.Hall || type == EffectType.Kitchen)
        {
            // 특정 구역만 활성화, 다른 구역은 음소거
            _audioMixer.SetFloat("Hall", type == EffectType.Hall ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen", type == EffectType.Kitchen ? 0f : -80f);
        }
    }

    public void ChangePlayEffectType(EffectType type, float duration = 0.02f)
    {
        if (_effectType == type)
        {
            DebugLog.Log("현재 재생중인 효과음 타입과 동일한 타입을 재생 시도 했습니다: " + type.ToString());
            return;
        }

        duration = Mathf.Max(0.02f, duration);

        if (_changeEffectTypeRoutine != null)
            StopCoroutine(_changeEffectTypeRoutine);

        _effectType = type;
        _changeEffectTypeRoutine = StartCoroutine(IEChangeEffectType(type, duration));
    }

    public void PlayEffectAudio(EffectType type, AudioClip clip, float waitTime = 0)
    {
        if (clip == null)
        {
            DebugLog.LogError("재생할 효과음이 없습니다: " + type.ToString());
            return;
        }

        if (_delayPlayEffectAudioRoutine != null)
            StopCoroutine(_delayPlayEffectAudioRoutine);

        if (waitTime == 0)
        {
            bool isRestaurantRelated = (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen);
            bool isCurrentRestaurantRelated = (_effectType == EffectType.Restaurant || _effectType == EffectType.Hall || _effectType == EffectType.Kitchen);

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
                availableSource.clip = clip;
                availableSource.volume = _effectVolume;
                availableSource.Play();
            }
        }
        else
        {
            _delayPlayEffectAudioRoutine = StartCoroutine(IEDelayPlayEffectAudio(type, clip, waitTime));
        }
    }

    private AudioSource GetAvailableAudioSource(EffectType type)
    {
        if (!_effectAudioDic.ContainsKey(type))
        {
            DebugLog.LogError("등록되지 않은 EffectType입니다: " + type.ToString());
            return null;
        }

        // 해당 타입의 오디오 소스 풀에서 현재 재생 중이지 않은 소스 찾기
        foreach (AudioSource source in _effectAudioDic[type])
        {
            if (!source.isPlaying)
                return source;
        }

        // 모든 소스가 재생 중이면 가장 오래된 소스 선택 (첫 번째 소스 반환)
        return _effectAudioDic[type][0];
    }

    public void PlayEffectAudio(EffectType type, SoundEffectType soundEffectType)
    {
        if (type != EffectType.None && type != _effectType)
        {
            DebugLog.Log("현재 재생 가능한 타입이 아닙니다: (현재 타입: " + _effectType + ", 요청 타입: " + type + ")");
            return;
        }

        AudioClip clip = _clips[(int)soundEffectType];
        if (clip == null)
        {
            DebugLog.LogError("재생할 효과음이 없습니다: " + soundEffectType.ToString());
            return;
        }

        // 해당 타입의 오디오 소스 풀에서 재생 가능한 소스 찾기
        AudioSource availableSource = GetAvailableAudioSource(type);
        if (availableSource != null)
        {
            availableSource.clip = clip;
            availableSource.volume = _effectVolume;
            availableSource.Play();
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
            _audios[(int)AudioType.EffectAudio].Stop();
            return;
        }

        _stopEffectAudioRoutine = StartCoroutine(IEStopBackgroundAudio(duration));
    }

    public void SetVolume(float value, AudioType type)
    {
        value = Mathf.Clamp(value, 0.0f, 1.0f);
        float volume = value != 0 ? Mathf.Log10(value) * 20 : -80;

        switch (type)
        {
            case AudioType.Master:
                _audioMixer.SetFloat("Master", volume);
                SaveSoundData("Master", value);
                break;

            case AudioType.BackgroundAudio:
                _audioMixer.SetFloat("Background", volume);
                SaveSoundData("Background", value);
                break;

            case AudioType.EffectAudio:
                _audioMixer.SetFloat("SoundEffect", volume);
                SaveSoundData("SoundEffect", value);
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

        bool isRestaurantRelated = (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen);
        bool isCurrentRestaurantRelated = (_effectType == EffectType.Restaurant || _effectType == EffectType.Hall || _effectType == EffectType.Kitchen);

        // 다음 조건 중 하나만 충족하면 재생 가능:
        // 1. type이 None (모든 곳에서 재생 가능한 기본 효과음)
        // 2. type이 현재 활성화된 타입과 일치
        // 3. type과 현재 타입 모두 레스토랑 관련 타입(Restaurant, Hall, Kitchen)
        if (type != EffectType.None && type != _effectType && !(isRestaurantRelated && isCurrentRestaurantRelated))
        {
            DebugLog.Log("현재 재생 가능한 타입이 아닙니다: (현재 타입: " + _effectType + ", 요청 타입: " + type + ")");
            yield break;
        }

        // 지연 후 재생 가능한 오디오 소스 찾기
        AudioSource availableSource = GetAvailableAudioSource(type);
        if (availableSource != null)
        {
            availableSource.clip = clip;
            availableSource.volume = _effectVolume;
            availableSource.Play();
        }
    }

    private IEnumerator IEChangeEffectType(EffectType type, float duration)
    {
        float changeDuration = duration * 0.5f;
        float timer = 0;

        // Restaurant 카테고리가 활성화될지 여부
        bool willRestaurantBeActive = (type == EffectType.Hall || type == EffectType.Kitchen || type == EffectType.Restaurant);
        bool willUIBeActive = (type == EffectType.UI);

        // 현재 타입이 레스토랑 관련 타입인지 확인
        bool isCurrentRestaurantRelated = (_effectType == EffectType.Hall || _effectType == EffectType.Kitchen || _effectType == EffectType.Restaurant);

        // Hall <-> Kitchen 전환인지 확인 (둘 다 레스토랑 관련 타입)
        bool isHallKitchenTransition = isCurrentRestaurantRelated && willRestaurantBeActive &&
                                      (_effectType != type) &&
                                      (_effectType != EffectType.Restaurant && type != EffectType.Restaurant);

        // 이전 믹서 그룹 페이드 아웃
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;

            // 현재 활성화된 타입에 따른 페이드 아웃
            if (_effectType == EffectType.Restaurant)
            {
                // Restaurant 타입에서 변경 시 하위 믹서도 함께 페이드 아웃 (레스토랑 타입 간 이동이 아닐 경우에만)
                if (!willRestaurantBeActive)
                {
                    float dbValue = Mathf.Lerp(0, -80, t);
                    _audioMixer.SetFloat("Restaurant", dbValue);
                    _audioMixer.SetFloat("Hall", dbValue);
                    _audioMixer.SetFloat("Kitchen", dbValue);
                }
            }
            else if (_effectType == EffectType.Hall || _effectType == EffectType.Kitchen)
            {
                if (isHallKitchenTransition)
                {
                    // 홀-주방 간 이동 시 Restaurant 그룹은 유지, 개별 그룹만 변경
                    float dbValue = Mathf.Lerp(0, -80, t);
                    if (_effectType == EffectType.Hall)
                        _audioMixer.SetFloat("Hall", dbValue);
                    else
                        _audioMixer.SetFloat("Kitchen", dbValue);
                }
                else if (!willRestaurantBeActive)
                {
                    // 레스토랑 관련 타입에서 벗어날 경우 모든 레스토랑 그룹 페이드 아웃
                    float dbValue = Mathf.Lerp(0, -80, t);
                    _audioMixer.SetFloat("Restaurant", dbValue);
                    _audioMixer.SetFloat("Hall", dbValue);
                    _audioMixer.SetFloat("Kitchen", dbValue);
                }
            }
            else if (_effectType == EffectType.UI && !willUIBeActive)
            {
                float dbValue = Mathf.Lerp(0, -80, t);
                _audioMixer.SetFloat("UI", dbValue);
            }

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        // 페이드 아웃 후 모든 그룹 초기화 (다음 페이드인 준비)
        // 단, 홀-주방 전환 시 Restaurant는 유지
        if (!isHallKitchenTransition)
        {
            _audioMixer.SetFloat("Restaurant", -80f);
        }
        _audioMixer.SetFloat("Hall", -80f);
        _audioMixer.SetFloat("Kitchen", -80f);
        _audioMixer.SetFloat("UI", -80f);

        // 새 타입 페이드 인 부분 수정
        timer = 0;
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;
            float dbValue = Mathf.Lerp(-80, 0, t);

            // 새 타입에 따른 믹서 그룹 페이드 인
            if (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen)
            {
                // Restaurant 그룹 페이드 인 (홀-주방 전환이 아닐 경우에만)
                if (!isHallKitchenTransition)
                {
                    _audioMixer.SetFloat("Restaurant", dbValue);
                }
                else
                {
                    // 홀-주방 전환 시에도 레스토랑 볼륨을 유지하도록 설정
                    _audioMixer.SetFloat("Restaurant", 0f);
                }

                // 세부 타입별 설정
                if (type == EffectType.Restaurant)
                {
                    _audioMixer.SetFloat("Hall", dbValue);
                    _audioMixer.SetFloat("Kitchen", dbValue);
                }
                else if (type == EffectType.Hall || type == EffectType.Kitchen)
                {
                    if (type == EffectType.Hall)
                        _audioMixer.SetFloat("Hall", dbValue);
                    else
                        _audioMixer.SetFloat("Kitchen", dbValue);

                    // 디버그 로그 추가하여 현재 볼륨 값이 어떻게 변하는지 확인
                    DebugLog.Log($"[SoundManager] 페이드 인 - 타입: {type}, 진행: {t:F2}, 볼륨: {dbValue:F2}");
                }
            }
            else if (type == EffectType.UI)
            {
                _audioMixer.SetFloat("UI", dbValue);
            }

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        // 최종 믹서 그룹 설정
        if (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen)
        {
            // Restaurant 믹서는 항상 활성화 (레스토랑 관련 타입일 때)
            _audioMixer.SetFloat("Restaurant", 0f);

            if (type == EffectType.Restaurant)
            {
                // Restaurant 타입이면 모든 하위 그룹 활성화
                _audioMixer.SetFloat("Hall", 0f);
                _audioMixer.SetFloat("Kitchen", 0f);
            }
            else if (type == EffectType.Hall || type == EffectType.Kitchen)
            {
                _audioMixer.SetFloat("Hall", type == EffectType.Hall ? 0f : -80f);
                _audioMixer.SetFloat("Kitchen", type == EffectType.Kitchen ? 0f : -80f);
            }

            _audioMixer.SetFloat("UI", -80f);
        }
        else if (type == EffectType.UI)
        {
            _audioMixer.SetFloat("Restaurant", -80f);
            _audioMixer.SetFloat("Hall", -80f);
            _audioMixer.SetFloat("Kitchen", -80f);
            _audioMixer.SetFloat("UI", 0);
        }
    }

    private IEnumerator IEStopEffectAudio(float duration)
    {
        float maxVolume = _audios[(int)AudioType.EffectAudio].volume;
        float changeDuration = duration;
        float timer = 0;

        while (timer < changeDuration)
        {
            timer += 0.02f;
            _audios[(int)AudioType.EffectAudio].volume = Mathf.Lerp(maxVolume, 0, timer / changeDuration);

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        _audios[(int)AudioType.BackgroundAudio].Stop();
    }
}

