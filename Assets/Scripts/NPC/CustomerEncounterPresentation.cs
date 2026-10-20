using System;
using UnityEngine;
using Muks.BackEnd;

public static class CustomerEncounterPresentation
{
    public static void OnCustomerReachedWaitingLine(CustomerVisitMember member)
    {
        if (member == null || !member.IsVisitCurrent || member.Customer == null
            || !ReferenceEquals(member.Customer.VisitMember, member) || member.Party == null || !member.Party.IsActive)
            return;
        NormalCustomerData data = member.Customer.NormalCustomerData;
        if (!IsSupported(data)) return;
        UserInfo.RecordCustomerEncounterAppearance(data.Id);
        TryPresentPending(data);
    }

    public static void OnRestoredCustomerWaiting(NormalCustomer customer)
    {
        if (customer == null || !IsSupported(customer.NormalCustomerData)) return;
        // A restored queue position is not a new appearance; it may only resume an already-pending card.
        TryPresentPending(customer.NormalCustomerData);
    }

    public static void OnCustomerVisitCompleted(CustomerData data)
    {
        if (!IsSupported(data as NormalCustomerData)) return;
        TryPresentPending(data);
    }

    public static bool IsDisplayContextAllowed(bool tutorialCleared, bool tutorialActive,
        bool restaurantSceneReady, RestaurantType restaurantType, int openViewCount,
        bool popupOpen, bool miniGame, bool accountReady, bool shopOpenOrOpening = false)
    {
        return tutorialCleared && !tutorialActive && restaurantSceneReady
            && (restaurantType == RestaurantType.Hall || restaurantType == RestaurantType.Kitchen)
            && openViewCount == 0 && !shopOpenOrOpening && !popupOpen && !miniGame && accountReady;
    }

    private static bool IsSupported(NormalCustomerData data)
        => data != null && CustomerEncounterCatalog.IsSupportedCustomer(data.Id);

    private static void TryPresentPending(CustomerData data)
    {
        if (!IsSupported(data as NormalCustomerData) || data.ThumbnailSprite == null
            || !UserInfo.IsCustomerEncounterStateWritable) return;
        MainScene scene = UnityEngine.Object.FindObjectOfType<MainScene>();
        BackendManager backend = BackendManager.ExistingInstance;
        bool accountReady = backend != null && backend.IsLogin && backend.IsLoaded;
        bool miniGame = GameManager.HasInstance && GameManager.Instance.IsMiniGameStart;
        int openViewCount = scene == null ? -1 : scene.CustomerEncounterOpenViewCount;
        RestaurantType restaurantType = scene == null ? default : scene.CurrentRestaurantType;
        UIMainCanvas mainCanvas = UnityEngine.Object.FindObjectOfType<UIMainCanvas>();
        bool shopOpenOrOpening = mainCanvas != null && mainCanvas.IsRestaurantAdminOpenOrOpening;
        if (!IsDisplayContextAllowed(UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart,
            scene != null, restaurantType, openViewCount, PopupManager.IsShowPopup, miniGame, accountReady,
            shopOpenOrOpening))
            return;
        if (!UserInfo.TryGetPendingCustomerEncounter(data.Id, out CustomerEncounterSaveData record, out string message)) return;

        long contextGeneration = UserInfo.CustomerEncounterContextGeneration;
        try
        {
            PopupManager.Instance.ShowCustomerEncounter(data.Name, message, data.ThumbnailSprite,
                () => UserInfo.MarkCustomerEncounterSeen(data.Id, (CustomerEncounterEventKind)record.EventKind,
                    record.VisitMilestone, contextGeneration));
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
    }
}
