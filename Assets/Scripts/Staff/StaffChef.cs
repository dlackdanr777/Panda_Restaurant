using UnityEngine;

public class StaffChef : Staff
{
    [Header("Waiter Components")]
    [SerializeField] private Animator _animator;



    public override void Init(EquipStaffType type, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)
    {
        base.Init(type, tableManager, kitchenSystem, customerController);
    }

    public override void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        if (!(staffData is ChefData))
            throw new System.Exception("���� ���ǿ��� ���� �����Ͱ� ������ �ʾҽ��ϴ�.");

        base.SetStaffData(staffData, equipFloorType);

    }

    public override void SetStaffState(EStaffState state)
    {
        base.SetStaffState(state);
        _animator.SetInteger("State", (int)_state);
    }
}

