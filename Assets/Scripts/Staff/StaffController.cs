using System.Collections.Generic;
using UnityEngine;

public class StaffController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private KitchenSystem _kitchenSystem;
    [SerializeField] private FeverSystem _feverSystem;
    [SerializeField] private StaffGroup[] _staffGroups;


    private Dictionary<ERestaurantFloorType, StaffGroup> _staffGroupDic = new Dictionary<ERestaurantFloorType, StaffGroup>();

    private void Awake()
    {
        for (int i = 0, cnt = _staffGroups.Length; i < cnt; ++i)
        {
            StaffGroup group = _staffGroups[i];
            if (_staffGroupDic.ContainsKey(group.FloorType))
            {
                DebugLog.LogError("해당 타입이 이미 등록되어 있습니다: " + group.name + "(" + group.FloorType + ")");
                continue;
            }

            group.Init(_customerController, _tableManager, _kitchenSystem, _feverSystem);
            _staffGroupDic.Add(group.FloorType, group);
        }
    }

    private void FixedUpdate()
    {
        if (!UserInfo.IsFirstTutorialClear || UserInfo.IsTutorialStart)
        {
            return;
        }

        foreach (StaffGroup group in _staffGroupDic.Values)
        {
            if (!UserInfo.IsFloorValid(UserInfo.CurrentStage, group.FloorType))
                continue;

            group.UpdateStaff();
        }
    }
}
