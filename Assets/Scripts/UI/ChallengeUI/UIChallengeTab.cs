using System;
using System.Collections.Generic;
using UnityEngine;

public class UIChallengeTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private RectTransform _contentRect;
    [SerializeField] private UIChallengeTabSlot _slotPrefab;

    private UIChallenge _uiChallenge;
    private List<UIChallengeTabSlot> _uiChallengeTabSlotList = new List<UIChallengeTabSlot>();
    private List<ChallengeData> _dataList = new List<ChallengeData>();
    private Func<List<ChallengeData>> _onUpdateEvent;

    public void Init(List<ChallengeData> dataList)
    {
        _dataList = dataList;
        CreateSlots();
        ChallengeManager.Instance.OnAddChallengeHandler += OnUpdateEvent;
    }

    public void Init(UIChallenge uiChallenge, Func<List<ChallengeData>> onUpdateEvent)
    {
        _uiChallenge = uiChallenge;
        _onUpdateEvent = onUpdateEvent;
    }

    private void CreateSlots()
    {
        // 기존 슬롯들 제거
        foreach (var slot in _uiChallengeTabSlotList)
        {
            if (slot != null)
                DestroyImmediate(slot.gameObject);
        }
        _uiChallengeTabSlotList.Clear();

        // 새 슬롯들 생성
        DebugLog.Log($"[UIChallengeTab] CreateSlots - 데이터 수: {_dataList.Count}");
        for (int i = 0; i < _dataList.Count; i++)
        {
            UIChallengeTabSlot slot = Instantiate(_slotPrefab, _contentRect);
            slot.Init();
            slot.SetUIChellenge(_uiChallenge);
            slot.UpdateSlot(_dataList[i]);
            _uiChallengeTabSlotList.Add(slot);
        }
    }

    private void UpdateSlots(List<ChallengeData> dataList)
    {
        _dataList = dataList;

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
        _rectTransform.SetAsLastSibling();
        _contentRect.gameObject.SetActive(true);
    }

    public void SetAsFirstSibling()
    {
        _rectTransform.SetAsFirstSibling();
        _contentRect.gameObject.SetActive(false);
    }


    public bool UpdateUI()
    {
        bool returnValue = false;
        List<ChallengeData> noneDataList = new List<ChallengeData>();
        List<ChallengeData> doneDataList = new List<ChallengeData>();
        List<ChallengeData> clearDataList = new List<ChallengeData>();
        for (int i = 0, cnt = _dataList.Count; i < cnt; i++)
        {
            if (UserInfo.GetIsClearChallenge(_dataList[i]))
            {
                if (_dataList[i].Id.Contains("Repeat"))
                {
                    //DebugLog.Log($" - Repeat Data: {_dataList[i].Id}");
                    continue;
                }

                clearDataList.Add(_dataList[i]);
            }


            else if (UserInfo.GetIsDoneChallenge(_dataList[i]))
            {
                doneDataList.Add(_dataList[i]);
                returnValue = true;
            }

            else
                noneDataList.Add(_dataList[i]);
        }
        List<ChallengeData> returnList = new List<ChallengeData>();
        returnList.AddRange(doneDataList);
        returnList.AddRange(noneDataList);
        returnList.AddRange(clearDataList);

        UpdateSlots(returnList);
        return returnValue;
    }

    private void OnUpdateEvent()
    {
        _dataList = _onUpdateEvent.Invoke();
        UpdateUI();
    }

    private void OnDestroy()
    {

        ChallengeManager.Instance.OnAddChallengeHandler -= OnUpdateEvent;
    }
}
