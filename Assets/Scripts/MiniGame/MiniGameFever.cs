using System;
using System.Collections;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public struct FeverRewardConfig
{
    // 단계별 목표 터치 수
    private int[] _stageTouchCounts;
    public int[] StageTouchCounts => _stageTouchCounts;

    // 각 단계별 보상 값
    private int[] _stageRewardValues;
    public int[] StageRewardValues => _stageRewardValues;

    private MoneyType _rewardType;
    public MoneyType RewardType => _rewardType;

    // 현재 단계에 해당하는 터치 수 목표 얻기
    public int GetTouchCountForStage(int stage)
    {
        if (_stageTouchCounts == null || stage < 0 || stage >= _stageTouchCounts.Length)
            return 0;
        return _stageTouchCounts[stage];
    }

    // 현재 단계에 해당하는 보상 값 얻기
    public int GetRewardForStage(int stage)
    {
        if (_stageRewardValues == null || stage < 0 || stage >= _stageRewardValues.Length)
            return 0;
        return _stageRewardValues[stage];
    }

    // 현재 터치 수에 해당하는 단계 계산
    public int GetCurrentStage(int touchCount)
    {
        if (_stageTouchCounts == null || _stageTouchCounts.Length == 0)
            return -1;
            
        for (int i = _stageTouchCounts.Length - 1; i >= 0; i--)
        {
            if (touchCount >= _stageTouchCounts[i])
                return i;
        }
        
        return -1; // 아직 첫 단계에도 도달하지 못함
    }

    // 다음 단계 터치 목표 얻기
    public int GetNextStageTouchCount(int currentTouchCount)
    {
        if (_stageTouchCounts == null || _stageTouchCounts.Length == 0)
            return 0;
            
        for (int i = 0; i < _stageTouchCounts.Length; i++)
        {
            if (currentTouchCount < _stageTouchCounts[i])
                return _stageTouchCounts[i];
        }
        
        return _stageTouchCounts[_stageTouchCounts.Length - 1]; // 이미 최대 단계
    }

    public FeverRewardConfig(int[] touchCounts, int[] rewardValues, MoneyType type)
    {
        _stageTouchCounts = touchCounts;
        _stageRewardValues = rewardValues;
        _rewardType = type;
    }

    public bool IsDefault()
    {
        return _stageTouchCounts == null || _stageTouchCounts.Length == 0;
    }
}

public class MiniGameFever : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject[] _feverObjects;
    [SerializeField] private GameObject[] _feverHideObjects;
    [SerializeField] private RectTransform _blackImage;
    [SerializeField] private Button _leftTouchButton;
    [SerializeField] private Button _rightTouchButton;
    [SerializeField] private MiniGameTimer _timer;
    [SerializeField] private AudioSource _timerAudio;
    [SerializeField] private MiniGame_GaugeBar _miniGame_GaugeBar;
    [SerializeField] private MiniGameStartTimer _startTimer;
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Button _screenTouchButton;
    [SerializeField] private Transform _feverImageGroup;

    [SerializeField] private Animator _jarAnimator;

    [SerializeField] private Transform _diaImage;

    [SerializeField] private ParticleSystem _leftTouchButtonParticle;
    [SerializeField] private ParticleSystem _rightTouchButtonParticle;



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
        _blackImage.gameObject.SetActive(true);
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

        _leftTouchButton.onClick.RemoveAllListeners();
        _rightTouchButton.onClick.RemoveAllListeners();

        _leftTouchButton.onClick.AddListener(() => OnTouchEvent(true));
        _rightTouchButton.onClick.AddListener(() => OnTouchEvent(false));
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
        _miniGame_GaugeBar.SetScore(0, 0);
        _diaImage.TweenStop();
        _diaImage.transform.localScale = Vector3.one;

        _jarAnimator.SetFloat("StickSpeed", -1);

        _rewardImage.gameObject.SetActive(false);
    }

    private void StopEffects()
    {
        for (int i = 0, cnt = _feverObjects.Length; i < cnt; i++)
        {
            _feverObjects[i].SetActive(false);
        }
        
        for(int i = 0, cnt = _feverHideObjects.Length; i < cnt; i++)
        {
            _feverHideObjects[i].SetActive(true);
        }
    }

    private void StartEffects()
    {
        for (int i = 0, cnt = _feverObjects.Length; i < cnt; i++)
        {
            _feverObjects[i].SetActive(true);
        }

                for(int i = 0, cnt = _feverHideObjects.Length; i < cnt; i++)
        {
            _feverHideObjects[i].SetActive(false);
        }
    }

    private IEnumerator Play()
    {
        StopEffects();
        _dontTouchArea.SetActive(true);
        _blackImage.gameObject.SetActive(true);
        _feverImageGroup.gameObject.SetActive(true);
        _feverImageGroup.localScale = Vector3.one * 0.3f;
        _feverImageGroup.TweenScale(Vector3.one, 0.3f, Ease.OutBack);
       SoundManager.Instance.PlayEffectAudio(EffectType.UI, _startSound);
        SoundManager.Instance.StopBackgroundAudio();
        yield return YieldCache.WaitForSeconds(3f);
        _feverImageGroup.gameObject.SetActive(false);
        _blackImage.gameObject.SetActive(false);
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
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _endSound);
        yield return YieldCache.WaitForSeconds(2f);
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

    int rewardValue = GetRewardValue();
    
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
        int rewardValue = GetRewardValue();
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

  private void OnTouchEvent(bool isLeft)
{
    if(_roundEnd)
        return;

    _touchCount++;
    _currentPower++;
    
    int currentStage = _feverRewardConfig.GetCurrentStage(_touchCount);
    int rewardValue = currentStage >= 0 ? _feverRewardConfig.GetRewardForStage(currentStage) : 0;
    int nextTouchTarget = _feverRewardConfig.GetNextStageTouchCount(_touchCount);
    
    // 현재 점수와 다음 단계 터치 목표를 표시
    _miniGame_GaugeBar.SetScore(_touchCount, nextTouchTarget);
    _miniGame_GaugeBar.SetStageText(currentStage >= 0 ? $"x{Utility.ConvertToMoney(rewardValue)}" : "x0");
    
    _diaImage.transform.localScale = Vector3.one;
    _diaImage.TweenStop();
    _diaImage.TweenScale(Vector3.one * 1.2f, 0.2f, Ease.OutBack).OnComplete(() =>
    {
        _diaImage.TweenScale(Vector3.one, 0.2f, Ease.OutBack);
    });

    if(isLeft)
    {
        _leftTouchButtonParticle.Emit(6);
    }
    else
    {
        _rightTouchButtonParticle.Emit(6);
    }

    SoundManager.Instance.PlayEffectAudio(EffectType.UI, _touchSound);
}

    private void OnScreenTouchButtonClicked()
    {
        _isScreenTouch = true;
        _uiMiniGameController.HideUI();
    }

private int GetRewardValue()
{
    if (_feverRewardConfig.IsDefault())
        return 0;
        
    int currentStage = _feverRewardConfig.GetCurrentStage(_touchCount);
    if (currentStage < 0)
        return 0;
        
    return _feverRewardConfig.GetRewardForStage(currentStage);
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
