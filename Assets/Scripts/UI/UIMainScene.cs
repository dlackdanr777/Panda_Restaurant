using Muks.MobileUI;
using Muks.DataBind;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MobileUINavigation))]
public class UIMainScene : MonoBehaviour
{
    private MobileUINavigation _uiNav;

    private void Awake()
    {
        _uiNav = GetComponent<MobileUINavigation>();
    }

    void Start()
    {
        DataBind.SetUnityActionValue("ShowRestaurantAdminUI", OnShowRestaurantAdminUI);
        DataBind.SetUnityActionValue("HideRestaurantAdminUI", OnHideRestaurantAdminUI);

        DataBind.SetUnityActionValue("ShowStaffUI", OnShowStaffUI);
        DataBind.SetUnityActionValue("HideStaffUI", OnHideStaffUI);
    }


    private void OnShowRestaurantAdminUI()
    {
        _uiNav.Push("RestaurantAdminUI");
    }

    private void OnHideRestaurantAdminUI()
    {
        _uiNav.Pop("RestaurantAdminUI");
    }

    private void OnShowStaffUI()
    {
        _uiNav.Push("UIStaff");
    }

    private void OnHideStaffUI()
    {
        _uiNav.Pop("UIStaff");
    }
}
