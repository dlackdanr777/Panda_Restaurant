using System.Collections.Generic;
using UnityEngine;


public class KichenBurnerData
{
    public float Time;
    public CookingData CookingData;
}


public class KitchenSystem : MonoBehaviour
{
    [SerializeField] private KitchenUtensil[] _kitchenUtensils;
    private Dictionary<KitchenUtensilType, List<KitchenUtensil>> _kitchenUtensilDic = new Dictionary<KitchenUtensilType, List<KitchenUtensil>>();
    private Queue<CookingData> _cookingQueue = new Queue<CookingData>();

    [SerializeField] private int _burnerCount = 0;
    private KichenBurnerData[] _burnerDatas;


    [Space]
    [Header("UI Options")]
    [SerializeField] private RectTransform _burnerTimerParent;
    [SerializeField] private UIBurnerTimer _burnerTimerPrefab;
    [SerializeField] private Transform[] _burnerTimerTrs;


    private UIBurnerTimer[] _burnerTimers;

    private void Awake()
    {
        _burnerDatas = new KichenBurnerData[(int)KitchenUtensilType.Burner5 + 1];
        _burnerTimers = new UIBurnerTimer[(int)KitchenUtensilType.Burner5 + 1];
        for (int i = 0, cnt = (int)KitchenUtensilType.Burner5 + 1; i < cnt; ++i)
        {
            _burnerDatas[i] = new KichenBurnerData();
            UIBurnerTimer obj = Instantiate(_burnerTimerPrefab, _burnerTimerParent);
            _burnerTimers[i] = obj;
            obj.SetWorldTransform(_burnerTimerTrs[i]);
            obj.SetFillAmount(0);
        }

        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            _kitchenUtensilDic.Add((KitchenUtensilType)i, new List<KitchenUtensil>());
        }

        for (int i = 0, cnt = _kitchenUtensils.Length; i < cnt; ++i)
        {
            _kitchenUtensilDic[_kitchenUtensils[i].Type].Add(_kitchenUtensils[i]);
        }
        UpdateKitchen();
        UserInfo.OnChangeKitchenUtensilHandler += OnChangeKitchenUtensilEvent;
    }


    void Update()
    {
        for(int i = 0; i < _burnerCount; ++i)
        {
            if (_burnerDatas[i].Time <= 0)
            {
                DequeueFood(i);
            }

            else
            {
                _burnerDatas[i].Time -= Time.deltaTime * GameManager.Instance.CookingSpeedMul;
                _burnerTimers[i].SetFillAmount(1 - (_burnerDatas[i].Time / _burnerDatas[i].CookingData.CookingTime));
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
            _burnerDatas[burnerIndex].CookingData.OnCompleted?.Invoke();



        if (_cookingQueue.Count == 0)
        {
            _burnerDatas[burnerIndex].CookingData = default;
            _burnerDatas[burnerIndex].Time = 0;
            _burnerTimers[burnerIndex].SetFillAmount(0);
            _burnerTimers[burnerIndex].SetActive(false);
            return;
        }

        CookingData cookingData = _cookingQueue.Dequeue();
        _burnerDatas[burnerIndex].CookingData = cookingData;
        _burnerDatas[burnerIndex].Time = cookingData.CookingTime;
        _burnerTimers[burnerIndex].SetActive(true);
        _burnerTimers[burnerIndex].SetFillAmount(0);
        _burnerTimers[burnerIndex].SetImage(cookingData.Sprite);
    }

    private void UpdateKitchen()
    {
        KitchenUtensilData equipData;
        KitchenUtensilType type;
        for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            type = (KitchenUtensilType)i;
            equipData = UserInfo.GetEquipKitchenUtensil(type);
            foreach (KitchenUtensil data in _kitchenUtensilDic[type])
            {
                data.SetData(equipData);
            }

            if ((type >= KitchenUtensilType.Burner1 && type <= KitchenUtensilType.Burner5) && equipData != null)
            {
                _burnerCount++;
            }

        }
    }


    private void OnChangeKitchenUtensilEvent(KitchenUtensilType type)
    {
        KitchenUtensilData equipData = UserInfo.GetEquipKitchenUtensil(type);

        foreach (KitchenUtensil data in _kitchenUtensilDic[type])
        {
            data.SetData(equipData);
        }

        if(type >= KitchenUtensilType.Burner1 && type <= KitchenUtensilType.Burner5)
        {
            int _tmpBurnerCount = _burnerCount;
            _burnerCount = 0;
            for (int i = 0, cnt = (int)KitchenUtensilType.Cabinet; i < cnt; i++)
            {
                if (UserInfo.GetEquipKitchenUtensil((KitchenUtensilType)i) != null)
                    _burnerCount++;
            }

            for(int i = _tmpBurnerCount; i < _burnerCount; ++i)
            {
                _burnerDatas[i].Time = 0;
                _burnerTimers[i].SetActive(false);
                _burnerTimers[i].SetWorldTransform(_burnerTimerTrs[i]);
            }

            for (int i = _burnerCount, cnt = _burnerDatas.Length; i < cnt; ++i)
            {
                if (!_burnerDatas[i].CookingData.IsDefault())
                    _cookingQueue.Enqueue(_burnerDatas[i].CookingData);

                _burnerDatas[i].CookingData = default;
                _burnerTimers[i].SetFillAmount(0);
                _burnerTimers[i].SetActive(false);
                _burnerTimers[i].SetWorldTransform(_burnerTimerTrs[i]);
            }
        }
    }
}
