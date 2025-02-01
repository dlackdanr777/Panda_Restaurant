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


    public Vector3 GetStaffPos(TableData data, StaffType type)
    {
        return _furnitureGroupDic[data.FloorType].GetStaffPos(data, type);
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
            if (data.Key < UserInfo.CurrentFloor)
                continue;

            count += data.Value.GetUsableTableCount();
        }

        return count;
    }

    public int GetUsableTableCount(ERestaurantFloorType floorType)
    {
        if (floorType < UserInfo.CurrentFloor)
            return 0;

        return _furnitureGroupDic[floorType].GetUsableTableCount();
    }

    public TableData GetTableType(ERestaurantFloorType floorType, ETableState state)
    {
       return _furnitureGroupDic[floorType].GetTableType(state);
    }

    public List<TableData> GetTableDataList(ERestaurantFloorType floorType)
    {
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
