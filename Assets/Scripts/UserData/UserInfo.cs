using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class UserInfo
{
    private static StaffData[] _equipStaffDatas = new StaffData[(int)StaffType.Length];


    public static void SetEquipStaff(StaffData data)
    {
        _equipStaffDatas[(int)StaffDataManager.Instance.GetStaffType(data)] = data;
    }

    public static StaffData GetSEquipStaff(StaffType type)
    {
        return _equipStaffDatas[(int)type];
    }


}
