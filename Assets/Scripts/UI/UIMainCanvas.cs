using Muks.DataBind;
using Muks.MobileUI;
using UnityEngine;
using System;
using System.Collections;
using Muks.BackEnd;

[RequireComponent(typeof(MobileUINavigation))]
public class UIMainCanvas : MonoBehaviour
{
    private enum GachaEntrySource
    {
        None,
        Main,
        StaffShop,
        QuestStaff
    }

    private MobileUINavigation _uiNav;

    [SerializeField] private UIRestaurantAdmin _uiAdmin;
    [SerializeField] private UIGacha _uiGacha;

    private GachaEntrySource _gachaEntrySource;
    private Coroutine _shopShortcut;
    private BackendManager _questShopOwner;
    private QuestStaffOffer _questShopOffer;
    private QuestOwnedStaffDetail _questOwnedStaffDetail;
    private int _questShopSelectionRevision;

#if UNITY_EDITOR
    private bool _editorOfflineNavigation;

    /// <summary>Bind only the copied shop entry and gacha exit before an isolated scene is activated.</summary>
    public void ConfigureEditorOfflineNavigation(UIGacha view, UIRestaurantAdmin shop,
        UnityEngine.UI.Button enter, UnityEngine.UI.Button exit)
    {
        if (gameObject.activeInHierarchy || view == null || shop == null || enter == null || exit == null ||
            view.gameObject.activeInHierarchy || shop.gameObject.activeInHierarchy)
            throw new System.InvalidOperationException("Offline navigation requires inactive copied views and their buttons.");
        _uiNav = GetComponent<MobileUINavigation>();
        if (_uiNav == null)
            throw new System.InvalidOperationException("The copied main canvas has no native mobile navigation.");
        _editorOfflineNavigation = true;
        _uiGacha = view;
        _uiAdmin = shop;
        _gachaEntrySource = GachaEntrySource.None;
        ClearQuestShopContext();
        enter.onClick.RemoveListener(OnShowStaffGachaUI);
        enter.onClick.AddListener(OnShowStaffGachaUI);
        exit.onClick.RemoveListener(OnHideGachaUI);
        exit.onClick.AddListener(OnHideGachaUI);
    }
#endif

    private void Awake()
    {
        _uiNav = GetComponent<MobileUINavigation>();
        _uiNav.OnHideUIHandler += OnMainNavigationViewHidden;

        if (_uiGacha != null)
            _uiGacha.HiddenHandler += CompleteGachaSession;
    }



