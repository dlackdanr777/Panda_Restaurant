using System.Collections.Generic;
using UnityEngine;


public class KitchenUtensilGroup: MonoBehaviour
{
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

    public SinkKitchenUtensil GetSinkKitchenUtensil()
    {
        return _sinkKitchenUtensil;
    }



    public Vector2 GetStaffPos(EquipStaffType type)
    {
        switch (type)
        {
            case EquipStaffType.Chef1:
                return _defaultChef1Pos.position;
            case EquipStaffType.Chef2:
                return _defaultChef2Pos.position;
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

        DebugLog.LogError("위치 값이 이상합니다. door1: " + _door1.position + " door2: " + _door2.position + " tablePos: " + pos);
        return Vector3.zero;
    }

    public void Init()
    {   
        _burnerDatas = new KitchenBurnerData[(int)KitchenUtensilType.Burner5 + 1];
        _burnerTimers = new UIBurnerTimer[(int)KitchenUtensilType.Burner5 + 1];
        for (int i = 0, cnt = (int)KitchenUtensilType.Burner5 + 1; i < cnt; ++i)
        {
            _burnerDatas[i] = new KitchenBurnerData();
            _burnerDatas[i].SetKitchenUtensil(_kitchenUtensils[i]);
            _burnerTimers[i] = Instantiate(_burnerTimerPrefab, _burnerTimerParent);
            _burnerTimers[i].Init();
            _burnerTimers[i].SetWorldTransform(_burnerTimerTrs[i]);
            _burnerTimers[i].SetFillAmount(0);
            _burnerTimers[i].gameObject.SetActive(false);
            _smokeAnimations[i].gameObject.SetActive(false);
        }

        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            _kitchenUtensilDic.Add((KitchenUtensilType)i, new List<KitchenUtensil>());
        }

        for (int i = 0, cnt = _kitchenUtensils.Length; i < cnt; ++i)
        {
            _kitchenUtensils[i].Init(_floorType);
            _kitchenUtensilDic[_kitchenUtensils[i].Type].Add(_kitchenUtensils[i]);
        }
        _burnerDatas[0].IsUsable = true;
        _sinkKitchenUtensil = (SinkKitchenUtensil)_kitchenUtensilDic[KitchenUtensilType.Sink][0];
        UpdateKitchen();
        UserInfo.OnChangeKitchenUtensilHandler += OnChangeKitchenUtensilEvent;
    }


    void Update()
    {
        for(int i = 0, cnt = _burnerDatas.Length; i < cnt; ++i)
        {
            if (!_burnerDatas[i].IsUsable)
                continue;

            if (_burnerDatas[i].Time <= 0)
            {
                DequeueFood(i);
            }

            else
            {
                if (_burnerDatas[i].CookingData.TableData == null || _burnerDatas[i].CookingData.TableData.CurrentCustomer == null)
                {
                    DequeueFood(i);
                    return;
                }

                _burnerDatas[i].Time -= Time.deltaTime * GameManager.Instance.GetCookingSpeedMul(_floorType, _burnerDatas[i].CookingData.FoodData.FoodType) * (1 + _burnerDatas[i].AddCookSpeedMul * 0.01f * (_burnerDatas[i].UseStaff != null ? _burnerDatas[i].UseStaff.SpeedMul : 1));
                _burnerTimers[i].SetFillAmount(1 - (_burnerDatas[i].Time / _burnerDatas[i].CookingData.CookTime));
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
            foreach (KitchenUtensil data in _kitchenUtensilDic[type])
            {
                data.SetData(equipData);
            }

            if ((type >= KitchenUtensilType.Burner2 && type <= KitchenUtensilType.Burner5))
            {
                if(equipData != null)
                {
                    _burnerDatas[i].IsUsable = true;
                }
                else
                {
                    _burnerDatas[i].IsUsable = false;
                    SetDefalutBurnerData(i);
                }

            }
        }
    }


    private void OnChangeKitchenUtensilEvent(ERestaurantFloorType floorType, KitchenUtensilType type)
    {
        if (_floorType != floorType)
            return;

        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, floorType, type);
        foreach (KitchenUtensil data in _kitchenUtensilDic[type])
        {
            data.SetData(equipData);
        }

        if(type >= KitchenUtensilType.Burner2 && type <= KitchenUtensilType.Burner5)
        {
            if(equipData == null)
            {
                _burnerDatas[(int)type].IsUsable = false;
                SetDefalutBurnerData((int)type);
            }
            else
            {
                _burnerDatas[(int)type].IsUsable = true;
            }
        }
    }

    private void ResetBurnerData(int index)
    {
        _burnerDatas[index].Time = 0;
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


    private void OnDestroy()
    {
        UserInfo.OnChangeKitchenUtensilHandler -= OnChangeKitchenUtensilEvent;
    }
}
