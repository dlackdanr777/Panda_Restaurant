using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SinkGaugeBar : MonoBehaviour
{
    [SerializeField] private SpriteFillAmount _spriteFillAmount;
    [SerializeField] private TextMeshPro _tmp;
    [SerializeField] private SpriteRenderer _emptyRenderer;
    [SerializeField] private SpriteRenderer _fullRenderer;

    [SerializeField] private GameObject _smallGraduationPrefab;
    [SerializeField] private GameObject _bigGraduationPrefab;

    private List<GameObject> _bigGraduationList = new List<GameObject>();
    private List<GameObject> _smallGraduationList = new List<GameObject>();

    int _maxBowlCount = 0;
    private ERestaurantFloorType _currentfloor;
    private void SetMaxBowlCount()
    {
        int maxBowlCount = UserInfo.GetMaxSinkBowlCount(UserInfo.CurrentStage, _currentfloor);
        if (_maxBowlCount == maxBowlCount)
            return;
        
        // 기존 눈금들 정리
        if (maxBowlCount < _maxBowlCount)
        {
            // maxBowlCount가 줄어든 경우, 초과 눈금 비활성화
            for (int i = maxBowlCount - 1; i < _bigGraduationList.Count; i++)
            {
                if (i >= 0)
                    _bigGraduationList[i].gameObject.SetActive(false);
            }
            
            // 작은 눈금도 필요한 만큼만 활성화
            int requiredSmallGraduations = maxBowlCount;  // 수정된 부분
            for (int i = requiredSmallGraduations; i < _smallGraduationList.Count; i++)
            {
                if (i >= 0)
                    _smallGraduationList[i].gameObject.SetActive(false);
            }
        }
        else if (maxBowlCount > _maxBowlCount)
        {
            // maxBowlCount가 증가한 경우, 눈금 추가 생성
            SpriteRenderer gaugeSprite = _spriteFillAmount.SpriteRenderer;
            float gaugeWidth = gaugeSprite.bounds.size.x;
            
            // 게이지 영역의 시작과 끝 위치 계산
            Vector3 gaugeLeft = gaugeSprite.transform.position - new Vector3(gaugeWidth * 0.5f, 0, 0);
            Vector3 gaugeRight = gaugeSprite.transform.position + new Vector3(gaugeWidth * 0.5f, 0, 0);
            
            // 기존 큰 눈금 활성화 (이미 생성되어 있는 것)
            for (int i = 0; i < _bigGraduationList.Count && i < maxBowlCount - 1; i++)
            {
                _bigGraduationList[i].gameObject.SetActive(true);
            }
            
            // 새로운 큰 눈금 생성 (필요한 경우)
            for (int i = _bigGraduationList.Count; i < maxBowlCount - 1; i++)
            {
                GameObject newBigGraduation = Instantiate(_bigGraduationPrefab, transform);
                _bigGraduationList.Add(newBigGraduation);
            }
            
            // 기존 작은 눈금 활성화
            for (int i = 0; i < _smallGraduationList.Count && i < maxBowlCount; i++)
            {
                _smallGraduationList[i].gameObject.SetActive(true);
            }
            
            // 새로운 작은 눈금 생성 (필요한 경우)
            for (int i = _smallGraduationList.Count; i < maxBowlCount; i++)
            {
                GameObject newSmallGraduation = Instantiate(_smallGraduationPrefab, transform);
                _smallGraduationList.Add(newSmallGraduation);
            }
        }
        
        // 모든 눈금 위치 조정
        PositionGraduations(maxBowlCount);
        
        // 새 maxBowlCount 저장
        _maxBowlCount = maxBowlCount;
    }

    private void PositionGraduations(int maxBowlCount)
    {
        if (maxBowlCount <= 1)
            return;
        
        SpriteRenderer gaugeSprite = _spriteFillAmount.SpriteRenderer;
        float gaugeWidth = gaugeSprite.bounds.size.x;
        
        // 고정된 게이지 좌표 사용
        Vector3 gaugeLeft = new Vector3(-1.3f, gaugeSprite.transform.position.y, gaugeSprite.transform.position.z);
        Vector3 gaugeRight = new Vector3(1.3f, gaugeSprite.transform.position.y, gaugeSprite.transform.position.z);
        
        // 구간 수 계산
        int sections = maxBowlCount;
        float sectionWidth = (gaugeRight.x - gaugeLeft.x) / sections;
        
        // 큰 눈금 위치 계산 (양 끝 제외)
        for (int i = 0; i < maxBowlCount - 1; i++)
        {
            // 게이지를 maxBowlCount 등분했을 때 각 구간의 경계에 큰 눈금 배치
            // 단, 첫 구간의 시작과 마지막 구간의 끝은 제외
            float xPos = gaugeLeft.x + sectionWidth * (i + 1);
            Vector3 position = new Vector3(xPos, 0, 0);
            
            if (i < _bigGraduationList.Count)
            {
                _bigGraduationList[i].transform.localPosition = position;
                Debug.Log($"Big graduation {i} position: {position}");
            }
        }
        
        // 작은 눈금 위치 계산 (큰 눈금 사이와 양 끝)
        int smallGradCount = maxBowlCount; // 작은 눈금은 각 구간의 중간에 위치 (양 끝 포함)
        
        // 각 구간의 중간에 작은 눈금 배치
        for (int i = 0; i < smallGradCount; i++)
        {
            float xPos = gaugeLeft.x + sectionWidth * i + sectionWidth * 0.5f;
            Vector3 position = new Vector3(xPos, 0, 0);
            
            if (i < _smallGraduationList.Count)
            {
                _smallGraduationList[i].transform.localPosition = position;
            }
        }
    }

    public void Init(ERestaurantFloorType floor)
    {
        _currentfloor = floor;
        // 초기화 시 maxBowlCount 설정
        SetMaxBowlCount();
        UserInfo.OnChangeMaxSinkBowlHandler += SetMaxBowlCount;
    }

    public void SetGauge(int currentBowlCount, int maxBowlCount, float gauge)
    {
        _spriteFillAmount.SetFillAmount(gauge);
        _tmp.SetText(currentBowlCount + "/" + maxBowlCount);
        SetChangeRenderer(currentBowlCount, maxBowlCount);
    }

    private void SetChangeRenderer(int currentBowlCount, int maxBowlCount)
    {
        bool isFull = maxBowlCount <= currentBowlCount;
        if (isFull)
        {
            _emptyRenderer.gameObject.SetActive(false);
            _fullRenderer.gameObject.SetActive(true);
        }
        else
        {
            _emptyRenderer.gameObject.SetActive(true);
            _fullRenderer.gameObject.SetActive(false);
        }
    }
}
