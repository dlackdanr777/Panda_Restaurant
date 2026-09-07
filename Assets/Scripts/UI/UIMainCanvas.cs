using Muks.DataBind;
using Muks.MobileUI;
using UnityEngine;

[RequireComponent(typeof(MobileUINavigation))]
public class UIMainCanvas : MonoBehaviour
{
    private enum GachaEntrySource
    {
        None,
        Main,
        StaffShop
    }

    private MobileUINavigation _uiNav;

    [SerializeField] private UIRestaurantAdmin _uiAdmin;
    [SerializeField] private UIGacha _uiGacha;

    private GachaEntrySource _gachaEntrySource;

    private void Awake()
    {
        _uiNav = GetComponent<MobileUINavigation>();
        _uiNav.OnHideUIHandler += OnMainNavigationViewHidden;

        if (_uiGacha != null)
            _uiGacha.HiddenHandler += CompleteGachaSession;
    }



    void Start()
    {
        DataBind.SetUnityActionValue("PopUI", OnPopUI);

        DataBind.SetUnityActionValue("ShowFurnitureTab", OnShowFurnitureTab);
        DataBind.SetUnityActionValue("ShowRecipeTab", OnShowRecipeTab);
        DataBind.SetUnityActionValue("ShowKitchenTab", OnShowKitchenTab);
        DataBind.SetUnityActionValue("ShowStaffTab", OnShowStaffTab);

        DataBind.SetUnityActionValue("ShowRestaurantAdminUI", OnShowRestaurantAdminUI);
        DataBind.SetUnityActionValue("HideRestaurantAdminUI", OnHideRestaurantAdminUI);

        DataBind.SetUnityActionValue("ShowStaffUI", OnShowStaffUI);
        DataBind.SetUnityActionValue("HideStaffUI", OnHideStaffUI);
        DataBind.SetUnityActionValue("HideNoAnimeStaffUI", OnHideNoAnimeStaffUI);

        DataBind.SetUnityActionValue("ShowStaffUpgradeUI", OnShowStaffUpgradeUI);
        DataBind.SetUnityActionValue("HideStaffUpgradeUI", OnHideStaffUpgradeUI);
        DataBind.SetUnityActionValue("HideNoAnimeStaffUpgradeUI", OnHideNoAnimeStaffUpgradeUI);

        DataBind.SetUnityActionValue("ShowRecipeUpgradeUI", OnShowRecipeUpgradeUI);
        DataBind.SetUnityActionValue("HideRecipeUpgradeUI", OnHideRecipeUpgradeUI);
        DataBind.SetUnityActionValue("HideNoAnimeRecipeUpgradeUI", OnHideNoAnimeRecipeUpgradeUI);

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

        DataBind.SetUnityActionValue("ShowManagementUI", OnShowManagementUI);
        DataBind.SetUnityActionValue("HideManagementUI", OnHideManagementUI);
        DataBind.SetUnityActionValue("HideNoAnimeManagementUI", OnHideNoAnimeManagementUI);

        DataBind.SetUnityActionValue("ShowPictorialBookUI", OnShowPictorialBookUI);
        DataBind.SetUnityActionValue("HidePictorialBookUI", OnHidePictorialBookUI);
        DataBind.SetUnityActionValue("HideNoAnimePictorialBookUI", OnHideNoAnimePictorialBookUI);

        DataBind.SetUnityActionValue("ShowGachaUI", OnShowGachaUI);
        DataBind.SetUnityActionValue("ShowStaffGachaUI", OnShowStaffGachaUI);
        DataBind.SetUnityActionValue("HideGachaUI", OnHideGachaUI);
        DataBind.SetUnityActionValue("HideNoAnimeGachaUI", OnHideNoAnimeGachaUI);

        DataBind.SetUnityActionValue("ShowSettingUI", OnShowSettingUI);
        DataBind.SetUnityActionValue("HideSettingUI", OnHideSettingUI);
        DataBind.SetUnityActionValue("HideNoAnimeSettingUI", OnHideNoAnimeSettingUI);

        DataBind.SetUnityActionValue("ShowAttendanceUI", OnShowAttendanceUI);
        DataBind.SetUnityActionValue("HideAttendanceUI", OnHideAttendanceUI);
        DataBind.SetUnityActionValue("HideNoAnimeAttendanceUI", OnHideNoAnimeAttendanceUI);

        DataBind.SetUnityActionValue("ShowUserReportUI", OnShowUserReportUI);
        DataBind.SetUnityActionValue("HideUserReportUI", OnHideUserReportUI);
        DataBind.SetUnityActionValue("HideNoAnimeUserReportUI", OnHideNoAnimeUserReportUI);

        DataBind.SetUnityActionValue("ShowTimeSkipUI", OnShowTimeSkipUI);
        DataBind.SetUnityActionValue("HideTimeSkipUI", OnHideTimeSkipUI);
        DataBind.SetUnityActionValue("HideNoAnimeTimeSkipUI", OnHideNoAnimeTimeSkipUI);

        DataBind.SetUnityActionValue("ShowFloorLockUI", OnShowFloorLockUI);
        DataBind.SetUnityActionValue("HideFloorLockUI", OnHideFloorLockUI);
        DataBind.SetUnityActionValue("HideNoAnimeFloorLockUI", OnHideNoAnimeFloorLockUI);

        DataBind.SetUnityActionValue("ShowAdUI", OnShowAdUI);
        DataBind.SetUnityActionValue("HideAdUI", OnHideAdUI);
        DataBind.SetUnityActionValue("HideNoAnimeAdUI", OnHideNoAnimeAdUI);

        DataBind.SetUnityActionValue("ShowPaymentUI", OnShowPaymentUI);
        DataBind.SetUnityActionValue("HidePaymentUI", OnHidePaymentUI);
        DataBind.SetUnityActionValue("HideNoAnimePaymentUI", OnHideNoAnimePaymentUI);

        DataBind.SetUnityActionValue("ShowFloor3UI", OnShowFloor3UI);
        DataBind.SetUnityActionValue("HideFloor3UI", OnHideFloor3UI);
        DataBind.SetUnityActionValue("HideNoAnimeFloor3UI", OnHideNoAnimeFloor3UI);

        DataBind.SetUnityActionValue("ShowMailboxUI", OnShowMailboxUI);
        DataBind.SetUnityActionValue("HideMailboxUI", OnHideMailboxUI);
        DataBind.SetUnityActionValue("HideNoAnimeMailboxUI", OnHideNoAnimeMailboxUI);


        DataBind.SetUnityActionValue("ShowFurnitureTable1", () => _uiAdmin.ShowUIFurniture(FurnitureType.Table1));
        DataBind.SetUnityActionValue("ShowFurnitureTable2", () => _uiAdmin.ShowUIFurniture(FurnitureType.Table2));
        DataBind.SetUnityActionValue("ShowFurnitureTable3", () => _uiAdmin.ShowUIFurniture(FurnitureType.Table3));
        DataBind.SetUnityActionValue("ShowFurnitureTable4", () => _uiAdmin.ShowUIFurniture(FurnitureType.Table4));
        DataBind.SetUnityActionValue("ShowFurnitureTable5", () => _uiAdmin.ShowUIFurniture(FurnitureType.Table5));
        DataBind.SetUnityActionValue("ShowFurnitureCounter", () => _uiAdmin.ShowUIFurniture(FurnitureType.Counter));
        DataBind.SetUnityActionValue("ShowFurnitureRack", () => _uiAdmin.ShowUIFurniture(FurnitureType.Rack));
        DataBind.SetUnityActionValue("ShowFurnitureFrame", () => _uiAdmin.ShowUIFurniture(FurnitureType.Frame));
        DataBind.SetUnityActionValue("ShowFurnitureFlower", () => _uiAdmin.ShowUIFurniture(FurnitureType.Flower));
        DataBind.SetUnityActionValue("ShowFurnitureAcc", () => _uiAdmin.ShowUIFurniture(FurnitureType.Acc));
        DataBind.SetUnityActionValue("ShowFurnitureWallpaper", () => _uiAdmin.ShowUIFurniture(FurnitureType.Wallpaper));

        DataBind.SetUnityActionValue("ShowKitchenBurner1", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Burner1));
        DataBind.SetUnityActionValue("ShowKitchenBurner2", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Burner2));
        DataBind.SetUnityActionValue("ShowKitchenBurner3", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Burner3));
        DataBind.SetUnityActionValue("ShowKitchenBurner4", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Burner4));
        DataBind.SetUnityActionValue("ShowKitchenBurner5", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Burner5));
        DataBind.SetUnityActionValue("ShowKitchenFridge", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Fridge));
        DataBind.SetUnityActionValue("ShowKitchenCabinet", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Cabinet));
        DataBind.SetUnityActionValue("ShowKitchenWindow", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Window));
        DataBind.SetUnityActionValue("ShowKitchenSink", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Sink));
        DataBind.SetUnityActionValue("ShowKitchenKitchenrack", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.Kitchenrack));
        DataBind.SetUnityActionValue("ShowKitchenCookingTools", () => _uiAdmin.ShowUIKitchen(KitchenUtensilType.CookingTools));

        DataBind.SetUnityActionValue("ShowStaffManager", () => _uiAdmin.ShowUIStaff(EquipStaffType.Manager));
        DataBind.SetUnityActionValue("ShowStaffMarketer", () => _uiAdmin.ShowUIStaff(EquipStaffType.Marketer));
        DataBind.SetUnityActionValue("ShowStaffWaiter", () => _uiAdmin.ShowUIStaff(EquipStaffType.Waiter));
        DataBind.SetUnityActionValue("ShowStaffCleaner", () => _uiAdmin.ShowUIStaff(EquipStaffType.Cleaner));
        DataBind.SetUnityActionValue("ShowStaffGuard", () => _uiAdmin.ShowUIStaff(EquipStaffType.Guard));
        DataBind.SetUnityActionValue("ShowStaffChef", () => _uiAdmin.ShowUIStaff(EquipStaffType.Chef));
    }


    private void OnPopUI()
    {
        _uiNav.Pop();
    }

    private void OnShowRecipeTab()
    {
        _uiNav.Push("RestaurantAdminUI");
        _uiAdmin.ShowRecipeTab();
    }

    private void OnShowFurnitureTab()
    {
        _uiNav.Push("RestaurantAdminUI");
        _uiAdmin.ShowFurnitureTab();
    }

    private void OnShowStaffTab()
    {
        _uiNav.Push("RestaurantAdminUI");
        _uiAdmin.ShowStaffTab();
    }

    private void OnShowKitchenTab()
    {
        _uiNav.Push("RestaurantAdminUI");
        _uiAdmin.ShowKitchenTab();
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

    private void OnShowManagementUI()
    {
        _uiNav.Push("UIManagement");
    }

    private void OnHideManagementUI()
    {
        _uiNav.Pop("UIManagement");
    }

    private void OnHideNoAnimeManagementUI()
    {
        _uiNav.PopNoAnime("UIManagement");
    }

    private void OnShowPictorialBookUI()
    {
        _uiNav.Push("UIPictorialBook");
    }

    private void OnHidePictorialBookUI()
    {
        _uiNav.Pop("UIPictorialBook");
    }

    private void OnHideNoAnimePictorialBookUI()
    {
        _uiNav.PopNoAnime("UIPictorialBook");
    }


    private void OnShowGachaUI()
    {
        if (IsGachaOpenOrOpening()
            || !CanShowGachaUI()
            || !_uiNav.ViewsVisibleStateCheck())
            return;

        if (_uiGacha == null || !_uiGacha.PrepareItemMachine())
        {
            DebugLog.LogError("아이템 가챠 머신을 준비할 수 없습니다.");
            return;
        }

        _gachaEntrySource = GachaEntrySource.Main;
        _uiNav.Push("UIGacha");
        RestoreAfterFailedGachaPush();
    }

    private void OnShowStaffGachaUI()
    {
        if (IsGachaOpenOrOpening()
            || !CanShowGachaUI()
            || !_uiNav.ViewsVisibleStateCheck())
            return;

        if (_uiGacha == null || !_uiGacha.PrepareStaffMachine())
        {
            DebugLog.LogError("직원 가챠 머신을 준비할 수 없습니다.");
            return;
        }

        if (_uiAdmin == null || !_uiAdmin.TrySuspendStaffViewForGacha())
        {
            DebugLog.LogError("상점 직원 화면을 가챠 전환용으로 숨길 수 없습니다.");
            return;
        }

        _gachaEntrySource = GachaEntrySource.StaffShop;
        _uiNav.Push("UIGacha");
        RestoreAfterFailedGachaPush();
    }

    private bool IsGachaOpenOrOpening()
    {
        return _gachaEntrySource != GachaEntrySource.None
            || _uiNav.CheckActiveView("UIGacha");
    }

    private void RestoreAfterFailedGachaPush()
    {
        if (!_uiNav.CheckActiveView("UIGacha"))
            CompleteGachaSession();
    }

    private static bool CanShowGachaUI()
    {
        if (UIGacha.IsEntryUnlocked())
            return true;

        PopupManager.Instance.ShowDisplayText("할일 목록 미달성");
        return false;
    }

    private void OnHideGachaUI()
    {
        _uiNav.Pop("UIGacha");
    }

    private void OnHideNoAnimeGachaUI()
    {
        if (!_uiNav.CheckActiveView("UIGacha"))
            return;

        _uiGacha.Hide();
        _uiNav.PopNoAnime("UIGacha");
    }

    private void OnMainNavigationViewHidden()
    {
        if (_gachaEntrySource == GachaEntrySource.None
            || _uiNav.CheckActiveView("UIGacha"))
        {
            return;
        }

        CompleteGachaSession();
    }

    private void CompleteGachaSession()
    {
        GachaEntrySource completedSource = _gachaEntrySource;
        _gachaEntrySource = GachaEntrySource.None;

        if (completedSource == GachaEntrySource.StaffShop
            && (_uiAdmin == null || !_uiAdmin.ResumeStaffViewAfterGacha()))
        {
            DebugLog.LogError("가챠 진입 전 상점 직원 화면을 복원할 수 없습니다.");
        }
    }

    private void OnShowSettingUI()
    {
        _uiNav.Push("UISetting");
    }

    private void OnHideSettingUI()
    {
        _uiNav.Pop("UISetting");
    }

    private void OnHideNoAnimeSettingUI()
    {
        _uiNav.PopNoAnime("UISetting");
    }

    private void OnShowAttendanceUI()
    {
        _uiNav.Push("UIAttendance");
    }

    private void OnHideAttendanceUI()
    {
        _uiNav.Pop("UIAttendance");
    }

    private void OnHideNoAnimeAttendanceUI()
    {
        _uiNav.PopNoAnime("UIAttendance");
    }


    private void OnShowUserReportUI()
    {
        _uiNav.Push("UIUserReport");
    }

    private void OnHideUserReportUI()
    {
        _uiNav.Pop("UIUserReport");
    }

    private void OnHideNoAnimeUserReportUI()
    {
        _uiNav.PopNoAnime("UIUserReport");
    }


    private void OnShowTimeSkipUI()
    {
        _uiNav.Push("UITimeSkip");
    }

    private void OnHideTimeSkipUI()
    {
        _uiNav.Pop("UITimeSkip");
    }

    private void OnHideNoAnimeTimeSkipUI()
    {
        _uiNav.PopNoAnime("UITimeSkip");
    }

    public void OnShowFloorLockUI()
    {
        _uiNav.Push("UIFloorLock");
    }

    private void OnHideFloorLockUI()
    {
        _uiNav.Pop("UIFloorLock");
    }

    private void OnHideNoAnimeFloorLockUI()
    {
        _uiNav.PopNoAnime("UIFloorLock");
    }

    public void OnShowAdUI()
    {
        _uiNav.Push("UIAd");
    }

    private void OnHideAdUI()
    {
        _uiNav.Pop("UIAd");
    }

    private void OnHideNoAnimeAdUI()
    {
        _uiNav.PopNoAnime("UIAd");
    }


    public void OnShowPaymentUI()
    {
        _uiNav.Push("UIPayment");
    }

    private void OnHidePaymentUI()
    {
        _uiNav.Pop("UIPayment");
    }

    private void OnHideNoAnimePaymentUI()
    {
        _uiNav.PopNoAnime("UIPayment");
    }


    public void OnShowFloor3UI()
    {
        _uiNav.Push("UIFloor3");
    }

    private void OnHideFloor3UI()
    {
        _uiNav.Pop("UIFloor3");
    }

    private void OnHideNoAnimeFloor3UI()
    {
        _uiNav.PopNoAnime("UIFloor3");
    }

        public void OnShowMailboxUI()
    {
        _uiNav.Push("UIMailbox");
    }

    private void OnHideMailboxUI()
    {
        _uiNav.Pop("UIMailbox");
    }

    private void OnHideNoAnimeMailboxUI()
    {
        _uiNav.PopNoAnime("UIMailbox");
    }

    private void OnDestroy()
    {
        if (_uiNav != null)
            _uiNav.OnHideUIHandler -= OnMainNavigationViewHidden;

        if (_uiGacha != null)
            _uiGacha.HiddenHandler -= CompleteGachaSession;
    }
}
