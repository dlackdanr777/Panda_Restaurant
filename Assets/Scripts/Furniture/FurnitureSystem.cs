using System;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureSystem : MonoBehaviour
{
    [Header("Button Options")]
    [SerializeField] private RectTransform _buttonParent;
    [SerializeField] private TableButton _orderButtonPrefab;
    [SerializeField] private TableButton _servingButtonPrefab;

    [Space]
    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private FurnitureGroup[] _furnitureGroups;

    


    private Dictionary<ERestaurantFloorType, FurnitureGroup> _furnitureGroupDic = new Dictionary<ERestaurantFloorType, FurnitureGroup>();

    public Vector3 GetStaffPos(ERestaurantFloorType floorType, EquipStaffType type)
    {
        return _furnitureGroupDic[floorType].GetStaffPos(type);
    }

    public TableData GetFirstTableData()
    {
        for(int i = 0, cnt =  _furnitureGroupDic.Count; i < cnt; ++i)
        {
            if (GetUsableTableCount((ERestaurantFloorType)i) <= 0)
                continue;

            return _furnitureGroupDic[(ERestaurantFloorType)i].GetUsableFirstTableData();
        }

        return null;
    }

    public int GetUsableTableCount()
    {
        int count = 0;
        foreach(var data in _furnitureGroupDic)
        {
            if (!UserInfo.IsFloorValid(UserInfo.CurrentStage, data.Key))
                continue;

            count += data.Value.GetUsableTableCount();
        }

        return count;
    }

    public int GetUsableTableCount(ERestaurantFloorType floorType)
    {
        if (!UserInfo.IsFloorValid(UserInfo.CurrentStage, floorType))
            return 0;

        return _furnitureGroupDic[floorType].GetUsableTableCount();
    }

    public TableData GetTableType(ERestaurantFloorType floorType, ETableState state)
    {
       return _furnitureGroupDic[floorType].GetTableType(state);
    }


    public TableData GetTableType(ETableState state)
    {
        for(int i = 0, cnt = (int)UserInfo.GetUnlockFloor(UserInfo.CurrentStage); i <= cnt; ++i)
        {
            TableData data = _furnitureGroupDic[(ERestaurantFloorType)i].GetTableType(state);
            if (data == null)
                continue;

            return data;
        }

        return null;
    }


    public TableData GetTableType(ERestaurantFloorType floorType, TableType type)
    {
        return _furnitureGroupDic[floorType].GetTableType(type);
    }


    public List<TableData> GetTableDataList(ERestaurantFloorType floorType)
    {
        if (!_furnitureGroupDic.ContainsKey(floorType))
        {
            DebugLog.LogError("���� Ÿ���� ���� ������ �׷��� �����ϴ�:" + floorType.ToString());
            return null;
        }

        return _furnitureGroupDic[floorType].GetTableDataList();
    }

    public List<TableData> GetAllTableDataList()
    {
        List<TableData> tableDataList = new List<TableData>();
        foreach (FurnitureGroup group in _furnitureGroupDic.Values)
        {
            tableDataList.AddRange(group.GetTableDataList());
        }
        return tableDataList;
    }

    public List<DropGarbageArea> GetDropGarbageAreaList(ERestaurantFloorType floorType)
    {
        if(!_furnitureGroupDic.ContainsKey(floorType))
        {
            DebugLog.LogError("���� Ÿ���� ���� ������ �׷��� �����ϴ�:" + floorType.ToString());
            return new List<DropGarbageArea>();
        }

        return _furnitureGroupDic[floorType].GetDropGarbageAreaList();
    }

    public List<DropCoinArea> GetDropCoinAreaList(ERestaurantFloorType floorType)
    {
        return _furnitureGroupDic[floorType].GetDropCoinAreaList();
    }


    public Vector3 GetDoorPos(Vector3 pos)
    {
        foreach(FurnitureGroup group in  _furnitureGroupDic.Values)
        {
            Vector3 doorPos = group.GetDoorPos(pos);
            if (doorPos == Vector3.zero)
                continue;

            return doorPos;
        }

        DebugLog.LogError("�ش� ��ġ���� �´� �� ��ġ���� �����ϴ�: " + pos);
        return Vector3.zero;
    }

    public Vector3 GetFoodPos(ERestaurantFloorType floor, RestaurantType type, Vector3 pos)
    {
        if (!_furnitureGroupDic.TryGetValue(floor, out FurnitureGroup group))
            throw new Exception("�ش� FloorŸ���� ���� ���� �׷��� �����ϴ�: " + floor);

        return group.GetFoodPos(type, pos);
    }

    public FoodType GetFoodType(ERestaurantFloorType floor)
    {
        if (!_furnitureGroupDic.TryGetValue(floor, out FurnitureGroup group))
            throw new Exception("�ش� FloorŸ���� ���� ���� �׷��� �����ϴ�: " + floor);

        return group.GetFoodType();
    }



    private void Awake()
    {
        for (int i = 0, cnt = _furnitureGroups.Length; i < cnt; ++i)
        {
            FurnitureGroup group = _furnitureGroups[i];
            if (_furnitureGroupDic.ContainsKey(group.FloorType))
            {
                DebugLog.LogError("�ش� Ÿ���� �̹� ��ϵǾ� �ֽ��ϴ�: " + group.name + "(" + group.FloorType + ")");
                continue;
            }

            group.Init(_tableManager, _buttonParent, _orderButtonPrefab, _servingButtonPrefab);
            _furnitureGroupDic.Add(group.FloorType, group);
        }
    }

}
