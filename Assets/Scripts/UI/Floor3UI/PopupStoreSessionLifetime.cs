using System;
using System.Collections.Generic;
using Muks.MobileUI;
using Muks.UI;
using UnityEngine;

/// <summary>Replaces a retired local popup graph with the authored prefab. No account or reward authority.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreSessionLifetime : MonoBehaviour
{
    private const string PrefabResource = "UI/PopupStore/UIPopupStore";
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private PopupStoreGradeSession _grade;
    [SerializeField] private PopupStoreMaterialCatalog _catalog;

    private bool _completedForClose, _preparing;
    public string LastError { get; private set; }
    public string SessionId => Grade != null ? Grade.SessionId : null;
    public bool IsRetired => _completedForClose || (Clock != null && Clock.State == PopupStoreBusinessState.Abandoned);
    public PopupStoreBusinessEndReason? EndReason => Clock != null ? Clock.EndReason : null;
    private PopupStoreBusinessClock Clock => _clock != null ? _clock : (_clock = GetComponent<PopupStoreBusinessClock>());
    private PopupStoreGradeSession Grade => _grade != null ? _grade : (_grade = GetComponent<PopupStoreGradeSession>());

    internal bool MarkCompletedForClose()
    {
        if (Clock == null || Clock.gameObject != gameObject || Clock.State != PopupStoreBusinessState.Ended)
            return false;
        _completedForClose = true;
        return true;
    }

    internal void UndoCompletedClose() => _completedForClose = false;

    internal bool MarkAccountPhaseClosed(PopupStorePhasePreview owner)
    {
        if (owner == null || owner.gameObject != gameObject || !owner.IsAccountMode
            || !owner.AccountClaimConfirmed || owner.Session?.IsTerminal != true) return false;
        _completedForClose = true;
        return true;
    }

    internal bool MarkAccountRecoveryClosed(PopupStoreAccountRecoveryPresentation owner)
    {
        if (owner == null || owner.gameObject != gameObject || !owner.IsConfirmed) return false;
        _completedForClose = true;
        return true;
    }

    /// <summary>Called by the existing map before Push. A continuing view is unchanged; a retired one is replaced once.</summary>
    public bool TryPrepareForEntry(out UIPopupStore ready)
    {
        ready = null;
        var owner = GetComponent<UIPopupStore>();
        if (_preparing || owner == null || owner.IsClosing) return false;
        if (!IsRetired) { ready = owner; LastError = null; return true; }
        var navigation = owner.Navigation as MobileUINavigation;
        if (navigation == null || navigation.IsViewsInactive || gameObject.activeSelf || gameObject.activeInHierarchy
            || owner.VisibleState != VisibleState.Disappeared || !navigation.ViewsVisibleStateCheck())
            return Reject("A retired popup can be replaced only after navigation has hidden it.");

        _preparing = true;
        GameObject candidateRoot = null;
        bool replaced = false;
        try
        {
            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab == null || prefab.activeSelf || prefab.GetComponent<UIPopupStore>() == null
                || prefab.GetComponent<PopupStoreSessionLifetime>() == null)
                return Reject("The inactive authored popup prefab is unavailable.");

            candidateRoot = Instantiate(prefab, transform.parent, false);
            candidateRoot.name = gameObject.name;
            var candidate = candidateRoot.GetComponent<UIPopupStore>();
            var lifetime = candidateRoot.GetComponent<PopupStoreSessionLifetime>();
            var admission = GetComponent<PopupStoreStartController>()?.Host;
            if (candidateRoot.activeSelf || !lifetime.ValidateFreshGraph()
                || string.Equals(SessionId, lifetime.SessionId, StringComparison.Ordinal))
                return Reject("The replacement popup did not contain a fresh validated business graph.");
            var start = candidateRoot.GetComponent<PopupStoreStartController>();
            if (admission != null && !admission.CanAttach(start))
                return Reject("The existing admission owner cannot bind the replacement popup.");
            if (!navigation.TryReplaceInactiveView(UIPopupStore.ViewName, owner, candidate))
                return Reject("The inactive popup registration changed before replacement.");

            // The registry is already committed. Later adapter/UI failures must not destroy its live entry.
            replaced = true;
            ready = candidate;

            // Preserve the scene/review owner's usage and pending-request context across View replacement.
            // No authority exists in Player until an explicit adapter is injected.
            if (admission != null)
                if (!start.BindHost(admission))
                    throw new InvalidOperationException("Admission attachment failed; the new view remains safely blocked.");

            LastError = null;
#if UNITY_EDITOR
            if (Application.isPlaying)
                Debug.Log("POPUP_STORE_SESSION_REPLACED old=" + SessionId + "; reason=" + EndReason
                    + "; next=" + lifetime.SessionId, candidate);
#endif
            // Registry and caller's output now own the new graph. Old callbacks retain only old graph references.
            DestroyGraph(gameObject);
            return true;
        }
        catch (Exception exception)
        {
            // Registration is the commit boundary. Cleanup/logging failure must not leave the map pointing at the old view.
            if (replaced)
            {
                ready = candidateRoot != null ? candidateRoot.GetComponent<UIPopupStore>() : ready;
                Debug.LogWarning("PopupStoreSessionLifetime: previous graph cleanup was interrupted: " + exception.GetType().Name);
                return true;
            }
            return Reject("Could not prepare the next local popup: " + exception.GetType().Name);
        }
        finally
        {
            if (!replaced && candidateRoot != null) DestroyGraph(candidateRoot);
            _preparing = false;
        }
    }

    private bool ValidateFreshGraph()
    {
        if (Clock == null || Clock.gameObject != gameObject || Clock.State != PopupStoreBusinessState.NotStarted
            || Grade == null || Grade.gameObject != gameObject || Grade.IsFinalized || _completedForClose
            || _catalog == null || !_catalog.TryValidate(out _) || GetComponent<Canvas>() == null)
            return false;
        var counters = GetComponentsInChildren<PopupStoreSalesCounter>(true);
        if (counters.Length != 1 || counters[0].GetComponentInParent<UIPopupStore>(true) != GetComponent<UIPopupStore>())
            return false;
        var counter = counters[0];
        var orders = GetComponent<PopupStoreOrderDisplay>();
        var packing = GetComponent<PopupStorePacking>();
        var score = GetComponent<PopupStoreScore>();
        var rewards = GetComponent<PopupStoreRewardSession>();
        var exit = GetComponent<PopupStoreExitConfirmation>();
        if (counter == null || orders == null || packing == null || score == null || rewards == null || exit == null
            || GetComponent<PopupStoreCustomerEntrance>() == null || GetComponent<PopupStoreCustomerPatience>() == null
            || GetComponent<PopupStorePackingPresentation>() == null || GetComponent<PopupStoreOrderProgress>() == null
            || GetComponent<PopupStoreResultPanel>() == null || GetComponent<PopupStoreRewardBox>() == null
            || GetComponent<PopupStoreRewardRevealPreview>() == null || GetComponent<PopupStorePauseController>() == null
            || GetComponent<PopupStoreStartController>() == null || GetComponent<PopupStoreStartPanel>() == null
            || !GetComponent<PopupStoreStartPanel>().Refresh())
            return false;
        // These existing validators bind only the inactive candidate. They cannot start a clock or commit a sale.
        if (!counter.Refresh() || !orders.Refresh() || !packing.Refresh() || !score.Refresh()
            || !Grade.Refresh() || !rewards.Refresh() || !exit.Refresh()) return false;
        var lists = GetComponentsInChildren<PopupStoreMaterialList>(true);
        if (lists.Length != 3) return false;
        int materialCount = 0;
        var materials = new HashSet<PopupStoreMaterialEntry>();
        var kinds = new HashSet<PopupStoreMaterialKind>();
        foreach (var list in lists)
        {
            if (list.GetComponentInParent<UIPopupStore>(true) != GetComponent<UIPopupStore>() || !list.Refresh()) return false;
            materialCount += list.MaterialCount;
            var items = list.GetComponentsInChildren<PopupStoreMaterialItem>(true);
            if (items.Length != list.MaterialCount || items.Length == 0 || items[0].Material == null) return false;
            var kind = items[0].Material.Kind;
            if (!kinds.Add(kind) || !_catalog.TryGetMaterials(kind, out var approved, out _)
                || approved.Length != items.Length) return false;
            foreach (var item in items)
                if (item.Owner != list || item.Material == null || item.Material.Kind != kind
                    || Array.IndexOf(approved, item.Material) < 0 || !materials.Add(item.Material)) return false;
        }
        return materialCount == 38 && materials.Count == 38 && kinds.Count == 3 && counter.Count == 0 && packing.SuccessfulPackCount == 0
            && packing.CommittedReceipts.Count == 0 && score.TotalScore == 0 && !Grade.IsFinalized
            && !rewards.IsResolved && rewards.RandomDrawCount == 0 && !IsRetired;
    }

    private bool Reject(string error) { LastError = error; return false; }

    private static void DestroyGraph(GameObject graph)
    {
        if (graph == null) return;
        graph.SetActive(false);
        if (Application.isPlaying) Destroy(graph);
        else DestroyImmediate(graph);
    }
}
