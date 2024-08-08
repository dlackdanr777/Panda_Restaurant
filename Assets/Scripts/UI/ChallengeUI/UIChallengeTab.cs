using System.Collections;
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
    private List<UIChallengeTabSlot> _slotList = new List<UIChallengeTabSlot>();

    public void Init(List<ChallengeData> dataList)
    {
        _challengeDataList = dataList;

        for(int i = 0, cnt = dataList.Count; i < cnt; i++)
        {
            UIChallengeTabSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.SetData(dataList[i]);
            _slotList.Add(slot);
        }
    }



}
