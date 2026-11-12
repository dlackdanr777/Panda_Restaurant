#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>Explicit admission preparation for the older local gameplay regressions.
/// Each fixture owns an isolated three-use account, fixed authority time, C2 repository and start context.
/// No runtime bypass, UserInfo identity, SDK call or persistent storage is used.</summary>
internal static class PopupStoreApprovedTestSession
{
    internal static readonly DateTimeOffset AuthorityTime = new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero);

    internal static PopupStoreAdmissionHost Attach(GameObject root, int dailyLimit = 3)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        var controller = root.GetComponent<PopupStoreStartController>();
        if (controller == null) throw new InvalidOperationException("The authored start controller is required for explicit test admission.");
        if (controller.Host != null) return controller.Host;
        var store = new PopupStoreBusinessStartFakeStore(() => AuthorityTime,
            new PopupStoreBusinessAccessPolicy("DEV05E1-regression-v1", dailyLimit));
        string account = "popup-regression-" + Guid.NewGuid().ToString("N");
        store.RegisterAccount(account);
        var session = store.OpenSession(account);
        var repository = new PopupStoreBusinessStartFakeRepository(store);
        var host = new PopupStoreAdmissionHost(repository, repository, () => session, new PopupStoreBusinessStartContext(), editorReview: true);
        if (!controller.BindHost(host)) throw new InvalidOperationException("The explicit test admission host could not be bound: " + controller.LastError);
        controller.RefreshAccess();
        return host;
    }

    internal static bool Start(GameObject root)
    {
        Attach(root);
        root.GetComponent<PopupStoreStartController>().RequestStart();
        var clock = root.GetComponent<PopupStoreBusinessClock>();
        return clock != null && (clock.State == PopupStoreBusinessState.Running || clock.State == PopupStoreBusinessState.Ended);
    }
}
#endif
