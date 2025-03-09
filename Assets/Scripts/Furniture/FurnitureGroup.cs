using System;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureGroup : MonoBehaviour
{
    [Header("Option")]
    [SerializeField] private ERestaurantFloorType _floorType;
    public ERestaurantFloorType FloorType => _floorType;

    [Space]
    [Header("Components")]
    [SerializeField] private Furniture[] _furniture;


    [Space]
    [Header("TableData")]
    [SerializeField] private TableData _table1Data;
    [SerializeField] private TableData _table2Data;
    [SerializeField] private TableData _table3Data;
    [SerializeField] private TableData _table4Data;
    [SerializeField] private TableData _table5Data;

    [Space]
    [Header("Transforms")]
    [SerializeField] private Transform _cashTableTr;
    [SerializeField] private Transform _marketerTr;
    [SerializeField] private Transform _guardTr;
    [SerializeField] private Transform _cleanerWaitTr;
    [SerializeField] private Transform _door1;
    [SerializeField] private Transform _door2;


    private TableManager _tableManager;
    private Dictionary<FurnitureType, List<Furniture>> _furnitureDic = new Dictionary<FurnitureType, List<Furniture>>();
    private Dictionary<TableType, TableData> _tableDataDic = new Dictionary<TableType, TableData>();
    private List<TableData> _tableDataList = new List<TableData>();
    private List<DropCoinArea> _dropCoinAreaList = new List<DropCoinArea>();
    private List<DropGarbageArea> _dropGarbageAreaList = new List<DropGarbageArea>();
    private FoodType _foodType;

    public TableData GetTableData(TableType type)
    {
        return _tableDataDic[type];
    }


    public Vector2 GetStaffPos(TableData data, StaffType type)
    {
        switch (type)
        {
            case StaffType.Waiter:
                return data.LeftStaffTr.position;
            case StaffType.Server:
                return data.RightStaffTr.position;
            case StaffType.Cleaner:
                return _cleanerWaitTr.position;
            case StaffType.Manager:
                return _cashTableTr.position;
            case StaffType.Marketer:
                return _marketerTr.position;
            case StaffType.Guard:
                return _guardTr.position;
        }

        Debug.LogError("직원 종류 값이 잘못 입력되었습니다.");
        return new Vector2(0, 0);
    }


    public TableData GetUsableFirstTableData()
    {
        foreach (TableData data in _tableDataDic.Values)
        {
            if (data.TableState != ETableState.None)
                continue;

            return data;
        }

        return null;
    }


    public int GetUsableTableCount()
    {
        int count = 0;
        foreach (TableData data in _tableDataDic.Values)
        {
            if (data.TableState != ETableState.None)
                continue;

            count++;
        }

        return count;
    }


    public TableData GetTableType(ETableState state)
    {
        foreach(TableData data in _tableDataDic.Values)
        {
            if (data.TableState != state)
                continue;

            return data;
        }

        return null;
    }
    
    public TableData GetTableType(TableType type)
    {
        return _tableDataDic[type];
    }

    public List<TableData> GetTableDataList()
    {
        return _tableDataList;
    }

    public List<DropGarbageArea> GetDropGarbageAreaList()
    {
        return _dropGarbageAreaList;
    }

    public List<DropCoinArea> GetDropCoinAreaList()
    {
        return _dropCoinAreaList;
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

    public FoodType GetFoodType()
    {
        return _foodType;
    }


    public void Init(TableManager tableManager, RectTransform buttonParent, TableButton orderButtonPrefab, TableButton servingButtonPrefab)
    {
        _tableManager = tableManager;
        _tableDataDic.Add(TableType.Table1, _table1Data);
        _tableDataDic.Add(TableType.Table2, _table2Data);
        _tableDataDic.Add(TableType.Table3, _table3Data);
        _tableDataDic.Add(TableType.Table4, _table4Data);
        _tableDataDic.Add(TableType.Table5, _table5Data);
        _table1Data.TableType = TableType.Table1;
        _table2Data.TableType = TableType.Table2;
        _table3Data.TableType = TableType.Table3;
        _table4Data.TableType = TableType.Table4;
        _table5Data.TableType = TableType.Table5;

        foreach (TableData data in _tableDataDic.Values)
        {
            _tableDataList.Add(data);
            _dropGarbageAreaList.Add(data.DropGarbageArea);

            for(int i = 0, cnt = data.DropCoinAreas.Length; i < cnt; ++i)
            {
                _dropCoinAreaList.Add(data.DropCoinAreas[i]);
            }
        }

        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            _furnitureDic.Add((FurnitureType)i, new List<Furniture>());
        }

        for (int i = 0, cnt = _furniture.Length; i < cnt; ++i)
        {
            _furnitureDic[_furniture[i].Type].Add(_furniture[i]);
            _furniture[i].Init();
        }

        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            OnChangeFurnitureEvent(_floorType, (FurnitureType)i);
        }

        GameObject obj = new GameObject(_floorType.ToString() + " Table");
        obj.transform.parent = buttonParent.transform;

        for (int i = 0, cnt = (int)FurnitureType.Table5; i <= cnt; ++i)
        {
            TableButton orderButton = Instantiate(orderButtonPrefab, obj.transform);
            TableButton servingButton = Instantiate(servingButtonPrefab, obj.transform);

            orderButton.Init();
            servingButton.Init();

            TableType tableType = (TableType)i;
            TableData data = _tableDataDic[tableType];
            data.FloorType = _floorType;
            data.TableState = ETableState.Empty;
            orderButton.AddListener(() => _tableManager.OnCustomerOrder(data));
            servingButton.AddListener(() => _tableManager.OnServing(data));

            orderButton.SetWorldTransform(data.ChairTrs[0]);
            servingButton.SetWorldTransform(data.ChairTrs[0]);

            data.ServingButton = servingButton;
            data.OrderButton = orderButton;

            CheckTableEnabled(_floorType, (FurnitureType)i);
        }

        _tableManager.OnTableUpdateHandler += OnTableUpdateEvent;
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
        UserInfo.OnChangeFurnitureHandler += CheckTableEnabled;
        OnTableUpdateEvent();
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            FurnitureType type = (FurnitureType)i;
            OnChangeFurnitureEvent(_floorType, type);
            CheckTableEnabled(_floorType, type);
        }

        for(int i = 0, cnt = (int)TableType.Length; i < cnt; ++i)
        {
            TableType type = (TableType)i;
            TableData table = _tableDataDic[type];
            GarbageAreaData garbageData = UserInfo.GetGarbageAreaData(UserInfo.CurrentStage, _floorType, type);
            table.DropGarbageArea.Init(garbageData);

            for(int j = 0, cntJ = table.DropCoinAreas.Length; j < cntJ; ++j)
            {
                CoinAreaData coinData = UserInfo.GetCoinAreaData(UserInfo.CurrentStage, _floorType, type, j);
                table.DropCoinAreas[j].Init(coinData);
            }
        }
    }


    private void OnChangeFurnitureEvent(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (floorType != _floorType)
            return;

        FurnitureData equipFurniture = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, floorType, type);
        foreach (Furniture data in _furnitureDic[type])
        {
            data.SetFurnitureData(equipFurniture);
        }

        OnChangeFoodTypeEvent();
    }


    private void CheckTableEnabled(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (_floorType != floorType)
            return;

        if (type < FurnitureType.Table1 || FurnitureType.Table5 < type)
            return;

        FurnitureData equipFurniture = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _floorType, type);
        TableData data = _tableDataDic[(TableType)type];
        if (equipFurniture == null)
        {
            _tableManager.NotFurnitureTable(data);
            return;
        }
        else
        {
            if (data.TableState != ETableState.DontUse)
                return;

            data.TableState = ETableState.Empty;
        }
    }


    private void OnTableUpdateEvent()
    {
        for(int i = 0, cnt = (int)TableType.Length; i < cnt; ++i)
        {
            TableType type = (TableType)i;
            TableData data = _tableDataDic[type];

            if (UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _floorType, (FurnitureType)i) == null)
            {
                _tableManager.NotFurnitureTable(data);
                data.OrderButton.gameObject.SetActive(false);
                data.ServingButton.gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.DontUse)
            {
                data.TableState = ETableState.Empty;
                data.OrderButton.gameObject.SetActive(false);
                data.ServingButton.gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.Empty)
            {
                data.OrderButton.gameObject.SetActive(false);
                data.ServingButton.gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.Seating)
            {
                data.OrderButton.gameObject.SetActive(true);
                data.ServingButton.gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.CanServing)
            {
                data.ServingButton.gameObject.SetActive(true);
                data.OrderButton.gameObject.SetActive(false);
            }
            else if (data.TableState == ETableState.Move || data.TableState == ETableState.WaitFood || data.TableState == ETableState.Eating || data.TableState == ETableState.UseStaff || data.TableState == ETableState.DontUse || data.TableState == ETableState.None)
            {
                data.OrderButton.gameObject.SetActive(false);
                data.ServingButton.gameObject.SetActive(false);
            }
        }
    }

    private void OnChangeFoodTypeEvent()
    {
        SetData setData = UserInfo.GetEquipFurnitureSetData(UserInfo.CurrentStage, _floorType);

        if (setData == null)
        {
            _foodType = FoodType.Length;
            return;
        }

        _foodType = setData.SetFoodType;
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
        UserInfo.OnChangeFurnitureHandler -= CheckTableEnabled;
    }
}
