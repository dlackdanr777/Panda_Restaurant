using Muks.DataBind;
using System;
using System.Collections.Generic;

public class StageInfo
{
    public event Action OnChangeFloorHandler;
    public static event Action OnChangeTipHandler;

    public event Action<ERestaurantFloorType, StaffType> OnChangeStaffHandler;
    public event Action OnGiveStaffHandler;
    public event Action OnUpgradeStaffHandler;

    public event Action<ERestaurantFloorType, FurnitureType> OnChangeFurnitureHandler;
    public event Action OnGiveFurnitureHandler;
    public event Action OnChangeFurnitureSetDataHandler;

    public event Action<ERestaurantFloorType, KitchenUtensilType> OnChangeKitchenUtensilHandler;
    public event Action OnGiveKitchenUtensilHandler;
    public event Action OnChangeKitchenUtensilSetDataHandler;

    private EStage _stage;
    public EStage Stage => _stage;


    private ERestaurantFloorType _unlockFloor;
    public ERestaurantFloorType UnlockFloor => _unlockFloor;


    private int _score;
    public int Score => _score;


    private int _tip;
    public int Tip => _tip;


    private StaffData[,] _equipStaffDatas = new StaffData[(int)ERestaurantFloorType.Length, (int)StaffType.Length];
    private Dictionary<string, int> _giveStaffLevelDic = new Dictionary<string, int>();

    private List<string> _giveFurnitureList = new List<string>();
    private FurnitureData[,] _equipFurnitureDatas = new FurnitureData[(int)ERestaurantFloorType.Length, (int)FurnitureType.Length];

    private List<string> _giveKitchenUtensilList = new List<string>();
    private KitchenUtensilData[,] _equipKitchenUtensilDatas = new KitchenUtensilData[(int)ERestaurantFloorType.Length, (int)KitchenUtensilType.Length];

    private SetData[] _furnitureEnabledSetData = new SetData[(int)ERestaurantFloorType.Length];
    private SetData[] _kitchenuntensilEnabledSetData = new SetData[(int)ERestaurantFloorType.Length];

    private Dictionary<string, int> _furnitureEffectSetCountDic = new Dictionary<string, int>();
    private Dictionary<string, int> _kitchenUtensilEffectSetCountDic = new Dictionary<string, int>();
    private HashSet<string> _activatedFurnitureEffectSet = new HashSet<string>();
    private HashSet<string> _activatedKitchenUtensilEffectSet = new HashSet<string>();

    private CoinAreaData[,] _coinAreaDatas = new CoinAreaData[(int)ERestaurantFloorType.Length, (int)TableType.Length * 2];

    private GarbageAreaData[,] _garbageAreaDatas = new GarbageAreaData[(int)ERestaurantFloorType.Length, (int)TableType.Length];


    public StageInfo()
    {
        for(int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            for(int j = 0, cntJ = (int)TableType.Length; j < cntJ; ++j)
            {
                _garbageAreaDatas[i, j] = new GarbageAreaData();
            }

            for (int j = 0, cntJ = (int)TableType.Length; j < cntJ; ++j)
            {
                _coinAreaDatas[i, i * 2 + j] = new CoinAreaData();
            }
        }
    }

    #region StageData

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
        if (_giveStaffLevelDic.ContainsKey(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveStaffLevelDic.Add(data.Id, 1);
        OnGiveStaffHandler?.Invoke();
    }

    public void GiveStaff(string id)
    {
        if (_giveStaffLevelDic.ContainsKey(id))
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

        _giveStaffLevelDic.Add(id, 1);
        OnGiveStaffHandler?.Invoke();
    }


    public bool IsGiveStaff(string id)
    {
        return _giveStaffLevelDic.ContainsKey(id);
    }


    public bool IsGiveStaff(StaffData data)
    {
        return _giveStaffLevelDic.ContainsKey(data.Id);
    }


    public bool IsEquipStaff(ERestaurantFloorType floor, StaffData data)
    {
        int floorIndex = (int)floor;
        int typeIndex = (int)StaffDataManager.Instance.GetStaffType(data);
        StaffData equipData = _equipStaffDatas[floorIndex, typeIndex];

        return equipData != null && equipData.Id == data.Id;
    }

    public bool IsEquipStaff(StaffData data)
    {
        int typeIndex = (int)StaffDataManager.Instance.GetStaffType(data);
        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            if (_equipStaffDatas[i, typeIndex] == null)
                continue;

            if (_equipStaffDatas[i, typeIndex].Id == data.Id)
                return true;
        }

        return false;
    }


