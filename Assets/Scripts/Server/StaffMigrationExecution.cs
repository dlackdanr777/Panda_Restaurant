using System;
using System.Linq;
using BackEnd;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public enum StaffMigrationExecutionStatus
    {
        Waiting, Validating, Sending, Completed, RejectedBeforeSend,
        Indeterminate, LocalCompletionFailed, InvalidatedAfterSend
    }

    /// <summary>
    /// One local migration intent and its fixed transmission evidence. Only BackendManager creates it.
    /// Not a server idempotency key, durable recovery record, or fabricated GameData restore evidence.
    /// </summary>
    public sealed class StaffMigrationExecution
    {
        private readonly BackendManager _owner;
        private readonly GameDataSaveCoordinator _coordinator;
        private StaffMigrationExecutionStatus _phase = StaffMigrationExecutionStatus.Waiting;
        private string _error;
        private bool _installing;
        public GameDataRestoreQuery Query => Collection.Query;
        public StaffStageMigrationCollection Collection { get; }
        public StaffMigrationPreparation Preparation { get; private set; }
        public GameDataSaveIdentity Identity { get; }
        public GameDataSaveRequest Request { get; private set; }
        public string StaffAccountJson { get; private set; }
        public bool IsCompleted { get; private set; }
        public int CompletionCount { get; private set; }
        public string Error => Request?.Error ?? _error;
        public StaffMigrationExecutionStatus Status
        {
            get
            {
                if (IsCompleted) return StaffMigrationExecutionStatus.Completed;
                if (Request != null)
                    switch (Request.Status)
                    {
                        case GameDataSaveRequestStatus.RejectedBeforeSend: return StaffMigrationExecutionStatus.RejectedBeforeSend;
                        case GameDataSaveRequestStatus.Indeterminate: return StaffMigrationExecutionStatus.Indeterminate;
                        case GameDataSaveRequestStatus.LocalCompletionFailed: return StaffMigrationExecutionStatus.LocalCompletionFailed;
                        case GameDataSaveRequestStatus.InvalidatedAfterSend: return StaffMigrationExecutionStatus.InvalidatedAfterSend;
                        case GameDataSaveRequestStatus.Sending: return StaffMigrationExecutionStatus.Sending;
                    }
                return _phase;
            }
        }

        internal StaffMigrationExecution(BackendManager owner, StaffStageMigrationCollection collection,
            GameDataSaveCoordinator coordinator, GameDataSaveTarget target)
        {
            _owner = owner; Collection = collection; _coordinator = coordinator;
            Identity = new GameDataSaveIdentity("staff-migration:" + Guid.NewGuid().ToString("N"), target);
        }

        internal void Enqueue() => Request = _coordinator.EnqueueStaffMigration(this);

        // FIFO reaches this intent before any candidate is retained. Waiting saves/mail never retain a stale payload.
        internal bool PrepareBeforeProtection(out string error)
        {
            error = null;
            _phase = StaffMigrationExecutionStatus.Validating;
            if (!_owner.IsCurrentStaffMigration(this)) return Fail("이전 준비의 계정·행·조회 회차가 변경되었습니다.", out error);
            StaffMigrationPreparationResult result = _owner.PrepareStaffMigration();
            if (result.Status != StaffMigrationPreparationStatus.Valid || result.Preparation == null)
                return Fail(result.Error ?? "이전 준비 자료가 유효하지 않습니다.", out error);
            Preparation = result.Preparation;
            if (!ReferenceEquals(Preparation.Owner, Collection) || !Identity.Target.Matches(Preparation.Target)
                || !_owner.IsCurrentStaffMigration(this)) return Fail("준비 확인 중 이전 대상이 변경되었습니다.", out error);
            return true;
        }

        internal Param CreateValuesUnderProtection()
        {
            string error = null;
            if (!HasProtection() || !Revalidate(out error))
                throw new InvalidOperationException(error ?? "현재 이전 작업의 보호권이 없습니다.");
            if (!StaffAccountSaveConverter.TrySerialize(Preparation.Candidate, out string json, out error))
                throw new InvalidOperationException(error);
            StaffAccountJson = json;
            var values = new Param();
            values.Add(GameDataRestoreContext.StaffAccountFieldName, json);
            _phase = StaffMigrationExecutionStatus.Sending;
            return values;
        }

        // Called by the production-bound updater AFTER its ordinary final readiness check, immediately before UpdateOnce.
        internal string ValidateTransmission(GameDataSaveIdentity identity, GameDataSavePayload payload)
        {
            if (IsCompleted || !HasProtection() || !Identity.Matches(identity)
                || _coordinator.State != GameDataSaveCoordinatorState.Sending || !MatchesPayload(payload))
                return "현재 이전 요청의 고정된 StaffAccount 전송 권한이 아닙니다.";
            return Revalidate(out string error) && HasProtection() ? null : error ?? "전송 직전 이전 보호권이 변경되었습니다.";
        }

        internal void Confirm(GameDataSaveReceipt receipt)
        {
            if (IsCompleted) return;
            _installing = true;
            try
            {
                if (!_owner.StaffRuntime.TryInstallMigration(this, receipt, out string error))
                    throw new InvalidOperationException(error ?? "저장 성공 후 공용 직원 설치를 완료하지 못했습니다.");
                IsCompleted = true;
                CompletionCount = 1;
                _phase = StaffMigrationExecutionStatus.Completed;
                // State is committed before observers. The coordinator still owns the row until this callback returns.
                _owner.NotifyStaffMigrationCompleted(this);
            }
            finally { _installing = false; }
        }

        internal bool ValidateInstallation(StaffAccountRuntime runtime, GameDataSaveReceipt receipt, out string error)
        {
            error = null;
            if (!_installing || IsCompleted || !ReferenceEquals(runtime, _owner.StaffRuntime) || !HasProtection()
                || _coordinator.State != GameDataSaveCoordinatorState.ApplyingConfirmedState
                || !ReferenceEquals(receipt, _coordinator.LastReceipt) || receipt == null || !receipt.SendStarted
                || receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed || !Identity.Matches(receipt.Identity)
                || !MatchesPayload(receipt.Payload))
            { error = "동일 이전 요청의 저장 성공·보호·고정 자료 근거가 없습니다."; return false; }
            return Revalidate(out error) && HasProtection();
        }

        private bool HasProtection() => ReferenceEquals(_coordinator.ActiveStaffMigration, this);

        private bool Revalidate(out string error)
        {
            error = null;
            if (Preparation == null || !_owner.IsCurrentStaffMigration(this))
            { error = "현재 이전 원본·계정·세대가 아닙니다."; return false; }
            StaffMigrationPreparationResult result = _owner.RevalidateStaffMigration(Preparation);
            if (result.Status != StaffMigrationPreparationStatus.Valid || !_owner.IsCurrentStaffMigration(this))
            { error = result.Error ?? "이전 원본 또는 보호 대상이 변경되었습니다."; return false; }
            // Keep Preparation itself; revalidation must not silently substitute a new candidate or baseline.
            return true;
        }

        private bool MatchesPayload(GameDataSavePayload payload)
        {
            if (payload == null || StaffAccountJson == null) return false;
            try
            {
                JObject root = JObject.Parse(payload.Json);
                return root.Properties().Count() == 1
                    && root[GameDataRestoreContext.StaffAccountFieldName]?.Type == JTokenType.String
                    && (string)root[GameDataRestoreContext.StaffAccountFieldName] == StaffAccountJson;
            }
            catch { return false; }
        }

        private bool Fail(string reason, out string error)
        { _error = error = reason; _phase = StaffMigrationExecutionStatus.RejectedBeforeSend; return false; }
    }
}
