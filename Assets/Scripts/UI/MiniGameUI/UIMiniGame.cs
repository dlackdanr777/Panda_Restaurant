using Muks.MobileUI;
using Muks.Tween;
using System.Collections;
using System.Text;
using TMPro;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UI;
using static UIMiniGame;

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
    [SerializeField] private UIMiniGameFeverTime _uiFeverTime;
    [SerializeField] private Button _screenButton;
    [SerializeField] private GameObject _descriptionGroup;
    [SerializeField] private UIImageAndText _descriptionText;
    [SerializeField] private TextMeshProUGUI _timeCountText;
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private TextMeshProUGUI _successCountText;
    [SerializeField] private ButtonPressEffect _leftTouchButton;
    [SerializeField] private ButtonPressEffect _rightTouchButton;
    [SerializeField] private FillAmountImage _gaugeBar;
    [SerializeField] private Animator _stickAnimator;

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


    private FoodMiniGameData _currentData;
    private int _successCount;
    private int _currentCount;
    private int _totalCount;
    private int _totalHealth;
    private int _firstHealth;
    private int _addHealth;
    private int _currentHealth;
    private int _currentGauge;
    private int _touchPower = 1;
    private float _totalTime;
    private float _currentTime;
    private bool _onButtonClicked;
    private MiniGameState _miniGameState;

    public override void Init()
    {
        _screenButton.onClick.AddListener(() => _onButtonClicked = true);
        _leftTouchButton.AddListener(OnTouchButtonClicked);
        _rightTouchButton.AddListener(OnTouchButtonClicked);
        _miniGameState = MiniGameState.Wait;
        _uiFeverTime.SetNormalSprite();

        _descriptionGroup.gameObject.SetActive(false);
        _descriptionText.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _timeCountText.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
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
            VisibleState = VisibleState.Appeared;
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        gameObject.SetActive(true);
        _miniGameState = MiniGameState.Wait;
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _popEnabled = false;
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

        if (data == null)
        {
            DebugLog.LogError("미니게임 데이터가 존재하지 않습니다.");
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
        _totalTime = 60;
        _currentTime = _totalTime;

        _uiFeverTime.SetNormalSprite();
        _gaugeBar.SetFillAmonut(0);
        _successCountText.text = _successCount.ToString();
        _timeText.text = ((int)_currentTime).ToString();
        _uiNav.Push("UIMiniGame");
        Tween.Wait(_showDuration + 0.5f, () =>
        {
            StartCoroutine(StartDescriptionRoutine());
        });
    }


    private void EndMiniGame()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        _miniGameState = MiniGameState.End;

    }


    private void Update()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        if (_currentTime <= 0)
        {
            _currentTime = 0;
            _timeText.text = 0.ToString();
            EndMiniGame();
        }

        _currentTime -= Time.deltaTime;
        _timeText.text = ((int)_currentTime).ToString();
    }


    private IEnumerator StartDescriptionRoutine()
    {
        _canvasGroup.blocksRaycasts = true;
        _descriptionGroup.gameObject.SetActive(true);
        _descriptionText.gameObject.SetActive(true);
        _screenButton.gameObject.SetActive(true);
        _timeCountText.gameObject.SetActive(false);

        
        yield return StartCoroutine(DescriptionRoutine(Utility.SetStringColor(((int)_totalTime).ToString(), ColorType.Negative) + "초 동안"));
        yield return StartCoroutine(DescriptionRoutine("발바닥을 눌러 휘저으세요!!"));
        yield return StartCoroutine(DescriptionRoutine("이번 목표는 " + Utility.SetStringColor(_successCount.ToString(), ColorType.Negative) + "!!!"));


        yield return YieldCache.WaitForSeconds(0.5f);
        _descriptionText.gameObject.SetActive(false);

        yield return YieldCache.WaitForSeconds(0.5f);
        _timeCountText.gameObject.SetActive(true);
        _timeCountText.text = "3";
        yield return YieldCache.WaitForSeconds(1);
        _timeCountText.text = "2";
        yield return YieldCache.WaitForSeconds(1);
        _timeCountText.text = "1";
        yield return YieldCache.WaitForSeconds(1);
        _timeCountText.text = "Let's Go!";
        yield return YieldCache.WaitForSeconds(1);
        _descriptionGroup.gameObject.SetActive(false);
        _descriptionText.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _timeCountText.gameObject.SetActive(false);
        _miniGameState = MiniGameState.Start;
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


    private void OnTouchButtonClicked()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        _stickAnimator.SetTrigger("Play");
        AddGaugeEvent();
    }


    private void OnLeftTouchButtonClicked()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        _stickAnimator.SetTrigger("LeftPlay");
        AddGaugeEvent();
    }


    private void OnRightTouchButtonClicked()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        _stickAnimator.SetTrigger("RightPlay");
        AddGaugeEvent();
    }

    private void AddGaugeEvent()
    {

        _currentGauge += _touchPower + 10;
        if (_currentHealth <= _currentGauge)
        {
            _totalCount++;
            _currentCount--;
            _currentGauge = 0;
            _currentHealth = _firstHealth + (_totalCount * 10);
            _successCountText.text = Mathf.Abs(_currentCount).ToString();

            if (_currentCount == -1)
            {
                if (_miniGameState == MiniGameState.Start)
                {
                    _miniGameState = MiniGameState.FeverTime;
                    _uiFeverTime.SetFeverSprite();
                }

            }
        }

        _gaugeBar.SetFillAmonut(_currentGauge <= 0 ? 0 : (float)_currentGauge / _currentHealth);
    }



}
