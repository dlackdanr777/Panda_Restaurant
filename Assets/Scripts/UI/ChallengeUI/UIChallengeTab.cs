using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIChallengeTab : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _tabButton;
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIChallengeTabSlot _slotPrefab;

    private List<ChallengeData> _challengeDataList = new List<ChallengeData>();
    private Dictionary<int, ChallengeData> _doneChallengeDataDic = new Dictionary<int, ChallengeData>();
    private Dictionary<int, ChallengeData> _clearChallengeDataDic = new Dictionary<int, ChallengeData>();
    private List<UIChallengeTabSlot> _slotList = new List<UIChallengeTabSlot>();

    private Func<string, bool> _isChallengeDone;
    private Func<string, bool> _isChallengeClear;


    public void Init(List<ChallengeData> dataList, Func<string, bool> isChallengeDone, Func<string, bool> isChallengeClear)
    {
        _challengeDataList = dataList;
        _isChallengeDone = isChallengeDone;
        _isChallengeClear = isChallengeClear;

        for (int i = 0, cnt = dataList.Count; i < cnt; i++)
        {
            UIChallengeTabSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetData(dataList[i]);
            slot.Init(null, () =>
            {
                CheckChallenge();
                UpdateSlot();
            });
            _slotList.Add(slot);
        }

        CheckChallenge();
        UpdateSlot();
    }


    public void UpdateUI()
    {
        CheckChallenge();
        UpdateSlot();
    }


    private void CheckChallenge()
    {
        for (int i = 0, cnt = _challengeDataList.Count; i < cnt; i++)
        {
            if (!_doneChallengeDataDic.ContainsKey(i) && _isChallengeDone(_challengeDataList[i].Id))
                _doneChallengeDataDic.Add(i, _challengeDataList[i]);
                

            if (!_clearChallengeDataDic.ContainsKey(i) && _isChallengeClear(_challengeDataList[i].Id))
                _clearChallengeDataDic.Add(i, _challengeDataList[i]);
        }
    }


    private void UpdateSlot()
    {
        for (int i = 0, cnt = _slotList.Count; i < cnt; i++)
        {
            _slotList[i].SetNone();
        }

        foreach (var data in _doneChallengeDataDic)
        {
            if (_clearChallengeDataDic.ContainsKey(data.Key))
                continue;

            _slotList[data.Key].SetDone();
        }

        foreach(var data in _clearChallengeDataDic)
        {
            _slotList[data.Key].SetClear();
        }

    }


}