    void Start()
    {
#if UNITY_EDITOR
        if (_editorOfflineNavigation) return;
#endif
        QuestProgressGuidance.Install(this);
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
        DataBind.SetUnityActionValue("ShowQuestStaffGachaUI", OnShowQuestStaffGachaUI);
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


        DataBind.SetUnityActionValue("ShowFurnitureTable1", () => ShowFurnitureShortcut(FurnitureType.Table1));
        DataBind.SetUnityActionValue("ShowFurnitureTable2", () => ShowFurnitureShortcut(FurnitureType.Table2));
        DataBind.SetUnityActionValue("ShowFurnitureTable3", () => ShowFurnitureShortcut(FurnitureType.Table3));
        DataBind.SetUnityActionValue("ShowFurnitureTable4", () => ShowFurnitureShortcut(FurnitureType.Table4));
        DataBind.SetUnityActionValue("ShowFurnitureTable5", () => ShowFurnitureShortcut(FurnitureType.Table5));
        DataBind.SetUnityActionValue("ShowFurnitureCounter", () => ShowFurnitureShortcut(FurnitureType.Counter));
        DataBind.SetUnityActionValue("ShowFurnitureRack", () => ShowFurnitureShortcut(FurnitureType.Rack));
        DataBind.SetUnityActionValue("ShowFurnitureFrame", () => ShowFurnitureShortcut(FurnitureType.Frame));
        DataBind.SetUnityActionValue("ShowFurnitureFlower", () => ShowFurnitureShortcut(FurnitureType.Flower));
        DataBind.SetUnityActionValue("ShowFurnitureAcc", () => ShowFurnitureShortcut(FurnitureType.Acc));
        DataBind.SetUnityActionValue("ShowFurnitureWallpaper", () => ShowFurnitureShortcut(FurnitureType.Wallpaper));

        DataBind.SetUnityActionValue("ShowKitchenBurner1", () => ShowKitchenShortcut(KitchenUtensilType.Burner1));
        DataBind.SetUnityActionValue("ShowKitchenBurner2", () => ShowKitchenShortcut(KitchenUtensilType.Burner2));
        DataBind.SetUnityActionValue("ShowKitchenBurner3", () => ShowKitchenShortcut(KitchenUtensilType.Burner3));
        DataBind.SetUnityActionValue("ShowKitchenBurner4", () => ShowKitchenShortcut(KitchenUtensilType.Burner4));
        DataBind.SetUnityActionValue("ShowKitchenBurner5", () => ShowKitchenShortcut(KitchenUtensilType.Burner5));
        DataBind.SetUnityActionValue("ShowKitchenFridge", () => ShowKitchenShortcut(KitchenUtensilType.Fridge));
        DataBind.SetUnityActionValue("ShowKitchenCabinet", () => ShowKitchenShortcut(KitchenUtensilType.Cabinet));
        DataBind.SetUnityActionValue("ShowKitchenWindow", () => ShowKitchenShortcut(KitchenUtensilType.Window));
        DataBind.SetUnityActionValue("ShowKitchenSink", () => ShowKitchenShortcut(KitchenUtensilType.Sink));
        DataBind.SetUnityActionValue("ShowKitchenKitchenrack", () => ShowKitchenShortcut(KitchenUtensilType.Kitchenrack));
        DataBind.SetUnityActionValue("ShowKitchenCookingTools", () => ShowKitchenShortcut(KitchenUtensilType.CookingTools));

        DataBind.SetUnityActionValue("ShowStaffManager", () => ShowStaffShortcut(EquipStaffType.Manager));
        DataBind.SetUnityActionValue("ShowStaffMarketer", () => ShowStaffShortcut(EquipStaffType.Marketer));
        DataBind.SetUnityActionValue("ShowStaffWaiter", () => ShowStaffShortcut(EquipStaffType.Waiter));
        DataBind.SetUnityActionValue("ShowStaffCleaner", () => ShowStaffShortcut(EquipStaffType.Cleaner));
        DataBind.SetUnityActionValue("ShowStaffGuard", () => ShowStaffShortcut(EquipStaffType.Guard));
        DataBind.SetUnityActionValue("ShowStaffChef", () => ShowStaffShortcut(EquipStaffType.Chef));
    }


    private void OnPopUI()
    {
        _uiNav.Pop();
    }

    private void OnShowRecipeTab()
    {
        var quest = CaptureProductShortcut(ChallengeType.TYPE03);
        var stage = UserInfo.CurrentStage;
        string id = GetQuestProductId(quest);
        OpenShopShortcut(() =>
        {
            if (quest == null) _uiAdmin.ShowRecipeTab();
            else if (IsProductShortcutCurrent(quest, stage, id)) _uiAdmin.ShowQuestRecipe(id);
        });
    }

    private void OnShowFurnitureTab()
    {
        OpenShopShortcut(() => _uiAdmin.ShowFurnitureTab());
    }

    private void OnShowStaffTab()
    {
        OpenShopShortcut(() => _uiAdmin.ShowStaffTab());
    }

    private void OnShowKitchenTab()
    {
        OpenShopShortcut(() => _uiAdmin.ShowKitchenTab());
    }


    private void OnShowRestaurantAdminUI()
    {
        OpenShopShortcut(null);
    }

    private void OnHideRestaurantAdminUI()
    {
        ClearQuestShopContext();
        _uiNav.Pop("RestaurantAdminUI");
    }

    private void ShowFurnitureShortcut(FurnitureType type)
    {
        var quest = CaptureProductShortcut(ChallengeType.TYPE01);
        var stage = UserInfo.CurrentStage;
        string id = GetQuestProductId(quest);
        OpenShopShortcut(() =>
        {
            if (quest == null) _uiAdmin.ShowUIFurniture(type);
            else if (IsProductShortcutCurrent(quest, stage, id)) _uiAdmin.ShowQuestFurniture(id);
        });
    }

    private void ShowKitchenShortcut(KitchenUtensilType type)
    {
        var quest = CaptureProductShortcut(ChallengeType.TYPE02);
        var stage = UserInfo.CurrentStage;
        string id = GetQuestProductId(quest);
        OpenShopShortcut(() =>
        {
            if (quest == null) _uiAdmin.ShowUIKitchen(type);
            else if (IsProductShortcutCurrent(quest, stage, id)) _uiAdmin.ShowQuestKitchen(id);
        });
    }

