using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;

public class UIGacha : MobileUIView
{
    public event Action<int> GachaStepHandler;

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

    private bool _isStartGacha;
    public bool IsStartGacha => _isStartGacha;
    public void SetStartGacha(bool isStart)
    {
        _isStartGacha = isStart;
        _scrollRect.enabled = !isStart;

    }
    public override void Init()
    {
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

    public void StartGachaStepEvent(int step)
    {
        GachaStepHandler?.Invoke(step);
    }


    private void OnScrollBeginDrag(PointerEventData eventData)
    {
        if(_isStartGacha)
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
        if(_isStartGacha)
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
        if (_isStartGacha)
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
            _rightButton.gameObject.SetActive(true);
            _leftButton.gameObject.SetActive(true);
        });
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_backgroundAudio, 0.5f);
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        SetStartGacha(false);
        SetMachine(_gachaMachines[0]);
        SetMachineParentPos();
        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;

            if(!UserInfo.IsTutorialStart && !UserInfo.IsMiniGameTutorialClear)
            {
                _miniGameTutorial.StartTutorial();
            }
        });
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        _mainScene.PlayMainMusic();
        gameObject.SetActive(false);
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
        int currentIndex = Array.IndexOf(_gachaMachines, _currentGachaMachine);
        int nextIndex = currentIndex + dir;

        if (nextIndex < 0)
            nextIndex = _gachaMachines.Length - 1;
        else if (nextIndex >= _gachaMachines.Length)
            nextIndex = 0;

        SetMachine(_gachaMachines[nextIndex]);


    }

    private void SetMachine(GachaMachineParent gachaMachine)
    {
        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Hide();
        }
        _currentGachaMachine = gachaMachine;
        _gachaItemList.UpdateData(gachaMachine.ItemDataList);

        SetMachineParentPosAnime();
    }

    private void SetMachineNoAnime(GachaMachineParent gachaMachine)
    {
        for (int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Hide();
        }
        _currentGachaMachine = gachaMachine;
        _gachaItemList.UpdateData(gachaMachine.ItemDataList);
        _currentGachaMachine.Show();
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
        });
    }

    private void SetMachineParentPos()
    {
        for(int i = 0; i < _gachaMachines.Length; i++)
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
