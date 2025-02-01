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
}
