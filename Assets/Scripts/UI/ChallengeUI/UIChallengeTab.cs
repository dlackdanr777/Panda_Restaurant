using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIChallengeTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _tabButton;
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIChallengeTabSlot _slotPrefab;

    private List<ChallengeData> _challengeDataList = new List<ChallengeData>();
    private Dictionary<string, UIChallengeTabSlot> _challengeDataDic = new Dictionary<string, UIChallengeTabSlot>();
    private List<UIChallengeTabSlot> _slotList = new List<UIChallengeTabSlot>();

    private Func<string, bool> _isChallengeDone;
    private Func<string, bool> _isChallengeClear;
    List<UIChallengeTabSlot> _doneSlotList = new List<UIChallengeTabSlot>();
    List<UIChallengeTabSlot> _clearSlotList = new List<UIChallengeTabSlot>();

    public void Init(List<ChallengeData> dataList, Func<string, bool> isChallengeDone, Func<string, bool> isChallengeClear)
    {
        _tabButton.onClick.AddListener(() => _rectTransform.SetAsLastSibling());
        _challengeDataList = dataList;
        _isChallengeDone = isChallengeDone;
        _isChallengeClear = isChallengeClear;

        for (int i = 0, cnt = dataList.Count; i < cnt; i++)
        {
            UIChallengeTabSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(dataList[i],null, () =>
            {
                UpdateSlot();
            });
            _slotList.Add(slot);
            _challengeDataDic.Add(dataList[i].Id, slot);
        }

        UpdateSlot();
    }


    public void UpdateUI()
    {
        UpdateSlot();
    }


    public void ResetScrollviewY()
    {
        _slotParent.anchoredPosition = new Vector2(0, 0);
    }


    private void UpdateSlot()
    {
        _doneSlotList.Clear();
        _clearSlotList.Clear();

        for (int i = 0, cnt = _slotList.Count; i < cnt; i++)
        {
            if (_isChallengeClear(_slotList[i].Data.Id))
            {
                _clearSlotList.Add(_slotList[i]);
                continue;
            }

            if (_isChallengeDone(_slotList[i].Data.Id))
            {
                _doneSlotList.Add(_slotList[i]);

                continue;
            }

            _slotList[i].SetNone();
        }

        for(int i = _doneSlotList.Count - 1; 0 <= i; --i)
        {
            _doneSlotList[i].SetDone();
        }

        for(int i = 0, cnt = _clearSlotList.Count; i < cnt; ++i)
        {
            _clearSlotList[i].SetClear();
        }
    }
}
