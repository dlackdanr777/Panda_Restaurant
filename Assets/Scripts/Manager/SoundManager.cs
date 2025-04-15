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
    private Dictionary<EffectType, AudioSource> _effectAudioDic = new Dictionary<EffectType, AudioSource>();
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

    // private void Start()
    // {
    //     ChangePlayEffectType(_effectType);
    // }

    private void Init()
    {
        _audioMixer = Resources.Load<AudioMixer>("Audio/AudioMixer");
        _clips = new AudioClip[(int)SoundEffectType.Length];
        for (int i = 0, cnt = (int)SoundEffectType.Length; i < cnt; ++i)
        {
            int index = i;
            _clips[i] = Resources.Load<AudioClip>("Audio/" + ((SoundEffectType)i).ToString());
            DataBind.SetUnityActionValue(((SoundEffectType)index).ToString(), () => PlayEffectAudio(EffectType.None, (SoundEffectType)index));
        }

        _audios = new AudioSource[(int)AudioType.Count];

        for (int i = (int)AudioType.BackgroundAudio, count = (int)AudioType.Count; i < count; i++)
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

        _audios[(int)AudioType.EffectAudio].loop = false;
        _audios[(int)AudioType.EffectAudio].playOnAwake = false;
        _audios[(int)AudioType.EffectAudio].volume = _effectVolume;
        _audios[(int)AudioType.EffectAudio].dopplerLevel = 0;
        _audios[(int)AudioType.EffectAudio].reverbZoneMix = 0;
        _audios[(int)AudioType.EffectAudio].outputAudioMixerGroup = _audioMixer.FindMatchingGroups("Master")[2];
        _effectAudioDic.Add(EffectType.None, _audios[(int)AudioType.EffectAudio]);

        for (int i = (int)EffectType.None + 1, cnt = (int)EffectType.UI + 1; i < cnt; ++i)
        {
            EffectType effectType = (EffectType)i;
            string effectTypeName = Enum.GetName(typeof(EffectType), effectType);

            GameObject obj = new GameObject(effectTypeName);
            obj.transform.parent = transform;
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
                //Debug.Log($"[SoundManager] {effectTypeName} ȿ������ {effectTypeName} �ͼ� �׷� �����");
            }
            else
            {
                audioSource.outputAudioMixerGroup = _audioMixer.FindMatchingGroups("SoundEffect")[0];
                //Debug.LogWarning($"[SoundManager] {effectTypeName} �ͼ� �׷��� ã�� ���� �⺻ SoundEffect �׷� ���");
            }

            _effectAudioDic.Add(effectType, audioSource);
        }

    }

    public void LoadSoundData()
    {
        //float masterVolume = PlayerPrefs.HasKey("MasterVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("MasterVolume"), 0, 1) : 1;
        //GameManager.Instance.Option.SetVolume(AudioType.Master, masterVolume);

        float backgroundVolume = PlayerPrefs.HasKey("BackgroundVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("BackgroundVolume"), 0, 1) : 1;
        //GameManager.Instance.Option.SetVolume(AudioType.BackgroundAudio, backgroundVolume);

        float soundEffectVolume = PlayerPrefs.HasKey("SoundEffectVolume") ? Mathf.Clamp(PlayerPrefs.GetFloat("SoundEffectVolume"), 0, 1) : 1;
        //GameManager.Instance.Option.SetVolume(AudioType.EffectAudio, soundEffectVolume);

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
        // Restaurant �׷� ���� ���� (UI �Ǵ� ��Ÿ�� ��� 0, Hall/Kitchen�� ��� 1)
        bool isRestaurantArea = (type == EffectType.Hall || type == EffectType.Kitchen || type == EffectType.Restaurant);
        _audioMixer.SetFloat("Restaurant", isRestaurantArea ? 0f : -80f);

        // UI �׷� ���� ���� (UI�� ��� 1, �� �� 0)
        _audioMixer.SetFloat("SoundEffect/UI", type == EffectType.UI ? 0f : -80f);
        _audioMixer.SetFloat("UI", type == EffectType.UI ? 0f : -80f);

        // �� ���� ȿ�� Ÿ�� ����
        foreach (var data in _effectAudioDic)
        {
            if (data.Key == EffectType.None)
            {
                // None Ÿ���� �⺻ ���� ����
                data.Value.volume = _effectVolume;
                continue;
            }
            // Restaurant ����� �ҽ��� Hall, Kitchen �Ǵ� Restaurant Ÿ���� �� Ȱ��ȭ
            else if (data.Key == EffectType.Restaurant)
            {
                data.Value.volume = isRestaurantArea ? _effectVolume : 0;
            }
            // Ȱ��ȭ�� Ư�� Ÿ�� (���� ���õ� Ÿ��)
            else if (data.Key == type)
            {
                data.Value.volume = _effectVolume;

                // ������� ���� Ÿ���� ��� �߰� �ͼ� ����
                if (type == EffectType.Restaurant)
                {
                    // Restaurant Ÿ���� Ȱ��ȭ�Ǹ� Hall�� Kitchen ��� Ȱ��ȭ
                    _audioMixer.SetFloat("Hall", 0);
                    _audioMixer.SetFloat("Kitchen", 0);
                }
                else if (type == EffectType.Hall || type == EffectType.Kitchen)
                {
                    // Ư�� ������ Ȱ��ȭ
                    _audioMixer.SetFloat("Hall", type == EffectType.Hall ? 0f : -80f);
                    _audioMixer.SetFloat("Kitchen", type == EffectType.Kitchen ? 0f : -80f);
                }
            }
            else
            {
                // ��Ȱ��ȭ�� Ÿ���� ���� ����
                data.Value.volume = 0;
            }
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
            _effectAudioDic[type].volume = _effectVolume;
            _effectAudioDic[type].PlayOneShot(clip);
        }

        else
        {
            _delayPlayEffectAudioRoutine = StartCoroutine(IEDelayPlayEffectAudio(type, clip, waitTime));
        }
    }

    public void PlayEffectAudio(EffectType type, SoundEffectType soundEffectType)
    {
        if (type != EffectType.None && type != _effectType)
        {
            DebugLog.Log("���� ��� ������ Ÿ���� �ƴմϴ�: (���� Ÿ��: " + _effectType + ", ��û Ÿ��: " + type + ")");
            return;
        }

        AudioClip clip = _clips[(int)soundEffectType];
        _effectAudioDic[type].volume = _effectVolume;
        _effectAudioDic[type].PlayOneShot(clip);
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
        if (type != EffectType.None && type != _effectType)
        {
            DebugLog.Log("���� ��� ������ Ÿ���� �ƴմϴ�: (���� Ÿ��: " + _effectType + ", ��û Ÿ��: " + type + ")");
            yield break;
        }

        _effectAudioDic[type].PlayOneShot(clip);
    }

    private IEnumerator IEChangeEffectType(EffectType type, float duration)
    {
        float maxVolume = _effectVolume;
        float changeDuration = duration * 0.5f;
        float timer = 0;

        // Restaurant ī�װ��� Ȱ��ȭ���� ����
        bool willRestaurantBeActive = (type == EffectType.Hall || type == EffectType.Kitchen || type == EffectType.Restaurant);
        bool willUIBeActive = (type == EffectType.UI);
        
        // ���� Ÿ�� ���̵� �ƿ�
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;

            // AudioSource ���� ���̵� �ƿ�
            foreach (var data in _effectAudioDic)
            {
                if (data.Key == EffectType.None || data.Value.volume <= 0)
                    continue;
                    
                data.Value.volume = Mathf.Lerp(maxVolume, 0, t);
            }
            
            // ���� Ȱ��ȭ�� Restaurant/UI �׷��� ���̵� �ƿ� (�ʿ��)
            if (_effectType == EffectType.Restaurant || _effectType == EffectType.Hall || _effectType == EffectType.Kitchen)
            {
                if (!willRestaurantBeActive) // ���� ���°� Restaurant ������ �ƴ� ���
                {
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

        // ��� AudioSource ���� �ʱ�ȭ
        foreach (var data in _effectAudioDic)
        {
            if (data.Key != EffectType.None)
                data.Value.volume = 0;
        }

        // ���̵� �ƿ� �� ��� �׷� ���Ұ�
        _audioMixer.SetFloat("Restaurant", -80f);
        _audioMixer.SetFloat("Hall", -80f);
        _audioMixer.SetFloat("Kitchen", -80f);
        _audioMixer.SetFloat("UI", -80f);
        
        // �� Ÿ�� ���̵� �� �غ�
        _effectAudioDic[type].volume = 0;
        
        // �� Ÿ�� ���̵� ��
        timer = 0;
        while (timer < changeDuration)
        {
            timer += 0.02f;
            float t = timer / changeDuration;
            
            // AudioSource ���� ���̵� ��
            _effectAudioDic[type].volume = Mathf.Lerp(0, maxVolume, t);
            
            // �� Ÿ�Կ� ���� �ͼ� �׷� ���̵� ��
            if (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen)
            {
                float dbValue = Mathf.Lerp(-80, 0, t);
                
                // Restaurant �׷� �׻� Ȱ��ȭ
                _audioMixer.SetFloat("Restaurant", dbValue);
                // Restaurant Ÿ���̸� ��� ���� �׷� Ȱ��ȭ
                if (type == EffectType.Restaurant)
                {
                    _audioMixer.SetFloat("Hall", dbValue);
                    _audioMixer.SetFloat("Kitchen", dbValue);
                }
                // Ư�� Ÿ��(Hall/Kitchen)�̸� �ش� Ÿ�Ը� Ȱ��ȭ
                else if (type == EffectType.Hall || type == EffectType.Kitchen)
                {
                    _audioMixer.SetFloat("Hall", type == EffectType.Hall ? dbValue : -80f);
                    _audioMixer.SetFloat("Kitchen", type == EffectType.Kitchen ? dbValue : -80f);
                }
            }
            else if (type == EffectType.UI)
            {
                float dbValue = Mathf.Lerp(-80, 0, t);
                _audioMixer.SetFloat("UI", dbValue);
            }
            
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        
        // ���� ���� ����
        if (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen)
        {
            // Ȱ��ȭ�� Ÿ��
            _effectAudioDic[type].volume = maxVolume;
            
            // Restaurant Ÿ�Ե� Ȱ��ȭ (Hall/Kitchen/Restaurant Ÿ���� ��)
            if (_effectAudioDic.TryGetValue(EffectType.Restaurant, out var restaurantSource))
            {
                restaurantSource.volume = maxVolume;
            }
            
            // �ٸ� Ÿ���� ��Ȱ��ȭ
            foreach (var data in _effectAudioDic)
            {
                if (data.Key != EffectType.None && 
                    data.Key != type && 
                    data.Key != EffectType.Restaurant)
                {
                    data.Value.volume = 0;
                }
            }
        }
        else if (type == EffectType.UI)
        {
            // UI Ÿ�Ը� Ȱ��ȭ, �ٸ� Ÿ���� ��Ȱ��ȭ
            foreach (var data in _effectAudioDic)
            {
                if (data.Key == EffectType.None)
                    continue;
                    
                data.Value.volume = (data.Key == type) ? maxVolume : 0;
            }
        }
        
        // ���� �ͼ� �׷� ����
        if (type == EffectType.Restaurant || type == EffectType.Hall || type == EffectType.Kitchen)
        {
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

