using Muks.DataBind;
using System;
using System.Collections;
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
    Length
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
    private float _backgroundVolume = 0.5f;
    private float _effectVolume = 1f;

    private AudioSource[] _audios;
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


    private  void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void Start()
    {
        LoadSoundData();
    }


    private void Init()
    {
        _audioMixer = Resources.Load<AudioMixer>("Audio/AudioMixer");
        _clips = new AudioClip[(int)SoundEffectType.Length];
        for(int i = 0, cnt = (int)SoundEffectType.Length; i < cnt; ++i)
        {
            int index = i;
            _clips[i] = Resources.Load<AudioClip>("Audio/" + ((SoundEffectType)i).ToString());
            DataBind.SetUnityActionValue(((SoundEffectType)index).ToString(), () => PlayEffectAudio((SoundEffectType)index));
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
        _audios[((int)AudioType.EffectAudio)].volume = _effectVolume;
        _audios[(int)AudioType.EffectAudio].dopplerLevel = 0;
        _audios[(int)AudioType.EffectAudio].reverbZoneMix = 0;
        _audios[(int)AudioType.EffectAudio].outputAudioMixerGroup = _audioMixer.FindMatchingGroups("Master")[2];
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
        switch(audioType)
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
        if(clip == _currentBackgroundClip)
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


    public void PlayEffectAudio(AudioClip clip, float waitTime = 0)
    {
        if (_delayPlayEffectAudioRoutine != null)
            StopCoroutine(_delayPlayEffectAudioRoutine);

        if(waitTime == 0)
        {
            _audios[(int)AudioType.EffectAudio].volume = _effectVolume;
            _audios[(int)AudioType.EffectAudio].PlayOneShot(clip);
        }

        else
        {
            _delayPlayEffectAudioRoutine = StartCoroutine(IEDelayPlayEffectAudio(clip, waitTime));   
        }
    }


    public void PlayEffectAudio(SoundEffectType soundEffectType)
    {
        AudioClip clip = _clips[(int)soundEffectType];
        _audios[(int)AudioType.EffectAudio].volume = _effectVolume;
        _audios[(int)AudioType.EffectAudio].PlayOneShot(clip);
    }


    public void StopBackgroundAudio(float duration = 0)
    {
        if(_stopBackgroundAudioRoutine != null)
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


    private IEnumerator IEDelayPlayEffectAudio(AudioClip clip, float waitTime)
    {
        yield return YieldCache.WaitForSeconds(waitTime);
        _audios[(int)AudioType.EffectAudio].PlayOneShot(clip);
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