    private ChallengeData CaptureProductShortcut(ChallengeType type)
    {
        // Shared category bindings also serve ordinary shop/daily UI. Only a main-quest
        // button may borrow the current main goal; capture it before the panel closes.
        if (_uiNav == null || !_uiNav.CheckActiveView("UIMainChallenge")) return null;
        var quest = ChallengeManager.Instance.GetCurrentMainChallengeData();
        return quest != null && quest.Type == type ? quest : null;
    }

    internal static string GetQuestProductId(ChallengeData quest)
    {
        if (quest is Type01ChallengeData furniture && furniture.NeedFurnitureIds?.Length == 1)
            return furniture.NeedFurnitureIds[0];
        if (quest is Type02ChallengeData kitchen && kitchen.NeedKitchenUtensilId?.Length == 1)
            return kitchen.NeedKitchenUtensilId[0];
        return (quest as Type03ChallengeData)?.BuyRecipeId;
    }

    private static bool IsProductShortcutCurrent(ChallengeData quest, EStage stage, string id)
        => quest != null && !string.IsNullOrEmpty(id) && UserInfo.CurrentStage == stage
            && ReferenceEquals(ChallengeManager.Instance.GetCurrentMainChallengeData(), quest)
            && GetQuestProductId(quest) == id;

    private void ShowStaffShortcut(EquipStaffType type) => OpenShopShortcut(() => _uiAdmin.ShowUIStaff(type));

    private bool OpenShopShortcut(Action openDetail, bool preserveQuestContext = false)
    {
        if (_shopShortcut != null || _uiNav == null || _uiAdmin == null || !_uiNav.ViewsVisibleStateCheck()
            || IsGachaOpenOrOpening()) return false;
        if (!preserveQuestContext) ClearQuestShopContext();
        _shopShortcut = StartCoroutine(OpenShopShortcutRoutine(openDetail));
        return true;
    }

