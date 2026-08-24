using System;
using System.Collections.Generic;
using UnityEngine;


public class KitchenUtensilGroup: MonoBehaviour
{
    private const float MaximumAssignedCookingBonusPercent = 1000f;

    [Header("Option")]
    [SerializeField] private ERestaurantFloorType _floorType;
    public ERestaurantFloorType FloorType => _floorType;

    [Space]
    [Header("Components")]
    [SerializeField] private KitchenUtensil[] _kitchenUtensils;
    [SerializeField] private GameObject[] _smokeAnimations;

    [Space]
    [Header("UI Options")]
    [SerializeField] private RectTransform _burnerTimerParent;
    [SerializeField] private UIBurnerTimer _burnerTimerPrefab;
    [SerializeField] private Transform[] _burnerTimerTrs;

    [Space]
    [Header("Transforms")]
    [SerializeField] private Transform _defaultChef1Pos;
    [SerializeField] private Transform _defaultChef2Pos;
    [SerializeField] private Transform _door1;
    [SerializeField] private Transform _door2;


    private UIBurnerTimer[] _burnerTimers;
    private KitchenBurnerData[] _burnerDatas;
    private Dictionary<KitchenUtensilType, List<KitchenUtensil>> _kitchenUtensilDic = new Dictionary<KitchenUtensilType, List<KitchenUtensil>>();
    private Queue<CookingData> _cookingQueue = new Queue<CookingData>();
    private SinkKitchenUtensil _sinkKitchenUtensil;

    public List<KitchenBurnerData> GetCookingBurnerDataList()
    {
        List<KitchenBurnerData> dataList = new List<KitchenBurnerData>();
        for(int i = 0, cnt = _burnerDatas.Length; i < cnt; ++i)
        {
            if (!_burnerDatas[i].IsUsable || _burnerDatas[i].CookingData.IsDefault() || _burnerDatas[i].IsStaffUsable)
                continue;

            dataList.Add(_burnerDatas[i]);
        }

        return dataList;
    }

    public CookingRemainingTimeReductionBatchResult ApplyRemainingCookingTimeReductionToActiveBurners(
        float reductionPercent)
    {
        if (float.IsNaN(reductionPercent)
            || float.IsInfinity(reductionPercent)
            || reductionPercent < 0f
            || reductionPercent > 100f
            || _burnerDatas == null
            || _burnerTimers == null
            || _burnerDatas.Length != _burnerTimers.Length)
        {
            return default;
        }

        List<KitchenBurnerData> targets = new List<KitchenBurnerData>();
        List<CookingRemainingTimeReductionResult> previews =
            new List<CookingRemainingTimeReductionResult>();
        List<float> originalRemainingTimes = new List<float>();
        List<float> originalBaselines = new List<float>();
        double beforeTotal = 0d;
        double afterTotal = 0d;
        for (int index = 0; index < _burnerDatas.Length; index++)
        {
            KitchenBurnerData burnerData = _burnerDatas[index];
            if (!burnerData.IsUsable
                || burnerData.CookingData.IsDefault()
                || burnerData.Time <= 0f
                || burnerData.CookingData.TableData == null
                || burnerData.CookingData.TableData.CurrentCustomer == null)
            {
                continue;
            }

            CookingRemainingTimeReductionResult preview =
                CookingRuntimePolicyCalculator.ApplyInstantRemainingTimeReduction(
                    burnerData.CookingData.CookTime,
                    burnerData.Time,
                    burnerData.MinimumDurationBaselineCookTime,
                    burnerData.RealElapsedCookingSeconds,
                    reductionPercent);
            if (reductionPercent > 0f && !preview.Applied)
            {
                return default;
            }

            targets.Add(burnerData);
            previews.Add(preview);
            originalRemainingTimes.Add(burnerData.Time);
            originalBaselines.Add(burnerData.MinimumDurationBaselineCookTime);
            beforeTotal += burnerData.Time;
            afterTotal += preview.RemainingTime;
        }

        if (reductionPercent == 0f || targets.Count == 0)
        {
            return new CookingRemainingTimeReductionBatchResult(
                targets.Count,
                0,
                ToFiniteRemainingTimeTotal(beforeTotal),
                ToFiniteRemainingTimeTotal(beforeTotal));
        }

        int changedCount = 0;
        try
        {
            for (int index = 0; index < targets.Count; index++)
            {
                CookingRemainingTimeReductionResult applied;
                if (!targets[index].TryApplyRemainingCookingTimeReduction(
                        reductionPercent,
                        out applied))
                {
                    throw new InvalidOperationException(
                        "A validated Burner rejected the Skill09 remaining-time reduction.");
                }

                changedCount += applied.Applied ? 1 : 0;
            }

            for (int index = 0; index < targets.Count; index++)
            {
                int burnerIndex = Array.IndexOf(_burnerDatas, targets[index]);
                if (burnerIndex < 0 || _burnerTimers[burnerIndex] == null)
                {
                    throw new InvalidOperationException(
                        "A validated Burner has no matching timer UI.");
                }

                _burnerTimers[burnerIndex].SetFillAmount(
                    1 - (targets[index].Time / targets[index].CookingData.CookTime));
            }
        }
        catch (Exception exception)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                targets[index].RestoreCookingClockSnapshot(
                    originalRemainingTimes[index],
                    originalBaselines[index]);
            }

