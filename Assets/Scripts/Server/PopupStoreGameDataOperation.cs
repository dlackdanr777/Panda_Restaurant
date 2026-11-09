using System;
using BackEnd;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    /// <summary>Current authenticated restoration only. Commit changes memory without publishing
    /// observers; it must compare the source record and current account generation before mutation.</summary>
    public interface IPopupStoreGameDataState
    {
        bool IsCurrent { get; }
        PopupStoreGameDataRecord Record { get; }
        int Diamonds { get; }
        bool TryCommit(PopupStoreGameDataRecord expected, PopupStoreGameDataRecord next, int rewardDelta, out string error);
        void Notify();
    }

    /// <summary>Popup-only durable intent, never a wallet or proof of server commitment.</summary>
    public interface IPopupStoreGameDataOperationJournal
    {
        bool TryPrepare(GameDataSaveIdentity identity, PopupStoreGameDataRecord source, PopupStoreGameDataRecord candidate,
            GameDataSavePayload payload, out string error);
        void Complete(GameDataSaveIdentity identity, PopupStoreGameDataRecord candidate);
        void RejectBeforeSend(GameDataSaveIdentity identity, PopupStoreGameDataRecord candidate);
    }

    /// <summary>The existing attendance/gacha save-confirm contract with one extra GameData field.
    /// Does not issue SDK calls or reset uncertain coordinator locks. Unknown results never re-send.</summary>
    public sealed class PopupStoreGameDataOperation
    {
        private readonly GameDataSaveCoordinator _coordinator;
        private readonly IPopupStoreGameDataState _state;
        private readonly IPopupStoreGameDataOperationJournal _journal;
        private bool _started, _journalPrepared;
        public GameDataSaveIdentity Identity { get; }
        public PopupStoreGameDataRecord Source { get; }
        public PopupStoreGameDataRecord Candidate { get; }
        public GameDataSavePayload Payload { get; private set; }
        public int RewardDelta { get; }
        public bool Committed { get; private set; }
        public bool RejectedBeforeSend { get; private set; }
        public string Error { get; private set; }
        public GameDataSaveRequestStatus Status { get; private set; } = GameDataSaveRequestStatus.Accepted;
        public event Action Changed;

        public PopupStoreGameDataOperation(GameDataSaveIdentity identity, PopupStoreGameDataRecord source,
            PopupStoreGameDataRecord candidate, GameDataSaveCoordinator coordinator, IPopupStoreGameDataState state,
            IPopupStoreGameDataOperationJournal journal = null)
        {
            if (identity == null || !identity.IsValid || source == null || !source.IsValidSuccessor(candidate)
                || !MatchesLogicalRequest(identity.RequestId, candidate.LastRequestId)) throw new ArgumentException("Invalid popup save transition.");
            Identity = identity; Source = source; Candidate = candidate;
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _state = state ?? throw new ArgumentNullException(nameof(state)); _journal = journal;
            RewardDelta = candidate.State == PopupStoreGameDataState.Claimed ? candidate.Diamonds : 0;
        }

        public bool TryStart(out string error)
        {
            error = null;
            if (_started || !CurrentSource() || !_coordinator.CanStartPurchase)
            { error = "현재 영업 기록 또는 선행 저장 결과를 확인해 주세요."; return false; }
            _started = true;
            bool accepted = _coordinator.TryStartPurchase(Identity, CreateValues, Confirm, out error, OnStateChanged);
            if (!accepted) { Error = error; RejectedBeforeSend = true; Status = GameDataSaveRequestStatus.RejectedBeforeSend; }
            else if (!Committed && Status == GameDataSaveRequestStatus.Accepted) Status = GameDataSaveRequestStatus.Sending;
            return accepted;
        }

        private Param CreateValues()
        {
            if (!CurrentSource()) throw new InvalidOperationException("영업 저장 원본이 변경되었습니다.");
            var values = new Param(); values.Add(PopupStoreGameDataRecord.FieldName, Candidate.ToJson());
            if (RewardDelta > 0)
            {
                int current = _state.Diamonds;
                if (current < 0) throw new InvalidOperationException("다이아 잔액을 확인할 수 없습니다.");
                values.Add("Dia", checked(current + RewardDelta));
            }
            if (!GameDataSavePayload.TryCapture(values, out var captured, out string error)) throw new InvalidOperationException(error);
            Payload = captured;
            // Intent is durable before the SDK boundary. A previously existing intent is not a new send authority.
            if (_journal != null)
            {
                if (!_journal.TryPrepare(Identity, Source, Candidate, Payload, out error)) throw new InvalidOperationException(error ?? "기존 영업 저장 요청을 확인 중입니다.");
                _journalPrepared = true;
            }
            if (!CurrentSource()) throw new InvalidOperationException("저장 준비 중 계정이 변경되었습니다.");
            return values;
        }

        public string ValidateTransmission(GameDataSaveIdentity identity, GameDataSavePayload payload)
            => !Committed && !RejectedBeforeSend && CurrentSource() && Identity.Matches(identity) && Payload != null
                && payload != null && Payload.Json == payload.Json && ClaimBalanceStillCurrent()
                && _coordinator.State == GameDataSaveCoordinatorState.Sending
                ? null : "현재 팝업스토어 요청의 확정된 저장 자료가 아닙니다.";

        private bool ClaimBalanceStillCurrent()
        {
            if (RewardDelta == 0) return true;
            int current = _state.Diamonds;
            var value = Payload == null ? null : JObject.Parse(Payload.Json)["Dia"];
            return current >= 0 && value != null && value.Type == JTokenType.Integer
                && (long)value == (long)current + RewardDelta;
        }

        private void Confirm(GameDataSaveReceipt receipt)
        {
            if (Committed) return;
            if (!CurrentSource() || receipt == null || !ReferenceEquals(receipt, _coordinator.LastReceipt)
                || _coordinator.State != GameDataSaveCoordinatorState.ApplyingConfirmedState || !receipt.SendStarted
                || receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed || !Identity.Matches(receipt.Identity)
                || Payload == null || receipt.Payload?.Json != Payload.Json)
                throw new InvalidOperationException("팝업스토어 저장 성공 근거가 일치하지 않습니다.");
            // Add once to the current wallet: never assign the stale sent balance over later rewards.
            if (!_state.TryCommit(Source, Candidate, RewardDelta, out string error)) throw new InvalidOperationException(error);
            Committed = true;
            if (_journalPrepared)
            {
                try { _journal.Complete(Identity, Candidate); }
                catch (Exception ex) { Error = "지급은 확정됐으며 복구 기록 정리만 지연됐습니다: " + ex.GetType().Name; }
            }
            Status = GameDataSaveRequestStatus.SuccessConfirmed;
            _coordinator.MarkAutosaveNeeded();
            try { _state.Notify(); } catch (Exception ex) { Error = (Error == null ? "" : Error + " ") + "보상 표시는 새로 확인해 주세요: " + ex.GetType().Name; }
            NotifyChanged();
        }

        private bool CurrentSource() => _state.IsCurrent && Source.Matches(_state.Record);
        internal static bool MatchesLogicalRequest(string transport, string logical)
        {
            if (transport == logical) return true;
            string prefix = logical + ":attempt:";
            return transport != null && transport.StartsWith(prefix, StringComparison.Ordinal)
                && transport.Length == prefix.Length + 32 && Guid.TryParseExact(transport.Substring(prefix.Length), "N", out _);
        }
        private void OnStateChanged(GameDataSaveRequest request)
        {
            Status = request.Status; if (request.Error != null) Error = request.Error;
            if (request.Status == GameDataSaveRequestStatus.RejectedBeforeSend)
            {
                RejectedBeforeSend = true;
                if (_journalPrepared) _journal.RejectBeforeSend(Identity, Candidate);
            }
            NotifyChanged();
        }
        private void NotifyChanged()
        {
            var observers = Changed;
            if (observers == null) return;
            foreach (Action observer in observers.GetInvocationList())
                try { observer(); } catch { /* Display observers cannot reclassify a stored transaction. */ }
        }

        /// <summary>Ordinary old partial saves cannot replace a newer popup record. The production
        /// adapter may additionally compare Dia with the current wallet after a popup claim.</summary>
        public static string ValidateOrdinarySave(GameDataSavePayload payload, PopupStoreGameDataRecord current)
        {
            if (payload == null || current == null) return "팝업스토어 저장 상태를 확인할 수 없습니다.";
            try
            {
                var token = JObject.Parse(payload.Json)[PopupStoreGameDataRecord.FieldName];
                if (token == null) return null;
                return token.Type == JTokenType.String && PopupStoreGameDataRecord.TryRead((string)token, true, out var proposed, out _)
                    && current.Matches(proposed) ? null : "과거 팝업스토어 기록으로 현재 영업권·보상을 덮어쓸 수 없습니다.";
            }
            catch (Exception) { return "팝업스토어 저장 필드를 확인할 수 없습니다."; }
        }
    }
}
