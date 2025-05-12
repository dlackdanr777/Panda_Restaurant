using System;
using System.Collections;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public struct FeverRewardConfig
{
    private int _targetTouchCount;
    public int TargetTouchCount => _targetTouchCount;

    private int _rewardValue;
    public int RewardValue => _rewardValue;

    private MoneyType _rewardType;
    public MoneyType RewardType => _rewardType;

    public FeverRewardConfig(int touchCount, int rewardValue, MoneyType type)
    {
        _targetTouchCount = touchCount;
        _rewardValue = rewardValue;
        _rewardType = type;
    }

    public bool IsDefault()
    {
        return _targetTouchCount <= 0;
    }
}

public class MiniGameFever : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject[] _feverObjects;
    [SerializeField] private Button[] _touchButtons;
    [SerializeField] private MiniGameTimer _timer;
    [SerializeField] private AudioSource _timerAudio;
    [SerializeField] private MiniGame_GaugeBar _miniGame_GaugeBar;
    [SerializeField] private MiniGameStartTimer _startTimer;
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Button _screenTouchButton;

    [SerializeField] private Animator _jarAnimator;
    [SerializeField] private Animator _panda3Animator;

    [Space]
    [Header("Reward")]
    [SerializeField] private UIImageAndText _rewardImage;
    [SerializeField] private Sprite _smallGoldSprite;
    [SerializeField] private Sprite _smallDiaSprite;
    [SerializeField] private Sprite _middleGoldSprite;
    [SerializeField] private Sprite _middleDiaSprite;
    [SerializeField] private Sprite _bigGoldSprite;
    [SerializeField] private Sprite _bigDiaSprite;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _bgSound;
    [SerializeField] private AudioClip _startSound;
    [SerializeField] private AudioClip _endSound;
    [SerializeField] private AudioClip _touchSound;



    private bool _roundEnd = false;
    private Action _onComplete;
    private UIMiniGameController _uiMiniGameController;

    //보상 관련 필드
    private FeverRewardConfig _feverRewardConfig;
    private int _touchCount = 0;

    // 타이머 관련 필드
    private float _remainingTime;
    private bool _isTimerRunning;

    //터치 관련 필드
    private bool _isScreenTouch = false;

    private float _currentPower;


    public void Show(FeverRewardConfig feverRewardConfig, Action onComplete = null)
    {
        if(feverRewardConfig.IsDefault())
        {
            Debug.LogError("FeverRewardConfig is default.");
            return;
        }
        _feverRewardConfig = feverRewardConfig;
        _onComplete = onComplete;

        gameObject.SetActive(true);
        _dontTouchArea.SetActive(true);
        _screenTouchButton.gameObject.SetActive(false);
        ResetState();
        StopEffects();
        StartCoroutine(Play());
    }

    public void Hide()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    private void Awake()
    {

    }

    private void Update()
    {
        UpdateTimer();

        if(_roundEnd)
            return;


    }


    public void Init(UIMiniGameController uiMiniGameController)
    {
        _uiMiniGameController = uiMiniGameController;
        ResetState();
        _miniGame_GaugeBar.Init();
        _startTimer.Init();
        _screenTouchButton.onClick.AddListener(OnScreenTouchButtonClicked);
        StopEffects();

        for(int i = 0, cnt = _touchButtons.Length; i < cnt; i++)
        {
            _touchButtons[i].onClick.RemoveAllListeners();
            _touchButtons[i].onClick.AddListener(OnTouchEvent);
        }
    }

    private void ResetState()
    {
        _isTimerRunning = false;
        _roundEnd = false;
        _isScreenTouch = false;
        _remainingTime = 0;
        _touchCount = 0;
        _currentPower = 0;

        _timer.ResetTimer();
        _timer.SetClearSprite();
        _miniGame_GaugeBar.ResetScore();
        _miniGame_GaugeBar.SetClearSprite();

        _jarAnimator.SetFloat("StickSpeed", -1);
        _panda3Animator.SetFloat("StickSpeed", -1);

        _rewardImage.gameObject.SetActive(false);
    }

    private void StopEffects()
    {
        for(int i = 0, cnt = _feverObjects.Length; i < cnt; i++)
        {
            _feverObjects[i].SetActive(false);
        }
    }

    private void StartEffects()
    {
        for (int i = 0, cnt = _feverObjects.Length; i < cnt; i++)
        {
            _feverObjects[i].SetActive(true);
        }
    }

    private IEnumerator Play()
    {
        _dontTouchArea.SetActive(true);
        SoundManager.Instance.StopBackgroundAudio();
        yield return YieldCache.WaitForSeconds(0.5f);
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _startSound);
        StopEffects();
        yield return YieldCache.WaitForSeconds(2f);
        yield return StartCoroutine(_startTimer.StartTimer());

        SoundManager.Instance.PlayBackgroundAudio(_bgSound, 0.5f);
        _dontTouchArea.SetActive(false);
        StartTimer();
        StartEffects();
        yield return new WaitUntil(() => _roundEnd);

        _dontTouchArea.SetActive(true);
        StopTimer();
        _currentPower = 0;
        _jarAnimator.SetFloat("StickSpeed", -1);
        _panda3Animator.SetFloat("StickSpeed", -1);
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _endSound);
        yield return YieldCache.WaitForSeconds(2f);
        //_startTimer.ShowClearImage();
        Reward();
        ShowRewardImage();

        yield return YieldCache.WaitForSeconds(0.2f);
        _onComplete?.Invoke();
        _dontTouchArea.SetActive(false);
        _isScreenTouch = false;
        _screenTouchButton.gameObject.SetActive(true);
        yield return new WaitUntil(() => _isScreenTouch);
        _uiMiniGameController.HideUI();
    }


    private void Reward()
    {
        if (_feverRewardConfig.IsDefault())
            return;

        int rewardValue = _touchCount <= 0 ? 0 : (_touchCount / _feverRewardConfig.TargetTouchCount) * _feverRewardConfig.RewardValue;
        rewardValue = Mathf.Max(0, rewardValue);
        switch (_feverRewardConfig.RewardType)
        {
            case MoneyType.Gold:
                UserInfo.AddMoney(rewardValue);
                DebugLog.Log("Gold: " + rewardValue);
                break;
            case MoneyType.Dia:
                UserInfo.AddDia(rewardValue);
                DebugLog.Log("Dia: " + rewardValue);
                break;
            default:
                Debug.LogError("Unknown reward type.");
                break;
        }
    }

    private void ShowRewardImage()
    {
        int rewardValue = _touchCount <= 0 ? 0 : (_touchCount / _feverRewardConfig.TargetTouchCount) * _feverRewardConfig.RewardValue;
        rewardValue = Mathf.Max(0, rewardValue);
        _rewardImage.gameObject.SetActive(true);
        _rewardImage.transform.localScale = Vector3.one * 0.3f;
        _rewardImage.TweenScale(Vector3.one, 0.3f, Ease.OutBack);
        _rewardImage.SetText(Utility.ConvertToMoney(rewardValue));
        switch (_feverRewardConfig.RewardType)
        {
            case MoneyType.Gold:
                if (5000 <= rewardValue)
                    _rewardImage.SetSprite(_bigGoldSprite);
                else if (1000 <= rewardValue)
                    _rewardImage.SetSprite(_middleGoldSprite);
                else
                    _rewardImage.SetSprite(_smallGoldSprite);

                break;
            case MoneyType.Dia:
                if (100 <= rewardValue)
                    _rewardImage.SetSprite(_bigDiaSprite);
                else if (10 <= rewardValue)
                    _rewardImage.SetSprite(_middleDiaSprite);
                else
                    _rewardImage.SetSprite(_smallDiaSprite);

                break;
            default:
                Debug.LogError("Unknown reward type.");
                break;
        }
    }

    private void OnTouchEvent()
    {
        if(_roundEnd)
            return;

        _touchCount++;
        _currentPower++;
        _miniGame_GaugeBar.SetScore(_touchCount, _touchCount);
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _touchSound);
    }

    private void OnScreenTouchButtonClicked()
    {
        _isScreenTouch = true;
        _uiMiniGameController.HideUI();
    }


    #region Timer Management
    private void StartTimer()
    {
        _remainingTime = ConstValue.DEFAULT_MINIGAME_FEVER_TIME;
        _isTimerRunning = true;
        _timer.ResetTimer();
        _timer.StartAnimation();
        _timerAudio.Play();
    }

    private void StopTimer()
    {
        _isTimerRunning = false;
        _timer.StopAnimation();
        _timerAudio.Stop();
    }

    private void UpdateTimer()
    {
        if (_isTimerRunning && _remainingTime > 0)
        {
            _remainingTime -= Time.deltaTime;

            // 타이머 UI 업데이트 (1.0 ~ 0.0)
            float normalizedTime = Mathf.Clamp01(_remainingTime / ConstValue.DEFAULT_MINIGAME_FEVER_TIME);

            _timer.SetTimer(normalizedTime);
            if (-2 < _currentPower)
                _currentPower -= Time.deltaTime * 3 * Mathf.Lerp(1, 3, _currentPower <= 0 ? 0 : _currentPower / 60);
            else if (_currentPower < -2)
                _currentPower = -2;

            _currentPower = Mathf.Clamp(_currentPower, -2, 30);
            _jarAnimator.SetFloat("StickSpeed", _currentPower * 0.1f);
            _jarAnimator.SetFloat("StickSpeedMul", Mathf.Clamp(_currentPower * 0.3f, 0, 10f));
            _panda3Animator.SetFloat("StickSpeed", _currentPower * 0.2f);

            // 타이머가 0이 되면 시간 초과 처리
            if (_remainingTime <= 0)
            {
                _remainingTime = 0;
                _roundEnd = true;
            }
        }
    }

    #endregion

}
