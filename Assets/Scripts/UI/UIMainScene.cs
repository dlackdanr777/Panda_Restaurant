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

        DataBind.SetUnityActionValue("ShowRecipeUI", OnShowRecipeUI);
        DataBind.SetUnityActionValue("HideRecipeUI", OnHideRecipeUI);
        DataBind.SetUnityActionValue("HideNoAnimeRecipeUI", OnHideNoAnimeRecipeUI);
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

    private void OnHideNoAnimeStaffUI()
    {
        _uiNav.PopNoAnime("UIStaff");
    }

    private void OnShowRecipeUI()
    {
        _uiNav.Push("UIRecipe");
    }

    private void OnHideRecipeUI()
    {
        _uiNav.Pop("UIRecipe");
    }

    private void OnHideNoAnimeRecipeUI()
    {
        _uiNav.PopNoAnime("UIRecipe");
    }
}
