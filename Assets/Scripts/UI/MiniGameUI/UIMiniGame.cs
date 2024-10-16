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
    [SerializeField] private Animator _jarAnimator;

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

    private float _totalTime => 60 + GameManager.Instance.AddMiniGameTime;

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
            VisibleState = VisibleState.Appeared;
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
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

        _currentTime = _totalTime;
        _popEnabled = false;

        _uiFeverTime.SetNormalSprite();
        _gaugeBar.SetFillAmonut(0);
        _successCountText.text = _successCount.ToString();
        _timeText.text = Mathf.CeilToInt(_currentTime).ToString();
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
        StartCoroutine(EndRoutine());
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
        _timeOver.gameObject.SetActive(false);
        _resultGroup.gameObject.SetActive(false);
        _itemImage.gameObject.SetActive(false);
        _boomEffect.gameObject.SetActive(false);
        _resultEffect.gameObject.SetActive(false);
        _resultDescription.gameObject.SetActive(false);

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


    private IEnumerator EndRoutine()
    {
        yield return YieldCache.WaitForSeconds(0.5f);
        _descriptionGroup.gameObject.SetActive(true);
        _timeOver.gameObject.SetActive(true);
        _descriptionText.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _timeCountText.gameObject.SetActive(false);
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
            UserInfo.AppendMoney(Mathf.Abs(_currentCount) * 1000);

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

            yield return YieldCache.WaitForSeconds(1f);
            _resultDescription.gameObject.SetActive(true);
            _resultEffect.gameObject.SetActive(true);
            _boomEffect.gameObject.SetActive(false);

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
            _descriptionGroup.gameObject.SetActive(false);
            _uiNav.Pop("UIMiniGame");
        }

    }


    private void OnTouchButtonClicked()
    {
        if (_miniGameState != MiniGameState.Start && _miniGameState != MiniGameState.FeverTime)
            return;

        _jarAnimator.SetTrigger("Play");
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
