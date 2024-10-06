using Muks.RecyclableScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;

public class UIChallengeTab : RecyclableVerticalScrollView<ChallengeData>
{
    [Header("Components")]
    [SerializeField] private RectTransform _rectTransform;
    private Dictionary<string, UIChallengeTabSlot> _challengeDataDic = new Dictionary<string, UIChallengeTabSlot>();

    public void ResetScrollviewY()
    {
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


    public void UpdateUI()
    {
        List<ChallengeData> noneDataList = new List<ChallengeData>();
        List<ChallengeData> doneDataList = new List<ChallengeData>();
        List<ChallengeData> clearDataList = new List<ChallengeData>();
        for (int i = 0, cnt = _dataList.Count; i < cnt; i++)
        {
            if (UserInfo.GetIsClearChallenge(_dataList[i]))
                clearDataList.Add(_dataList[i]);

            else if (UserInfo.GetIsDoneChallenge(_dataList[i]))
                doneDataList.Add(_dataList[i]);

            else
                noneDataList.Add(_dataList[i]);
        }
        List<ChallengeData> returnList = new List<ChallengeData>();
        returnList.AddRange(doneDataList);
        returnList.AddRange(noneDataList);
        returnList.AddRange(clearDataList);

        UpdateData(returnList);
    }
}
