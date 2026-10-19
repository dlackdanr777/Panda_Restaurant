using System;
using System.Linq;
using UnityEngine;

public partial class CustomerController
{
    private static void StartPartyWaitingClock(WaitingEntry entry)
    {
        if (entry.ClockStarted || !entry.IsReady || entry.Party.Members.Count != 2) return;
        entry.WaitLimit = entry.Party.Members.Max(m => m.Customer.NormalCustomerData.WaitTime);
        entry.WaitStartedAt = Time.timeAsDouble;
        entry.ClockStarted = true;
    }

    // Scaled game time: a paused PlayerLoop cannot advance either kind of clock.
    internal void TickPartyClocks(double scaledNow)
    {
        foreach (var entry in _waitingTeams.ToArray())
            if (entry.ClockStarted && scaledNow - entry.WaitStartedAt >= entry.WaitLimit)
                ExpireWaitingParty(entry);
        foreach (var party in _visitParties.ToArray())
            if (party.IsActive && party.CurrentUse != null)
                _tableManager.TickPartyFoodClocks(party.CurrentUse, scaledNow);
    }

    internal bool TryGetPartyWaitingClock(CustomerVisitParty party, out double started, out float limit)
    {
        var entry = _waitingTeams.Find(e => ReferenceEquals(e.Party, party));
        started = entry == null ? 0 : entry.WaitStartedAt;
        limit = entry == null ? 0 : entry.WaitLimit;
        return entry != null && entry.ClockStarted;
    }

    private void ExpireWaitingParty(WaitingEntry entry)
    {
        var party = entry.Party;
        if (!_waitingTeams.Contains(entry) || party == null || !party.TryBeginWaitingDeparture()) return;
        // Claim the slot before notifications or movement can reenter guide/cancel.
        _waitingTeams.Remove(entry);
        foreach (var member in party.Members)
        {
            var customer = member.Customer;
            customer.StopWaiting(); customer.StopMove();
            customer.SetLayer("Customer", 0);
            try { customer.StartAnger(); }
            catch (Exception error) { LastVisitDiagnostic = error.ToString(); }
            int movementSequence = customer.MovementSequenceId + 1;
            try { customer.Move(GameManager.Instance.OutDoorPos, 0,
                () => ResolveExpiredPartyMemberExit(party, member, customer, movementSequence, false),
                () => ResolveExpiredPartyMemberExit(party, member, customer, movementSequence, true)); }
            catch (Exception error) { LastVisitDiagnostic = error.ToString(); }
        }
        SortCustomerLine(); NotifyWaitingChanged(); _tableManager.UpdateTable();
    }

    private void ResolveExpiredPartyMemberExit(CustomerVisitParty party, CustomerVisitMember member,
        NormalCustomer customer, int movementSequence, bool pathFailed)
    {
        if (party == null || member == null || customer == null
            || customer.MovementSequenceId != movementSequence
            || !party.IsActive || !party.WaitingDeparture || party.HasDeparted(member)
            || member.DepartureResolution != CustomerVisitDepartureResolution.None
            || !member.IsVisitCurrent || !ReferenceEquals(member.Party, party)
            || !ReferenceEquals(customer.VisitMember, member)) return;

        bool reachedExit = false;
        string failureReason = pathFailed ? "AStarPathUnavailable" : "DestinationNotReached";
        if (pathFailed)
        {
            if (!customer.HasPathFailure) return;
        }
        else
        {
            reachedExit = customer.ConfirmReachedMoveTarget();
            if (!reachedExit && !customer.HasPathFailure) return;
        }

        var resolution = reachedExit
            ? CustomerVisitDepartureResolution.ReachedExit
            : CustomerVisitDepartureResolution.TechnicalCleanup;
        if (!party.CompleteDeparture(member, resolution)) return;

        if (!reachedExit)
        {
            try { customer.StopMove(); }
            catch (Exception error) { LastVisitDiagnostic = error.ToString(); }
            LastVisitDiagnostic = "WaitingPartyExitTechnicalCleanup; reason=" + failureReason
                + "; member=" + customer.CustomerData.Id;
        }
        try { customer.StopAnger(); }
        catch (Exception error) { LastVisitDiagnostic = error.ToString(); }
        try { ObjectPoolManager.Instance.DespawnNormalCustomer(customer); }
        catch (Exception error) { LastVisitDiagnostic = error.ToString(); }
        if (party.Members.All(party.HasDeparted)) party.End();
    }
}
