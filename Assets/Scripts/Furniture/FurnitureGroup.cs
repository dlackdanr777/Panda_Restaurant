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


    private TableManager _tableManager;
    private Dictionary<FurnitureType, List<Furniture>> _furnitureDic = new Dictionary<FurnitureType, List<Furniture>>();
    private Dictionary<TableType, TableData> _tableDataDic = new Dictionary<TableType, TableData>();

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
                return data.CustomerMoveTr.position;
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

    public List<TableData> GetTableDataList()
    {
        List<TableData> tableDataList = new List<TableData>();
        foreach (TableData data in _tableDataDic.Values)
        {
            tableDataList.Add(data);
        }

        return tableDataList;
    }


    public void Init(TableManager tableManager, RectTransform buttonParent, TableButton orderButtonPrefab, TableButton servingButtonPrefab)
    {
        _tableManager = tableManager;
        _tableDataDic.Add(TableType.Table1, _table1Data);
        _tableDataDic.Add(TableType.Table2, _table2Data);
        _tableDataDic.Add(TableType.Table3, _table3Data);
        _tableDataDic.Add(TableType.Table4, _table4Data);
        _tableDataDic.Add(TableType.Table5, _table5Data);

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
    }


    private void OnChangeFurnitureEvent(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (floorType != _floorType)
            return;

        FurnitureData equipFurniture = UserInfo.GetEquipFurniture(floorType, type);

        foreach (Furniture data in _furnitureDic[type])
        {
            data.SetFurnitureData(equipFurniture);
        }
    }


    private void CheckTableEnabled(ERestaurantFloorType floorType, FurnitureType type)
    {
        if (_floorType != floorType)
            return;

        if (type < FurnitureType.Table1 || FurnitureType.Table5 < type)
            return;

        FurnitureData equipFurniture = UserInfo.GetEquipFurniture(_floorType, type);
        TableData data = _tableDataDic[(TableType)type];
        if (equipFurniture == null)
        {
            _tableManager.NotFurnitureTable(data);
            return;
        }
        else
        {
            if (data.TableState != ETableState.NotUse)
                return;

            data.TableState = ETableState.None;
        }
    }


    private void OnTableUpdateEvent()
    {
        for(int i = 0, cnt = (int)TableType.Length; i < cnt; ++i)
        {
            TableType type = (TableType)i;
            TableData data = _tableDataDic[type];
            if (UserInfo.GetEquipFurniture(ERestaurantFloorType.Floor1, (FurnitureType)i) == null)
            {
                _tableManager.NotFurnitureTable(data);
            }
            else if (data.TableState == ETableState.DontUse)
            {
                data.TableState = ETableState.NotUse;
            }
            else if (data.TableState == ETableState.NotUse)
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
            else if (data.TableState == ETableState.Move || data.TableState == ETableState.WaitFood || data.TableState == ETableState.Eating || data.TableState == ETableState.UseStaff || data.TableState == ETableState.DontUse)
            {
                data.OrderButton.gameObject.SetActive(false);
                data.ServingButton.gameObject.SetActive(false);
            }
        }
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeFurnitureHandler -= OnChangeFurnitureEvent;
        UserInfo.OnChangeFurnitureHandler -= CheckTableEnabled;
    }
}
