using System;
using System.Collections.Generic;
using System.Linq;
using Muks.PathFinding.AStar;
using UnityEngine;

public partial class CustomerController
{
    private sealed class WaitingEntry
    {
        internal CustomerVisitParty Party;
        internal bool ClockStarted;
        internal double WaitStartedAt;
        internal float WaitLimit;
        internal readonly HashSet<CustomerVisitMember> Ready = new HashSet<CustomerVisitMember>();
        internal bool IsReady => Party != null && Party.IsActive && Ready.Count == Party.Members.Count;
    }
    private readonly List<WaitingEntry> _waitingTeams = new List<WaitingEntry>();
    private static CustomerController _queueOwner;
    private bool _restoringWaiting;
    private WaitingPartySaveData _restoredSnapshot;
    public int Count => _waitingTeams.Count; // Includes reservations still being constructed.
    public int WaitCount => _waitingTeams.ToArray().Count(e => e.IsReady);
    public int WaitingPersonCount => _waitingTeams.Sum(e => e.Party?.Members.Count ?? 0);
    public IReadOnlyList<CustomerVisitParty> WaitingParties => _waitingTeams.Where(e => e.Party != null).Select(e => e.Party).ToArray();
    public IReadOnlyList<NormalCustomer> WaitingCustomers => WaitingParties.SelectMany(p => p.Members).Select(m => m.Customer).ToArray();
    public static bool IsMaxCount => _queueOwner != null && _queueOwner.Count >= GameManager.Instance.MaxWaitCustomerCount;
    public string WaitingLoadDiagnostic { get; private set; }

    [Header("Waiting party layout")]
    [Tooltip("World distance between teams. Zero retains lineSpacingGrid * node size.")]
    [Min(0)] [SerializeField] private float _teamSpacing = 4.5f;
    [SerializeField] private Vector2 _lineDirection = Vector2.right;
    [SerializeField] private Vector2 _memberLineOffset = new Vector2(2f, 0);

    public CustomerVisitParty FirstReadyParty
    {
        get
        {
            var first = _waitingTeams.Count > 0 ? _waitingTeams[0] : null;
            return first != null && first.IsReady && _waitingTeams.Contains(first) ? first.Party : null;
        }
    }
    public bool IsEmpty() => FirstReadyParty == null;
    // Compatibility for floor preference selection only. Joint guide uses the whole party.
    public NormalCustomer GetFirstCustomer() => FirstReadyParty?.Members[0].Customer;

    public Vector3 GetWaitingPosition(int teamIndex, int memberIndex)
    {
        float spacing = _teamSpacing > 0 ? _teamSpacing : _lineSpacingGrid * AStar.Instance.NodeSize;
        Vector2 direction = _lineDirection.sqrMagnitude > 0 ? _lineDirection.normalized : Vector2.right;
        float distance = spacing * teamIndex;
        if (_teamSpacing <= 0)
            for (int i = 0; i < teamIndex && i < _waitingTeams.Count; i++)
                if (_waitingTeams[i].Party?.Members.Count > 1) distance += Mathf.Abs(Vector2.Dot(_memberLineOffset, direction));
        return _startLine.position + (Vector3)(direction * distance + _memberLineOffset * memberIndex);
    }

    // Production implementation used by natural singleton calls, restore and the safe
    // fixed-ID editor entry. No companion selection, probability, reward or timeout policy.
    public bool TrySpawnWaitingParty(IReadOnlyList<string> memberIds, out CustomerVisitParty party)
        => SpawnWaitingParty(memberIds, false, out party);

