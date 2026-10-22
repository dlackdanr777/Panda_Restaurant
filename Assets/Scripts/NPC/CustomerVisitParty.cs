using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Muks.BackEnd;

// Runtime relationships only; CustomerController owns ordered waiting reservations.
public sealed class CustomerVisitParty
{
    private readonly CustomerController _owner;
    private readonly object _execution;
    private readonly EStage _stage;
    private readonly BackendManager _backend;
    private readonly GameDataRestoreQuery _accountQuery;
    private bool _ended;
    private TableUseIdentity _use;
    private readonly HashSet<CustomerVisitMember> _departed = new HashSet<CustomerVisitMember>();
    internal bool WaitingDeparture { get; private set; }
    internal bool HasDeparted(CustomerVisitMember member) => _departed.Contains(member);
    internal bool TryBeginWaitingDeparture()
    {
        if (!IsActive || _use != null || WaitingDeparture || Members.Count != 2) return false;
        WaitingDeparture = true;
        return true;
    }
    internal bool CompleteDeparture(CustomerVisitMember member,
        CustomerVisitDepartureResolution resolution = CustomerVisitDepartureResolution.ReachedExit)
    {
        if (_ended || (!WaitingDeparture && (_use == null || !_use.CompletionStarted))
            || !System.Linq.Enumerable.Contains(Members, member) || !_departed.Add(member)) return false;
        member.SetDepartureResolution(resolution);
        member.SetSeat(-1);
        member.Customer.DetachVisitMember(member);
        return true;
    }

    public IReadOnlyList<CustomerVisitMember> Members { get; }
    public bool IsActive
    {
        get
        {
            if (!_ended && !IsContextCurrent()) EndCore(true);
            return !_ended;
        }
    }
    public TableData CurrentTable => IsActive ? _use?.Table : null;
    internal TableUseIdentity CurrentUse => _use;
    internal CustomerController Owner => _owner;

    private CustomerVisitParty(CustomerController owner, IReadOnlyList<NormalCustomer> customers)
    {
        _owner = owner;
        _execution = owner.VisitExecution;
        _stage = UserInfo.CurrentStage;
        _backend = BackendManager.ExistingInstance;
        _accountQuery = _backend == null ? null : _backend.CurrentMailReceiveQuery;
        var members = new CustomerVisitMember[customers.Count];
        for (int i = 0; i < members.Length; i++) members[i] = new CustomerVisitMember(this, customers[i]);
        Members = new ReadOnlyCollection<CustomerVisitMember>(members);
    }

    // Validate every member before attaching any.
    internal static bool TryCreate(CustomerController owner, IReadOnlyList<NormalCustomer> customers,
        out CustomerVisitParty party, out string reason)
    {
        party = null;
        reason = "InvalidVisitMembers";
        if (owner == null || owner.VisitExecution == null || customers == null
            || customers.Count < 1 || customers.Count > 2) return false;
        for (int i = 0; i < customers.Count; i++)
        {
            var customer = customers[i];
            if (customer == null || !customer.IsVisitReady(owner)) return false;
            if (customer.VisitMember != null) { reason = "CustomerAlreadyInParty"; return false; }
            for (int j = 0; j < i; j++)
                if (ReferenceEquals(customer, customers[j])) { reason = "DuplicateVisitMember"; return false; }
        }
        var candidate = new CustomerVisitParty(owner, customers);
        // An account boundary must not leave a newly attached, already stale party.
        if (!candidate.IsContextCurrent()) { reason = "VisitContextEnded"; return false; }
        party = candidate;
        foreach (var member in party.Members) member.Customer.AttachVisitMember(member);
        owner.RegisterVisitParty(party);
        reason = "Created";
        return true;
    }

    private bool IsContextCurrent()
    {
        if (_owner == null || !_owner.IsVisitExecutionCurrent(_execution) || UserInfo.CurrentStage != _stage
            || !ReferenceEquals(_backend, BackendManager.ExistingInstance)
            || (!ReferenceEquals(_backend, null) && (_backend == null || _accountQuery == null
                || !_backend.IsCurrentGameDataQuery(_accountQuery)))) return false;
        foreach (var member in Members)
            if (!_departed.Contains(member) && !member.IsVisitCurrent) return false;
        return true;
    }

