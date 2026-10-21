using System;
using UnityEngine;
using Muks.BackEnd;

public static class CustomerEncounterPresentation
{
#if UNITY_EDITOR
    // Read-only observation of the production trigger, before UI eligibility is checked.
    // Tests cannot replace a popup, an admission result or the account context.
    internal static Action<CustomerEncounterSaveData> CandidatePreparedForTesting;
#endif
    public static void OnCustomerReachedWaitingLine(CustomerVisitMember member)
    {
        if (TryPrepareWaitingArrival(member, out CustomerEncounterSaveData record, out string message))
            TryPresentPending(member.Customer.NormalCustomerData, record, message);
    }

    internal static bool TryPrepareWaitingArrival(CustomerVisitMember member,
        out CustomerEncounterSaveData record, out string message)
    {
        record = null;
        message = null;
        if (member == null || !member.TryConsumeEncounterArrival()) return false;
        NormalCustomerData data = member.Customer.NormalCustomerData;
        if (!IsSupported(data)) return false;
        UserInfo.RecordCustomerEncounterAppearance(data.Id);
        return UserInfo.TryGetPendingCustomerFirstAppearance(data.Id, out record, out message);
    }

    public static void OnRestoredCustomerWaiting(CustomerVisitMember member)
    {
        // Restoration neither arms an appearance nor presents Pending. Also suppress
        // later position-only arrival callbacks for this restored runtime member.
        member?.SuppressRestoredEncounterEvents();
    }

    internal static void OnGuidedEntryCompleted(TableUseIdentity use)
    {
        if (TryPrepareGuidedEntry(use, out CustomerData data, out CustomerEncounterSaveData record, out string message))
            TryPresentPending(data, record, message);
    }

    internal static bool TryPrepareGuidedEntry(TableUseIdentity use, out CustomerData data,
        out CustomerEncounterSaveData record, out string message)
    {
        data = null;
        record = null;
        message = null;
        if (use == null || !use.IsPartyCurrent(use.Table)) return false;
        foreach (var member in use.Party.Members)
        {
            // Consume every member even if another card wins or an overlay blocks UI.
            // Returning from that overlay must never retry this admission.
            if (!member.TryConsumeEncounterEntry(use) || record != null) continue;
            NormalCustomerData customer = member.Customer.NormalCustomerData;
            if (IsSupported(customer) && UserInfo.TryGetCustomerEncounterForEntry(customer.Id,
                member.EncounterVisitsBeforeVisit, out record, out message)) data = customer;
        }
        return record != null;
    }

    public static bool IsDisplayContextAllowed(bool tutorialCleared, bool tutorialActive,
        bool restaurantSceneReady, RestaurantType restaurantType, int openViewCount,
        bool popupOpen, bool miniGame, bool accountReady, bool shopOpenOrOpening = false,
        bool navigationSettled = true)
    {
        return tutorialCleared && !tutorialActive && restaurantSceneReady
            && (restaurantType == RestaurantType.Hall || restaurantType == RestaurantType.Kitchen)
            && openViewCount == 0 && navigationSettled && !shopOpenOrOpening && !popupOpen && !miniGame && accountReady;
    }

    private static bool IsSupported(NormalCustomerData data)
        => data != null && CustomerEncounterCatalog.IsSupportedCustomer(data.Id);

    private static void TryPresentPending(CustomerData data, CustomerEncounterSaveData record, string message)
    {
#if UNITY_EDITOR
        CandidatePreparedForTesting?.Invoke(record);
#endif
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
            scene != null, restaurantType, openViewCount, PopupManager.IsCustomerEncounterPresentationBlocked, miniGame, accountReady,
            shopOpenOrOpening, scene != null && scene.CustomerEncounterViewsSettled))
            return;

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
