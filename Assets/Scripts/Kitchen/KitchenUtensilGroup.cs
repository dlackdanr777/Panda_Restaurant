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


    private UIBurnerTimer[] _burnerTimers;
    private KitchenBurnerData[] _burnerDatas;
    private Dictionary<KitchenUtensilType, List<KitchenUtensil>> _kitchenUtensilDic = new Dictionary<KitchenUtensilType, List<KitchenUtensil>>();
    private Queue<CookingData> _cookingQueue = new Queue<CookingData>();


    public void Init()
    {   
        _burnerDatas = new KitchenBurnerData[(int)KitchenUtensilType.Burner5 + 1];
        _burnerTimers = new UIBurnerTimer[(int)KitchenUtensilType.Burner5 + 1];
        for (int i = 0, cnt = (int)KitchenUtensilType.Burner5 + 1; i < cnt; ++i)
        {
            _burnerDatas[i] = new KitchenBurnerData();
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
                _burnerDatas[i].Time -= Time.deltaTime * GameManager.Instance.GetCookingSpeedMul(_floorType, _burnerDatas[i].CookingData.FoodData.FoodType);
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
