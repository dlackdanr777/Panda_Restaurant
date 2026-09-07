using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;

public class UIGacha : MobileUIView
{
#if UNITY_EDITOR
    public const bool EnableEditorEntryUnlockForTesting = true;
#else
    public const bool EnableEditorEntryUnlockForTesting = false;
#endif

    public event Action<int> GachaStepHandler;
    public event Action HiddenHandler;

    public static bool IsEntryUnlocked()
    {
        return EnableEditorEntryUnlockForTesting
            || UserInfo.GetIsClearChallenge("MainReward12");
    }

    [Header("Components")]
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GachaMachineParent[] _gachaMachines;
    [SerializeField] private GameObject _uiComponents;
    [SerializeField] private UIGachaSlotList _gachaItemList;
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;
    [SerializeField] private RectTransform _machineParent;
    [SerializeField] private ScrollRect _scrollRect;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _backgroundAudio;

    [Space]
    [Header("Tutorial Components")]
    [SerializeField] private GachaTutorial _miniGameTutorial;

    private GachaMachineParent _currentGachaMachine;
    private GachaMachineParent _requestedInitialMachine;
    private bool _isInitialized;

    private bool _isStartGacha;
    public bool IsStartGacha => _isStartGacha;
    public void SetStartGacha(bool isStart)
    {
        _isStartGacha = isStart;
        if (_scrollRect != null)
            _scrollRect.enabled = !isStart;

        SetNavigationButtonsActive(!isStart);

    }
    public override void Init()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        RemoveInvalidAndDuplicateMachines();

        if (_gachaMachines.Length == 0)
        {
            DebugLog.LogError("가챠 머신이 연결되어 있지 않습니다.");
            gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Init(this);
            _gachaMachines[i].Hide();
        }
        _gachaItemList.Init(_gachaMachines[0].ItemDataList);
        SetMachine(_gachaMachines[0]);
        _leftButton.onClick.AddListener(() => SetMachine(-1));
        _rightButton.onClick.AddListener(() => SetMachine(1));
        gameObject.SetActive(false);

        // ScrollRect에 EventTrigger 추가
        EventTrigger trigger = _scrollRect.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = _scrollRect.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        // BeginDrag 이벤트
        EventTrigger.Entry beginDragEntry = new EventTrigger.Entry();
        beginDragEntry.eventID = EventTriggerType.BeginDrag;
        beginDragEntry.callback.AddListener((data) => { OnScrollBeginDrag((PointerEventData)data); });
        trigger.triggers.Add(beginDragEntry);

        // Drag 이벤트
        EventTrigger.Entry dragEntry = new EventTrigger.Entry();
        dragEntry.eventID = EventTriggerType.Drag;
        dragEntry.callback.AddListener((data) => { OnScrollDrag((PointerEventData)data); });
        trigger.triggers.Add(dragEntry);

