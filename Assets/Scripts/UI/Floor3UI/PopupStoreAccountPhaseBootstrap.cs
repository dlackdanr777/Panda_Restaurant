using System;
using UnityEngine;

/// <summary>Explicit account binding. A disabled configuration, missing owner or Editor review never creates an account authority.</summary>
public static class PopupStoreAccountPhaseBootstrap
{
    public const string ConfigurationResource = "UI/PopupStore/Account/PopupStoreAccountPhaseConfiguration";
    public static Func<UIPopupStore, IPopupStoreAccountPhaseBridge> BridgeFactory { get; set; }

    public static bool TryPrepare(UIPopupStore popup, out string error)
    {
        error = null;
        var configuration = Resources.Load<PopupStoreAccountPhaseConfiguration>(ConfigurationResource);
        if (configuration == null || !configuration.EnableAccountMode) return false;
        return TryPrepare(popup, configuration, out error);
    }

    internal static bool TryPrepare(UIPopupStore popup, PopupStoreAccountPhaseConfiguration configuration, out string error)
    {
        error = null;
        if (popup == null || configuration == null || !configuration.EnableAccountMode || BridgeFactory == null) return false;
        var start = popup.GetComponent<PopupStoreStartController>();
        var clock = popup.GetComponent<PopupStoreBusinessClock>();
        if (start == null || !start.enabled || start.Host?.IsEditorReview == true || start.IsApproved
            || clock == null || clock.State != PopupStoreBusinessState.NotStarted
            || popup.GetComponent<PopupStorePhasePreview>() != null
            || popup.GetComponent<PopupStoreAccountRecoveryPresentation>() != null) return false;
#if UNITY_EDITOR
        try { if (PopupStoreStartController.EditorPhasePreviewEntryPending?.Invoke() == true) return false; }
        catch { return false; }
#endif
        try
        {
            var catalog = Resources.Load<PopupStoreMaterialCatalog>("UI/PopupStore/PopupStoreMaterials");
            if (!configuration.TryFreeze(catalog, out var profile, out var scoring, out error)) return false;
            var bridge = BridgeFactory(popup);
            if (bridge == null || !bridge.IsReady || bridge.AdmissionHost == null || bridge.AdmissionHost.IsEditorReview) return false;
            if (!start.BindHost(bridge.AdmissionHost)) { error = "Account admission binding was rejected."; return false; }
            if (bridge is IPopupStoreAccountRewardRecovery recovery && recovery.Recovery != null)
            {
                var recoveryReward = UnityEngine.Object.Instantiate(configuration.RewardPrefab, popup.transform, false)
                    .GetComponent<PopupStorePhaseRewardPresentation>();
                if (!popup.gameObject.AddComponent<PopupStoreAccountRecoveryPresentation>()
                    .Bind(bridge, recovery, recoveryReward, configuration.RewardTable))
                    throw new InvalidOperationException("The original saved account result could not bind.");
                return true;
            }
            var phase = popup.gameObject.AddComponent<PopupStorePhasePreview>();
            if (!phase.InitializeAccount(profile, scoring, bridge, out error)) throw new InvalidOperationException(error);
            var tutorial = UnityEngine.Object.Instantiate(configuration.TutorialPrefab, popup.transform, false)
                .GetComponent<PopupStorePhasePreviewView>();
            if (!phase.AttachView(tutorial, popup.PhaseCloseButton)) throw new InvalidOperationException("The original tutorial could not bind.");
            clock.UsePhaseTimePlaceholder();
            var polish = tutorial.GetComponent<PopupStorePhasePreviewPolish>();
            if (!popup.GetComponent<PopupStoreExitConfirmation>().BindPhasePolish(phase, polish)
                || !phase.AttachPolish(polish)) throw new InvalidOperationException("The approved phase presentation could not bind.");
            var reward = UnityEngine.Object.Instantiate(configuration.RewardPrefab, popup.transform, false)
                .GetComponent<PopupStorePhaseRewardPresentation>();
            if (!phase.AttachRewardView(reward, configuration.RewardTable)) throw new InvalidOperationException("The approved reward presentation could not bind.");
            return true;
        }
        catch (Exception exception)
        {
            // Keep any partial graph unable to approve a start. Never fall back to ordinary account play.
            start.enabled = false;
            error = "팝업스토어 계정 연결 확인 필요: " + exception.GetType().Name;
            return false;
        }
    }
}
