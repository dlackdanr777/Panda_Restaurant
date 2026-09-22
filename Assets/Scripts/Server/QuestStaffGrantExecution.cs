using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using UnityEngine;

namespace Muks.BackEnd
{
    /// <summary>One free current-quest grant. Saved evidence is separate from the in-memory request lifetime.</summary>
    public sealed class QuestStaffGrantExecution
    {
        private readonly BackendManager _owner;
        private readonly GameDataSaveCoordinator _coordinator;
        private readonly IQuestStaffTutorialState _state;
        private readonly QuestStaffOffer _offer;
        private bool _confirming;
        private string _staffDefinition;
        private GameDataSavePayload _payload;
        private GachaStaffData _display;
        public GameDataRestoreQuery Query { get; }
        public GameDataSaveIdentity Identity { get; }
        public GameDataSaveRequest Request { get; private set; }
        public QuestStaffTutorialMemory Before { get; }
        public string QuestId => _offer.QuestId;
        public StaffAccountSaveData Source { get; private set; }
        public StaffAccountSaveData Result { get; private set; }
        public int GrantedMask { get; private set; }
        public int DiamondCost => 0;
        public StaffGachaAcquisitionResult Acquisition { get; private set; }
        public IReadOnlyList<GachaStaffData> DisplayStaff { get; private set; }
        public bool LocalCompleted { get; private set; }
        public int CompletionCount { get; private set; }
        public string NotificationError { get; internal set; }
        public string Error => Request?.Error;

