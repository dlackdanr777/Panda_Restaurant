using UnityEngine;

public class StaffChef : Staff
{
    [Header("Chef Components")]
    [SerializeField] private Animator _animator;
    [SerializeField] private GameObject _handParent;
    [SerializeField] private SpriteRenderer _handSprite;


    private Sprite _backSprite;

    public override void Init(EquipStaffType type, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController, FeverSystem feverSystem)
    {
        base.Init(type, tableManager, kitchenSystem, customerController, feverSystem);
    }

    public override void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        base.SetStaffData(staffData, equipFloorType);

        if (staffData == null)
            return;

        if (!(staffData is ChefData))
            throw new System.Exception("���� ���ǿ��� ���� �����Ͱ� ������ �ʾҽ��ϴ�.");

        ChefData chefData = (ChefData)staffData;
        _handSprite.sprite = chefData.HandSprite;
        _handParent.transform.localPosition = chefData.HandOffset;
        _backSprite = chefData.BackSprite;
    }

    public override void SetStaffState(EStaffState state)
    {
        base.SetStaffState(state);
        _spriteRenderer.sprite = state == EStaffState.Action ? _backSprite : _staffData.Sprite;
        _animator.SetInteger("State", (int)_state);
    }
}

