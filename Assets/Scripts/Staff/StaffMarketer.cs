using UnityEngine.UI;

public class StaffMarketer : Staff
{

    private Image _skillEffectImage;
    private UIMarketerImage _skillEffectOwner;
    private bool _skillEffectRequested;
    public bool IsSkillEffectVisible => _skillEffectRequested && gameObject.activeInHierarchy && _staffData != null;

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
            throw new System.Exception("치어리더 스탭에게 치어리더 데이터가 들어오지 않았습니다.");
    }

    public void SetSkillEffect(Image skillEffect, ERestaurantFloorType floor)
    {
        if (_skillEffectOwner != null) _skillEffectOwner.UnregisterSkillEffect(this);
        else if (_skillEffectImage != null) _skillEffectImage.gameObject.SetActive(false);
        _skillEffectImage = skillEffect;
        _skillEffectOwner = skillEffect == null ? null : skillEffect.GetComponentInParent<UIMarketerImage>(true);
        if (_skillEffectOwner != null) _skillEffectOwner.RegisterSkillEffect(floor, this);
        else if (_skillEffectImage != null) _skillEffectImage.gameObject.SetActive(IsSkillEffectVisible);
    }

    protected override void SkillEffectSetActive(bool isActive)
    {
        _skillEffectRequested = isActive;
        if (_skillEffectOwner != null) _skillEffectOwner.RefreshSkillEffect();
        else if (_skillEffectImage != null) _skillEffectImage.gameObject.SetActive(IsSkillEffectVisible);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_skillEffectOwner != null) _skillEffectOwner.UnregisterSkillEffect(this);
    }
}

