using UnityEngine.UI;

public class StaffMarketer : Staff
{

    private Image _skillEffectImage;

    public override void Init(EquipStaffType type, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController, FeverSystem feverSystem)
    {
        base.Init(type, tableManager, kitchenSystem, customerController, feverSystem);
    }

    public override void SetStaffData(StaffData staffData, ERestaurantFloorType equipFloorType)
    {
        base.SetStaffData(staffData, equipFloorType);

        if (staffData == null)
            return;

        if (!(staffData is MarketerData))
            throw new System.Exception("ġ��� ���ǿ��� ġ��� �����Ͱ� ������ �ʾҽ��ϴ�.");
    }

    public void SetSkillEffect(Image skillEffect)
    {
        _skillEffectImage = skillEffect;
    }

    protected override void SkillEffectSetActive(bool isActive)
    {
        _skillEffectImage?.gameObject.SetActive(isActive);
    }
}

