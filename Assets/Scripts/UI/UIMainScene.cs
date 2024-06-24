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
        DataBind.SetUnityActionValue("HideNoAnimeStaffUI", OnHideNoAnimeStaffUI);

        DataBind.SetUnityActionValue("ShowRecipeUpgradeUI", OnShowRecipeUpgradeUI);
        DataBind.SetUnityActionValue("HideRecipeUpgradeUI", OnHideRecipeUpgradeUI);
        DataBind.SetUnityActionValue("HideNoAnimeRecipeUpgradeUI", OnHideNoAnimeRecipeUpgradeUI);

        DataBind.SetUnityActionValue("ShowStaffUpgradeUI", OnShowStaffUpgradeUI);
        DataBind.SetUnityActionValue("HideStaffUpgradeUI", OnHideStaffUpgradeUI);
        DataBind.SetUnityActionValue("HideNoAnimeStaffUpgradeUI", OnHideNoAnimeStaffUpgradeUI);
    }


    private void OnShowRestaurantAdminUI()
    {
        _uiNav.Push("RestaurantAdminUI");
    }

    private void OnHideRestaurantAdminUI()
    {
        _uiNav.PopNoAnime("UIStaff");
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

    private void OnHideNoAnimeStaffUI()
    {
        _uiNav.PopNoAnime("UIStaff");
    }

    private void OnShowRecipeUpgradeUI()
    {
        _uiNav.Push("UIRecipeUpgrade");
    }

    private void OnHideRecipeUpgradeUI()
    {
        _uiNav.Pop("UIRecipeUpgrade");
    }

    private void OnHideNoAnimeRecipeUpgradeUI()
    {
        _uiNav.PopNoAnime("UIRecipeUpgrade");
    }

    private void OnShowStaffUpgradeUI()
    {
        _uiNav.Push("UIStaffUpgrade");
    }

    private void OnHideStaffUpgradeUI()
    {
        _uiNav.Pop("UIStaffUpgrade");
    }

    private void OnHideNoAnimeStaffUpgradeUI()
    {
        _uiNav.PopNoAnime("UIStaffUpgrade");
    }
}
