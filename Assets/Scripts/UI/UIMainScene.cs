using Muks.MobileUI;
using Muks.DataBind;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

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
        DataBind.SetUnityActionValue("PopUI", OnPopUI);

        DataBind.SetUnityActionValue("ShowRestaurantAdminUI", OnShowRestaurantAdminUI);
        DataBind.SetUnityActionValue("HideRestaurantAdminUI", OnHideRestaurantAdminUI);

        DataBind.SetUnityActionValue("ShowStaffUI", OnShowStaffUI);
        DataBind.SetUnityActionValue("HideStaffUI", OnHideStaffUI);
        DataBind.SetUnityActionValue("HideNoAnimeStaffUI", OnHideNoAnimeStaffUI);

        DataBind.SetUnityActionValue("ShowStaffUpgradeUI", OnShowStaffUpgradeUI);
        DataBind.SetUnityActionValue("HideStaffUpgradeUI", OnHideStaffUpgradeUI);
        DataBind.SetUnityActionValue("HideNoAnimeStaffUpgradeUI", OnHideNoAnimeStaffUpgradeUI);

        DataBind.SetUnityActionValue("ShowFurnitureUI", OnShowFurnitureUI);
        DataBind.SetUnityActionValue("HideFurnitureUI", OnHideFurnitureUI);
        DataBind.SetUnityActionValue("HideNoAnimeFurnitureUI", OnHideNoAnimeFurnitureUI);

        DataBind.SetUnityActionValue("ShowKitchenUI", OnShowKitchenUI);
        DataBind.SetUnityActionValue("HideKitchenUI", OnHideKitchenUI);
        DataBind.SetUnityActionValue("HideNoAnimeKitchenUI", OnHideNoAnimeKitchenUI);

        DataBind.SetUnityActionValue("ShowTipUI", OnShowTipUI);
        DataBind.SetUnityActionValue("HideTipUI", OnHideTipUI);

        DataBind.SetUnityActionValue("ShowChallengeUI", OnShowChallengeUI);
        DataBind.SetUnityActionValue("HideChallengeUI", OnHideChallengeUI);
        DataBind.SetUnityActionValue("HideNoAnimeChallengeUI", OnHideNoAnimeChallengeUI);

        DataBind.SetUnityActionValue("ShowMainChallengeUI", OnShowMainChallengeUI);
        DataBind.SetUnityActionValue("HideMainChallengeUI", OnHideMainChallengeUI);
        DataBind.SetUnityActionValue("HideNoAnimeMainChallengeUI", OnHideNoAnimeMainChallengeUI);
    }


    private void Update()
    {
        if (!Input.anyKey)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            _uiNav.Pop();
    }

    private void OnPopUI()
    {
        _uiNav.Pop();
    }

    private void OnShowRestaurantAdminUI()
    {
        _uiNav.Push("RestaurantAdminUI");
    }

    private void OnHideRestaurantAdminUI()
    {
        _uiNav.PopNoAnime("UIStaff");
        _uiNav.PopNoAnime("UIFurniture");
        _uiNav.PopNoAnime("UIKitchen");
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

    private void OnShowFurnitureUI()
    {
        _uiNav.Push("UIFurniture");
    }

    private void OnHideFurnitureUI()
    {
        _uiNav.Pop("UIFurniture");
    }

    private void OnHideNoAnimeFurnitureUI()
    {
        _uiNav.PopNoAnime("UIFurniture");
    }

    private void OnShowKitchenUI()
    {
        _uiNav.Push("UIKitchen");
    }

    private void OnHideKitchenUI()
    {
        _uiNav.Pop("UIKitchen");
    }

    private void OnHideNoAnimeKitchenUI()
    {
        _uiNav.PopNoAnime("UIKitchen");
    }


    private void OnShowTipUI()
    {
        _uiNav.Push("UITip");
    }

    private void OnHideTipUI()
    {
        _uiNav.Pop("UITip");
    }

    private void OnShowChallengeUI()
    {
        _uiNav.Push("UIChallenge");
    }

    private void OnHideChallengeUI()
    {
        _uiNav.Pop("UIChallenge");
    }

    private void OnHideNoAnimeChallengeUI()
    {
        _uiNav.PopNoAnime("UIChallenge");
    }

    private void OnShowMainChallengeUI()
    {
        _uiNav.Push("UIMainChallenge");
    }

    private void OnHideMainChallengeUI()
    {
        _uiNav.Pop("UIMainChallenge");
    }

    private void OnHideNoAnimeMainChallengeUI()
    {
        _uiNav.PopNoAnime("UIMainChallenge");
    }
}