    public ERestaurantFloorType GetEquipStaffFloorType(StaffData data)
    {
        int typeIndex = (int)StaffDataManager.Instance.GetStaffType(data);

        for (int i = 0, cnt = (int)ERestaurantFloorType.Length; i < cnt; ++i)
        {
            if (_equipStaffDatas[i, typeIndex] == null)
                continue;

            if (_equipStaffDatas[i, typeIndex].Id == data.Id)
                return (ERestaurantFloorType)i;
        }

        return ERestaurantFloorType.Error;
    }


    public void SetEquipStaff(ERestaurantFloorType floorType, StaffData data)
    {
        if (!_giveStaffLevelDic.ContainsKey(data.Id))
        {
            DebugLog.LogError("해당 스탭은 현재 가지고 있지 않습니다: " + data.Id);
            return;
        }

        _equipStaffDatas[(int)floorType, (int)StaffDataManager.Instance.GetStaffType(data)] = data;
        OnChangeStaffHandler?.Invoke(floorType, StaffDataManager.Instance.GetStaffType(data));
    }

    public void SetEquipStaff(ERestaurantFloorType floorType, string id)
    {
        StaffData data = StaffDataManager.Instance.GetStaffData(id);
        if (data == null)
            throw new Exception("해당 Id를 가진 스탭이 없습니다: " + id);

        SetEquipStaff(floorType, data);
    }


    public void SetNullEquipStaff(ERestaurantFloorType floorType, StaffType type)
    {
        _equipStaffDatas[(int)floorType, (int)type] = null;
        OnChangeStaffHandler?.Invoke(floorType, type);
    }


    public StaffData GetEquipStaff(ERestaurantFloorType floorType, StaffType type)
    {
        return _equipStaffDatas[(int)floorType, (int)type];
    }


    public int GetStaffLevel(StaffData data)
    {
        if (_giveStaffLevelDic.TryGetValue(data.Id, out int level))
        {
            return level;
        }

        throw new Exception("가지고 있지 않은 스태프 입니다: " + data.Id);
    }


    public int GetStaffLevel(string id)
    {
        if (_giveStaffLevelDic.TryGetValue(id, out int level))
        {
            return level;
        }

        throw new Exception("가지고 있지 않은 스태프 입니다: " + id);
    }


