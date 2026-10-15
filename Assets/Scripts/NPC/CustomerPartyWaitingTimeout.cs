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
            try { customer.Move(GameManager.Instance.OutDoorPos, 0, () =>
            {
                if (!party.IsActive || party.HasDeparted(member) || !member.IsVisitCurrent
                    || !ReferenceEquals(customer.VisitMember, member) || !customer.ConfirmReachedMoveTarget()) return;
                party.CompleteDeparture(member);
                try { customer.StopAnger(); }
                catch (Exception error) { LastVisitDiagnostic = error.ToString(); }
                ObjectPoolManager.Instance.DespawnNormalCustomer(customer);
                if (party.Members.All(party.HasDeparted)) party.End();
            }); }
            catch (Exception error) { LastVisitDiagnostic = error.ToString(); }
        }
        SortCustomerLine(); NotifyWaitingChanged(); _tableManager.UpdateTable();
    }
}