            for (int index = 0; index < targets.Count; index++)
            {
                try
                {
                    int burnerIndex = Array.IndexOf(_burnerDatas, targets[index]);
                    if (burnerIndex >= 0 && _burnerTimers[burnerIndex] != null)
                    {
                        _burnerTimers[burnerIndex].SetFillAmount(
                            1 - (targets[index].Time / targets[index].CookingData.CookTime));
                    }
                }
                catch (Exception rollbackException)
                {
                    Debug.LogError(
                        "[STAFF_SKILL09_REMAINING_TIME] Timer rollback failed: "
                        + rollbackException.Message);
                }
            }

            Debug.LogError(
                "[STAFF_SKILL09_REMAINING_TIME] Atomic Burner reduction failed: "
                + exception.Message);
            return default;
        }

        return new CookingRemainingTimeReductionBatchResult(
            targets.Count,
            changedCount,
            ToFiniteRemainingTimeTotal(beforeTotal),
            ToFiniteRemainingTimeTotal(afterTotal));
    }

    private static float ToFiniteRemainingTimeTotal(double value)
    {
        return value >= float.MaxValue ? float.MaxValue : (float)value;
    }

    public SinkKitchenUtensil GetSinkKitchenUtensil()
    {
        return _sinkKitchenUtensil;
    }

    private static float CalculateAssignedCookingSpeedMultiplier(
        bool hasAssignedStaff,
        bool isStaffWorking,
        float assignedCookingBonusPercent)
    {
        if (!hasAssignedStaff
            || !isStaffWorking
            || assignedCookingBonusPercent <= 0f
            || float.IsNaN(assignedCookingBonusPercent)
            || float.IsInfinity(assignedCookingBonusPercent)
            || assignedCookingBonusPercent > MaximumAssignedCookingBonusPercent)
        {
            return 1f;
        }

        float multiplier = 1f + assignedCookingBonusPercent * 0.01f;
        if (float.IsNaN(multiplier)
            || float.IsInfinity(multiplier)
            || multiplier < 1f
            || multiplier > 11f)
        {
            return 1f;
        }

        return multiplier;
    }



    public Vector2 GetStaffPos(EquipStaffType type)
    {
        switch (type)
        {
            case EquipStaffType.Chef:
                return _defaultChef1Pos.position;
            //case EquipStaffType.Chef2:
                //return _defaultChef2Pos.position;
        }

        Debug.LogError("직원 종류 값이 잘못 입력되었습니다:" + type);
        return new Vector2(0, 0);
    }

    public Vector3 GetDoorPos(Vector3 pos)
    {
        if (Mathf.Abs(_door1.position.y - pos.y) < 2)
            return _door1.position;

        else if (Mathf.Abs(_door2.position.y - pos.y) < 2)
            return _door2.position;

        return Vector3.zero;
    }

    public void Init()
    {   
        // Burner는 5개 (Burner1~Burner5, enum 인덱스 0~4)
        int burnerCount = (int)KitchenUtensilType.Burner5 + 1;
        _burnerDatas = new KitchenBurnerData[burnerCount];
        _burnerTimers = new UIBurnerTimer[burnerCount];
        
        // Burner 데이터 초기화
        for (int i = 0; i < burnerCount; ++i)
        {
            _burnerDatas[i] = new KitchenBurnerData();
            _burnerTimers[i] = Instantiate(_burnerTimerPrefab, _burnerTimerParent);
            _burnerTimers[i].Init();
            _burnerTimers[i].SetWorldTransform(_burnerTimerTrs[i]);
            _burnerTimers[i].SetFillAmount(0);
            _burnerTimers[i].gameObject.SetActive(false);
            _smokeAnimations[i].gameObject.SetActive(false);
        }

        // KitchenUtensil 딕셔너리 초기화
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            _kitchenUtensilDic.Add((KitchenUtensilType)i, new List<KitchenUtensil>());
        }

        // 모든 KitchenUtensil 초기화 및 딕셔너리에 추가
        for (int i = 0, cnt = _kitchenUtensils.Length; i < cnt; ++i)
        {
            _kitchenUtensils[i].Init(_floorType);
            KitchenUtensilType type = _kitchenUtensils[i].Type;
            _kitchenUtensilDic[type].Add(_kitchenUtensils[i]);
            
            // Burner 타입이면 BurnerKitchenUtensil 리스트에 추가 및 데이터 설정
            if(type >= KitchenUtensilType.Burner1 && type <= KitchenUtensilType.Burner5)
            {
                BurnerKitchenUtensil burner = (BurnerKitchenUtensil)_kitchenUtensils[i];
                if (burner != null)
                {
                    int burnerIndex = (int)type; // Burner1=0, Burner2=1, ..., Burner5=4
                    _burnerDatas[burnerIndex].SetKitchenUtensil(burner);
                    burner.SetData(_burnerDatas[burnerIndex]);
                }
            }
        }

        _burnerDatas[0].IsUsable = true; // Burner1은 기본으로 사용 가능
        _sinkKitchenUtensil = (SinkKitchenUtensil)_kitchenUtensilDic[KitchenUtensilType.Sink][0];
        UpdateKitchen();
        UserInfo.OnChangeKitchenUtensilHandler += OnChangeKitchenUtensilEvent;
    }


    void Update()
    {
        if (!UserInfo.IsFloorValid(UserInfo.CurrentStage, _floorType))
            return;

        GameManager gameManager = GameManager.Instance;
        float feverCookingMultiplier =
            gameManager.FeverRuntimeContext.CookingMultiplier;

        for (int i = 0, cnt = _burnerDatas.Length; i < cnt; ++i)
        {
            if (!_burnerDatas[i].IsUsable)
                continue;

            if (_burnerDatas[i].Time <= 0)
            {
                DequeueFood(i);
                continue; // return이 아닌 continue로 변경 - 다음 버너도 체크해야 함
            }

            // 요리 중인 데이터가 있는 경우
            if (!_burnerDatas[i].CookingData.IsDefault())
            {
                if (_burnerDatas[i].CookingData.TableData == null || _burnerDatas[i].CookingData.TableData.CurrentCustomer == null)
                {
                    DequeueFood(i);
                    continue; // return이 아닌 continue로 변경
                }

                KitchenBurnerData burnerData = _burnerDatas[i];
                Staff assignedStaff = burnerData.UseStaff;
                BurnerKitchenUtensil burner = burnerData.KitchenUtensil;
                float sharedBaseCookingMultiplier =
                    gameManager.GetCookingSpeedMul(
                        _floorType,
                        burnerData.CookingData.FoodData.FoodType);
                float localEquipmentCookingMultiplier =
                    CookingRuntimeMultiplierCalculator.CalculateEquipmentCookingMultiplier(
                        burner != null ? burner.EquipmentCookingBonusPercent : 0f);
                float chefPassiveCookingMultiplier =
                    CookingRuntimeMultiplierCalculator.CalculateChefPassiveCookingMultiplier(
                        assignedStaff != null,
                        burner != null && burner.IsStaffWorking,
                        burnerData.AddCookSpeedMul);
                float assignedCookingSkillMultiplier = CalculateAssignedCookingSpeedMultiplier(
                    assignedStaff != null,
                    burner != null && burner.IsStaffWorking,
                    assignedStaff != null
                        ? assignedStaff.RuntimeSkillContext.AssignedCookingBonusPercent
                        : 0f);

                float burnerTouchMultiplier = burner != null ? burner.CookSpeedMul : 1f;
                float sameFoodTypeMultiplier = 1f;

                if (_burnerDatas[i].FoodType == _burnerDatas[i].CookingData.FoodData.FoodType)
                {
                    sameFoodTypeMultiplier = 1.1f; // 같은 음식 타입일 때는 10% 더 빠르게 요리
                }

                float finalCookingMultiplier =
                    FeverRuntimeMultiplierCalculator.CalculateCookingMultiplier(
                        sharedBaseCookingMultiplier,
                        localEquipmentCookingMultiplier,
                        chefPassiveCookingMultiplier,
                        assignedCookingSkillMultiplier,
                        feverCookingMultiplier,
                        burnerTouchMultiplier,
                        sameFoodTypeMultiplier);
                float deltaSeconds = Time.deltaTime;
                burnerData.Time = CookingRuntimePolicyCalculator.CalculateNextRemainingTime(
                    burnerData.CookingData.CookTime,
                    burnerData.MinimumDurationBaselineCookTime,
                    burnerData.Time,
                    burnerData.RealElapsedCookingSeconds,
                    deltaSeconds,
                    finalCookingMultiplier);
                burnerData.AdvanceCookingClock(deltaSeconds);
                _burnerTimers[i].SetFillAmount(1 - (burnerData.Time / burnerData.CookingData.CookTime));
            }
        }
    }


    public void EqueueFood(CookingData foodData)
    {
        _cookingQueue.Enqueue(foodData);
    }


    private void DequeueFood(int burnerIndex)
    {
        if (!_burnerDatas[burnerIndex].CookingData.IsDefault())
        {
            UserInfo.AddCookCount(_burnerDatas[burnerIndex].CookingData.FoodData.Id);
            _burnerDatas[burnerIndex].CookingData.OnCompleted?.Invoke();
        }

        if (_cookingQueue.Count == 0)
        {
            ResetBurnerData(burnerIndex);
            return;
        }

        CookingData cookingData = _cookingQueue.Dequeue();
        _burnerDatas[burnerIndex].CookingData = cookingData;
        _burnerDatas[burnerIndex].Time = cookingData.CookTime;
        _burnerDatas[burnerIndex].InitializeCookingClock(cookingData.CookTime);
        _burnerTimers[burnerIndex].SetActive(true);
        _smokeAnimations[burnerIndex].gameObject.SetActive(true);
        _burnerTimers[burnerIndex].SetFillAmount(0);
        _burnerTimers[burnerIndex].SetImage(cookingData.FoodData.ThumbnailSprite);
    }

    private void UpdateKitchen()
    {
        KitchenUtensilData equipData;
        KitchenUtensilType type;
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            type = (KitchenUtensilType)i;
            equipData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _floorType, type);
            
            // 해당 타입의 모든 KitchenUtensil에 데이터 설정
            foreach (KitchenUtensil data in _kitchenUtensilDic[type])
            {
                data.SetData(equipData);
            }

            // Burner 타입인 경우 추가 처리
            if (type >= KitchenUtensilType.Burner1 && type <= KitchenUtensilType.Burner5)
            {
                int burnerIndex = (int)type; // Burner1=0, Burner2=1, ..., Burner5=4
                _burnerDatas[burnerIndex].SetFoodType(equipData != null ? equipData.FoodType : FoodType.None);
                
                // Burner2~5는 장비가 있어야 사용 가능
                if (type >= KitchenUtensilType.Burner2)
                {
                    if (equipData != null)
                    {
                        _burnerDatas[burnerIndex].IsUsable = true;
                    }
                    else
                    {
                        _burnerDatas[burnerIndex].IsUsable = false;
                        SetDefalutBurnerData(burnerIndex);
                    }
                }
            }
        }
        CheckSink();
    }


    private void OnChangeKitchenUtensilEvent(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        if (_floorType != floorType)
            return;

        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, floorType, type);
        
        // 해당 타입의 모든 KitchenUtensil에 데이터 설정
        foreach (KitchenUtensil data in _kitchenUtensilDic[type])
        {
            data.SetData(equipData);
        }

        // Burner 타입인 경우에만 처리
        if (type >= KitchenUtensilType.Burner1 && type <= KitchenUtensilType.Burner5)
        {
            int burnerIndex = (int)type; // Burner1=0, Burner2=1, ..., Burner5=4
            _burnerDatas[burnerIndex].SetFoodType(equipData != null ? equipData.FoodType : FoodType.None);

            // Burner2~5는 장비가 있어야 사용 가능
            if (type >= KitchenUtensilType.Burner2)
            {
                if (equipData == null)
                {
                    _burnerDatas[burnerIndex].IsUsable = false;
                    SetDefalutBurnerData(burnerIndex);
                }
                else
                {
                    _burnerDatas[burnerIndex].IsUsable = true;
                }
            }
        }
        
        CheckSink();
    }

    private void ResetBurnerData(int index)
    {
        _burnerDatas[index].Time = 0;
        _burnerDatas[index].ResetCookingClock();
        _burnerDatas[index].CookingData.SetDefault();
        _burnerTimers[index].SetFillAmount(0);
        _burnerTimers[index].SetActive(false);
        _smokeAnimations[index].gameObject.SetActive(false);
    }

    private void SetDefalutBurnerData(int index)
    {
        if (!_burnerDatas[index].CookingData.IsDefault())
        {
            _cookingQueue.Enqueue(_burnerDatas[index].CookingData);
        }
        ResetBurnerData(index);
    }

    
    private void CheckSink()
    {
        KitchenUtensilData data = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _floorType, KitchenUtensilType.Sink);
        if (data == null)
        {
            UserInfo.SetMaxSinkBowlCount(UserInfo.CurrentStage, _floorType, ConstValue.DEFAULT_MAX_BOLW_COUNT);
            return;
        }
        KitchenUtensilSinkData sinkData = (KitchenUtensilSinkData)data;
        UserInfo.SetMaxSinkBowlCount(UserInfo.CurrentStage, _floorType, sinkData.MaxSinkBowlCount);
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeKitchenUtensilHandler -= OnChangeKitchenUtensilEvent;
    }
}
