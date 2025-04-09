using UnityEngine;

public class KitchenBurnerData
{
    public float Time;
    public CookingData CookingData;
    public bool IsUsable;

    private float _addCookSpeedMul = 0;
    public float AddCookSpeedMul => _addCookSpeedMul;
    public void SetAddCookSpeedMul(float value) => _addCookSpeedMul = Mathf.Max(value, 0);

    private KitchenUtensil _kitchenUtensil;
    public KitchenUtensil KitchenUtensil => _kitchenUtensil;
    public void SetKitchenUtensil(KitchenUtensil kitchenUtensil) => _kitchenUtensil = kitchenUtensil;

    private bool _staffUsable;
    public bool IsStaffUsable => _staffUsable;
    public bool SetStaffUsable(bool value) => _staffUsable = value;

    private Staff _useStaff;
    public Staff UseStaff => _useStaff;
    public void SetUseStaff(Staff value) => _useStaff = value;

}


