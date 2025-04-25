using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

public class SatisfactionSystem : MonoBehaviour
{
    public event Action OnChangeSatisfactionHandler;


    [Header("Components")]
    [SerializeField] private UISatisfaction _uiSatisfaction;
    [SerializeField] private FurnitureSystem _furnitureSystem;


    private float _satisfaction;
    public float Satisfaction => _satisfaction;
    private List<DropGarbageArea> _garbageAreaList = new List<DropGarbageArea>();


    public void Start()
    {
        _uiSatisfaction.Init(this);
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            List<DropGarbageArea> garbageAreaList = _furnitureSystem.GetDropGarbageAreaList((ERestaurantFloorType)i);
            for (int j = 0, cnt2 = garbageAreaList.Count; j < cnt2; ++j)
            {
                garbageAreaList[j].OnChangeGarbageCountHandler += OnChangeSatisfactionEvent;
            }

            _garbageAreaList.AddRange(garbageAreaList);
        }

        UserInfo.OnChangeSatisfactionHandler += OnChangeSatisfactionEvent;

        OnChangeSatisfactionEvent();
    }


    /// <summary>손님의 성향과 만족도를 비교하며 식당을 나갈지 결정하는 함수</summary>
    public bool CheckCustomerTendency(CustomerTendencyType tendencyType)
    {
        if (tendencyType != CustomerTendencyType.Normal && _satisfaction <= -26)
            return false;

        if(tendencyType == CustomerTendencyType.HighlySensitive && _satisfaction <= 0)
            return false;

        return true;
    }


    /// <summary>손님의 성향과 만족도로 얼마나 더 팁을 추가로 줄지 반환하는 함수</summary>
    public float AddCustomerTipMul(CustomerTendencyType tendencyType)
    {
        //매우 불만족 상태 일경우 팁 및 추가 보상 X
        if(_satisfaction <= -26)
            return 0;

        //무난한 손님의 경우 10점이상 30점 미만일때 3% 증가
        if(tendencyType == CustomerTendencyType.Normal && 10 <= _satisfaction && _satisfaction < 30)
            return 1.03f;

        //무난한 손님이면서 30점 이상일 경우 5% 증가
        if(tendencyType == CustomerTendencyType.Normal && 30 <= _satisfaction)
            return 1.05f;

        //예민한 손님이면서 30점 이상일 경우 5% 증가 
        if(tendencyType == CustomerTendencyType.Sensitive && 30 <= _satisfaction)
            return 1.05f;

        //초 예민한 손님이면서 40점 이상일 경우 5% 증가 
        if (tendencyType == CustomerTendencyType.HighlySensitive && 40 <= _satisfaction)
            return 1.05f;

        return 1;
    }


    public SatisfactionType GetSatisfactionType()
    {
        if (_satisfaction <= -26)
            return SatisfactionType.VeryDissatisfactory;

        if (_satisfaction <= 0)
            return SatisfactionType.Dissatisfactory;

        if (_satisfaction <= 24)
            return SatisfactionType.Average;

        if (_satisfaction <= 50)
            return SatisfactionType.Satisfactory;

        return SatisfactionType.Satisfactory;
    }


    private void OnChangeSatisfactionEvent()
    {
        int satisfaction = UserInfo.GetSatisfaction(UserInfo.CurrentStage);
        
        // 모든 쓰레기 영역의 쓰레기 수 합계 계산
        int totalGarbageCount = 0;
        for (int i = 0, cnt = _garbageAreaList.Count; i < cnt; ++i)
        {
            totalGarbageCount += _garbageAreaList[i].Count;
        }
        
        // 쓰레기 개수에 따른 만족도 차감 점수 계산
        int satisfactionPenalty = CalculateGarbagePenalty(totalGarbageCount);
        satisfaction = Mathf.Clamp(satisfaction + satisfactionPenalty, ConstValue.MIN_SATISFACTION, ConstValue.MAX_SATISFACTION);

        if(satisfaction == _satisfaction)
            return;

        _satisfaction = satisfaction;
        OnChangeSatisfactionHandler?.Invoke();
    }

    /// <summary>쓰레기 개수에 따른 만족도 차감 점수를 계산</summary>
    private int CalculateGarbagePenalty(int garbageCount)
    {
        // 쓰레기 개수에 따른 만족도 차감 규칙 적용
        if (garbageCount <= 2)
            return 0;       // 0~2개: 변화 없음 (0점)
        else if (garbageCount <= 5)
            return -1;       // 3~5개: -1점
        else if (garbageCount <= 8)
            return -3;       // 6~8개: -3점
        else if (garbageCount <= 11)
            return -6;       // 9~11개: -6점
        else if (garbageCount <= 14)
            return -8;       // 12~14개: -8점
        else
        {
            // 15개 이상: -10점 + 추가 2점씩 차감
            int additionalPenalty = (garbageCount - 15) / 3; // 3개 추가될 때마다
            int additionalPoints = additionalPenalty * 2;    // 2점씩 추가 차감
            return -(10 + additionalPoints);
        }
    }
}
