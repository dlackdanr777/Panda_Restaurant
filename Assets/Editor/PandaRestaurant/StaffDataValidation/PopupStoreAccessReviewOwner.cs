#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

/// <summary>One explicitly armed original memory-Play review owns its isolated admission store.
/// The owner survives popup replacement and never uses a login identity, device clock or account save.</summary>
internal sealed class PopupStoreAccessReviewOwner : IDisposable
{
    internal readonly string AccountId;
    internal readonly DateTimeOffset AuthorityTime = PopupStoreApprovedTestSession.AuthorityTime;
    internal readonly PopupStoreBusinessStartFakeStore Store;
    internal readonly PopupStoreBusinessStartFakeRepository Repository;
    internal readonly PopupStoreAdmissionHost Host;
    internal readonly PopupStoreRewardAccountSession Session;
    internal readonly int DailyLimit;
    private PopupStoreBusinessClock _observedClock;
    private string _lastLoggedState;
    private bool _disposed;

    internal PopupStoreAccessReviewOwner(string label, int? isolatedLimit = null)
    {
        var settings = Resources.Load<PopupStoreBusinessAccessSettings>("PopupStore/PopupStoreBusinessAccessSettings");
        string error = null;
        PopupStoreBusinessAccessSchedule schedule = null;
        if (settings == null || !settings.TryCreateSchedule(out schedule, out error))
            throw new InvalidOperationException("Explicit review access settings are unavailable: " + (settings == null ? "missing asset" : error));
        if (isolatedLimit.HasValue)
        {
            var policy = new PopupStoreBusinessAccessSetting(isolatedLimit.Value, "DEV05E1-isolated-" + isolatedLimit.Value, "2026-11-03");
            if (!PopupStoreBusinessAccessSchedule.TryCreate(policy, null, out schedule, out error)) throw new InvalidOperationException(error);
        }
        if (!schedule.TryResolve("2026-11-03", out var applied, out error)) throw new InvalidOperationException(error);
        DailyLimit = applied.RewardedDailyLimit;
        AccountId = "popup-review-" + label + "-" + Guid.NewGuid().ToString("N");
        Store = new PopupStoreBusinessStartFakeStore(schedule, () => AuthorityTime);
        Store.RegisterAccount(AccountId);
        Session = Store.OpenSession(AccountId);
        Repository = new PopupStoreBusinessStartFakeRepository(Store);
        Host = new PopupStoreAdmissionHost(Repository, Repository, () => Session, new PopupStoreBusinessStartContext(), editorReview: true);
        Host.Changed += LogStateIfChanged;
        Debug.Log("POPUP_STORE_ACCESS_REVIEW_CREATED account=" + AccountId + "; authorityUtc=" + AuthorityTime.ToString("O")
            + "; gameDay=2026-11-03; limit=" + DailyLimit + "; policy=" + applied.Version + "; isolated=True; liveAccount=False");
    }

    internal void Bind(UIPopupStore popup)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PopupStoreAccessReviewOwner));
        if (popup == null) throw new ArgumentNullException(nameof(popup));
        var controller = popup.GetComponent<PopupStoreStartController>();
        if (controller == null) throw new InvalidOperationException("The authored start controller is required.");
        if (controller.Host != null && !ReferenceEquals(controller.Host, Host))
            throw new InvalidOperationException("A review cannot replace an existing admission owner.");
        if (controller.Host == null && !controller.BindHost(Host)) throw new InvalidOperationException(controller.LastError);
        LogStateIfChanged();
    }

    private void LogStateIfChanged()
    {
        if (_disposed || !Application.isPlaying || BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin) return;
        // Replacement changes the View, never the owner. Observe only this host's current local identity.
        var controller = Resources.FindObjectsOfTypeAll<PopupStoreStartController>()
            .FirstOrDefault(candidate => candidate != null && ReferenceEquals(candidate.Host, Host)
                && candidate.LocalSessionId == Host.LocalSessionId && candidate.gameObject.scene.IsValid());
        var clock = controller != null ? controller.GetComponent<PopupStoreBusinessClock>() : null;
        if (_observedClock != clock)
        {
            if (_observedClock != null) _observedClock.StateChanged -= LogStateIfChanged;
            _observedClock = clock;
            if (_observedClock != null) _observedClock.StateChanged += LogStateIfChanged;
        }
        string day = Host.Access?.GameDay;
        if (string.IsNullOrEmpty(day) && !PopupStoreDailyUsage.TryGetDayKey(AuthorityTime, out day)) day = "unavailable";
        var stored = Store.Read(AccountId, day); // Read-only authority snapshot; no extra access query or clock advancement.
        int limit = Host.Access?.DailyLimit ?? DailyLimit;
        string state = "account=" + AccountId + "; localSession=" + (Host.LocalSessionId ?? "none")
            + "; approvalBusiness=" + (Host.BusinessSessionId ?? "none")
            + "; requestId=" + (Host.Transaction?.Request.StartRequestId ?? "none")
            + "; txState=" + (Host.Transaction?.State.ToString() ?? "none")
            + "; gameDay=" + day + "; used=" + stored.Usage + "; remaining=" + Math.Max(0, limit - stored.Usage)
            + "; limit=" + limit + "; LocalStartCount=" + Host.LocalStartCount + "; BindingCount=" + Host.BindingCount
            + "; clock=" + (_observedClock != null ? _observedClock.State.ToString() : "unbound")
            + "; issued=" + Store.IssuedBusinessCount + "; isolated=true; liveAccount=false";
        if (string.Equals(_lastLoggedState, state, StringComparison.Ordinal)) return;
        _lastLoggedState = state;
        Debug.Log("POPUP_STORE_ACCESS_STATE " + state);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Host.Changed -= LogStateIfChanged;
        if (_observedClock != null) _observedClock.StateChanged -= LogStateIfChanged;
        _observedClock = null;
        Host.Dispose();
    }
}
#endif
