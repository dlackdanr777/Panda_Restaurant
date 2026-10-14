using Muks.BackEnd;

// Runtime ownership: CurrentUse owns seats and the initial party order batch. Single-person
// CurrentCustomer/CurrentFood are compatibility data. Identities are never account-save data.
internal sealed class TableUseIdentity
{
    private readonly TableManager _owner;
    private readonly object _execution;
    private readonly NormalCustomer _customer;
    private readonly object _visit;
    private readonly EStage _stage;
    private readonly BackendManager _backend;
    private readonly GameDataRestoreQuery _accountQuery;
    internal readonly TableData Table;
    internal readonly CustomerVisitMember Member;
    internal readonly CustomerVisitParty Party;
    private readonly System.Collections.Generic.HashSet<CustomerVisitMember> _arrivals = new System.Collections.Generic.HashSet<CustomerVisitMember>();
    private readonly System.Collections.Generic.HashSet<CustomerVisitMember> _seated = new System.Collections.Generic.HashSet<CustomerVisitMember>();
    internal bool IsSingle => Party.Members.Count == 1;
    internal bool AllSeated => _seated.Count == Party.Members.Count;

    internal readonly System.Collections.Generic.List<PartyMemberCompletion> Completions = new System.Collections.Generic.List<PartyMemberCompletion>();
    internal PartyMemberCompletion GetCompletion(CustomerVisitMember member) => Completions.Find(x => ReferenceEquals(x.Member, member));
    internal EStage Stage => _stage;
    internal int Round;
    internal bool PolicyPending;
    internal bool Invalidated;
    internal bool IsCompletionContextCurrent => !Invalidated && IsExecutionContextCurrent;
    internal PartyOrderBatch Orders;
    internal TableServiceReservation OrderReservation;
    internal bool Arrived;
    internal bool Seated;
    internal bool CompletionStarted;
    internal bool SettlementCompleted;

    internal TableUseIdentity(TableManager owner, object execution, NormalCustomer customer,
        TableData table, CustomerVisitMember member)
    {
        _owner = owner;
        _execution = execution;
        _customer = customer;
        _visit = customer.VisitIdentity;
        Table = table;
        Member = member;
        Party = member.Party;
        _stage = UserInfo.CurrentStage;
        _backend = BackendManager.ExistingInstance;
        _accountQuery = _backend == null ? null : _backend.CurrentMailReceiveQuery;
    }

    // Also valid for the captured customer's exit movement after table ownership ended.
    internal bool IsExecutionContextCurrent => _owner != null
        && _owner.IsExecutionCurrent(_execution)
        && UserInfo.CurrentStage == _stage

        && ReferenceEquals(_backend, BackendManager.ExistingInstance)
        && (ReferenceEquals(_backend, null)
            || (_backend != null && _accountQuery != null && _backend.IsCurrentGameDataQuery(_accountQuery)));

    internal bool IsCustomerContextCurrent => IsExecutionContextCurrent
        && _customer != null && ReferenceEquals(_visit, _customer.VisitIdentity);

    internal bool IsPartyCurrent(TableData table) => table != null
        && ReferenceEquals(table.CurrentUse, this)
        && ReferenceEquals(Table, table) && Party.IsActive
        && ReferenceEquals(Party.CurrentUse, this)
        && (IsSingle ? IsCustomerContextCurrent : IsExecutionContextCurrent);

    internal bool IsMemberCurrent(CustomerVisitMember member) => IsPartyCurrent(Table)
        && member != null && ReferenceEquals(member.Party, Party) && member.IsVisitCurrent
        && ReferenceEquals(member.Customer.VisitMember, member) && member.SeatIndex >= 0;
    internal bool TryArrive(CustomerVisitMember member) => IsMemberCurrent(member) && _arrivals.Add(member);
    internal bool TrySeat(CustomerVisitMember member) => IsMemberCurrent(member) && _arrivals.Contains(member) && _seated.Add(member);
    internal bool HasArrived(CustomerVisitMember member) => _arrivals.Contains(member);
    internal bool HasSeated(CustomerVisitMember member) => _seated.Contains(member);

    // Existing meal/order code is explicitly single-person only until the next phase.
    internal bool IsCurrent(TableData table) => IsSingle && IsPartyCurrent(table)
        && ReferenceEquals(table.CurrentCustomer, _customer)
        && ReferenceEquals(_customer.VisitMember, Member)
        && ReferenceEquals(Member.VisitIdentity, _visit) && Member.SeatIndex == table.SitIndex;
}

// Shared by CookingData value copies in the table, queue and burner.
internal sealed class TableOrderIdentity
{
    internal readonly TableUseIdentity Use;
    internal readonly PartyFoodOrder PartyOrder;
    internal bool CookingTerminal;
    internal bool CookingCancelled;
    internal bool CookingCompleted;
    internal bool OrderAccepted;
    internal TableServiceReservation ServingReservation;
    internal bool EatingAnimationStarted;
    internal bool ServingStarted;
    internal bool EatingCompleted;

    internal TableOrderIdentity(TableUseIdentity use, PartyFoodOrder partyOrder = null) { Use = use; PartyOrder = partyOrder; }
    internal bool CanCook(TableData table) => !CookingTerminal && IsCurrent(table) && !Use.CompletionStarted && !Use.PolicyPending
        && (PartyOrder == null || PartyOrder.Batch.State == PartyOrderBatchState.Accepted);
    internal bool TryCompleteCooking(TableData table)
    {
        if (!CanCook(table)) return false;
        CookingTerminal = true;
        return true;
    }
    internal void CancelCooking() { if (!CookingTerminal) { CookingCancelled = true; CookingTerminal = true; } }

    internal bool IsCurrent(TableData table) => Use != null && (PartyOrder != null
        ? ReferenceEquals(Use.Table, table) && PartyOrder.IsCurrent
        : Use.IsCurrent(table) && !table.CurrentFood.IsDefault() && ReferenceEquals(table.CurrentFood.Identity, this));
}
