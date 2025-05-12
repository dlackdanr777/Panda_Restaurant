using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame1 : MiniGameSystem
{
    #region Serialized Fields
    [Header("Data")]
    [SerializeField] private Minigame1ItemDatabase _itemDatabase;
    [SerializeField] private MiniGame1StageDataList _stageDatabase;

    [Space]
    [Header("Components")]
    [SerializeField] private MiniGameFever _miniGameFever;
    [SerializeField] private MiniGame_GaugeBar _scoreBar;
    [SerializeField] private MiniGame1_SelectFrame _selectFrame;
    [SerializeField] private MiniGameTimer _timer;
    [SerializeField] private MiniGameStartTimer _startTimer;
    [SerializeField] private UIMiniGameJarGroup _jarGroup;
    [SerializeField] private UIMiniGameResultGroup _resultGroup;
    [SerializeField] private Animator _jarAnimator;
    [SerializeField] private RectTransform _dontTouchArea;   
    [SerializeField] private AudioSource _timerAudio;
    [SerializeField] private GameObject _correctImage;
    [SerializeField] private GameObject _wrongImage;
    [SerializeField] private Button _screenButton;


    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _toturialAudio;
    [SerializeField] private AudioClip _bgAudio;
    [SerializeField] private AudioClip _cardSettingAudio;
    [SerializeField] private AudioClip _cardFlipAudio;
    [SerializeField] private AudioClip _cardClickAudio;
    [SerializeField] private AudioClip _wrongAudio;
    [SerializeField] private AudioClip _correctAudio;
    [SerializeField] private AudioClip _gaugeAudio;


[Space]
    [Header("Slot Option")]
    [SerializeField] private MiniGame1_ButtonSlot _buttonSlotPrefab;
    [SerializeField] private UIMiniGame1GridLayout _buttonSlotParent;
    #endregion

    #region Private Fields
    // 데이터 관련 필드
    private List<MiniGame1ItemData> _itemList = new List<MiniGame1ItemData>();
    private List<MiniGame1StageData> _stageDataList = new List<MiniGame1StageData>();  
    private List<MiniGame1_ButtonSlot> _buttonSlotList = new List<MiniGame1_ButtonSlot>();

    // 스테이지 관련 필드
    private Action _onComplete;
    private MiniGame1StageData _currentStageData;
    private FoodData _currentFoodData;
    private int _currentStage = 0;
    private int _currentScore = 0;
    private int[] _randIndexs;
    private List<MiniGame1ItemData> _selectedItemList = new List<MiniGame1ItemData>();
    private int _selectedItemListIndex = 0;
    private int _wrongCount = 0;
    private bool _roundEnd;
    private bool _isFail;
    
    // 상수
    private const int MAX_WRONG_COUNT = 3; 
    private const int CLEAR_SCORE = 800;
    private const float SPAWN_DELAY = 0.1f;
    private const float FLIP_DELAY = 0.05f;

    // 타이머 관련 필드
    private float _remainingTime;
    private bool _isTimerRunning;

    private UIMiniGameController _uiMiniGameController;

    #endregion

    #region Unity Lifecycle
    
    private void Update()
    {
        // 타이머 업데이트
        UpdateTimer();
    }
    #endregion

    #region Initialization
    
    private void ResetGameState()
    {
        _screenButton.gameObject.SetActive(false);
        _selectedItemListIndex = 0;
        _wrongCount = 0;
        _currentStage = 0;
        _currentScore = 0;
        _currentStageData = null;
        _selectedItemList = null;
        _randIndexs = null;
        _roundEnd = false;
        _isFail = false;

        _selectFrame.Hide();
        _timer.ResetTimer();
        _startTimer.ResetTimer();
        _scoreBar.SetNormalSprite();
        _timer.SetNormalSprite();
        _timer.StopAnimation();
        _scoreBar.ResetScore();

        _timerAudio.Stop();
    }

    public override void Init(UIMiniGameController uiIiniGameController)
    {
        _uiMiniGameController = uiIiniGameController;
        ResetGameState();
        LoadDataFromDatabases();
        CreateSlots();

        _scoreBar.Init();
        _selectFrame.Init();
        _startTimer.Init();
        _resultGroup.Hide();
        _buttonSlotParent.Init();

        // 초기 UI 상태 설정
        _correctImage.SetActive(false);
        _wrongImage.SetActive(false);
        _screenButton.gameObject.SetActive(false);
    }
    
    private void LoadDataFromDatabases()
    {
        _itemList = _itemDatabase.ItemDataList.ToList();
        _stageDataList = _stageDatabase.StageDataList.ToList();
    }
    
    private void CreateSlots()
    {
        int xMax = _stageDataList.Max(x => x.CardSize.x);
        int yMax = _stageDataList.Max(x => x.CardSize.y);
        int totalSlots = xMax * yMax;
        
        for(int i = 0; i < totalSlots; i++)
        {
            MiniGame1_ButtonSlot slot = Instantiate(_buttonSlotPrefab, _buttonSlotParent.transform);
            slot.Init(OnButtonClicked);
            _buttonSlotList.Add(slot);
            slot.gameObject.SetActive(false);
        }
    }
    #endregion

    #region Public Interface
    public override void Show(FoodData foodData, Action onComplete = null)
    {
        if(foodData == null)
            throw new System.Exception("FoodData is null.");

        _onComplete = onComplete;
        SoundManager.Instance.PlayBackgroundAudio(_bgAudio, 0.5f);
        gameObject.SetActive(true);
        _wrongImage.SetActive(false);
        _correctImage.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _resultGroup.Hide();
        HideSlots();
        _currentFoodData = foodData;
        _miniGameFever.Hide();       
        StopAllCoroutines();
        ResetGameState();
        _startTimer.ShowBlackImage();
        StartCoroutine(MainGameLoop());
    }

    public override void Hide()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }
    #endregion

    #region Game Logic
    private IEnumerator MainGameLoop()
    {
        yield return YieldCache.WaitForSeconds(2);
        yield return StartCoroutine(_startTimer.StartTimer());
        while (true)
        {
            // 게임 종료 조건 체크
            if(IsGameOver())
            {
                DebugLog.Log("미니게임 실패");
                TimeManager.Instance.AddTime(_currentFoodData.Id + "_MiniGame", 900);
                _uiMiniGameController.HideUI();
                yield break;
            }

            else if(IsGameClear())
            {
                if(UserInfo.IsGiveRecipe(_currentFoodData.Id))
                {
                    DebugLog.Log("레시피 업그레이드");
                    UserInfo.UpgradeRecipe(_currentFoodData.Id);
                }
                else
                {
                    DebugLog.Log("레시피 획득");
                    UserInfo.GiveRecipe(_currentFoodData.Id);
                }
                StartCoroutine(ShowResult(_currentFoodData));
                yield break;
            }

            _scoreBar.SetStage(_currentStage + 1);
            _scoreBar.SetScore(_currentScore, CLEAR_SCORE);
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _gaugeAudio);
            bool isClear = CLEAR_SCORE <= _currentScore;
            if(isClear)
            {
                _scoreBar.SetClearSprite();
                _timer.SetClearSprite();
            }
            else
            {
                _scoreBar.SetNormalSprite();
                _timer.SetNormalSprite();
            }

            yield return StartCoroutine(PlayRound());

            _currentStage++;
            _scoreBar.SetStage(_currentStage + 1);
            if (MAX_WRONG_COUNT <= _wrongCount)
            {
                DebugLog.Log("게임 종료");
                _uiMiniGameController.HideUI();
                TimeManager.Instance.AddTime(_currentFoodData.Id + "_MiniGame", 900);
                yield break;
            }
        }
    }
    
    private bool IsGameOver()
    {
        return _stageDataList.Count <= _currentStage &&  _currentScore < CLEAR_SCORE;
    }

    private bool IsGameClear()
    {
        return _stageDataList.Count <= _currentStage && CLEAR_SCORE <= _currentScore;
    }


    private IEnumerator PlayRound()
    {
        PrepareRound();
        
        // 카드 배치 및 애니메이션
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(SpawnSlotAnimation());
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(ForntFilpAnimation());

        // 선택 프레임 설정
        yield return new WaitForSeconds(1f);
        SetSelectFrame();
        yield return new WaitForSeconds(0.1f);
        
        // 게임 시작 및 타이머 시작
        _dontTouchArea.gameObject.SetActive(false);
        StartTimer();  // 타이머 시작
        // 라운드 종료 대기
        yield return new WaitUntil(() => _roundEnd);
        StopTimer();  // 타이머 정지
        yield return new WaitForSeconds(2f);
    }
    
    private void PrepareRound()
    {
        _roundEnd = false;
        _isFail = false;
        _selectedItemListIndex = 0;
        _dontTouchArea.gameObject.SetActive(true);
        _wrongImage.SetActive(false);
        _correctImage.SetActive(false);
        _currentStageData = _stageDataList[_currentStage];
        _selectFrame.Hide();
        _timer.ResetTimer();
        _timer.StopAnimation();
        SetupCards();
    }
    
    private void SetupCards()
    {
        SetRandIndexs();
        SetButtonItemData();
        HideSlots();
    }
    
    private void OnButtonClicked(MiniGame1ItemData data)
    {
        // 이미 모든 항목을 선택한 경우 클릭 방지
        if(_selectedItemList.Count <= _selectedItemListIndex)
        {
            _dontTouchArea.gameObject.SetActive(true);
            return;
        }

        MiniGame1ItemData correctData = _selectedItemList[_selectedItemListIndex];
        _jarAnimator.SetTrigger("Play");
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _cardClickAudio);
        if (data.Id == correctData.Id)
        {
            HandleCorrectAnswer();
        }
        else
        {
            HandleWrongAnswer();
        }
    }
    
    private void HandleCorrectAnswer()
    {
        DebugLog.Log("정답");
        _selectFrame.SetCorrect(_selectedItemListIndex, true);
        _selectedItemListIndex++;
        
        // 모든 항목을 맞혔으면 라운드 완료
        if (_selectedItemList.Count <= _selectedItemListIndex)
        {
            CompleteRound();
        }
    }
    
    private void HandleWrongAnswer()
    {
        DebugLog.Log("오답");
        ShowWrongImage();
        _dontTouchArea.gameObject.SetActive(true);
        _selectFrame.SetCorrect(_selectedItemListIndex, false);
        _selectedItemListIndex = 0;
        _wrongCount++;
        _roundEnd = true;
        _isFail = true;
        StopTimer();  // 타이머 정지
    }
    
    private void CompleteRound()
    {
        _dontTouchArea.gameObject.SetActive(true);
        ShowCorrectImage();
        _currentScore += _currentStageData.SuccessScore;
        _scoreBar.SetScore(_currentScore, CLEAR_SCORE);
        _roundEnd = true;
        StopTimer();  // 타이머 정지
    }
    #endregion
    
    #region Card Management
    private void SetRandIndexs()
    {
        int cardSize = _currentStageData.CardSize.x * _currentStageData.CardSize.y;
        _randIndexs = GetRandomIndexArray(cardSize);
    }

    private void SetButtonItemData()
    {
        if(_currentStageData == null)
            throw new System.Exception("Current stage data is null.");
            
        _buttonSlotParent.SetConstraintCount(_currentStageData.CardSize.x);
        int cardSize = _currentStageData.CardSize.x * _currentStageData.CardSize.y;
        int itemCount = _itemList.Count;

        for (int i = 0; i < _buttonSlotList.Count; i++)
        {
            MiniGame1_ButtonSlot slot = _buttonSlotList[i];
            if (i < cardSize)
            {
                slot.SetData(_itemList[_randIndexs[i % itemCount]]);
            }
            else
            {
                slot.SetData(null);
            }
        }
    }
    
    private void SetSelectFrame()
    {
        int cardSize = _currentStageData.CardSize.x * _currentStageData.CardSize.y;
        int selectCount = Mathf.Min(cardSize, _currentStageData.CardCount);
        
        // 사용 가능한 인덱스에서 중복 없이 선택
        int[] selectedCardIndices = GetRandomIndexArray(cardSize);
        
        // 필요한 개수만큼만 사용
        List<MiniGame1ItemData> itemDataList = new List<MiniGame1ItemData>();
        for (int i = 0; i < selectCount; i++)
        {
            int cardIndex = selectedCardIndices[i];
            int itemIndex = _randIndexs[cardIndex] % _itemList.Count;
            itemDataList.Add(_itemList[itemIndex]);
        }
        
        _selectedItemList = itemDataList;
        _selectFrame.SetItemImage(itemDataList);
    }

    private void HideSlots()
    {
        foreach (var slot in _buttonSlotList)
        {
            slot.gameObject.SetActive(false);
        }
    }
    #endregion
    
    #region Animation
    private IEnumerator SpawnSlotAnimation()
    {
        float duration = 0.5f;
        foreach (var slot in _buttonSlotList)
        {
            if(slot.CurrentData == null)
                continue;

            slot.gameObject.SetActive(true);
            slot.FlipBack();
            slot.ScaleAnimation(1.2f, 1f, duration);
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _cardSettingAudio, duration * 0.5f);
            yield return YieldCache.WaitForSeconds(SPAWN_DELAY);
        }
    }

    private IEnumerator ForntFilpAnimation()
    {
        foreach (var slot in _buttonSlotList)
        {
            if(!slot.gameObject.activeSelf)
                continue;

            slot.FlipForwardAnimation();
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _cardFlipAudio, slot.FlipSpeed * 0.5f);
            yield return new WaitForSeconds(FLIP_DELAY);
        }
    }

    private IEnumerator ShowResult(FoodData foodData)
    {
        _resultGroup.SetResult(foodData.ThumbnailSprite, foodData.Name);
        yield return YieldCache.WaitForSeconds(1f);
        _screenButton.gameObject.SetActive(true);
        _screenButton.onClick.RemoveAllListeners();
        _screenButton.onClick.AddListener(() =>
        {
            FeverRewardConfig feverRewardConfig = new FeverRewardConfig(8, 1, MoneyType.Dia);
            _miniGameFever.Show(feverRewardConfig, _onComplete);
            _screenButton.gameObject.SetActive(false);
        });
    }

    private void ShowCorrectImage()
    {
        _wrongImage.SetActive(false);
        _correctImage.SetActive(true);
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _correctAudio);
        AnimateUIElement(_correctImage);
    }

    private void ShowWrongImage()
    {
        _wrongImage.SetActive(true);
        _correctImage.SetActive(false);
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _wrongAudio);
        AnimateUIElement(_wrongImage);
    }
    
    private void AnimateUIElement(GameObject element)
    {
        element.TweenStop();
        element.transform.localScale = Vector3.one * 0.2f;
        element.TweenScale(Vector3.one, 0.3f, Ease.OutBack);
    }
    #endregion

    #region Timer Management
    private void StartTimer()
    {
        _remainingTime = _currentStageData.StageTime;
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
            float normalizedTime = Mathf.Clamp01(_remainingTime / _currentStageData.StageTime);
            _timer.SetTimer(normalizedTime);
            
            // 타이머가 0이 되면 시간 초과 처리
            if (_remainingTime <= 0)
            {
                _remainingTime = 0;
                HandleTimeOut();
            }
        }
    }
    
    private void HandleTimeOut()
    {
        if (_roundEnd) return;  // 이미 라운드가 끝났으면 중복 처리 방지
        
        DebugLog.Log("시간 초과");
        _dontTouchArea.gameObject.SetActive(true);
        ShowWrongImage();
        _roundEnd = true;
        _isFail = true;
        _wrongCount++;
        StopTimer();
    }
    #endregion

    #region Utility Functions
    private int[] GetRandomIndexArray(int size)
    {
        int[] indexArray = new int[size];
        
        // 배열 초기화
        for (int i = 0; i < size; i++)
        {
            indexArray[i] = i;
        }

        // Fisher-Yates 셔플
        for (int i = 0; i < size; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, size);
            int temp = indexArray[i];
            indexArray[i] = indexArray[randomIndex];
            indexArray[randomIndex] = temp;
        }

        return indexArray;
    }
    #endregion
}