    public bool UpgradeStaff(StaffData data)
    {
        if (_giveStaffLevelDic.TryGetValue(data.Id, out int level))
        {
            if (data.UpgradeEnable(level))
            {
                _giveStaffLevelDic[data.Id] = level + 1;
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
        if (_giveStaffLevelDic.TryGetValue(id, out int level))
        {
            StaffData data = StaffDataManager.Instance.GetStaffData(id);
            if (data.UpgradeEnable(level))
            {
                _giveStaffLevelDic[id] = level + 1;
                OnUpgradeStaffHandler?.Invoke();
                return true;
            }

            DebugLog.LogError("레벨 초과: " + id);
            return false;
        }

        DebugLog.LogError("소유중이지 않음: " + id);
        return false;
    }

    #endregion

    #region Furniture & Kitchen Data

    public int GetFurnitureAndKitchenUtensilCount()
    {
        return _giveKitchenUtensilList.Count + _giveFurnitureList.Count;
    }


    #endregion

    #region FurnitureData

    public SetData GetEquipFurnitureSetData(ERestaurantFloorType type)
    {
        return _furnitureEnabledSetData[(int)type];
    }

    public void SetEquipFurnitureSetData(ERestaurantFloorType type, SetData data)
    {
        if (_furnitureEnabledSetData[(int)type] == data)
            return;

        _furnitureEnabledSetData[(int)type] = data;
        OnChangeFurnitureSetDataHandler?.Invoke();
    }


    public void GiveFurniture(FurnitureData data)
    {
        if (_giveFurnitureList.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveFurnitureList.Add(data.Id);
        CheckFurnitureSetCount();
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

        _giveFurnitureList.Add(id);
        CheckFurnitureSetCount();
        OnGiveFurnitureHandler?.Invoke();
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
        OnChangeFurnitureHandler?.Invoke(type, data.Type);
    }


    public void SetNullEquipFurniture(ERestaurantFloorType floor, FurnitureType type)
    {
        _equipFurnitureDatas[(int)floor, (int)type] = null;
        OnChangeFurnitureHandler?.Invoke(floor, type);
    }


    public FurnitureData GetEquipFurniture(ERestaurantFloorType floor, FurnitureType type)
    {
        return _equipFurnitureDatas[(int)floor, (int)type];
    }

    #endregion

    #region KitchenData

    public SetData GetEquipKitchenUntensilSetData(ERestaurantFloorType type)
    {
        return _kitchenuntensilEnabledSetData[(int)type];
    }

    public void SetEquipKitchenUntensilSetData(ERestaurantFloorType type, SetData data)
    {
        if (_kitchenuntensilEnabledSetData[(int)type] == data)
            return;

        _kitchenuntensilEnabledSetData[(int)type] = data;
        OnChangeKitchenUtensilSetDataHandler?.Invoke();
    }


    public void GiveKitchenUtensil(KitchenUtensilData data)
    {
        if (_giveKitchenUtensilList.Contains(data.Id))
        {
            DebugLog.Log("이미 가지고 있습니다.");
            return;
        }

        _giveKitchenUtensilList.Add(data.Id);
        CheckKitchenSetCount();
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
        OnChangeKitchenUtensilHandler?.Invoke(floor, type);
    }

    public KitchenUtensilData GetEquipKitchenUtensil(ERestaurantFloorType floor, KitchenUtensilType type)
    {
        return _equipKitchenUtensilDatas[(int)floor, (int)type];
    }

    #endregion

    #region EffectSetData

    public int GetActivatedFurnitureEffectSetCount()
    {
        return _activatedFurnitureEffectSet.Count;
    }

    public int GetActivatedKitchenUtensilEffectSetCount()
    {
        return _activatedKitchenUtensilEffectSet.Count;
    }

    public bool IsActivatedFurnitureEffectSet(string setId)
    {
        if (_activatedFurnitureEffectSet.Contains(setId))
            return true;

        return false;
    }


    public bool IsActivatedKitchenUtensilEffectSet(string setId)
    {
        if (_activatedKitchenUtensilEffectSet.Contains(setId))
            return true;

        return false;
    }


    public int GetEffectSetFurnitureCount(string setId)
    {
        if (_activatedFurnitureEffectSet.Contains(setId))
            return ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT;

        if (_furnitureEffectSetCountDic.ContainsKey(setId))
            return _furnitureEffectSetCountDic[setId];

        _furnitureEffectSetCountDic.Add(setId, 0);
        return 0;
    }


    public int GetEffectSetKitchenUtensilCount(string setId)
    {
        if (_activatedKitchenUtensilEffectSet.Contains(setId))
            return ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT;

        if (_kitchenUtensilEffectSetCountDic.ContainsKey(setId))
            return _kitchenUtensilEffectSetCountDic[setId];

        _kitchenUtensilEffectSetCountDic.Add(setId, 0);
        return 0;
    }

    private void CheckFurnitureSetCount()
    {
        _furnitureEffectSetCountDic.Clear();
        string setId = string.Empty;

        for (int i = 0, cnt = _giveFurnitureList.Count; i < cnt; ++i)
        {
            var furnitureData = FurnitureDataManager.Instance.GetFurnitureData(_giveFurnitureList[i]);
            if (furnitureData == null)
                continue;

            setId = furnitureData.SetId;
            if (string.IsNullOrEmpty(setId) || SetDataManager.Instance.GetSetData(setId) == null)
                continue;

            if (_furnitureEffectSetCountDic.ContainsKey(setId))
                _furnitureEffectSetCountDic[setId] += 1;
            else
                _furnitureEffectSetCountDic.Add(setId, 1);
        }

        foreach (var data in _furnitureEffectSetCountDic)
        {
            if (_activatedFurnitureEffectSet.Contains(data.Key))
                continue;

            if (data.Value >= ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT)
                _activatedFurnitureEffectSet.Add(data.Key);
        }
    }


    private void CheckKitchenSetCount()
    {
        _kitchenUtensilEffectSetCountDic.Clear();
        string setId = string.Empty;

        for (int i = 0, cnt = _giveKitchenUtensilList.Count; i < cnt; ++i)
        {
            var kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(_giveKitchenUtensilList[i]);
            if (kitchenData == null)
                continue;

            setId = kitchenData.SetId;
            if (string.IsNullOrEmpty(setId) || SetDataManager.Instance.GetSetData(setId) == null)
                continue;

            if (_kitchenUtensilEffectSetCountDic.ContainsKey(setId))
                _kitchenUtensilEffectSetCountDic[setId] += 1;
            else
                _kitchenUtensilEffectSetCountDic.Add(setId, 1);
        }

        foreach (var data in _kitchenUtensilEffectSetCountDic)
        {
            if (_activatedKitchenUtensilEffectSet.Contains(data.Key))
                continue;

            if (data.Value >= ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT)
                _activatedKitchenUtensilEffectSet.Add(data.Key);
        }
    }


    #endregion

    #region TableData

    public CoinAreaData GetCoinAreaData(ERestaurantFloorType floor, TableType type, int index)
    {
        int floorIndex = (int)floor;
        int typeIndex = (int)type;

        if (_coinAreaDatas[floorIndex, typeIndex * 2 + index] == null)
            _coinAreaDatas[floorIndex, typeIndex * 2 + index] = new CoinAreaData();

        return _coinAreaDatas[floorIndex, typeIndex * 2 + index];
    }


    public GarbageAreaData GetGarbageAreaData(ERestaurantFloorType floor, TableType type)
    {
        int floorIndex = (int)floor;
        int typeIndex = (int)type;

        if (_garbageAreaDatas[floorIndex, typeIndex] == null)
            _garbageAreaDatas[floorIndex, typeIndex] = new GarbageAreaData();   

        return _garbageAreaDatas[floorIndex, typeIndex];
    }




    #endregion

    #region ServerData

    public ServerStageData SaveData()
    {
        ServerStageData data = new ServerStageData();

        data.UnlockFloor = _unlockFloor;
        data.Score = _score;
        data.Tip = _tip;

        data.CoinAreaDataList = ConvertCoinAreaDataToList();
        data.GarbageAreaDataList = ConvertGarbageAreaDataToList();

        // 1. 직원 장착 데이터 변환 (StaffData[,] → List<List<string>>)
        data.EquipStaffDataList = new List<List<string>>();
        for (int i = 0; i < (int)ERestaurantFloorType.Length; i++)
        {
            List<string> staffList = new List<string>();
            for (int j = 0; j < (int)StaffType.Length; j++)
            {
                staffList.Add(_equipStaffDatas[i, j] != null ? _equipStaffDatas[i, j].Id : ""); // StaffData에서 ID 추출
            }
            data.EquipStaffDataList.Add(staffList);
        }

        // 2. 직원 레벨 정보 복사 (Dictionary 그대로 할당)
        data.GiveStaffLevelDic = new Dictionary<string, int>(_giveStaffLevelDic);

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
        _giveStaffLevelDic = loadData.GiveStaffLevelDic;
        for (int i = 0, cnt = loadData.EquipStaffDataList.Count; i < cnt; ++i)
        {
            for (int j = 0, cntJ = loadData.EquipStaffDataList[i].Count; j < cntJ; ++j)
            {
                string staffId = loadData.EquipStaffDataList[i][j];
                if (string.IsNullOrWhiteSpace(staffId))
                    continue;

                StaffData data = StaffDataManager.Instance.GetStaffData(staffId);
                SetEquipStaff((ERestaurantFloorType)i, data);
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

        ConvertListToCoinAreaDataArray(loadData.CoinAreaDataList);
        ConvertListToGarbageAreaDataArray(loadData.GarbageAreaDataList);

        CheckKitchenSetCount();
        CheckFurnitureSetCount();
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
