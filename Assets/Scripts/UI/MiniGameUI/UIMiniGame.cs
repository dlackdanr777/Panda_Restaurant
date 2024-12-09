using Muks.MobileUI;
using Muks.Tween;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMiniGame : MobileUIView
{
    public enum MiniGameState
    {
        Description,
        Wait,
        Start,
        FeverTime,
        End
    }

    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIMiniGameTutorial _uiMiniGameTotorial;
    [SerializeField] private UIMiniGameFeverTime _uiFeverTime;
    [SerializeField] private Button _screenButton;
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private TextMeshProUGUI _successCountText;
    [SerializeField] private ButtonPressEffect _leftTouchButton;
    [SerializeField] private ButtonPressEffect _rightTouchButton;
    [SerializeField] private UITweenFillAmountImage _gaugeBar;
    [SerializeField] private UIMiniGameJarGroup _jarGroup;
    [SerializeField] private Animator _jarAnimator;
    [SerializeField] private Animator _panda3Animator;

    [Space]
    [Header("Description Components")]
    [SerializeField] private GameObject _descriptionGroup;
    [SerializeField] private Image _descriptionLeftTouchImage;
    [SerializeField] private Image _descriptionRightTouchImage;
    [SerializeField] private GameObject _1Image;
    [SerializeField] private GameObject _2Image;
    [SerializeField] private GameObject _3Image;
    [SerializeField] private UIImageAndText _descriptionText;
    [SerializeField] private UIImageAndText _descriptionTimeText;
    [SerializeField] private UIImageAndText _descriptionSuccessCountText;

    [Space]
    [Header("Result Components")]
    [SerializeField] private GameObject _resultGroup;
    [SerializeField] private GameObject _resultEffect;
    [SerializeField] private GameObject _boomEffect;
    [SerializeField] private GameObject _timeOver;
    [SerializeField] private Image _itemImage;
    [SerializeField] private UIImageAndText _resultDescription;
    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _animeUI;
    [SerializeField] private RectTransform _startPos;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private RectTransform _targetPos;
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioSource _timerAudio;
    [SerializeField] private AudioClip _toturialAudio;
    [SerializeField] private AudioClip _bgAudio;
    [SerializeField] private AudioClip _feverBgAudio;
    [SerializeField] private AudioClip _mainSceneAudio;

    [SerializeField] private AudioClip _touchSound;
    [SerializeField] private AudioClip _countSound;
    [SerializeField] private AudioClip _gaugeUpSound;
    [SerializeField] private AudioClip _startAndEndSound;
    [SerializeField] private AudioClip _failSound;
    [SerializeField] private AudioClip _successSound;
    [SerializeField] private AudioClip _getSound;


    private float _totalTime => ConstValue.DEFAULT_MINIGAME_TIME + GameManager.Instance.AddMiniGameTime;

    private FoodMiniGameData _currentData;
    private int _successCount;
    private int _currentCount;
    private int _totalCount;
    private int _totalHealth;
    private int _firstHealth;
    private int _addHealth;
    private int _currentHealth;
    private float _currentGauge;
    private int _touchPower = 1;
    private float _currentTime;
    private bool _onButtonClicked;
    private MiniGameState _miniGameState;

    private float _currentPower;
    public float CurrentPower => _currentPower;
    private float _maxPower => 60;


    public override void Init()
    {
        _jarGroup.Init(this);
        _screenButton.onClick.AddListener(() => _onButtonClicked = true);
        _leftTouchButton.AddListener(OnTouchButtonClicked);
        _rightTouchButton.AddListener(OnTouchButtonClicked);
        _miniGameState = MiniGameState.Wait;
        _uiFeverTime.SetNormalSprite();
        _uiMiniGameTotorial.Init();

        _descriptionGroup.gameObject.SetActive(false);
        _descriptionText.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _1Image.gameObject.SetActive(false);
        _2Image.gameObject.SetActive(false);
        _3Image.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_toturialAudio, 0.5f);
        _timerAudio.Stop();
        StopAllCoroutines();
        gameObject.SetActive(true);
        _descriptionGroup.gameObject.SetActive(false);
        _miniGameState = MiniGameState.Wait;
        _canvasGroup.blocksRaycasts = false;
        _startPos.anchoredPosition = new Vector2(Screen.width, 0);
        _animeUI.position = _startPos.position;
        transform.SetAsLastSibling();

      TweenData tween = _animeUI.TweenMove(_targetPos.position, _showDuration, _showTweenMode);
        tween.OnComplete(() => 
        {
            _canvasGroup.blocksRaycasts = true;
            VisibleState = VisibleState.Appeared;
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        SoundManager.Instance.PlayBackgroundAudio(_mainSceneAudio, 0.5f);
        _timerAudio.Stop();
        StopAllCoroutines();
        gameObject.SetActive(true);
        _currentData = null;
        _miniGameState = MiniGameState.Wait;
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _startPos.anchoredPosition = new Vector2(Screen.width, 0);
        _animeUI.position = _targetPos.position;

        TweenData tween = _animeUI.TweenMove(_startPos.position, _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            _canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(false);
        });
    }


    public void StartMiniGame(FoodMiniGameData data)
    {
        if (VisibleState == VisibleState.Appeared || VisibleState == VisibleState.Appearing)
            return;

        if(_currentData != null)
        {
            DebugLog.LogError("이미 요리가 진행중입니다: " + _currentData.Id);
            return;
        }

        if (data == null)
        {
            DebugLog.LogError("미니게임 데이터가 존재하지 않습니다: " + data.Id);
            return;
        }

        if(FoodDataManager.Instance.GetFoodData(data.Id) == null)
        {
            DebugLog.LogError("완성 요리 데이터가 존재하지 않습니다: " +  data.Id);
            return;
        }

        _currentData = data;
        _successCount = data.SuccessCount;
        _currentCount = _successCount;
        _totalCount = 0;
        _totalHealth = data.MaxHealth;
        _firstHealth = data.FirstHealth;
        _addHealth = data.AddHealth;
        _currentHealth = _firstHealth;
        _currentGauge = 0;
        _currentPower = 0;

        _currentTime = _totalTime;
        _popEnabled = false;

        _jarAnimator.SetFloat("StickSpeed", -1);
        _panda3Animator.SetFloat("StickSpeed", -1);
        _uiFeverTime.SetNormalSprite();
        _gaugeBar.SetFillAmonut(0);
        _successCountText.text = _successCount.ToString();
        _descriptionSuccessCountText.SetText(_successCount.ToString());
        _timeText.text = Mathf.CeilToInt(_totalTime).ToString();
        _descriptionTimeText.SetText(Mathf.CeilToInt(_totalTime).ToString());
        _uiNav.Push("UIMiniGame");

        _uiMiniGameTotorial.StartTutorial(() =>
        {
            _uiMiniGameTotorial.gameObject.SetActive(false);
            Tween.Wait(_showDuration + 0.5f, () =>
            {
                StartCoroutine(StartDescriptionRoutine());
            });
        });
    }


    private void EndMiniGame()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        _miniGameState = MiniGameState.End;
        StartCoroutine(EndRoutine());
    }


    private void Update()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        if (-2 < _currentPower)
            _currentPower -= Time.deltaTime * 4 * Mathf.Lerp(1, 3, _currentPower <= 0 ? 0 : _currentPower / _maxPower);
        else if (_currentPower < -2)
            _currentPower = -2;

        _jarAnimator.SetFloat("StickSpeed", _currentPower * 0.1f);
        _jarAnimator.SetFloat("StickSpeedMul", Mathf.Clamp(_currentPower * 0.3f, 0, 10f));
        _panda3Animator.SetFloat("StickSpeed", _currentPower * 0.2f);

        if(!_timerAudio.isPlaying && _currentTime <= 5)
        {
            _timerAudio.Play();
        }

        if (_currentTime <= 0)
        {
            _currentTime = 0;
            _timeText.text = 0.ToString();
            _currentPower = 0;
            EndMiniGame();
            _jarAnimator.SetFloat("StickSpeed", 0);
            _jarAnimator.SetFloat("StickSpeedMul", 0);
            _panda3Animator.SetFloat("StickSpeed", -1);
            SoundManager.Instance.StopBackgroundAudio(0.5f);
            SoundManager.Instance.PlayEffectAudio(_startAndEndSound);
        }

        _currentTime -= Time.deltaTime;
        _timeText.text = ((int)_currentTime).ToString();
        _currentGauge = Mathf.Clamp(_currentGauge + _currentPower * Time.deltaTime, 0, 10000000);    
        _gaugeBar.SetFillAmountNoAnime(_currentGauge <= 0 ? 0 : _currentGauge / _currentHealth);

        if (_currentHealth <= _currentGauge)
        {
            _totalCount++;
            _currentCount--;
            _currentGauge = 0;
            _currentHealth = _firstHealth + (_totalCount * _addHealth);
            _successCountText.text = Mathf.Abs(_currentCount).ToString();
            SoundManager.Instance.PlayEffectAudio(_gaugeUpSound);

            if (_currentCount == -1 && _miniGameState == MiniGameState.Start)
            {
                _miniGameState = MiniGameState.FeverTime;
                SoundManager.Instance.PlayBackgroundAudio(_feverBgAudio, 0.5f);
                _uiFeverTime.SetFeverSprite();
            }
        } 
    }


    private IEnumerator StartDescriptionRoutine()
    {
        _canvasGroup.blocksRaycasts = true;
        _screenButton.gameObject.SetActive(true);
        _1Image.gameObject.SetActive(false);
        _2Image.gameObject.SetActive(false);
        _3Image.gameObject.SetActive(false);
        _timeOver.gameObject.SetActive(false);
        _resultGroup.gameObject.SetActive(false);
        _itemImage.gameObject.SetActive(false);
        _boomEffect.gameObject.SetActive(false);
        _resultEffect.gameObject.SetActive(false);
        _resultDescription.gameObject.SetActive(false);

        _descriptionGroup.gameObject.SetActive(true);
        _descriptionText.gameObject.SetActive(true);
        _descriptionLeftTouchImage.gameObject.SetActive(false);
        _descriptionRightTouchImage.gameObject.SetActive(false);
        _descriptionSuccessCountText.gameObject.SetActive(false);
        _descriptionTimeText.gameObject.SetActive(true);


        yield return StartCoroutine(DescriptionRoutine(Utility.SetStringColor(Mathf.CeilToInt(_totalTime).ToString(), ColorType.Negative) + "초 동안"));
        _descriptionTimeText.gameObject.SetActive(false);
        _descriptionLeftTouchImage.gameObject.SetActive(true);
        _descriptionRightTouchImage.gameObject.SetActive(true);

        yield return StartCoroutine(DescriptionRoutine("발바닥을 눌러 휘저으세요!!"));
        _descriptionLeftTouchImage.gameObject.SetActive(false);
        _descriptionRightTouchImage.gameObject.SetActive(false);
        _descriptionSuccessCountText.gameObject.SetActive(true);

        yield return StartCoroutine(DescriptionRoutine("이번 목표는 " + Utility.SetStringColor(_successCount.ToString(), ColorType.Negative) + "!!!"));
        yield return YieldCache.WaitForSeconds(0.5f);
        _descriptionText.gameObject.SetActive(false);
        _descriptionSuccessCountText.gameObject.SetActive(false);


        yield return YieldCache.WaitForSeconds(0.5f);
        _3Image.gameObject.SetActive(true);
        SoundManager.Instance.PlayEffectAudio(_countSound);
        yield return YieldCache.WaitForSeconds(1);
        _2Image.gameObject.SetActive(true);
        _3Image.gameObject.SetActive(false);
        SoundManager.Instance.PlayEffectAudio(_countSound);
        yield return YieldCache.WaitForSeconds(1);
        _1Image.gameObject.SetActive(true);
        _2Image.gameObject.SetActive(false);
        SoundManager.Instance.PlayEffectAudio(_countSound);
        yield return YieldCache.WaitForSeconds(1);
        _descriptionGroup.gameObject.SetActive(false);
        _descriptionText.gameObject.SetActive(false);
        _descriptionLeftTouchImage.gameObject.SetActive(false);
        _descriptionRightTouchImage.gameObject.SetActive(false);
        _descriptionSuccessCountText.gameObject.SetActive(false);
        _descriptionTimeText.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _1Image.gameObject.SetActive(false);
        _2Image.gameObject.SetActive(false);
        _3Image.gameObject.SetActive(false);
        _miniGameState = MiniGameState.Start;
        SoundManager.Instance.PlayEffectAudio(_startAndEndSound);
        SoundManager.Instance.PlayBackgroundAudio(_bgAudio, 0.5f);
    }


    private IEnumerator DescriptionRoutine(string description)
    {
        _onButtonClicked = false;
        _descriptionText.SetText(description);

        yield return YieldCache.WaitForSeconds(0.01f);
        _descriptionText.SetText(description + " ");
        yield return YieldCache.WaitForSeconds(0.01f);
        _descriptionText.SetText(description);

        yield return YieldCache.WaitForSeconds(0.1f);

        while (!_onButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _onButtonClicked = false;
    }


    private IEnumerator EndRoutine()
    {
        _timerAudio.Stop();
        yield return YieldCache.WaitForSeconds(0.5f);
        _descriptionGroup.gameObject.SetActive(true);
        _timeOver.gameObject.SetActive(true);
        _descriptionText.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _resultGroup.gameObject.SetActive(false);
        _itemImage.gameObject.SetActive(false);
        _boomEffect.gameObject.SetActive(false);
        _resultEffect.gameObject.SetActive(false);
        _resultDescription.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _onButtonClicked = false;

        bool isClear = _currentCount < 0;
        if (isClear)
        {
            FoodData giveFoodData = FoodDataManager.Instance.GetFoodData(_currentData.Id);
            UserInfo.GiveRecipe(giveFoodData);
            UserInfo.AddMoney(Mathf.Abs(_currentCount) * 1000);

            _itemImage.sprite = giveFoodData.Sprite;
            _resultDescription.SetText(giveFoodData.Name + " 제작 완료");
        }

        yield return YieldCache.WaitForSeconds(0.1f);
        _screenButton.gameObject.SetActive(true);

        while (!_onButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        //성공
        _onButtonClicked = false;

        if (isClear)
        {
            _boomEffect.gameObject.SetActive(true);
            _itemImage.gameObject.SetActive(true);
            _resultGroup.gameObject.SetActive(true);
            _timeOver.gameObject.SetActive(false);
            SoundManager.Instance.PlayEffectAudio(_successSound);
            yield return YieldCache.WaitForSeconds(1.2f);
            _resultDescription.gameObject.SetActive(true);
            _resultEffect.gameObject.SetActive(true);
            _boomEffect.gameObject.SetActive(false);
            SoundManager.Instance.PlayEffectAudio(_getSound);
            yield return YieldCache.WaitForSeconds(0.2f);

            while (!_onButtonClicked)
                yield return YieldCache.WaitForSeconds(0.02f);

            _popEnabled = true;
            _descriptionGroup.gameObject.SetActive(false);
            _uiNav.Pop("UIMiniGame");
        }

        //실패
        else
        {
            _popEnabled = true;
            SoundManager.Instance.PlayEffectAudio(_failSound);
            _descriptionGroup.gameObject.SetActive(false);
            _uiNav.Pop("UIMiniGame");
        }

    }


    private void OnTouchButtonClicked()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        _currentPower = Mathf.Clamp(_currentPower + _touchPower, 0, _maxPower);
        SoundManager.Instance.PlayEffectAudio(_touchSound);
    }
}
