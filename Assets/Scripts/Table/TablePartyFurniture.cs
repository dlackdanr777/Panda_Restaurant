using System.Collections.Generic;
using Muks.BackEnd;
using Muks.PathFinding.AStar;
using UnityEngine;

public partial class TableManager
{
    // A shop lifetime owns affected slots and expected uses, never a copy of their ledger.
    private sealed class FurnitureEdit
    {
        internal object Execution;
        internal EStage Stage;
        internal BackendManager Backend;
        internal GameDataRestoreQuery Account;
        internal readonly Dictionary<TableData, TableUseIdentity> Uses = new Dictionary<TableData, TableUseIdentity>();
    }
    private FurnitureEdit _furnitureEdit;

    private bool IsFurnitureEditCurrent(FurnitureEdit edit) => edit != null
        && IsExecutionCurrent(edit.Execution) && edit.Stage == UserInfo.CurrentStage
        && ReferenceEquals(edit.Backend, BackendManager.ExistingInstance)
        && (ReferenceEquals(edit.Backend, null)
            || (edit.Backend != null && edit.Account != null && edit.Backend.IsCurrentGameDataQuery(edit.Account)));

    internal object BeginFurnitureEdit()
    {
        if (IsFurnitureEditCurrent(_furnitureEdit)) return _furnitureEdit;
        var backend = BackendManager.ExistingInstance;
        _furnitureEdit = new FurnitureEdit { Execution = _executionIdentity, Stage = UserInfo.CurrentStage,
            Backend = backend, Account = backend == null ? null : backend.CurrentMailReceiveQuery };
        return _furnitureEdit;
    }

    internal void CancelFurnitureEdit(object token)
    { if (ReferenceEquals(_furnitureEdit, token)) _furnitureEdit = null; }

    private bool TrackFurnitureEdit(TableData table)
    {
        if (!IsFurnitureEditCurrent(_furnitureEdit)) return false;
        var use = table == null ? null : table.CurrentUse;
        if (use == null || use.IsSingle || !use.IsPartyCurrent(table)) return false;
        // A new furniture event may belong to a later use during this same shop session.
        // Closing itself never discovers a replacement use.
        _furnitureEdit.Uses[table] = use;
        return true;
    }

    internal static bool HasUsableFurniture(TableData table) => table != null && table.TableFurniture != null
        && (table.TableType == TableType.Table1
            || UserInfo.GetEquipFurniture(UserInfo.CurrentStage, table.FloorType, (FurnitureType)table.TableType) != null);

    internal void CompleteFurnitureEdit(object token)
    {
        var edit = _furnitureEdit;
        if (edit == null || !ReferenceEquals(edit, token)) return;
        _furnitureEdit = null; // Consume before notifications can re-enter the shop or furniture events.
        if (!IsFurnitureEditCurrent(edit)) return;
        foreach (var entry in edit.Uses)
        {
            var use = entry.Value;
            if (!use.IsPartyCurrent(entry.Key) || use.SettlementCompleted) continue;
            if (HasUsableFurniture(entry.Key)) RefreshPartyFurniture(use);
            else RemovePartyFurniture(use);
        }
        UpdateTable();
    }

    private void RefreshPartyFurniture(TableUseIdentity use)
    {
        if (!use.IsPartyCurrent(use.Table) || use.CompletionStarted || use.PolicyPending) return;
        var table = use.Table;
        foreach (var member in use.Party.Members)
        {
            if (!use.IsMemberCurrent(member)) continue;
            var target = table.GetSeatPosition(member.SeatIndex);
            if (use.HasArrived(member))
            {
                member.Customer.transform.position = target;
                member.Customer.SetSpriteDir(member.SeatIndex == 0 ? 1 : -1);
            }
            else
            {
                target.y = table.TableFurniture.transform.position.y + AStar.Instance.NodeSize * .5f;
                member.Customer.RetargetCurrentMove(target);
            }
        }
        if (use.Orders != null)
            foreach (var food in use.Orders.Orders)
                food.Identity.ServingReservation?.Action.RefreshPartySeat(use);
        table.RefreshPartyOrderButtons();
    }

    private void RemovePartyFurniture(TableUseIdentity use)
    {
        if (!use.IsPartyCurrent(use.Table) || use.CompletionStarted) return;
        use.FurnitureRemoved = true;
        use.PolicyPending = false;
        if (!use.ServiceAborted) use.ServiceAbortReason = "FurnitureRemovedAtShopClose";
        use.ServiceAborted = true;
        if (use.Completions.Count == 0)
            foreach (var member in use.Party.Members) use.Completions.Add(new PartyMemberCompletion(member));
        // Keep completion records and the pending personal tip alive. Cancel only unfinished work.
        var workers = new HashSet<WaiterAction>();
        if (use.OrderReservation != null) workers.Add(use.OrderReservation.Action);
        if (use.Orders != null)
            foreach (var food in use.Orders.Orders)
            {
                if (food.Identity.ServingReservation != null) workers.Add(food.Identity.ServingReservation.Action);
                if (!food.CompletionRecorded) food.Cancel();
            }
        use.OrderReservation?.Release();
        foreach (var worker in workers)
            try { worker.CancelCurrentWork(); }
            catch (System.Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        try { use.Orders?.Kitchen?.CancelUse(use); }
        catch (System.Exception error) { use.Table.LastServiceDiagnostic = error.ToString(); }
        PartyEffect(use, ref use.ServicePenaltyApplied,
            () => UserInfo.AddSatisfaction(use.Stage, BasicRecipeRejectionSatisfaction * use.Party.Members.Count));
        if (!use.IsPartyCurrent(use.Table)) return;
        TryFinishPartyInterruption(use);
    }
}
