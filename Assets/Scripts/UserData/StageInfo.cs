using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StageInfo
{
    public event Action OnChangeFloorHandler;
    public event Action OnChangeTipHandler;

    public event Action<ERestaurantFloorType, EquipStaffType> OnChangeStaffHandler;
    public event Action OnGiveStaffHandler;
    public event Action OnUpgradeStaffHandler;

    public event Action<ERestaurantFloorType, FurnitureType> OnChangeFurnitureHandler;
    public event Action OnGiveFurnitureHandler;
    public event Action OnChangeFurnitureSetDataHandler;

    public event Action<ERestaurantFloorType, KitchenUtensilType> OnChangeKitchenUtensilHandler;
    public event Action OnGiveKitchenUtensilHandler;
    public event Action OnChangeKitchenUtensilSetDataHandler;

    public event Action OnChangeSinkBowlHandler;

    public event Action OnChangeSatisfactionHandler;

    private EStage _stage;
    public EStage Stage => _stage;


    private ERestaurantFloorType _unlockFloor = ERestaurantFloorType.Floor1;
    public ERestaurantFloorType UnlockFloor => _unlockFloor;


    private int _score;
    public int Score => _score;

    private int _tip;
    public int Tip => _tip;

    private int _satisfaction;
    public int Satisfaction => _satisfaction;


    private StaffData[,] _equipStaffDatas = new StaffData[(int)ERestaurantFloorType.Length, (int)EquipStaffType.Length];
    private Dictionary<string, SaveStaffData> _giveStaffDic = new Dictionary<string, SaveStaffData>();

    private List<string> _giveFurnitureList = new List<string>();
    private FurnitureData[,] _equipFurnitureDatas = new FurnitureData[(int)ERestaurantFloorType.Length, (int)FurnitureType.Length];

    private List<string> _giveKitchenUtensilList = new List<string>();
    private KitchenUtensilData[,] _equipKitchenUtensilDatas = new KitchenUtensilData[(int)ERestaurantFloorType.Length, (int)KitchenUtensilType.Length];

    private SaveKitchenData[] _saveKitchenDatas = new SaveKitchenData[(int)ERestaurantFloorType.Length];
    private SaveTableData[,] _saveTableDatas = new SaveTableData[(int)ERestaurantFloorType.Length, (int)TableType.Length];
    private CoinAreaData[,] _coinAreaDatas = new CoinAreaData[(int)ERestaurantFloorType.Length, (int)TableType.Length * 2];
    private GarbageAreaData[,] _garbageAreaDatas = new GarbageAreaData[(int)ERestaurantFloorType.Length, (int)TableType.Length];

    private FoodType[] _furnitureEnabledFoodType = new FoodType[(int)ERestaurantFloorType.Length];
    private FoodType[] _kitchenuntensilEnabledFoodType = new FoodType[(int)ERestaurantFloorType.Length];



    //서버에 저장 X
    private List<string> _collectFurnitureSetDataList = new List<string>();
    private List<string> _collectKitchenUtensilSetDataList = new List<string>();

    public StageInfo()
    {
        for(int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            for (int j = 0, cntJ = (int)TableType.Length; j < cntJ; ++j)
            {
                TableType type = (TableType)j;
                _saveTableDatas[i, j] = new SaveTableData(floor, type);
            }
        }

        for(int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            _saveKitchenDatas[i] = new SaveKitchenData();
            _saveKitchenDatas[i].SetFloorType((ERestaurantFloorType)i);
        }
    }

    #region StageData
    public void AddSatisfaction(int value)
    {
        _satisfaction = Mathf.Clamp(_satisfaction + value, ConstValue.MIN_SATISFACTION, ConstValue.MAX_SATISFACTION);
        OnChangeSatisfactionHandler?.Invoke();
    }

    public void ChangeUnlockFloor(ERestaurantFloorType floor)
    {
        if (floor <= _unlockFloor)
            return;

        _unlockFloor = floor;
        OnChangeFloorHandler?.Invoke();
    }

    public void TipCollection(bool isWatchingAds = false)
    {
        int addMoneyValue = isWatchingAds ? _tip * 2 : _tip;
        UserInfo.AddMoney(addMoneyValue);
        _tip = 0;

        OnChangeTipHandler?.Invoke();
    }


    public void TipCollection(int value, bool isWatchingAds = false)
    {
        if (_tip < value)
        {
            DebugLog.LogError("보유 팁 보다 많은 팁을 회수하려고 합니다(Tip: " + _tip + ", Value: " + value + ")");
            return;
        }

        _tip -= value;
        int addMoneyValue = isWatchingAds ? value * 2 : value;
        UserInfo.AddMoney(addMoneyValue);
        OnChangeTipHandler?.Invoke();
    }

    public void AddTip(int value)
    {
        if (GameManager.Instance.MaxTipVolume <= _tip)
            return;

        _tip = _tip + value;
        OnChangeTipHandler?.Invoke();
    }

    #endregion

    #region StaffData

    public void GiveStaff(StaffData data)
    {
        if (_giveStaffDic.ContainsKey(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        SaveStaffData saveData = new SaveStaffData(data.Id, 1);
        _giveStaffDic.Add(data.Id, saveData);
        OnGiveStaffHandler?.Invoke();
    }

    public void GiveStaff(string id)
    {
        if (_giveStaffDic.ContainsKey(id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        StaffData data = StaffDataManager.Instance.GetStaffData(id);
        if (data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다: " + id);
            return;
        }

        GiveStaff(data);
    }


    public bool IsGiveStaff(string id)
    {
        return _giveStaffDic.ContainsKey(id);
    }


    public bool IsGiveStaff(StaffData data)
    {
        return _giveStaffDic.ContainsKey(data.Id);
    }


    public bool IsEquipStaff(ERestaurantFloorType floor, StaffData data)
    {
        int floorIndex = (int)floor;
        for(int i = 0, cnt = (int)EquipStaffType.Length; i < cnt; ++i)
        {
            EquipStaffType type = (EquipStaffType)i;
            StaffData equipData = _equipStaffDatas[floorIndex, i];
            if (equipData == null || equipData.Id != data.Id)
                continue;

            return true;
        }

        return false;
    }

    public bool IsEquipStaff(StaffData data)
    {
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            if (IsEquipStaff(floor, data))
                return true;
        }

        return false;
    }

    public EquipStaffType GetEquipStaffType(StaffData data)
    {
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            for (int j = 0, cntJ = (int)EquipStaffType.Length; j < cntJ; ++j)
            {
                StaffData equipData = _equipStaffDatas[i, j];
                if (equipData == null || equipData.Id != data.Id)
                    continue;

                return (EquipStaffType)j;
            }
        }

        DebugLog.LogError("해당 스탭이 장착되어있지 않습니다:" + data.Id);
        return EquipStaffType.Length;
    }


    public ERestaurantFloorType GetEquipStaffFloorType(StaffData data)
    {
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            for (int j = 0, cntJ = (int)EquipStaffType.Length; j < cntJ; ++j)
            {
                StaffData equipData = _equipStaffDatas[i, j];
                if (equipData == null || equipData.Id != data.Id)
                    continue;

                return floor;
            }
        }

        return ERestaurantFloorType.Error;
    }


    public void SetEquipStaff(ERestaurantFloorType floorType, EquipStaffType equipType, StaffData data)
    {
        if (!_giveStaffDic.ContainsKey(data.Id))
        {
            DebugLog.LogError("해당 스탭은 현재 가지고 있지 않습니다: " + data.Id);
            return;
        }

        List<EquipStaffType> equipStaffTypeList = StaffDataManager.Instance.GetEquipStaffTypeList(data);
        if(!equipStaffTypeList.Contains(equipType))
        {
            DebugLog.LogError("해당 스탭과 관련 없는 장착 슬롯입니다: (스탭 타입 " + StaffDataManager.Instance.GetStaffGroupType(data) + ")(장착 슬롯" + equipType + ")");
            return;
        }
        _equipStaffDatas[(int)floorType, (int)equipType] = data;
        OnChangeStaffHandler?.Invoke(floorType, equipType);
    }

    public void SetEquipStaff(ERestaurantFloorType floorType, EquipStaffType equipType, string id)
    {
        StaffData data = StaffDataManager.Instance.GetStaffData(id);
        if (data == null)
            throw new Exception("해당 Id를 가진 스탭이 없습니다: " + id);

        SetEquipStaff(floorType, equipType, data);
    }


    public void SetNullEquipStaff(ERestaurantFloorType floorType, EquipStaffType type)
    {
        _equipStaffDatas[(int)floorType, (int)type] = null;
        OnChangeStaffHandler?.Invoke(floorType, type);
    }

    public void SetNullEquipStaff(ERestaurantFloorType floorType, StaffData data)
    {
        if(data == null)
        {
            DebugLog.LogError("Null이 들어왔습니다.");
            return;
        }

        for(int i = 0, cnt = (int)EquipStaffType.Length; i < cnt; ++i)
        {
            if (_equipStaffDatas[(int)floorType, i] == null)
                continue;

            if (_equipStaffDatas[(int)floorType, i].Id != data.Id)
                continue;

            EquipStaffType type = (EquipStaffType)i;
            _equipStaffDatas[(int)floorType, i] = null;
            OnChangeStaffHandler?.Invoke(floorType, type);
            return;
        }

        DebugLog.LogError("해당 스탭이 장착되어 있지 않습니다: " + data.Id);
    }


    public StaffData GetEquipStaff(ERestaurantFloorType floorType, EquipStaffType type)
    {
        return _equipStaffDatas[(int)floorType, (int)type];
    }


    public int GetStaffLevel(StaffData data)
    {
        if (_giveStaffDic.TryGetValue(data.Id, out SaveStaffData saveData))
        {
            return saveData.Level;
        }

        throw new Exception("가지고 있지 않은 스태프 입니다: " + data.Id);
    }


    public int GetStaffLevel(string id)
    {
        if (_giveStaffDic.TryGetValue(id, out SaveStaffData saveData))
        {
            return saveData.Level;
        }

        throw new Exception("가지고 있지 않은 스태프 입니다: " + id);
    }


    public bool UpgradeStaff(StaffData data)
    {
        if (_giveStaffDic.TryGetValue(data.Id, out SaveStaffData saveData))
        {
            if (data.UpgradeEnable(saveData.Level))
            {
                _giveStaffDic[data.Id].LevelUp();
                OnUpgradeStaffHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("레벨 초과: " + data.Id);
            return false;
        }

        DebugLog.LogError("소유중이지 않음: " + data.Id);
        return false;
    }


    public bool UpgradeStaff(string id)
    {
        StaffData data = StaffDataManager.Instance.GetStaffData(id);
        return UpgradeStaff(data);
    }

    #endregion

    #region Furniture & Kitchen Data

    public int GetFurnitureAndKitchenUtensilCount()
    {
        return _giveKitchenUtensilList.Count + _giveFurnitureList.Count;
    }


    #endregion

    #region FurnitureData

    public void GiveFurniture(FurnitureData data)
    {
        if (_giveFurnitureList.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveFurnitureList.Add(data.Id);
        CheckCollectFurnitureSetData();
        OnGiveFurnitureHandler?.Invoke();
    }


    public void GiveFurniture(string id)
    {
        if (_giveFurnitureList.Contains(id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        FurnitureData data = FurnitureDataManager.Instance.GetFurnitureData(id);
        if (data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다" + id);
            return;
        }

        GiveFurniture(data);
    }

    public int GetGiveFurnitureCount()
    {
        return _giveFurnitureList.Count;
    }


    public bool IsGiveFurniture(string id)
    {
        return _giveFurnitureList.Contains(id);
    }

    public bool IsGiveFurniture(FurnitureData data)
    {
        return _giveFurnitureList.Contains(data.Id);
    }


    public bool IsEquipFurniture(ERestaurantFloorType floor, FurnitureData data)
    {
        int floorIndex = (int)floor;
        int typeIndex = (int)data.Type;
        FurnitureData equipData = _equipFurnitureDatas[floorIndex, typeIndex];

        return equipData != null && equipData.Id == data.Id;
    }


    public bool IsEquipFurniture(ERestaurantFloorType floor, FurnitureType type)
    {
        int floorIndex = (int)floor;
        int typeIndex = (int)type;
        return _equipFurnitureDatas[floorIndex, typeIndex] != null;
    }


    public ERestaurantFloorType GetEquipFurnitureFloorType(FurnitureData data)
    {
        int typeIndex = (int)data.Type;

        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            if (_equipFurnitureDatas[i, typeIndex] == null)
                continue;

            if (_equipFurnitureDatas[i, typeIndex].Id == data.Id)
                return (ERestaurantFloorType)i;
        }

        return ERestaurantFloorType.Error;
    }

    public ERestaurantFloorType GetEquipFurnitureFloorType(string id)
    {
        FurnitureData data = FurnitureDataManager.Instance.GetFurnitureData(id);
        if (data == null)
        {
            DebugLog.LogError("존재하지 않는 ID입니다" + id);
            return ERestaurantFloorType.Error;
        }

        return GetEquipFurnitureFloorType(data);
    }


    public void SetEquipFurniture(ERestaurantFloorType type, FurnitureData data)
    {
        if (!_giveFurnitureList.Contains(data.Id))
        {
            DebugLog.LogError("현재 가구를 보유하지 않았습니다: " + data.Id);
            return;
        }

        _equipFurnitureDatas[(int)type, (int)data.Type] = data;
        CheckFurnitureFoodType(type);
        OnChangeFurnitureHandler?.Invoke(type, data.Type);
    }

    public void SetEquipFurniture(ERestaurantFloorType type, string id)
    {
        FurnitureData data = FurnitureDataManager.Instance.GetFurnitureData(id);
        if (data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다" + id);
            return;
        }
        SetEquipFurniture(type, data);
    }


    public void SetNullEquipFurniture(ERestaurantFloorType floor, FurnitureType type)
    {
        _equipFurnitureDatas[(int)floor, (int)type] = null;
        CheckFurnitureFoodType(floor);
        OnChangeFurnitureHandler?.Invoke(floor, type);
    }


    public FurnitureData GetEquipFurniture(ERestaurantFloorType floor, FurnitureType type)
    {
        return _equipFurnitureDatas[(int)floor, (int)type];
    }

    #endregion

    #region KitchenData

    public void GiveKitchenUtensil(KitchenUtensilData data)
    {
        if (_giveKitchenUtensilList.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveKitchenUtensilList.Add(data.Id);
        CheckCollectKitchenUtensilSetData();
        OnGiveKitchenUtensilHandler?.Invoke();
    }


    public void GiveKitchenUtensil(string id)
    {
        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);
        if (data == null)
        {
            DebugLog.Log("존재하지 않는 ID입니다" + id);
            return;
        }
        GiveKitchenUtensil(data);
    }

    public int GetGiveKitchenUtensilCount()
    {
        return _giveKitchenUtensilList.Count;
    }


    public bool IsGiveKitchenUtensil(string id)
    {
        return _giveKitchenUtensilList.Contains(id);
    }

    public bool IsGiveKitchenUtensil(KitchenUtensilData data)
    {
        return _giveKitchenUtensilList.Contains(data.Id);
    }


    public bool IsEquipKitchenUtensil(ERestaurantFloorType floor, KitchenUtensilData data)
    {
        int floorIndex = (int)floor;
        int typeIndex = (int)data.Type;
        KitchenUtensilData equipData = _equipKitchenUtensilDatas[floorIndex, typeIndex];
        return equipData != null && equipData.Id == data.Id;
    }


    public ERestaurantFloorType GetEquipKitchenUtensilFloorType(KitchenUtensilData data)
    {
        int typeIndex = (int)data.Type;

        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            if (_equipKitchenUtensilDatas[i, typeIndex] == null)
                continue;

            if (_equipKitchenUtensilDatas[i, typeIndex].Id == data.Id)
                return (ERestaurantFloorType)i;
        }

        return ERestaurantFloorType.Error;
    }

    public ERestaurantFloorType GetEquipKitchenUtensilFloorType(string id)
    {
        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);
        if (data == null)
        {
            DebugLog.LogError("존재하지 않는 ID입니다" + id);
            return ERestaurantFloorType.Error;
        }

        return GetEquipKitchenUtensilFloorType(data);
    }


    public void SetEquipKitchenUtensil(ERestaurantFloorType type, KitchenUtensilData data)
    {
        if (!_giveKitchenUtensilList.Contains(data.Id))
        {
            DebugLog.LogError("현재 주방 기구를 보유하지 않았습니다: " + data.Id);
            return;
        }

        _equipKitchenUtensilDatas[(int)type, (int)data.Type] = data;
        CheckKitchenUtensilFoodType(type);
        OnChangeKitchenUtensilHandler?.Invoke(type, data.Type);
    }

    public void SetEquipKitchenUtensil(ERestaurantFloorType type, string id)
    {
        KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(id);
        if (data == null)
        {
            DebugLog.LogError("존재하지 않는 ID입니다" + id);
            return;
        }

        SetEquipKitchenUtensil(type, data);
    }


    public void SetNullEquipKitchenUtensil(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        _equipKitchenUtensilDatas[(int)floor, (int)type] = null;
        CheckKitchenUtensilFoodType(floor);
        OnChangeKitchenUtensilHandler?.Invoke(floor, type);
    }

    public KitchenUtensilData GetEquipKitchenUtensil(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        return _equipKitchenUtensilDatas[(int)floor, (int)type];
    }

    #endregion

    #region EffectSetData


    public FoodType GetEquipFurnitureFoodType(ERestaurantFloorType type)
    {
        return _furnitureEnabledFoodType[(int)type];
    }


    public FoodType GetEquipKitchenFoodType(ERestaurantFloorType type)
    {
        return _kitchenuntensilEnabledFoodType[(int)type];
    }


    public List<string> GetCollectKitchenUtensilSetDataList()
    {
        return _collectKitchenUtensilSetDataList;
    }

    public List<string> GetCollectFurnitureSetDataList()
    {
        return _collectFurnitureSetDataList;
    }


    private void CheckKitchenUtensilFoodType()
    {
        for(int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            CheckFurnitureFoodType(floor);
        }
    }

    private void CheckFurnitureFoodType()
    {
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            ERestaurantFloorType floor = (ERestaurantFloorType)i;
            CheckKitchenUtensilFoodType(floor);
        }
    }

    private void CheckFurnitureFoodType(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;

        if (_equipFurnitureDatas[floorIndex, 0] == null)
        {
            _furnitureEnabledFoodType[floorIndex] = FoodType.None;
            return;
        }
        FoodType foodType = _equipFurnitureDatas[floorIndex, 0].FoodType;

        for (int i = 1, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            FurnitureData data = _equipFurnitureDatas[floorIndex, i];
            if (data == null)
            {
                _furnitureEnabledFoodType[floorIndex] = FoodType.None;
                return;
            }

            if(foodType != data.FoodType)
            {
                _furnitureEnabledFoodType[floorIndex] = FoodType.None;
                return;
            }
        }

        _furnitureEnabledFoodType[floorIndex] = foodType;
    }


    private void CheckKitchenUtensilFoodType(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;
        KitchenUtensilData data = _equipKitchenUtensilDatas[floorIndex, 0];
        if (data == null)
        {
            _kitchenuntensilEnabledFoodType[floorIndex] = FoodType.None;
            return;
        }
        FoodType foodType = _equipKitchenUtensilDatas[floorIndex, 0].FoodType;

        for (int i = 1, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
        {
            data = _equipKitchenUtensilDatas[floorIndex, i];
            if (data == null)
            {
                _kitchenuntensilEnabledFoodType[floorIndex] = FoodType.None;
                return;
            }

            if (foodType != data.FoodType)
            {
                _kitchenuntensilEnabledFoodType[floorIndex] = FoodType.None;
                return;
            }
        }

        _kitchenuntensilEnabledFoodType[floorIndex] = foodType;
    }

    private void CheckCollectFurnitureSetData()
    {
        _collectFurnitureSetDataList.Clear();
       Dictionary<string, int> _setDataCountDic = new Dictionary<string, int>();
        for(int i = 0, cnt = _giveFurnitureList.Count; i < cnt; ++i)
        {
            FurnitureData data = FurnitureDataManager.Instance.GetFurnitureData(_giveFurnitureList[i]);
            if (data == null)
                throw new Exception("해당 id를 가진 가구 데이터가 존재하지 않습니다: " + _giveFurnitureList[i]);

            if (_setDataCountDic.ContainsKey(data.Id))
                _setDataCountDic[data.Id]++;
            else
                _setDataCountDic.Add(data.Id, 1);

        }

        foreach(var v in _setDataCountDic)
        {
            if (v.Value < ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT)
                continue;

            _collectFurnitureSetDataList.Add(v.Key);
        }
    }

    private void CheckCollectKitchenUtensilSetData()
    {
        _collectKitchenUtensilSetDataList.Clear();
        Dictionary<string, int> _setDataCountDic = new Dictionary<string, int>();
        for (int i = 0, cnt = _giveKitchenUtensilList.Count; i < cnt; ++i)
        {
            KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(_giveKitchenUtensilList[i]);
            if (data == null)
                throw new Exception("해당 id를 가진 주방 기구 데이터가 존재하지 않습니다: " + _giveKitchenUtensilList[i]);

            if (_setDataCountDic.ContainsKey(data.Id))
                _setDataCountDic[data.Id]++;
            else
                _setDataCountDic.Add(data.Id, 1);

        }

        foreach (var v in _setDataCountDic)
        {
            if (v.Value < ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT)
                continue;

            _collectKitchenUtensilSetDataList.Add(v.Key);
        }
    }

    #endregion

    #region TableData

    public SaveTableData GetTableData(ERestaurantFloorType floor, TableType type)
    {
        int floorIndex = (int)floor;
        int typeIndex = (int)type;

        return _saveTableDatas[floorIndex, typeIndex];
    }

    #endregion


    #region KitchenData

    public int GetSinkBowlCount(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;
        return _saveKitchenDatas[floorIndex].SinkBowlCount;
    }

    public int GetMaxSinkBowlCount(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;
        return _saveKitchenDatas[floorIndex].MaxSinkBowlCount;
    }

    public void AddSinkBowlCount(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;
        if (!_saveKitchenDatas[floorIndex].GetBowlAddEnabled())
        {
            DebugLog.LogError("현재 씽크대가 꽉찼습니다:" + _saveKitchenDatas[floorIndex].SinkBowlCount + "/" + _saveKitchenDatas[floorIndex].MaxSinkBowlCount);
            return;
        }

        _saveKitchenDatas[floorIndex].AddSinkBowlCount();
        OnChangeSinkBowlHandler?.Invoke();
    }

    public void SubSinkBowlCount(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;
        if (_saveKitchenDatas[floorIndex].SinkBowlCount <= 0)
        {
            DebugLog.LogError("현재 씽크대가 비어있습니다.");
            return;
        }

        _saveKitchenDatas[floorIndex].SubSinkBowlCount();
        OnChangeSinkBowlHandler?.Invoke();
    }

    public bool GetBowlAddEnabled(ERestaurantFloorType floor)
    {
        int floorIndex = (int)floor;
        return _saveKitchenDatas[floorIndex].GetBowlAddEnabled();
    }

    #endregion

    #region ServerData

    public ServerStageData SaveData()
    {
        ServerStageData data = new ServerStageData();

        data.UnlockFloor = _unlockFloor;
        data.Score = _score;
        data.Tip = _tip;
        data.Satisfaction = _satisfaction;

        data.SaveKitchenDataList = ConvertKitchenDataToList();
        data.SaveTableDataList = ConvertTableDataToList();
        data.CoinAreaDataList = ConvertCoinAreaDataToList();
        data.GarbageAreaDataList = ConvertGarbageAreaDataToList();

        // 1. 직원 장착 데이터 변환 (StaffData[,] → List<List<string>>)
        data.EquipStaffDataList = new List<List<string>>();
        for (int i = 0; i < (int)ERestaurantFloorType.Length; i++)
        {
            List<string> staffList = new List<string>();
            for (int j = 0; j < (int)EquipStaffType.Length; j++)
            {
                staffList.Add(_equipStaffDatas[i, j] != null ? _equipStaffDatas[i, j].Id : ""); // StaffData에서 ID 추출
            }
            data.EquipStaffDataList.Add(staffList);
        }

        // 2. 직원 정보 복사
        data.GiveStaffList = _giveStaffDic.Values.ToList();

        // 3. 획득한 가구 정보 (List 그대로 할당)
        data.GiveFurnitureList = new List<string>(_giveFurnitureList);

        // 4. 장착된 가구 데이터 변환 (FurnitureData[,] → List<List<string>>)
        data.EquipFurnitureList = new List<List<string>>();
        for (int i = 0; i < (int)ERestaurantFloorType.Length; i++)
        {
            List<string> furnitureList = new List<string>();
            for (int j = 0; j < (int)FurnitureType.Length; j++)
            {
                furnitureList.Add(_equipFurnitureDatas[i, j] != null ? _equipFurnitureDatas[i, j].Id : "");
            }
            data.EquipFurnitureList.Add(furnitureList);
        }

        // 5. 획득한 주방 기구 정보 (List 그대로 할당)
        data.GiveKitchenUtensilList = new List<string>(_giveKitchenUtensilList);

        // 6. 장착된 주방 기구 데이터 변환 (KitchenUtensilData[,] → List<List<string>>)
        data.EquipKitchenUtensilList = new List<List<string>>();
        for (int i = 0; i < (int)ERestaurantFloorType.Length; i++)
        {
            List<string> kitchenUtensilList = new List<string>();
            for (int j = 0; j < (int)KitchenUtensilType.Length; j++)
            {
                kitchenUtensilList.Add(_equipKitchenUtensilDatas[i, j] != null ? _equipKitchenUtensilDatas[i, j].Id : "");
            }
            data.EquipKitchenUtensilList.Add(kitchenUtensilList);
        }

        return data;
    }

    public bool LoadData(ServerStageData loadData)
    {
        if (loadData == null)
            return false;

        _unlockFloor = loadData.UnlockFloor;
        _score = loadData.Score;
        _tip = loadData.Tip;
        _satisfaction = Mathf.Clamp(loadData.Satisfaction, ConstValue.MIN_SATISFACTION, ConstValue.MAX_SATISFACTION);

        _giveStaffDic.Clear();
        for (int i = 0, cnt = loadData.GiveStaffList.Count; i < cnt; ++i)
        {
            if (string.IsNullOrWhiteSpace(loadData.GiveStaffList[i].Id))
                throw new Exception("아이디 값이 이상합니다: " + loadData.GiveStaffList[i].Id);

            _giveStaffDic.Add(loadData.GiveStaffList[i].Id, loadData.GiveStaffList[i]);
        }


        for (int i = 0, cnt = loadData.EquipStaffDataList.Count; i < cnt; ++i)
        {
            for (int j = 0, cntJ = loadData.EquipStaffDataList[i].Count; j < cntJ; ++j)
            {
                string staffId = loadData.EquipStaffDataList[i][j];
                if (string.IsNullOrWhiteSpace(staffId))
                    continue;

                StaffData data = StaffDataManager.Instance.GetStaffData(staffId);
                SetEquipStaff((ERestaurantFloorType)i, (EquipStaffType)j, data);
            }
        }

        _giveFurnitureList = loadData.GiveFurnitureList;
        for (int i = 0, cnt = loadData.EquipFurnitureList.Count; i < cnt; ++i)
        {
            for (int j = 0, cntJ = loadData.EquipFurnitureList[i].Count; j < cntJ; ++j)
            {
                string furnitureId = loadData.EquipFurnitureList[i][j];
                if (string.IsNullOrWhiteSpace(furnitureId))
                    continue;

                FurnitureData data = FurnitureDataManager.Instance.GetFurnitureData(furnitureId);
                SetEquipFurniture((ERestaurantFloorType)i, data);
            }
        }

        _giveKitchenUtensilList = loadData.GiveKitchenUtensilList;
        for (int i = 0, cnt = loadData.EquipKitchenUtensilList.Count; i < cnt; ++i)
        {
            for (int j = 0, cntJ = loadData.EquipKitchenUtensilList[i].Count; j < cntJ; ++j)
            {
                string kitchenUtensilId = loadData.EquipKitchenUtensilList[i][j];
                if (string.IsNullOrWhiteSpace(kitchenUtensilId))
                    continue;

                KitchenUtensilData data = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(kitchenUtensilId);
                SetEquipKitchenUtensil((ERestaurantFloorType)i, data);
            }
        }

        for(int i = 0; i < loadData.SaveKitchenDataList.Count; ++i)
        {
            SaveKitchenData kitchenData = loadData.SaveKitchenDataList[i];

            int floorIndex = (int)kitchenData.FloorType;
            _saveKitchenDatas[floorIndex] = kitchenData;
        }

        for (int i = 0; i < loadData.SaveTableDataList.Count; i++)
        {
            var floorList = loadData.SaveTableDataList[i];
            for (int j = 0; j < floorList.Count; j++)
            {
                SaveTableData tableData = floorList[j];

                // enum 인덱스 변환
                int floorIndex = (int)tableData.FloorType;
                int tableIndex = (int)tableData.TableType;

                // 배열 범위 체크
                if (floorIndex >= 0 && floorIndex < (int)ERestaurantFloorType.Length &&
                    tableIndex >= 0 && tableIndex < (int)TableType.Length)
                {
                    _saveTableDatas[floorIndex, tableIndex] = tableData;
                }
                else
                {
                    DebugLog.LogError($"[LoadData] 잘못된 테이블 인덱스 floor:{floorIndex}, table:{tableIndex}");
                }
            }
        }

        ConvertListToCoinAreaDataArray(loadData.CoinAreaDataList);
        ConvertListToGarbageAreaDataArray(loadData.GarbageAreaDataList);

        CheckKitchenUtensilFoodType();
        CheckFurnitureFoodType();

        CheckCollectKitchenUtensilSetData();
        CheckCollectFurnitureSetData();
        return true;
    }


    private List<List<CoinAreaData>> ConvertCoinAreaDataToList()
    {
        List<List<CoinAreaData>> list = new List<List<CoinAreaData>>();
        for (int i = 0; i < (int)ERestaurantFloorType.Length; i++)
        {
            List<CoinAreaData> row = new List<CoinAreaData>();
            for (int j = 0; j < (int)TableType.Length * 2; j++)
            {
                CoinAreaData data = _coinAreaDatas[i, j];
                row.Add(data ?? new CoinAreaData()); // NULL 방지
            }
            list.Add(row);
        }
        return list;
    }

    private List<List<SaveTableData>> ConvertTableDataToList()
    {
        List<List<SaveTableData>> list = new List<List<SaveTableData>>();
        for (int i = 0; i < (int)ERestaurantFloorType.Length; i++)
        {
            List<SaveTableData> row = new List<SaveTableData>();
            for (int j = 0; j < (int)TableType.Length; j++)
            {
                SaveTableData data = _saveTableDatas[i, j];
                row.Add(data ?? new SaveTableData((ERestaurantFloorType)i, (TableType)j)); // NULL 방지
            }
            list.Add(row);
        }
        return list;
    }

    private List<SaveKitchenData> ConvertKitchenDataToList()
    {
        List<SaveKitchenData> list = new List<SaveKitchenData>();
        for (int i = 0; i < _saveKitchenDatas.Length; i++)
        {
            SaveKitchenData data = _saveKitchenDatas[i];
            list.Add(data);
        }

        return list;
    }

    // 🔹 `GarbageAreaData[,]` → `List<List<GarbageAreaData>>` 변환
    private List<List<GarbageAreaData>> ConvertGarbageAreaDataToList()
    {
        List<List<GarbageAreaData>> list = new List<List<GarbageAreaData>>();
        for (int i = 0; i < (int)ERestaurantFloorType.Length; i++)
        {
            List<GarbageAreaData> row = new List<GarbageAreaData>();
            for (int j = 0; j < (int)TableType.Length; j++)
            {
                GarbageAreaData data = _garbageAreaDatas[i, j];
                row.Add(data ?? new GarbageAreaData()); // NULL 방지
            }
            list.Add(row);
        }
        return list;
    }


    private void ConvertListToCoinAreaDataArray(List<List<CoinAreaData>> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = 0; j < list[i].Count; j++)
            {
                if (_coinAreaDatas[i, j] == null)
                    _coinAreaDatas[i, j] = new CoinAreaData(); // NULL 방지

                _coinAreaDatas[i, j].SetCoinCount(list[i][j].CoinCount);
                _coinAreaDatas[i, j].SetMoney(list[i][j].Money);
            }
        }
    }

    // 🔹 `List<List<GarbageAreaData>>` → `GarbageAreaData[,]` 변환 함수
    private void ConvertListToGarbageAreaDataArray(List<List<GarbageAreaData>> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = 0; j < list[i].Count; j++)
            {
                if (_garbageAreaDatas[i, j] == null)
                    _garbageAreaDatas[i, j] = new GarbageAreaData(); // NULL 방지

                _garbageAreaDatas[i, j].SetCount(list[i][j].Count);
            }
        }
    }

    #endregion
}