        // EndDrag 이벤트
        EventTrigger.Entry endDragEntry = new EventTrigger.Entry();
        endDragEntry.eventID = EventTriggerType.EndDrag;
        endDragEntry.callback.AddListener((data) => { OnScrollEndDrag((PointerEventData)data); });
        trigger.triggers.Add(endDragEntry);
    }

    private void RemoveInvalidAndDuplicateMachines()
    {
        if (_gachaMachines == null)
        {
            _gachaMachines = Array.Empty<GachaMachineParent>();
            return;
        }

        List<GachaMachineParent> validMachines = new List<GachaMachineParent>(_gachaMachines.Length);
        HashSet<GachaMachineParent> seenMachines = new HashSet<GachaMachineParent>();

        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            GachaMachineParent machine = _gachaMachines[i];
            if (machine == null)
            {
                DebugLog.LogError($"가챠 머신 배열의 {i}번 참조가 비어 있습니다.");
                continue;
            }

            if (!seenMachines.Add(machine))
            {
                DebugLog.LogError($"동일한 가챠 머신이 중복 연결되어 제외했습니다: {machine.name}");
                continue;
            }

            validMachines.Add(machine);
        }

        _gachaMachines = validMachines.ToArray();
    }

    public bool PrepareItemMachine()
    {
        return PrepareMachine<UIItemGacha>();
    }

    public bool PrepareStaffMachine()
    {
        return PrepareMachine<UIStaffGacha>();
    }

    private bool PrepareMachine<T>() where T : GachaMachineParent
    {
        if (_gachaMachines == null)
            return false;

        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            if (_gachaMachines[i] is T)
            {
                _requestedInitialMachine = _gachaMachines[i];
                return true;
            }
        }

        DebugLog.LogError($"요청한 가챠 머신을 찾을 수 없습니다: {typeof(T).Name}");
        return false;
    }

    public void StartGachaStepEvent(int step)
    {
        GachaStepHandler?.Invoke(step);
    }


    private void OnScrollBeginDrag(PointerEventData eventData)
    {
        if (VisibleState != VisibleState.Appeared || _isStartGacha || _gachaMachines.Length < 2)
            return;

        DebugLog.Log("스크롤 시작");
        _machineParent.TweenStop();
        _rightButton.gameObject.SetActive(false);
        _leftButton.gameObject.SetActive(false);

        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Hide();
            _gachaMachines[i].TweenStop();
        }
    }

    private void OnScrollDrag(PointerEventData eventData)
    {
        if (VisibleState != VisibleState.Appeared || _isStartGacha || _gachaMachines.Length < 2)
            return;

        float currentX = _machineParent.anchoredPosition.x;

        // 각 인덱스 위치까지의 거리 계산
        float distanceTo0 = Mathf.Abs(currentX - (-440f));
        float distanceTo1 = Mathf.Abs(currentX - (-1130f));

        // 최대 거리 (두 위치 사이의 거리)
        float maxDistance = 690f; // |-440 - (-1130)| = 690

        // 0번 머신 크기 계산 (가까울수록 1.0, 멀수록 0.9)
        float scale0 = Mathf.Lerp(1.0f, 0.8f, Mathf.Clamp01(distanceTo0 / maxDistance));
        _gachaMachines[0].transform.localScale = Vector3.one * scale0;

        // 1번 머신 크기 계산 (가까울수록 1.0, 멀수록 0.9)
        float scale1 = Mathf.Lerp(1.0f, 0.8f, Mathf.Clamp01(distanceTo1 / maxDistance));
        _gachaMachines[1].transform.localScale = Vector3.one * scale1;
    }

    private void OnScrollEndDrag(PointerEventData eventData)
    {
        if (VisibleState != VisibleState.Appeared || _isStartGacha || _gachaMachines.Length < 2)
            return;
            
        DebugLog.Log("스크롤 종료");

        float currentX = _machineParent.anchoredPosition.x;
        int targetIndex = 0;
        float targetX = -440f;

        // 현재 X 위치에 따라 가장 가까운 인덱스 결정
        float distanceTo0 = Mathf.Abs(currentX - (-440f));
        float distanceTo1 = Mathf.Abs(currentX - (-1130f));

        if (distanceTo1 < distanceTo0)
        {
            targetIndex = 1;
            targetX = -1130f;
        }

        // 목표 위치로 Tween 이동
        float duration = 0.1f;
        Vector2 targetPos = new Vector2(targetX, _machineParent.anchoredPosition.y);
        
        // 모든 머신 스케일 0.8로
        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].TweenStop();
            _gachaMachines[i].TweenScale(Vector3.one * 0.8f, duration, Ease.Constant);
        }

        // 타겟 머신만 1.0으로
        _gachaMachines[targetIndex].TweenStop();
        _gachaMachines[targetIndex].TweenScale(Vector3.one, duration, Ease.Constant);

        _machineParent.TweenStop();
        _machineParent.TweenAnchoredPosition(targetPos, duration, Ease.Constant).OnComplete(() =>
        {
            // 이동 완료 후 해당 머신 설정
            SetMachineNoAnime(_gachaMachines[targetIndex]);
            SetNavigationButtonsActive(true);
        });
    }


    public override void Show()
    {
        if (_gachaMachines == null || _gachaMachines.Length == 0)
        {
            DebugLog.LogError("표시할 가챠 머신이 없습니다.");
            return;
        }

        // if(!UserInfo.GetIsClearChallenge("MainReward12"))
        // {
        //     PopupManager.Instance.ShowDisplayText("할일 목록 미달성");
        //     return;
        // }

        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_backgroundAudio, 0.5f);
        gameObject.SetActive(true);
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;
        _animeUI.TweenStop();
        _animeUI.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        SetStartGacha(false);
        if (_scrollRect != null)
        {
            _scrollRect.StopMovement();
            _scrollRect.enabled = false;
        }
        SetNavigationButtonsActive(false);
        GachaMachineParent initialMachine = _requestedInitialMachine ?? GetDefaultMachine();
        _requestedInitialMachine = null;
        SetMachine(initialMachine);
        SetMachineParentPos();
        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            if (_scrollRect != null)
                _scrollRect.enabled = !_isStartGacha;
            SetNavigationButtonsActive(!_isStartGacha);
            TryStartItemGachaTutorial();
        });
    }

    private void TryStartItemGachaTutorial()
    {
        if (EnableEditorEntryUnlockForTesting
            || VisibleState != VisibleState.Appeared
            || !(_currentGachaMachine is UIItemGacha)
            || UserInfo.IsTutorialStart
            || UserInfo.IsMiniGameTutorialClear
            || _miniGameTutorial == null)
        {
            return;
        }

        _miniGameTutorial.StartTutorial();
    }

    private GachaMachineParent GetDefaultMachine()
    {
        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            if (_gachaMachines[i] is UIItemGacha)
                return _gachaMachines[i];
        }

        return _gachaMachines[0];
    }


    public override void Hide()
    {
        if (VisibleState == VisibleState.Disappeared && !gameObject.activeSelf)
            return;

        VisibleState = VisibleState.Disappeared;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _animeUI.TweenStop();
        SetStartGacha(false);
        _requestedInitialMachine = null;
        _mainScene.PlayMainMusic();
        gameObject.SetActive(false);
        HiddenHandler?.Invoke();
    }

    private void SetNavigationButtonsActive(bool isActive)
    {
        bool showButtons = isActive && _gachaMachines != null && 1 < _gachaMachines.Length;

        if (_leftButton != null)
            _leftButton.gameObject.SetActive(showButtons);

        if (_rightButton != null)
            _rightButton.gameObject.SetActive(showButtons);
    }

    public void SetActiveUIComponents(bool isActive)
    {
        _uiComponents.SetActive(isActive);
    }

    public void SetActiveGachaMachine(bool isActive)
    {
        for(int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].SetActiveGachaMachine(isActive);
        }

    }

    private void SetMachine(int dir)
    {
        if (_gachaMachines == null || _gachaMachines.Length == 0)
            return;

        int currentIndex = Array.IndexOf(_gachaMachines, _currentGachaMachine);
        if (currentIndex < 0)
            currentIndex = 0;

        int nextIndex = currentIndex + dir;

        if (nextIndex < 0)
            nextIndex = _gachaMachines.Length - 1;
        else if (nextIndex >= _gachaMachines.Length)
            nextIndex = 0;

        SetMachine(_gachaMachines[nextIndex]);


    }

    private void SetMachine(GachaMachineParent gachaMachine)
    {
        if (gachaMachine == null)
            return;

        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Hide();
        }
        _currentGachaMachine = gachaMachine;
        _gachaItemList.UpdateMachineData(gachaMachine.ItemDataList);

        SetMachineParentPosAnime();
    }

    private void SetMachineNoAnime(GachaMachineParent gachaMachine)
    {
        if (gachaMachine == null)
            return;

        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Hide();
        }
        _currentGachaMachine = gachaMachine;
        _gachaItemList.UpdateMachineData(gachaMachine.ItemDataList);
        _currentGachaMachine.Show();
        TryStartItemGachaTutorial();
    }

    private void SetMachineParentPosAnime()
    {
        float duration = 0.5f;
        for(int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].TweenStop();
            _gachaMachines[i].TweenScale(Vector3.one * 0.8f, duration, Ease.Smoothstep);
        }

        int currentIndex = Array.IndexOf(_gachaMachines, _currentGachaMachine);
        Vector3 pos = _machineParent.anchoredPosition;
        
        // 인덱스에 따른 X 위치 설정
        if (currentIndex == 0)
            pos.x = -440f;
        else if (currentIndex == 1)
            pos.x = -1130f;

        _currentGachaMachine.TweenStop();
        _machineParent.TweenStop();
        _currentGachaMachine.TweenScale(Vector3.one, duration, Ease.Smoothstep);
        _machineParent.TweenAnchoredPosition(pos, duration, Ease.Smoothstep).OnComplete(() =>
        {
            _currentGachaMachine.Show();
            TryStartItemGachaTutorial();
        });
    }

    private void SetMachineParentPos()
    {
        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].TweenStop();
            _gachaMachines[i].transform.localScale = Vector3.one * 0.8f;
        }
        int currentIndex = Array.IndexOf(_gachaMachines, _currentGachaMachine);
        Vector3 pos = _machineParent.anchoredPosition;

        // 인덱스에 따른 X 위치 설정
        if (currentIndex == 0)
            pos.x = -440f;
        else if (currentIndex == 1)
            pos.x = -1130f;

        _currentGachaMachine.TweenStop();
        _machineParent.TweenStop();
        _machineParent.anchoredPosition = pos;
        _currentGachaMachine.Show();
        _currentGachaMachine.transform.localScale = Vector3.one;
    }

}