    internal bool TryBind(TableUseIdentity use, int[] seatIndices)
    {
        if (!IsActive || WaitingDeparture || _use != null || use == null || seatIndices == null
            || seatIndices.Length != Members.Count) return false;
        for (int i = 0; i < seatIndices.Length; i++)
            if (seatIndices[i] < 0 || seatIndices[i] > 1
                || (i > 0 && seatIndices[i] == seatIndices[0])) return false;
        _use = use;
        for (int i = 0; i < Members.Count; i++) Members[i].SetSeat(seatIndices[i]);
        return true;
    }

    internal void End() => EndCore(false);
    private void EndCore(bool preserveWaitingSave)
    {
        if (_ended) return;
        _ended = true;
        var use = _use;
        _use = null;
        foreach (var member in Members)
        {
            member.SetSeat(-1);
            if (member.Customer != null) member.Customer.DetachVisitMember(member);
        }
        if (use != null && use.Table != null) use.Table.EndUse(use);
        if (_owner != null) _owner.UnregisterVisitParty(this, preserveWaitingSave);
    }
}

public sealed class CustomerVisitMember
{
    public CustomerVisitParty Party { get; }
    public NormalCustomer Customer { get; }
    internal object VisitIdentity { get; }
    public int SeatIndex { get; private set; } = -1;
    internal CustomerVisitDepartureResolution DepartureResolution { get; private set; }
    private bool _encounterArrivalConsumed;
    private bool _encounterEntryConsumed;
    private readonly long _encounterContextGeneration;
    internal int EncounterVisitsBeforeVisit { get; }
    internal bool IsVisitCurrent => Customer != null && Customer.IsVisitReady(Party.Owner)
        && ReferenceEquals(Customer.VisitIdentity, VisitIdentity);

    // Belongs to this runtime visit, never to the character ID or pooled GameObject.
    // Consume even when UI blocks presentation; a line shuffle is not a retry event.
    internal bool TryConsumeEncounterArrival()
    {
        if (_encounterArrivalConsumed || !IsVisitCurrent || !Party.IsActive
            || _encounterContextGeneration != UserInfo.CustomerEncounterContextGeneration
            || !ReferenceEquals(Customer.VisitMember, this) || Party.CurrentUse != null
            || Party.WaitingDeparture) return false;
        _encounterArrivalConsumed = true;
        return true;
    }

    internal void SuppressRestoredEncounterEvents()
    {
        _encounterArrivalConsumed = true;
        _encounterEntryConsumed = true;
    }

    internal bool TryConsumeEncounterEntry(TableUseIdentity use)
    {
        if (_encounterEntryConsumed || use == null || !use.IsMemberCurrent(this)
            || _encounterContextGeneration != UserInfo.CustomerEncounterContextGeneration
            || use.CompletionStarted || use.ServiceAborted || use.FurnitureRemoved
            || (use.IsSingle ? !use.Arrived || !use.Seated : !use.AllSeated)) return false;
        _encounterEntryConsumed = true;
        return true;
    }

    internal CustomerVisitMember(CustomerVisitParty party, NormalCustomer customer)
    {
        Party = party;
        Customer = customer;
        VisitIdentity = customer.VisitIdentity;
        _encounterContextGeneration = UserInfo.CustomerEncounterContextGeneration;
        NormalCustomerData data = customer.NormalCustomerData;
        EncounterVisitsBeforeVisit = data != null && CustomerEncounterCatalog.IsSupportedCustomer(data.Id)
            && UserInfo.GetCustomerEnableState(data) ? UserInfo.GetVisitedCustomerCount(data.Id) : 0;
    }
    internal void SetSeat(int index) { SeatIndex = index; }
    internal void SetDepartureResolution(CustomerVisitDepartureResolution resolution) { DepartureResolution = resolution; }
}

internal enum CustomerVisitDepartureResolution : byte
{
    None,
    ReachedExit,
    TechnicalCleanup
}
