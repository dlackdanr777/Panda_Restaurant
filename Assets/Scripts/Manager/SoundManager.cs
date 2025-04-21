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

    //��� ���� ����� ���� ��, �ٿ� ����� ���� ����
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

                // �⺻ ����� �ҽ� ����
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
            DebugLog.Log("���� Ŭ���� ���� �̸��� Ŭ���� ��� �õ� �߽��ϴ�: " + clip.name);
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

        // Restaurant �׷� ���� ���� (UI �Ǵ� ��Ÿ�� ��� 0, Hall/Kitchen/Restaurant�� ��� 1)
        bool isRestaurantArea = (type == EffectType.Hall || type == EffectType.Kitchen || type == EffectType.Restaurant);
        _audioMixer.SetFloat("Restaurant", isRestaurantArea ? 0f : -80f);

        // UI �׷� ���� ���� (UI�� ��� 1, �� �� 0)
        _audioMixer.SetFloat("UI", type == EffectType.UI ? 0f : -80f);

        // ���� �׷� ����
        if (type == EffectType.Restaurant)
        {
            // Restaurant Ÿ���̸� ��� ���� �׷� Ȱ��ȭ
            _audioMixer.SetFloat("Hall", 0f);
            _audioMixer.SetFloat("Kitchen", 0f);
        }
        else if (type == EffectType.Hall || type == EffectType.Kitchen)
        {
            // Ư�� ������ Ȱ��ȭ, �ٸ� ������ ���Ұ�
            _audioMixer.SetFloat("Hall", type == EffectType.Hall ? 0f : -80f);
            _audioMixer.SetFloat("Kitchen", type == EffectType.Kitchen ? 0f : -80f);
        }
    }

    public void ChangePlayEffectType(EffectType type, float duration = 0.02f)
    {
        if (_effectType == type)
        {
            DebugLog.Log("���� ������� ȿ���� Ÿ�԰� ������ Ÿ���� ��� �õ� �߽��ϴ�: " + type.ToString());
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
            DebugLog.LogError("����� ȿ������ �����ϴ�: " + type.ToString());
            return;
        }

        if (_delayPlayEffectAudioRoutine != null)
            StopCoroutine(_delayPlayEffectAudioRoutine);

        if (waitTime == 0)
        {
            bool isRestaurantRelated = (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen);
            bool isCurrentRestaurantRelated = (_effectType == EffectType.Restaurant || _effectType == EffectType.Hall || _effectType == EffectType.Kitchen);

            // ���� ���� �� �ϳ��� �����ϸ� ��� ����:
            // 1. type�� None (��� ������ ��� ������ �⺻ ȿ����)
            // 2. type�� ���� Ȱ��ȭ�� Ÿ�԰� ��ġ
            // 3. type�� ���� Ÿ�� ��� ������� ���� Ÿ��(Restaurant, Hall, Kitchen)
            if (type != EffectType.None && type != _effectType && !(isRestaurantRelated && isCurrentRestaurantRelated))
            {
                DebugLog.Log("���� ��� ������ Ÿ���� �ƴմϴ�: (���� Ÿ��: " + _effectType + ", ��û Ÿ��: " + type + ")");
                return;
            }

            // �ش� Ÿ���� ����� �ҽ� Ǯ���� ��� ������ �ҽ� ã��
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
            DebugLog.LogError("��ϵ��� ���� EffectType�Դϴ�: " + type.ToString());
            return null;
        }

        // �ش� Ÿ���� ����� �ҽ� Ǯ���� ���� ��� ������ ���� �ҽ� ã��
        foreach (AudioSource source in _effectAudioDic[type])
        {
            if (!source.isPlaying)
                return source;
        }

        // ��� �ҽ��� ��� ���̸� ���� ������ �ҽ� ���� (ù ��° �ҽ� ��ȯ)
        return _effectAudioDic[type][0];
    }

    public void PlayEffectAudio(EffectType type, SoundEffectType soundEffectType)
    {
        if (type != EffectType.None && type != _effectType)
        {
            DebugLog.Log("���� ��� ������ Ÿ���� �ƴմϴ�: (���� Ÿ��: " + _effectType + ", ��û Ÿ��: " + type + ")");
            return;
        }

        AudioClip clip = _clips[(int)soundEffectType];
        if (clip == null)
        {
            DebugLog.LogError("����� ȿ������ �����ϴ�: " + soundEffectType.ToString());
            return;
        }

        // �ش� Ÿ���� ����� �ҽ� Ǯ���� ��� ������ �ҽ� ã��
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

        // ���� ���� �� �ϳ��� �����ϸ� ��� ����:
        // 1. type�� None (��� ������ ��� ������ �⺻ ȿ����)
        // 2. type�� ���� Ȱ��ȭ�� Ÿ�԰� ��ġ
        // 3. type�� ���� Ÿ�� ��� ������� ���� Ÿ��(Restaurant, Hall, Kitchen)
        if (type != EffectType.None && type != _effectType && !(isRestaurantRelated && isCurrentRestaurantRelated))
        {
            DebugLog.Log("���� ��� ������ Ÿ���� �ƴմϴ�: (���� Ÿ��: " + _effectType + ", ��û Ÿ��: " + type + ")");
            yield break;
        }

        // ���� �� ��� ������ ����� �ҽ� ã��
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

        // Restaurant ī�װ��� Ȱ��ȭ���� ����
        bool willRestaurantBeActive = (type == EffectType.Hall || type == EffectType.Kitchen || type == EffectType.Restaurant);
        bool willUIBeActive = (type == EffectType.UI);

        // ���� Ÿ���� ������� ���� Ÿ������ Ȯ��
        bool isCurrentRestaurantRelated = (_effectType == EffectType.Hall || _effectType == EffectType.Kitchen || _effectType == EffectType.Restaurant);

        // Hall <-> Kitchen ��ȯ���� Ȯ�� (�� �� ������� ���� Ÿ��)
        bool isHallKitchenTransition = isCurrentRestaurantRelated && willRestaurantBeActive &&
                                      (_effectType != type) &&
                                      (_effectType != EffectType.Restaurant && type != EffectType.Restaurant);

        // ���� �ͼ� �׷� ���̵� �ƿ�
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;

            // ���� Ȱ��ȭ�� Ÿ�Կ� ���� ���̵� �ƿ�
            if (_effectType == EffectType.Restaurant)
            {
                // Restaurant Ÿ�Կ��� ���� �� ���� �ͼ��� �Բ� ���̵� �ƿ� (������� Ÿ�� �� �̵��� �ƴ� ��쿡��)
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
                    // Ȧ-�ֹ� �� �̵� �� Restaurant �׷��� ����, ���� �׷츸 ����
                    float dbValue = Mathf.Lerp(0, -80, t);
                    if (_effectType == EffectType.Hall)
                        _audioMixer.SetFloat("Hall", dbValue);
                    else
                        _audioMixer.SetFloat("Kitchen", dbValue);
                }
                else if (!willRestaurantBeActive)
                {
                    // ������� ���� Ÿ�Կ��� ��� ��� ��� ������� �׷� ���̵� �ƿ�
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

        // ���̵� �ƿ� �� ��� �׷� �ʱ�ȭ (���� ���̵��� �غ�)
        // ��, Ȧ-�ֹ� ��ȯ �� Restaurant�� ����
        if (!isHallKitchenTransition)
        {
            _audioMixer.SetFloat("Restaurant", -80f);
        }
        _audioMixer.SetFloat("Hall", -80f);
        _audioMixer.SetFloat("Kitchen", -80f);
        _audioMixer.SetFloat("UI", -80f);

        // �� Ÿ�� ���̵� �� �κ� ����
        timer = 0;
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;
            float dbValue = Mathf.Lerp(-80, 0, t);

            // �� Ÿ�Կ� ���� �ͼ� �׷� ���̵� ��
            if (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen)
            {
                // Restaurant �׷� ���̵� �� (Ȧ-�ֹ� ��ȯ�� �ƴ� ��쿡��)
                if (!isHallKitchenTransition)
                {
                    _audioMixer.SetFloat("Restaurant", dbValue);
                }
                else
                {
                    // Ȧ-�ֹ� ��ȯ �ÿ��� ������� ������ �����ϵ��� ����
                    _audioMixer.SetFloat("Restaurant", 0f);
                }

                // ���� Ÿ�Ժ� ����
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

                    // ����� �α� �߰��Ͽ� ���� ���� ���� ��� ���ϴ��� Ȯ��
                    DebugLog.Log($"[SoundManager] ���̵� �� - Ÿ��: {type}, ����: {t:F2}, ����: {dbValue:F2}");
                }
            }
            else if (type == EffectType.UI)
            {
                _audioMixer.SetFloat("UI", dbValue);
            }

            yield return YieldCache.WaitForSeconds(0.02f);
        }

        // ���� �ͼ� �׷� ����
        if (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen)
        {
            // Restaurant �ͼ��� �׻� Ȱ��ȭ (������� ���� Ÿ���� ��)
            _audioMixer.SetFloat("Restaurant", 0f);

            if (type == EffectType.Restaurant)
            {
                // Restaurant Ÿ���̸� ��� ���� �׷� Ȱ��ȭ
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

