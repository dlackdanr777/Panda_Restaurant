using System;
using UnityEngine;

public class UIRestaurantAdminStaffSlot : UIRestaurantAdminSlot
{
    [Space]
    [Header("Staff")]
    [SerializeField] private UITextAndText _equipGroup;

    public void SetEquipText(string typeText)
    {
        _equipGroup.SetText1(typeText);
    }

    public void EquipGroupSetActive(bool value)
    {
        _equipGroup.gameObject.SetActive(value);
    }
}
