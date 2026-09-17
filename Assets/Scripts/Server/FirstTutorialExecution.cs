using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public enum FirstTutorialPreparationStatus { Waiting, Blocked, Ready, AlreadyCompleted }
    public enum FirstTutorialExecutionStatus { Preparing, SavingReward, SavingPlacement, Prepared, SavingCompletion, Completed, Blocked }

    /// <summary>Detached values. Missing reward evidence is not a newly unclaimed reward.</summary>
    public sealed class FirstTutorialMemory
    {
        public bool Clear { get; }
        public bool? RewardGranted { get; }
        public long Money { get; }
        public long Total { get; }
        public long Daily { get; }
        public long Weekly { get; }
        public FirstTutorialMemory(bool clear, bool? granted, long money, long total, long daily, long weekly)
        { Clear = clear; RewardGranted = granted; Money = money; Total = total; Daily = daily; Weekly = weekly; }
        public bool CanAdd(long value)
        {
            try { checked { _ = Money + value; _ = Total + value; _ = Daily + value; _ = Weekly + value; } return true; }
            catch (OverflowException) { return false; }
        }
    }

    /// <summary>Main-thread, non-notifying commit must leave all values unchanged on false.</summary>
    public interface IFirstTutorialState
    {
        FirstTutorialMemory Read();
        StageInfo Stage1 { get; }
        bool TryCommit(FirstTutorialExecution operation, out string error);
        void NotifyReward(bool staffAdded, long gold);
    }

    /// <summary>Native defaults make one SDK call, never ProcessBackendAPI's consumer retry loop.</summary>
    public interface IFirstTutorialStageTransport
    {
        void InsertInitial(EStage stage, Param values, Action<BackendReturnObject> response);
        void UpdatePlacement(GameDataSaveTarget target, Param values, Action<BackendReturnObject> response);
    }

    /// <summary>One current-session explicit tutorial intent, not a durable server transaction or a reset API.</summary>
    public sealed class FirstTutorialExecution
    {
        private readonly BackendManager _owner;
        private readonly GameDataSaveCoordinator _coordinator;
        private readonly IFirstTutorialState _state;
        private readonly bool _completion;
        private readonly StageInfo _stage;
        private readonly StaffStageRuntimeSnapshot _stageStaff;
        private readonly string _beforePlacement;
        private readonly string _afterPlacement;
        private readonly string _stageRow;
        private readonly StaffData _staff;
        private object _presenter;
        private bool _stageReplyConsumed;
        private bool _stageSendStarted;
        private bool _committing;
        private GameDataSavePayload _payload;
        public GameDataRestoreQuery Query { get; }
        public StaffStageMigrationCollection Collection { get; }
        public GameDataSaveIdentity Identity { get; }
        public GameDataSaveRequest Request { get; private set; }
        public GameDataRawResponse PlacementResponse { get; private set; }
        public FirstTutorialMemory Before { get; }
        public StaffAccountSaveData Source { get; }
        public StaffAccountSaveData Result { get; }
        public long Gold { get; }
        public bool StaffAdded { get; }
        public bool Skipped { get; }
        public bool CoreCommitted { get; private set; }
        public bool IsPrepared { get; private set; }
        public bool IsCompleted { get; private set; }
        public int CompletionCount { get; private set; }
        public bool HasStartedPresentation { get; private set; }
        public bool IsCurrent => _owner.IsCurrentFirstTutorial(this);
        public string Error { get; private set; }
        public bool IsFailed => Error != null || Request != null && (Request.Status == GameDataSaveRequestStatus.RejectedBeforeSend
            || Request.Status == GameDataSaveRequestStatus.Indeterminate || Request.Status == GameDataSaveRequestStatus.InvalidatedAfterSend
            || Request.Status == GameDataSaveRequestStatus.LocalCompletionFailed);
        public FirstTutorialExecutionStatus Status => IsFailed ? FirstTutorialExecutionStatus.Blocked : IsCompleted ? FirstTutorialExecutionStatus.Completed
            : IsPrepared ? FirstTutorialExecutionStatus.Prepared : _completion ? FirstTutorialExecutionStatus.SavingCompletion
            : CoreCommitted ? FirstTutorialExecutionStatus.SavingPlacement : FirstTutorialExecutionStatus.SavingReward;

        internal FirstTutorialExecution(BackendManager owner, GameDataSaveCoordinator coordinator, IFirstTutorialState state,
            GameDataRestoreQuery query, StaffStageMigrationCollection collection, GameDataSaveTarget target, StaffData staff,
            bool completion = false, bool skipped = false)
        {
            _owner = owner; _coordinator = coordinator; _state = state; Query = query; Collection = collection;
            _completion = completion; Skipped = skipped; _staff = staff; _stage = state.Stage1;
            Identity = new GameDataSaveIdentity("first-tutorial:" + Guid.NewGuid().ToString("N"), target);
            Before = state.Read(); Source = owner.StaffRuntime.Snapshot;
            if (Before.Clear || Source == null || _stage == null || _stage.IsApplying) throw new InvalidOperationException("튜토리얼 준비 상태가 아닙니다.");
            Gold = !completion && Before.RewardGranted == false ? 5000L : 0L;
            if (!Before.CanAdd(Gold)) throw new InvalidOperationException("시작 보상 금액의 범위를 확인할 수 없습니다.");
            StaffAdded = !completion && !Source.Staff.Any(s => s.Id == "STAFF11");
            Result = StaffAdded ? new StaffAccountSaveData(Source.Version, Source.Staff.Concat(new[] { new StaffAccountStaffRecord("STAFF11", 1) }).ToArray(), Source.PandaTokens, Source.GachaEconomy) : Source;
            _stageStaff = _stage.CaptureStaffRuntimeSnapshot();
            _beforePlacement = _stage.CaptureTutorialPlacement();
            var placements = JObject.Parse(_beforePlacement);
            string occupied = (string)placements["Floor1"]?["Marketer"];
            if (occupied == null || (occupied.Length != 0 && occupied != "STAFF11")) throw new InvalidOperationException("기존 홍보 직원 배치를 덮어쓸 수 없습니다.");
            // Do not duplicate an existing STAFF11 placement elsewhere in this Stage.
            if (placements.Properties().Any(f => ((JObject)f.Value).Properties().Any(p => (string)p.Value == "STAFF11" && (f.Name != "Floor1" || p.Name != "Marketer"))))
                throw new InvalidOperationException("기본 직원의 기존 배치 위치를 확인해 주세요.");
            placements["Floor1"]["Marketer"] = "STAFF11";
            _afterPlacement = placements.ToString(Newtonsoft.Json.Formatting.None);
            _stageRow = collection.Stages.Single(s => s.Stage == EStage.Stage1).RowInDate;
            if (string.IsNullOrWhiteSpace(_stageRow)) throw new InvalidOperationException("Stage1 저장 대상이 없습니다.");
        }

        internal void AttachRequest(GameDataSaveRequest request) => Request = request;
        public bool TryClaimPresentation(object presenter)
        {
            if (presenter == null || !IsPrepared || !IsCurrent || _completion || (_presenter != null && !ReferenceEquals(_presenter, presenter))) return false;
            _presenter = presenter; return true;
        }
        public void ReleasePresentation(object presenter) { if (ReferenceEquals(_presenter, presenter)) _presenter = null; }
        public bool TryMarkPresentationStarted(object presenter)
        {
            if (!IsCurrent || !IsPrepared || presenter == null || !ReferenceEquals(_presenter, presenter) || HasStartedPresentation) return false;
            HasStartedPresentation = true;
            return true;
        }
        internal bool HasPresenter => _presenter != null;
        private bool Protected => ReferenceEquals(_coordinator.ActiveFirstTutorial, this);

        internal Param CreateValuesUnderProtection()
        {
            string error = null;
            if (!Protected || !ValidateSource(out error)) throw new InvalidOperationException(error ?? "튜토리얼 저장 보호가 없습니다.");
            Param values;
            if (_completion)
            {
                values = _owner.CreateLatestFirstTutorialValues();
                values.Remove("IsFirstTutorialClear"); values.Add("IsFirstTutorialClear", true);
            }
            else
            {
                values = new Param();
                StaffAccountSaveConverter.TrySerialize(Result, out string json, out error);
                if (error != null) throw new InvalidOperationException(error);
                values.Add(GameDataRestoreContext.StaffAccountFieldName, json);
                values.Add(GameDataRestoreContext.FirstTutorialStartRewardGrantedFieldName, true);
                var current = _state.Read();
                if (!current.CanAdd(Gold)) throw new InvalidOperationException("보상 합산 범위를 초과했습니다.");
                values.Add("Money", checked(current.Money + Gold)); values.Add("TotalAddMoney", checked(current.Total + Gold));
                values.Add("DailyAddMoney", checked(current.Daily + Gold)); values.Add("WeeklyAddMoney", checked(current.Weekly + Gold));
            }
            if (!GameDataSavePayload.TryCapture(values, out _payload, out error)) throw new InvalidOperationException(error);
            return values;
        }
        internal string ValidateTransmission(GameDataSaveIdentity identity, GameDataSavePayload payload)
        {
            if (!Protected || CoreCommitted || !Identity.Matches(identity) || _payload == null || payload.Json != _payload.Json
                || _coordinator.State != GameDataSaveCoordinatorState.Sending) return "현재 튜토리얼 요청의 저장 권한이 아닙니다.";
            return ValidateSource(out string error) ? null : error;
        }
        private bool ValidateSource(out string error)
        {
            error = null;
            var current = _state.Read();
            if (!IsCurrent || !ReferenceEquals(_owner.StaffRuntime.Snapshot, Source) || current.Clear != Before.Clear
                || current.RewardGranted != Before.RewardGranted || !current.CanAdd(Gold)
                || !ReferenceEquals(_state.Stage1, _stage) || !_stageStaff.HasSameState(_stage.CaptureStaffRuntimeSnapshot())
                || _stage.CaptureTutorialPlacement() != _beforePlacement)
            { error = "튜토리얼 계정·직원·배치·지급 상태가 변경되었습니다."; return false; }
            return true;
        }
        internal bool ValidateCoreCommit(GameDataSaveReceipt receipt, out string error)
        {
            error = null;
            if (!_committing || !Protected || CoreCommitted || !ReferenceEquals(receipt, _coordinator.LastReceipt)
                || _coordinator.State != GameDataSaveCoordinatorState.ApplyingConfirmedState || receipt == null
                || !receipt.SendStarted || receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed
                || !Identity.Matches(receipt.Identity) || receipt.Payload.Json != _payload?.Json)
            { error = "같은 튜토리얼 저장 성공 근거가 없습니다."; return false; }
            return ValidateSource(out error);
        }
        internal void ConfirmGameData(GameDataSaveReceipt receipt)
        {
            if (CoreCommitted) return;
            _committing = true;
            try
            {
                if (!_owner.StaffRuntime.TryCommitFirstTutorial(this, receipt, _state, out string error)) throw new InvalidOperationException(error);
                CoreCommitted = true;
                // Receipt and core state are committed before any observer. Keep Stage/mutation protection.
                try { _state.NotifyReward(StaffAdded, Gold); } catch (Exception ex) { UnityEngine.Debug.LogWarning("[FirstTutorial] Reward observer: " + ex.GetType().Name); }
                _owner.RequestGameDataAutosave(requireGameplay: false); // Preserve any normal income arriving during the fixed request.
            }
            finally { _committing = false; }
        }
        internal bool IsCompletion => _completion;
        internal void AfterGameDataConfirmed()
        {
            if (_stageSendStarted || IsPrepared || IsCompleted) return;
            if (!Protected || !IsCurrent || !CoreCommitted) { Error = "저장 후 튜토리얼 계정 상태가 변경되었습니다."; _coordinator.FinishFirstTutorialStage(this, false, Error); return; }
            if (_completion) { Finish(); return; }
            var values = new Param(); values.Add("EquipStaffDataDic", JObject.Parse(_afterPlacement).ToObject<Dictionary<string, Dictionary<string, string>>>());
            try
            {
                _stageSendStarted = true; // Retained even if the transport throws after starting.
                _owner.SendFirstTutorialPlacement(this, new GameDataSaveTarget(Identity.Target.AccountInDate, _stageRow), values, response =>
                {
                    if (_stageReplyConsumed) return; _stageReplyConsumed = true;
                    PlacementResponse = response;
                    if (!IsCurrent || !Protected || response == null || !response.IsSuccess)
                    { Error = "직원 배치 저장 결과를 확정하지 못했습니다. 보상은 다시 지급하지 않습니다."; _coordinator.FinishFirstTutorialStage(this, false, Error); return; }
                    if (!_stage.TryCommitTutorialPlacement(this, _staff, _beforePlacement, out string error))
                    { Error = error; _coordinator.FinishFirstTutorialStage(this, false, error); return; }
                    Finish();
                });
            }
            catch (Exception ex) { Error = "배치 전송 결과 확인 필요: " + ex.GetType().Name; _coordinator.FinishFirstTutorialStage(this, false, Error); }
        }
        internal bool CanCommitPlacement(StageInfo stage) => CoreCommitted && IsCurrent && Protected && ReferenceEquals(stage, _stage)
            && PlacementResponse?.IsSuccess == true && _stageStaff.HasSameState(stage.CaptureStaffRuntimeSnapshot());
        private void Finish()
        {
            if (_completion) { IsCompleted = true; CompletionCount = 1; }
            else IsPrepared = true;
            _coordinator.FinishFirstTutorialStage(this, true, null);
            if (!_completion) { try { _stage.NotifyTutorialPlacement(); } catch (Exception ex) { UnityEngine.Debug.LogWarning(ex.GetType().Name); } }
        }
    }
}
