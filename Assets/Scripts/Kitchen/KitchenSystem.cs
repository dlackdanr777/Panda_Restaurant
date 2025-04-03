using System.Collections.Generic;
using UnityEngine;


public class KitchenSystem : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private KitchenUtensilGroup[] _kitchenUtensilGroups;


    private Dictionary<ERestaurantFloorType, KitchenUtensilGroup> _kitchenUtensilGroupDic = new Dictionary<ERestaurantFloorType, KitchenUtensilGroup>();


    private void Awake()
    {   
        for (int i = 0, cnt = _kitchenUtensilGroups.Length; i < cnt; ++i)
        {
            KitchenUtensilGroup group = _kitchenUtensilGroups[i];

            if(_kitchenUtensilGroupDic.ContainsKey(group.FloorType))
            {
                DebugLog.LogError("�ش� Ÿ���� �̹� ��ϵǾ� �ֽ��ϴ�: " + group.name + "("+ group.FloorType + ")");
                continue;
            }

            group.Init();
            _kitchenUtensilGroupDic.Add(group.FloorType, group);
        }
    }


    public void EqueueFood(ERestaurantFloorType floorType, CookingData foodData)
    {
        if (!_kitchenUtensilGroupDic.TryGetValue(floorType, out KitchenUtensilGroup group))
        {
            DebugLog.LogError("�ش� Ÿ���� ���� Ÿ���Դϴ�: " + floorType);
            return;
        }

        group.EqueueFood(foodData);
    }

    public List<KitchenBurnerData> GetCookingBurnerDataList(ERestaurantFloorType floorType)
    {
        return _kitchenUtensilGroupDic[floorType].GetCookingBurnerDataList();
    }

    public SinkKitchenUtensil GetSinkKitchenUtensil(ERestaurantFloorType floorType)
    {
        return _kitchenUtensilGroupDic[floorType].GetSinkKitchenUtensil();
    }

    public Vector3 GetStaffPos(ERestaurantFloorType floorType, EquipStaffType type)
    {
        return _kitchenUtensilGroupDic[floorType].GetStaffPos(type);
    }

    public Vector3 GetDoorPos(Vector3 pos)
    {
        foreach (KitchenUtensilGroup group in _kitchenUtensilGroupDic.Values)
        {
            Vector3 doorPos = group.GetDoorPos(pos);
            if (doorPos == Vector3.zero)
                continue;

            return doorPos;
        }

        DebugLog.LogError("�ش� ��ġ���� �´� �� ��ġ���� �����ϴ�: " + pos);
        return Vector3.zero;
    }
}