    private IEnumerator OpenShopShortcutRoutine(Action openDetail)
    {
        // Close only the originating quest panel. Do not clear unrelated UI or skip the shop's Show lifecycle.
        if (_uiNav.CheckActiveView("UIMainChallenge")) _uiNav.Pop("UIMainChallenge");
        yield return null;
        while (!_uiNav.ViewsVisibleStateCheck()) yield return null;
        if (!_uiNav.CheckActiveView("RestaurantAdminUI")) _uiNav.Push("RestaurantAdminUI");
        if (!_uiNav.CheckActiveView("RestaurantAdminUI")) { _shopShortcut = null; yield break; }
        while (_uiNav.CheckActiveView("RestaurantAdminUI") &&
            (!_uiNav.ViewsVisibleStateCheck() || !_uiAdmin.IsReadyForDetail)) yield return null;
        _shopShortcut = null;
        if (_uiNav.CheckActiveView("RestaurantAdminUI") && _uiAdmin.IsReadyForDetail) openDetail?.Invoke();
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

    private void OnShowQuestStaffGachaUI()
    {
        // Retain the existing shortcut binding, but enter the product shop detail instead of the machine.
        if (!TryShowQuestStaffShop())
            PopupManager.Instance.ShowDisplayText("현재 직원 안내를 시작할 수 없습니다. 진행 상태를 확인해 주세요.");
    }

    private bool TryShowQuestStaffShop()
    {
        if (UserInfo.IsTutorialStart || !UserInfo.IsFirstTutorialClear || IsGachaOpenOrOpening() ||
            _uiNav == null || !_uiNav.ViewsVisibleStateCheck() || _uiAdmin == null || _shopShortcut != null)
            return false;
        var owner = BackendManager.Instance;
        if (!TryGetQuestShopEntry(owner, out var offer, out var retained))
        {
            if (!owner.TryGetCurrentQuestOwnedStaffDetail(out var owned)) return false;
            return OpenShopShortcut(() =>
            {
                if (!IsOwnedQuestStaffDetailCurrent(owner, owned) || !_uiAdmin.ShowQuestStaff(owned.StaffData)) return;
                _questOwnedStaffDetail = owned;
            });
        }
        _questShopOwner = owner;
        _questShopOffer = offer;
        _questShopSelectionRevision = -1;
        return OpenShopShortcut(() =>
        {
            if (!IsQuestShopOfferCurrent(owner, offer) || !_uiAdmin.ShowQuestStaff(offer.StaffData))
            {
                ClearQuestShopContext();
                return;
            }
            _questShopSelectionRevision = _uiAdmin.StaffSelectionRevision;
            // This is a continuation of the user's explicit shortcut, not polling or
            // a completion callback. Owned staff have no purchase button to reopen a result.
            if (retained != null && !StaffGachaPurchaseDisplay.IsAcknowledged(retained))
                StartCoroutine(ResumeQuestStaffRequestWhenReady(owner, retained));
        }, true);
    }

    private static bool TryGetQuestShopEntry(BackendManager owner, out QuestStaffOffer entry,
        out QuestStaffGrantExecution retained)
    {
        entry = null;
        retained = null;
        if (owner == null) return false;
        return owner.TryGetRetainedQuestStaffEntry(out entry, out retained)
            || owner.TryGetCurrentQuestStaffOffer(out entry, out _);
    }

    private static bool IsOwnedQuestStaffDetailCurrent(BackendManager owner, QuestOwnedStaffDetail expected)
        => owner != null && expected != null && owner.IsCurrentGameDataQuery(expected.Query)
            && owner.TryGetCurrentQuestOwnedStaffDetail(out var live) && ReferenceEquals(live.Query, expected.Query)
            && live.QuestId == expected.QuestId && live.StaffData.Id == expected.StaffData.Id;

    private IEnumerator ResumeQuestStaffRequestWhenReady(BackendManager owner, QuestStaffGrantExecution retained)
    {
        while (_uiNav != null && _uiNav.CheckActiveView("RestaurantAdminUI")
            && _uiNav.CheckActiveView("UIStaff") && !_uiNav.ViewsVisibleStateCheck()) yield return null;
        if (_uiNav == null || !_uiNav.CheckActiveView("RestaurantAdminUI") || !_uiNav.CheckActiveView("UIStaff")
            || !ReferenceEquals(_questShopOwner, owner) || !IsQuestShopEntryCurrent()
            || !owner.TryGetRetainedQuestStaffEntry(out _, out var current) || !ReferenceEquals(current, retained)
            || StaffGachaPurchaseDisplay.IsAcknowledged(retained)) yield break;
        OnShowStaffGachaUI(); // Rebind the same request; the actual claim button still checks fresh eligibility.
    }

    private static bool IsQuestShopOfferCurrent(BackendManager owner, QuestStaffOffer expected)
        => owner != null && expected != null && owner.IsCurrentGameDataQuery(expected.Query)
            && TryGetQuestShopEntry(owner, out var live, out _)
            && ReferenceEquals(live.Query, expected.Query) && live.QuestId == expected.QuestId
            && live.StaffId == expected.StaffId;

    private bool IsQuestShopEntryCurrent()
        => _uiAdmin != null && _questShopSelectionRevision == _uiAdmin.StaffSelectionRevision
            && _uiAdmin.SelectedStaff != null && _uiAdmin.SelectedStaff.Id == _questShopOffer?.StaffId
            && IsQuestShopOfferCurrent(_questShopOwner, _questShopOffer);

    private void ClearQuestShopContext()
    {
        _questOwnedStaffDetail = null;
        _questShopOwner = null;
        _questShopOffer = null;
        _questShopSelectionRevision = -1;
    }

    private void OnShowStaffGachaUI()
    {
        // An owned-only detail never creates an offer or falls through into a paid draw.
        if (_questOwnedStaffDetail != null) return;
        if (_questShopOffer != null)
        {
            if (IsGachaOpenOrOpening() || _uiNav == null || !_uiNav.ViewsVisibleStateCheck()
                || !IsQuestShopEntryCurrent() || _uiGacha == null
                || !_uiGacha.PrepareQuestStaffMachine(_questShopOwner, _questShopOffer.QuestId, IsQuestShopEntryCurrent)
                || !_uiAdmin.TrySuspendStaffViewForGacha())
            {
                PopupManager.Instance.ShowDisplayText("직원 안내가 변경되었습니다. 할일 목록의 바로가기를 다시 선택해 주세요.");
                return; // An expired free entry never becomes a paid purchase entry.
            }
            _gachaEntrySource = GachaEntrySource.QuestStaff;
            _uiNav.Push("UIGacha");
            RestoreAfterFailedGachaPush();
            return;
        }
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

        if ((completedSource == GachaEntrySource.StaffShop || completedSource == GachaEntrySource.QuestStaff)
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