    private bool SpawnWaitingParty(IReadOnlyList<string> ids, bool restored, out CustomerVisitParty party, bool notify = true)
    {
        party = null;
        if (_visitExecution == null) { LastVisitDiagnostic = "VisitExecutionEnded"; return false; }
        var waitingSave = UserInfo.GetWaitingPartySave(UserInfo.CurrentStage);
        if (!_restoringWaiting && !waitingSave.CanSave)
        { LastVisitDiagnostic = "WaitingSaveHeld: " + waitingSave.Reason + "; pending=" + waitingSave.NeedsValidation; return false; }
        if (Count >= GameManager.Instance.MaxWaitCustomerCount)
        { LastVisitDiagnostic = "WaitingTeamLimitReached: " + Count; return false; }
        if (ids == null || ids.Count < 1 || ids.Count > 2)
        { LastVisitDiagnostic = "InvalidWaitingMemberCount"; return false; }
        var entry = new WaitingEntry();
        _waitingTeams.Add(entry); // Reserve before the first pool allocation.
        var customers = new List<NormalCustomer>();
        try
        {
            var data = new List<NormalCustomerData>();
            foreach (string id in ids)
            {
                var normal = CustomerDataManager.Instance.GetCustomerData(id) as NormalCustomerData;
                if (normal == null) throw new InvalidOperationException("InvalidNormalCustomerId");
                data.Add(normal);
            }
            for (int i = 0; i < data.Count; i++)
            {
                var customer = ObjectPoolManager.Instance.SpawnNormalCustomer(GameManager.Instance.OutDoorPos + _memberLineOffset * i, Quaternion.identity);
                if (customer == null) throw new InvalidOperationException("CustomerPoolAllocationFailed");
                customers.Add(customer);
                customer.SetData(data[i], this, _tableManager);
            }
            if (!CustomerVisitParty.TryCreate(this, customers, out party, out string reason)) throw new InvalidOperationException(reason);
            entry.Party = party;
            // Singleton timing retains its original entrance-time start.
            if (customers.Count == 1) customers[0].StartWaiting();
            if (restored)
            {
                int slot = _waitingTeams.IndexOf(entry);
                for (int i = 0; i < party.Members.Count; i++)
                {
                    party.Members[i].Customer.transform.position = GetWaitingPosition(slot, i);
                    entry.Ready.Add(party.Members[i]);
                    CustomerEncounterPresentation.OnRestoredCustomerWaiting(party.Members[i].Customer);
                }
            }
            else SortCustomerLine();
            StartPartyWaitingClock(entry);
            LastVisitDiagnostic = "WaitingPartyCreated";
            if (!_restoringWaiting && notify) NotifyWaitingChanged();
            return true;
        }
        catch (Exception error)
        {
            _waitingTeams.Remove(entry);
            party?.End(); party = null;
            foreach (var customer in customers)
                if (customer != null && customer.gameObject.activeSelf) ObjectPoolManager.Instance.DespawnNormalCustomer(customer);
            LastVisitDiagnostic = "WaitingPartyCreationFailed: " + error.Message;
            return false;
        }
    }

    internal bool EnqueueVisit(CustomerVisitParty party, bool ready)
    {
        if (party == null || !party.IsActive || party.WaitingDeparture || party.CurrentUse != null || !ReferenceEquals(party.Owner, this)
            || _waitingTeams.Any(e => ReferenceEquals(e.Party, party)) || Count >= GameManager.Instance.MaxWaitCustomerCount) return false;
        var entry = new WaitingEntry { Party = party };
        if (ready) foreach (var member in party.Members) entry.Ready.Add(member);
        _waitingTeams.Add(entry);
        StartPartyWaitingClock(entry);
        return true;
    }

    private void NotifyWaitingChanged()
    {
        if (_restoringWaiting) return;
        OnChangeCustomerHandler?.Invoke();
    }

    private void SortCustomerLine()
    {
        int layer = WaitingPersonCount;
        for (int slot = 0; slot < _waitingTeams.Count; slot++)
        {
            var entry = _waitingTeams[slot];
            if (entry.Party == null || !entry.Party.IsActive) continue;
            for (int i = 0; i < entry.Party.Members.Count; i++)
            {
                var member = entry.Party.Members[i]; var customer = member.Customer;
                Vector2 target = GetWaitingPosition(slot, i);
                if (!customer.HasMoved || customer.TargetPos != target)
                {
                    entry.Ready.Remove(member);
                    customer.Move(target, -1, () =>
                    {
                        if (!_waitingTeams.Contains(entry) || !member.Party.IsActive
                            || !ReferenceEquals(customer.VisitMember, member) || member.Party.CurrentUse != null
                            || (member.Party.Members.Count > 1 && !customer.ConfirmReachedMoveTarget())) return;
                        if (entry.Ready.Add(member))
                        {
                            CustomerEncounterPresentation.OnCustomerReachedWaitingLine(member);
                            StartPartyWaitingClock(entry); _tableManager.UpdateTable(); NotifyWaitingChanged();
                        }
                    });
                }
                customer.SetLayer("WaitCustomer", layer--);
            }
        }
    }

    // A caller holding a party identity cannot remove a later visit on the same pool object.
    public bool CancelWaitingParty(CustomerVisitParty party)
    {
        var entry = _waitingTeams.FirstOrDefault(e => ReferenceEquals(e.Party, party));
        if (entry == null) return false;
        _waitingTeams.Remove(entry);
        party.End();
        ReturnPartyMembers(party);
        SortCustomerLine(); NotifyWaitingChanged(); _tableManager.UpdateTable();
        return true;
    }

