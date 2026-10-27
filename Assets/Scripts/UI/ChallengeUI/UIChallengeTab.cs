using System;
using System.Collections.Generic;
using UnityEngine;

public class UIChallengeTab : MonoBehaviour
{
    private ChallengeManager _subscribedChallengeManager;

    [Header("Components")]
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private RectTransform _contentRect;
    [SerializeField] private UIChallengeTabSlot _slotPrefab;

    private UIChallenge _uiChallenge;
    private List<UIChallengeTabSlot> _uiChallengeTabSlotList = new List<UIChallengeTabSlot>();
    private List<ChallengeData> _dataList = new List<ChallengeData>();
    private Func<List<ChallengeData>> _onUpdateEvent;
    private readonly List<ChallengeData> _noneDataList = new List<ChallengeData>();
    private readonly List<ChallengeData> _doneDataList = new List<ChallengeData>();
    private readonly List<ChallengeData> _clearDataList = new List<ChallengeData>();
    private readonly List<ChallengeData> _sortedDataList = new List<ChallengeData>();
    private bool _isDirty = true;
    private bool _isSelected;

    public void Init(List<ChallengeData> dataList)
    {
        _dataList = dataList;
        _subscribedChallengeManager = ChallengeManager.Instance;
        _subscribedChallengeManager.OnChallengePercentUpdateHandler += OnPercentUpdated;
    }

    public void Init(UIChallenge uiChallenge, Func<List<ChallengeData>> onUpdateEvent)
    {
        _uiChallenge = uiChallenge;
        _onUpdateEvent = onUpdateEvent;
    }

    private void UpdateSlots(List<ChallengeData> dataList)
    {
        // 필요한 슬롯 수와 현재 슬롯 수 비교
        int requiredSlots = dataList.Count;
        int currentSlots = _uiChallengeTabSlotList.Count;

        // 부족한 슬롯 생성
        for (int i = currentSlots; i < requiredSlots; i++)
        {
            UIChallengeTabSlot slot = Instantiate(_slotPrefab, _contentRect);
            slot.Init();
            slot.SetUIChellenge(_uiChallenge);
            _uiChallengeTabSlotList.Add(slot);
        }

        // 초과 슬롯 비활성화
        for (int i = requiredSlots; i < currentSlots; i++)
        {
            _uiChallengeTabSlotList[i].gameObject.SetActive(false);
        }

        // 모든 활성 슬롯 업데이트
        for (int i = 0; i < requiredSlots; i++)
        {
            _uiChallengeTabSlotList[i].gameObject.SetActive(true);
            _uiChallengeTabSlotList[i].UpdateSlot(dataList[i]);
        }
    }

    public void ResetScrollviewY()
    {
        if (_contentRect != null)
            _contentRect.anchoredPosition = new Vector2(0, 0);
    }

    public void SetAsLastSibling()
    {
        _isSelected = true;
        _rectTransform.SetAsLastSibling();
        _contentRect.gameObject.SetActive(true);
        RefreshVisibleSlots();
    }

    public void SetAsFirstSibling()
    {
        _isSelected = false;
        _rectTransform.SetAsFirstSibling();
        _contentRect.gameObject.SetActive(false);
    }


    public bool UpdateUI()
    {
        if (_onUpdateEvent != null) _dataList = _onUpdateEvent.Invoke();
        _isDirty = true;
        for (int i = 0; i < _dataList.Count; i++)
        {
            if (!UserInfo.GetIsClearChallenge(_dataList[i]) && UserInfo.GetIsDoneChallenge(_dataList[i]))
                return true;
        }
        return false;
    }

    private bool CanBind => _isSelected && isActiveAndEnabled && _uiChallenge != null &&
        (_uiChallenge.VisibleState == VisibleState.Appearing || _uiChallenge.VisibleState == VisibleState.Appeared);

    private void LateUpdate()
    {
        RefreshVisibleSlots();
    }

    private void RefreshVisibleSlots()
    {
        if (!_isDirty || !CanBind) return;
        _isDirty = false;
        _noneDataList.Clear();
        _doneDataList.Clear();
        _clearDataList.Clear();
        _sortedDataList.Clear();
        for (int i = 0, cnt = _dataList.Count; i < cnt; i++)
        {
            if (UserInfo.GetIsClearChallenge(_dataList[i]))
            {
                if (_dataList[i].Id.Contains("Repeat"))
                {
                    //DebugLog.Log($" - Repeat Data: {_dataList[i].Id}");
                    continue;
                }

                _clearDataList.Add(_dataList[i]);
            }


            else if (UserInfo.GetIsDoneChallenge(_dataList[i]))
            {
                _doneDataList.Add(_dataList[i]);
            }

            else
                _noneDataList.Add(_dataList[i]);
        }
        _sortedDataList.AddRange(_doneDataList);
        _sortedDataList.AddRange(_noneDataList);
        _sortedDataList.AddRange(_clearDataList);

        UpdateSlots(_sortedDataList);
    }

    private void OnPercentUpdated(ChallengeType type)
    {
        if (!CanBind)
        {
            _isDirty = true;
            return;
        }
        if (_isDirty) return;
        foreach (var slot in _uiChallengeTabSlotList)
            if (slot.gameObject.activeInHierarchy) slot.UpdatePercent(type);
    }

    private void OnDisable()
    {
        _isDirty = true;
    }

    private void OnDestroy()
    {

        if (_subscribedChallengeManager != null)
            _subscribedChallengeManager.OnChallengePercentUpdateHandler -= OnPercentUpdated;
        _subscribedChallengeManager = null;
    }
}