        internal QuestStaffGrantExecution(BackendManager owner, GameDataSaveCoordinator coordinator,
            GameDataRestoreQuery query, GameDataSaveTarget target, IQuestStaffTutorialState state,
            QuestStaffTutorialMemory before, QuestStaffOffer offer)
        {
            _owner = owner; _coordinator = coordinator; Query = query; _state = state; Before = before; _offer = offer;
            Identity = new GameDataSaveIdentity("quest-staff:" + Guid.NewGuid().ToString("N"), target);
        }
        internal void AttachRequest(GameDataSaveRequest request) => Request = request;
        private bool Protected => ReferenceEquals(_coordinator.ActiveQuestStaffGrant, this);
        internal static bool ValidateCatalog(IReadOnlyList<StaffData> catalog, StaffAccountSaveData source,
            string id, out StaffData staff, out string error)
        {
            staff = null;
            if (!StaffAccountSaveConverter.Validate(source, out error)
                || GameDataRestoreContext.TryBuildStaffMaximums(() => catalog, out var maximums, out error) != GameDataRestoreStatus.Ready
                || GameDataRestoreContext.ValidateStaffLevels(source.Staff, maximums, out error) != GameDataRestoreStatus.Ready) return false;
            staff = catalog.SingleOrDefault(s => s.Id == id);
            if (staff == null || !Enum.IsDefined(typeof(Rank), staff.Rank))
            { error = "현재 퀘스트의 등록 직원 자료를 확인할 수 없습니다."; return false; }
            return true;
        }
        internal Param CreateValuesUnderProtection()
        {
            if (!Protected || !_owner.IsCurrentQuestStaffGrant(this) || !Before.IsEligible
                || Before.RequiredStaffId != _offer.StaffId || !Before.Matches(_state.Read()))
                throw new InvalidOperationException("현재 무료 직원 획득 보호·퀘스트 근거가 없습니다.");
            Source = _owner.StaffRuntime.Snapshot;
            if (Source.Staff.Any(s => s.Id == _offer.StaffId)) throw new InvalidOperationException("이미 보유한 직원에게 무료 중복 보상을 지급하지 않습니다.");
            if (!ValidateCatalog(_owner.ReadQuestStaffCatalog(), Source, _offer.StaffId, out var staff, out string error)
                || !ReferenceEquals(staff, _offer.StaffData)) throw new InvalidOperationException(error ?? "직원 등록 자료가 변경됐습니다.");
            _staffDefinition = JsonUtility.ToJson(staff);
            if (!Before.TryCreateGrantedMask(out int mask, out error)) throw new InvalidOperationException(error);
            GrantedMask = mask;
            _display = GachaStaffData.Create(staff); _display.hideFlags = HideFlags.HideAndDontSave;
            DisplayStaff = Array.AsReadOnly(new[] { _display });
            // No selector or price/duplicate policy copy. Only an unowned fixed employee reaches this calculator.
            if (!StaffGachaAccountApplyCalculator.TryCalculate(Source, DisplayStaff, out var result, out error))
                throw new InvalidOperationException(error);
            Result = result.UpdatedAccount; Acquisition = result.Acquisition;
            if (Acquisition.NewStaffIds.Count != 1 || Acquisition.TotalPandaTokens != 0 || Result.PandaTokens != Source.PandaTokens)
                throw new InvalidOperationException("무료 직원 획득 결과가 신규 한 명과 일치하지 않습니다.");
            if (!StaffAccountSaveConverter.TrySerialize(Result, out string json, out error)) throw new InvalidOperationException(error);
            var values = new Param(); values.Add(GameDataRestoreContext.StaffAccountFieldName, json);
            values.Add(QuestStaffTutorialPolicy.GrantFieldName, GrantedMask);
            if (!GameDataSavePayload.TryCapture(values, out _payload, out error) || !ValidateSource(out error)) throw new InvalidOperationException(error);
            return values;
        }
        private bool ValidateSource(out string error)
        {
            error = null;
            if (!Protected || !_owner.IsCurrentQuestStaffGrant(this) || !ReferenceEquals(Source, _owner.StaffRuntime.Snapshot)
                || !Before.IsEligible || Before.RequiredStaffId != _offer.StaffId || !Before.Matches(_state.Read()))
            { error = "무료 직원 획득의 계정·세대·직원·퀘스트 상태가 변경됐습니다."; return false; }
            if (!ValidateCatalog(_owner.ReadQuestStaffCatalog(), Source, _offer.StaffId, out var staff, out error)) return false;
            if (!ReferenceEquals(staff, _offer.StaffData) || JsonUtility.ToJson(staff) != _staffDefinition
                || !_owner.IsCurrentQuestStaffGrant(this) || !Before.Matches(_state.Read())
                || !ReferenceEquals(Source, _owner.StaffRuntime.Snapshot))
            { error = "무료 획득 확인 중 원본 상태가 변경됐습니다."; return false; }
            return true;
        }
        internal string ValidateTransmission(GameDataSaveIdentity identity, GameDataSavePayload payload)
        {
            if (LocalCompleted || !Protected || _coordinator.State != GameDataSaveCoordinatorState.Sending
                || !Identity.Matches(identity) || _payload == null || payload.Json != _payload.Json)
                return "현재 무료 직원 획득 요청의 고정 전송 자료가 아닙니다.";
            return ValidateSource(out string error) ? null : error;
        }
        internal bool ValidateCommit(GameDataSaveReceipt receipt, out string error)
        {
            error = null;
            if (!_confirming || LocalCompleted || !Protected || receipt == null || !ReferenceEquals(receipt, _coordinator.LastReceipt)
                || _coordinator.State != GameDataSaveCoordinatorState.ApplyingConfirmedState || !receipt.SendStarted
                || receipt.Disposition != GameDataSaveDisposition.SuccessConfirmed || !Identity.Matches(receipt.Identity)
                || _payload == null || receipt.Payload.Json != _payload.Json)
            { error = "같은 무료 직원 획득 요청의 저장 성공 근거가 없습니다."; return false; }
            return ValidateSource(out error);
        }
        internal void Confirm(GameDataSaveReceipt receipt)
        {
            if (LocalCompleted) return;
            _confirming = true;
            try
            {
                if (!_owner.StaffRuntime.TryCommitQuestStaffGrant(this, receipt, _state, out string error)) throw new InvalidOperationException(error);
                LocalCompleted = true; CompletionCount = 1;
                _owner.NotifyQuestStaffGrantCompleted(this, _state);
            }
            finally { _confirming = false; }
        }
        // Called only by unsent rejection or Backend lifetime cleanup, never by closing a card.
        internal void ReleaseDisplayData()
        {
            if (_display != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_display); else UnityEngine.Object.DestroyImmediate(_display);
            }
            _display = null; DisplayStaff = null;
        }
    }
}