    public void RemoveCustomerFromLineQueue(NormalCustomer customer)
    {
        var party = customer == null ? null : customer.VisitParty;
        var entry = _waitingTeams.FirstOrDefault(e => ReferenceEquals(e.Party, party));
        if (entry == null) return; // Includes seated food timeout: TableManager owns it.
        if (party.Members.Count > 1) { CancelWaitingParty(party); return; }
        _waitingTeams.Remove(entry); // Singleton timeout retains its existing exit movement.
        SortCustomerLine(); NotifyWaitingChanged(); _tableManager.UpdateTable();
    }

    private void OnWaitingPartyEnded(CustomerVisitParty party, bool preserveWaitingSave)
    {
        var entry = _waitingTeams.FirstOrDefault(e => ReferenceEquals(e.Party, party));
        if (entry == null)
        {
            if (party.WaitingDeparture) ReturnPartyMembers(party);
            return;
        }
        _waitingTeams.Remove(entry);
        ReturnPartyMembers(party);
        if (!preserveWaitingSave && _visitExecution != null && !_restoringWaiting) { SortCustomerLine(); NotifyWaitingChanged(); }
    }

    private static void ReturnPartyMembers(CustomerVisitParty party)
    {
        foreach (var member in party.Members)
            if (!party.HasDeparted(member) && member.Customer != null && ReferenceEquals(member.Customer.VisitIdentity, member.VisitIdentity))
            {
                member.Customer.StopMove(); member.Customer.StopWaiting();
                if (member.Customer.gameObject.activeSelf) ObjectPoolManager.Instance.DespawnNormalCustomer(member.Customer);
            }
    }

    internal bool TakeReadyParty(CustomerVisitParty party, TableUseIdentity use)
    {
        if (!ReferenceEquals(FirstReadyParty, party) || !use.IsPartyCurrent(use.Table)) return false;
        _waitingTeams.RemoveAt(0);
        SortCustomerLine();
        return true;
    }

    internal void PublishGuide() { NotifyWaitingChanged(); OnGuideCustomerHandler?.Invoke(); }

    public bool GuideCustomer(Vector3 targetPos, int moveEndDir = 0, Action onCompleted = null)
    {
        var party = FirstReadyParty;
        if (party == null || party.Members.Count != 1 || party.CurrentUse != null)
        { LastVisitDiagnostic = "UnseatedSingleVisitRequired"; return false; }
        _waitingTeams.RemoveAt(0);
        party.Members[0].Customer.Move(targetPos, moveEndDir, onCompleted);
        SortCustomerLine(); NotifyWaitingChanged(); OnGuideCustomerHandler?.Invoke();
        return true;
    }

    private void UpdateWaitingCustomerSave()
    {
        if (_restoringWaiting || _waitingTeams.Any(e => e.Party == null)) return;
        UserInfo.SetWaitingParties(UserInfo.CurrentStage, WaitingParties.Select(p => p.Members.Select(m => m.Customer.NormalCustomerData.Id).ToArray()).ToArray());
    }

    private void LoadWaitingCustomers()
    {
        var saved = UserInfo.GetWaitingPartySave(UserInfo.CurrentStage);
        if (saved.IsProtected) { WaitingLoadDiagnostic = saved.Reason; return; }
        if (Count != 0)
        {
            if (!ReferenceEquals(saved, _restoredSnapshot)) UserInfo.ProtectWaitingParties(UserInfo.CurrentStage, "QueueAlreadyLoaded");
            WaitingLoadDiagnostic = "QueueAlreadyLoaded"; return;
        }
        try
        {
            if (!saved.ValidateForRestore(GameManager.Instance.MaxWaitCustomerCount,
                id => CustomerDataManager.Instance.GetCustomerData(id) is NormalCustomerData, out string validationError))
                throw new InvalidOperationException(validationError);
            _restoringWaiting = true;
            foreach (var team in saved.Teams)
                if (!SpawnWaitingParty(team, true, out _)) throw new InvalidOperationException(LastVisitDiagnostic);
            if (!ReferenceEquals(saved, UserInfo.GetWaitingPartySave(UserInfo.CurrentStage))) throw new InvalidOperationException("WaitingSnapshotChanged");
            saved.ConfirmValidation();
            _restoredSnapshot = saved;
            WaitingLoadDiagnostic = "Restored";
        }
        catch (Exception error)
        {
            foreach (var party in WaitingParties.ToArray()) CancelWaitingParty(party);
            WaitingLoadDiagnostic = error.Message;
            if (ReferenceEquals(saved, UserInfo.GetWaitingPartySave(UserInfo.CurrentStage))) UserInfo.ProtectWaitingParties(UserInfo.CurrentStage, error.Message);
        }
        finally { _restoringWaiting = false; }
        // No write during restore: original persisted data remains authoritative until a later mutation.
        _tableManager.UpdateTable();
    }
}
