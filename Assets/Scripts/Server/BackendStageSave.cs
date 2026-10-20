using System;
using System.Collections.Generic;
using BackEnd;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public enum StageSaveStatus { Pending, Succeeded, Failed, Held, Superseded }

    // A Stage request receipt, not a global dirty flag. Success confirms only its
    // transmitted fields; an omitted waiting queue remains protected/unvalidated.
    public sealed class StageSaveRequest
    {
        private StageSaveStatus _status = StageSaveStatus.Pending;
        internal Func<bool> Current;
        internal Action Start;
        internal Action<StageSaveRequest> Completed;
        internal GameDataRestoreQuery Query;
        internal bool Finished;
        internal bool Started, WriteStarted;
        public StageSaveStatus Status => !Finished && Current != null && !Current() ? StageSaveStatus.Superseded : _status;
        public string Reason { get; internal set; }
        public bool WaitingQueuePreserved { get; internal set; }
        public string TransmittedJson { get; internal set; }
        public Func<StageSaveRequest> Retry { get; internal set; }
        internal void Finish(StageSaveStatus status, string reason)
        { if (Finished) return; Finished = true; _status = status; Reason = reason; Completed?.Invoke(this); }
        internal static StageSaveRequest Held(string reason, Action<StageSaveRequest> completed = null)
        { var request = new StageSaveRequest { Completed = completed }; request.Finish(StageSaveStatus.Held, reason); return request; }
    }

    // Thin extension of the existing Stage GET boundary. No Insert and no global
    // GameData transaction framework: only ordinary existing-Stage partial updates.
    public interface IStageDataWriteTransport
    {
        void ReadForSave(EStage stage, string account, Func<bool> current, bool asynchronous, Action<BackendReturnObject> reply);
        void Update(EStage stage, string account, string row, Func<Param> latestValues,
            Func<bool> current, bool asynchronous, Action<BackendReturnObject> reply);
    }

    public partial class BackendManager
    {
        private sealed class StageDestination
        { internal GameDataRestoreQuery Query; internal StageInfo Source; internal string Row; }
        private readonly Dictionary<EStage, StageDestination> _stageSaveDestinations = new Dictionary<EStage, StageDestination>();
        private readonly Dictionary<string, Queue<StageSaveRequest>> _stageSaveQueues = new Dictionary<string, Queue<StageSaveRequest>>();

        internal void BindStageSaveDestination(EStage stage, StageInfo source, BackendReturnObject response)
        {
            var query = GameDataRestore.LegacyQuery;
            if (query != null && TryStageRow(response, query.AccountInDate, out string row))
            {
                if (_stageSaveDestinations.TryGetValue(stage, out var old) && ReferenceEquals(old.Query, query)
                    && ReferenceEquals(old.Source, source) && old.Row == row) return;
                _stageSaveDestinations[stage] = new StageDestination { Query = query, Source = source, Row = row };
            }
        }
        internal void ForgetStageSaveDestination(EStage stage) => _stageSaveDestinations?.Remove(stage);

        private static bool TryStageRow(BackendReturnObject response, string account, out string row)
        {
            row = null;
            if (response == null || !response.IsSuccess()) return false;
            try
            {
                var root = WaitingPartySaveData.ParseRawObject(response.GetReturnValue());
                if (!(root["rows"] is JArray rows) || rows.Count != 1 || !(rows[0] is JObject value)
                    || root["firstKey"] != null && root["firstKey"].Type != JTokenType.Null
                    || (string)value["owner_inDate"]?["S"] != account) return false;
                row = (string)value["inDate"]?["S"];
                return !string.IsNullOrWhiteSpace(row);
            }
            catch { return false; }
        }

        internal StageSaveRequest RequestStageSave(EStage stage, StageInfo source, bool asynchronous,
            Func<bool> sameDestination, Action<StageSaveRequest> completed)
        {
            var query = GameDataRestore.LegacyQuery;
            if (!_stageSaveDestinations.TryGetValue(stage, out var binding)
                || !ReferenceEquals(binding.Query, query) || !ReferenceEquals(binding.Source, source))
                return StageSaveRequest.Held("Stage has no successfully loaded row in this account session", completed);
            var request = new StageSaveRequest { Query = query, Completed = completed };
            request.Current = () => !_destroyCleanupDone && SavingEnabledForOwner && GameDataTransport.LoggedIn
                && GameDataRestore.IsCurrent(query) && ReferenceEquals(GameDataRestore.LegacyQuery, query)
                && sameDestination() && _stageSaveDestinations.TryGetValue(stage, out var current)
                && ReferenceEquals(current, binding);
            request.Retry = () => request.Current() ? RequestStageSave(stage, source, asynchronous, sameDestination, completed)
                : StageSaveRequest.Held("Retry belongs to an expired Stage/account", completed);
            if (!request.Current()) { request.Finish(StageSaveStatus.Held, "Stage save session is unavailable"); return request; }

            string key = query.AccountInDate + ":" + stage + ":" + binding.Row;
            if (!_stageSaveQueues.TryGetValue(key, out var queue))
                _stageSaveQueues[key] = queue = new Queue<StageSaveRequest>();
            // An expired GET/queued intent can be dropped. A sent write to this
            // same account/row must retain the gate until its response arrives.
            while (queue.Count > 0 && !queue.Peek().Current() && !queue.Peek().WriteStarted)
                queue.Dequeue().Finish(StageSaveStatus.Superseded, "Stage save session changed before transmission");
            void Finish(StageSaveStatus status, string reason)
            {
                if (request.Finished) return;
                if (!request.Current()) status = StageSaveStatus.Superseded;
                bool ownsHead = queue.Count > 0 && ReferenceEquals(queue.Peek(), request);
                if (ownsHead) queue.Dequeue();
                try { request.Finish(status, reason); }
                finally { if (ownsHead && queue.Count > 0) queue.Peek().Start(); }
            }
            request.Start = () =>
            {
                if (request.Started || request.Finished) return;
                request.Started = true;
                if (!request.Current()) { Finish(StageSaveStatus.Superseded, "Stage save session changed"); return; }
                var writer = StageDataTransport as IStageDataWriteTransport;
                if (writer == null) { Finish(StageSaveStatus.Held, "Stage write transport unavailable"); return; }
                bool readHandled = false;
                void Receive(BackendReturnObject response)
                {
                    if (readHandled || request.Finished) return; readHandled = true;
                    if (!request.Current()) { Finish(StageSaveStatus.Superseded, "Late Stage read"); return; }
                    if (response == null || !response.IsSuccess()) { Finish(StageSaveStatus.Failed, "Stage read failed"); return; }
                    // Never decode the protected queue to locate the row. Never insert
                    // a partial payload if the original row is missing or has changed.
                    if (!TryStageRow(response, query.AccountInDate, out string row) || row != binding.Row)
                    { Finish(StageSaveStatus.Held, "Exact existing Stage row unavailable"); return; }
                    Param Latest()
                    {
                        if (!request.Current() || request.Finished) throw new InvalidOperationException("Stale Stage save");
                        var values = source.SaveData().GetParam(true);
                        if (!BindFirstTutorialStageSaveGuard(stage + "Data", values, request.Current)())
                            throw new InvalidOperationException("Tutorial Stage save held");
                        request.WaitingQueuePreserved = !source.WaitingParties.CanSave;
                        request.TransmittedJson = values.GetJson();
                        request.WriteStarted = true;
                        return values;
                    }
                    try
                    {
                        writer.Update(stage, query.AccountInDate, row, Latest,
                            () => !request.Finished && request.Current(), asynchronous,
                            reply => Finish(reply != null && reply.IsSuccess() ? StageSaveStatus.Succeeded : StageSaveStatus.Failed,
                                reply != null && reply.IsSuccess() ? null : "Stage update failed; latest values remain in memory"));
                    }
                    catch (InvalidOperationException error) { Finish(StageSaveStatus.Held, error.Message); }
                    catch (Exception error) { Finish(StageSaveStatus.Failed, error.GetType().Name); }
                }
                try
                {
                    writer.ReadForSave(stage, query.AccountInDate, request.Current, asynchronous, Receive);
                }
                catch (Exception error) { Finish(StageSaveStatus.Failed, error.GetType().Name); }
            };
            queue.Enqueue(request);
            queue.Peek().Start();
            return request;
        }

        private void ReadStageForSave(EStage stage, string account, Func<bool> current, bool asynchronous, Action<BackendReturnObject> reply)
        {
            var where = new Where(); where.Equal("owner_inDate", account);
            // A save failure retries a save, never a Stage reload over unsaved changes.
            if (asynchronous)
            {
                BackendReturnObject last = null;
                ProcessBackendAPI(stage + " save row lookup", callback =>
                {
                    bool received = false;
                    try { Backend.GameData.Get(stage + "Data", where, response =>
                    { if (received) return; received = true; last = response; if (current()) callback(response); else reply(response); }); }
                    catch (Exception) { if (!received) { received = true; reply(null); } }
                },
                    reply, _ => reply(last), 2, false, current);
            }
            else reply(ProcessBackendAPISync(stage + " save row lookup", () => Backend.GameData.Get(stage + "Data", where), 2, false, current));
        }

        private void WriteStageValues(EStage stage, string account, string row, Func<Param> latest,
            Func<bool> current, bool asynchronous, Action<BackendReturnObject> reply)
        {
            string table = stage + "Data";
            if (asynchronous)
            {
                BackendReturnObject last = null;
                ProcessBackendAPI(table + " update", callback => AttemptStageWrite(latest,
                    (values, received) => ObserveOrdinaryStageWrite(table,
                        done => Backend.GameData.UpdateV2(table, row, account, values, response => done(response)), received),
                    response => { last = response; if (current()) callback(response); else reply(response); }, () => reply(null)),
                    reply, _ => reply(last), 3, false, current);
            }
            else reply(ProcessBackendAPISync(table + " update", () =>
            {
                var values = latest();
                return ObserveOrdinaryStageWrite(table, () => Backend.GameData.UpdateV2(table, row, account, values));
            }, 3, false, current));
            // A failed request may be retried through its receipt or the next existing
            // autosave. A retry builds current values; no popup replays an old payload.
        }

        // Invoked on EVERY asynchronous attempt, including retries after the first
        // call stack has returned. A local factory failure must release the row gate.
        internal static void AttemptStageWrite(Func<Param> latest, Action<Param, Action<BackendReturnObject>> send,
            Action<BackendReturnObject> received, Action failed)
        {
            bool finished = false;
            void Complete(BackendReturnObject response)
            { if (finished) return; finished = true; received(response); }
            try { var values = latest(); send(values, Complete); }
            catch (Exception) { if (!finished) { finished = true; failed(); } }
        }
    }
}
