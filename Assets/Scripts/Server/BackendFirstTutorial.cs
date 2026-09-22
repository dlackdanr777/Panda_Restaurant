using System;
using System.Collections.Generic;
using System.Linq;
using BackEnd;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    public partial class BackendManager
    {
        private IFirstTutorialState _firstTutorialState;
        private IFirstTutorialStageTransport _firstTutorialStageTransport;
        private FirstTutorialExecution _firstTutorialStart;
        private FirstTutorialExecution _firstTutorialCompletion;
        private bool _startingFirstTutorial;
        private GameDataRestoreQuery _deferredTutorialStageSave;
        private Dictionary<string, string> _tutorialStageInitialSends;
        private Dictionary<string, string> _tutorialStageInitialErrors;
        private Dictionary<object, string> _ordinaryStage1Writes;
        private HashSet<object> _ordinaryStage1WriteFailures;
        public string FirstTutorialInitialStageError => _tutorialStageInitialErrors != null
            && _tutorialStageInitialErrors.TryGetValue(GameDataRestore.LegacyQuery?.AccountInDate ?? string.Empty, out var error) ? error : null;
        private void RecordInitialStageError(string account, string error)
        {
            if (_tutorialStageInitialErrors == null) _tutorialStageInitialErrors = new Dictionary<string, string>();
            _tutorialStageInitialErrors[account] = error;
        }
        public FirstTutorialExecution CurrentFirstTutorial => _firstTutorialStart;
        public FirstTutorialExecution FirstTutorialCompletion => _firstTutorialCompletion;
        public bool IsFirstTutorialProtected => GameDataSaveCoordinator.IsFirstTutorialTargetProtected(GameDataRestore.LegacyTarget);
        private IFirstTutorialState TutorialState => _firstTutorialState ?? (IsOfflineOwner
            ? throw new InvalidOperationException("Offline tutorial state must be explicitly injected.")
            : (_firstTutorialState = new UserFirstTutorialState()));
        private IFirstTutorialStageTransport TutorialStageTransport => _firstTutorialStageTransport ?? (IsOfflineOwner
            ? throw new InvalidOperationException("Offline tutorial Stage transport must be explicitly injected.")
            : (_firstTutorialStageTransport = new NativeFirstTutorialStageTransport()));

        public bool TryPrepareFirstTutorial(out FirstTutorialExecution operation, out FirstTutorialPreparationStatus status, out string error)
        {
            operation = null; error = null; status = FirstTutorialPreparationStatus.Waiting;
            if (_startingFirstTutorial) return false;
            var query = GameDataRestore.LegacyQuery;
            if (query == null)
            {
                bool waiting = GameDataRestoreResult.Status == GameDataRestoreStatus.QueryPending || GameDataRestoreResult.Status == GameDataRestoreStatus.Restoring
                    || GameDataRestoreResult.Status == GameDataRestoreStatus.CreatingInitialData;
                status = waiting ? FirstTutorialPreparationStatus.Waiting : FirstTutorialPreparationStatus.Blocked;
                error = GameDataRestoreResult.Reason; return false;
            }
            var state = TutorialState;
            if (state.Read().Clear) { status = FirstTutorialPreparationStatus.AlreadyCompleted; return true; }
            if (_firstTutorialStart != null && ReferenceEquals(_firstTutorialStart.Query, query))
            {
                operation = _firstTutorialStart;
                if (!operation.IsCurrent || operation.IsFailed) { status = FirstTutorialPreparationStatus.Blocked; error = operation.Error ?? operation.Request?.Error ?? "튜토리얼 저장 상태 확인이 필요합니다."; return false; }
                if (operation.IsPrepared && !StaffRuntime.CanMutate)
                {
                    var saveState = CurrentGameDataSaveCoordinator?.State;
                    status = saveState == GameDataSaveCoordinatorState.Indeterminate || saveState == GameDataSaveCoordinatorState.LocalCompletionFailed
                        || saveState == GameDataSaveCoordinatorState.InvalidatedAfterSend ? FirstTutorialPreparationStatus.Blocked : FirstTutorialPreparationStatus.Waiting;
                    error = CurrentGameDataSaveCoordinator?.LastError; return false;
                }
                status = operation.IsPrepared ? FirstTutorialPreparationStatus.Ready : FirstTutorialPreparationStatus.Waiting; return true;
            }
            if (!TryReadFirstTutorialPreparation(query, out var collection, out var coordinator, out var staff, out status, out error)) return false;
            _startingFirstTutorial = true;
            try
            {
                operation = new FirstTutorialExecution(this, coordinator, state, query, collection, GameDataRestore.LegacyTarget, staff);
                _firstTutorialStart = operation; _firstTutorialCompletion = null;
                operation.AttachRequest(coordinator.StartFirstTutorial(operation));
                if (operation.IsFailed) { status = FirstTutorialPreparationStatus.Blocked; error = operation.Request?.Error; return false; }
                status = operation.IsPrepared ? FirstTutorialPreparationStatus.Ready : FirstTutorialPreparationStatus.Waiting;
                return true;
            }
            catch (Exception ex) { status = FirstTutorialPreparationStatus.Blocked; error = ex.Message; return false; }
            finally { _startingFirstTutorial = false; }
        }

        private bool TryReadFirstTutorialPreparation(GameDataRestoreQuery query, out StaffStageMigrationCollection collection,
            out GameDataSaveCoordinator coordinator, out StaffData staff, out FirstTutorialPreparationStatus status, out string error)
        {
            collection = StageMigrationCollection; coordinator = null; staff = null; error = null; status = FirstTutorialPreparationStatus.Waiting;
            if (FirstTutorialInitialStageError != null) { error = FirstTutorialInitialStageError; status = FirstTutorialPreparationStatus.Blocked; return false; }
            if (collection == null || !ReferenceEquals(collection.Query, query)) return false;
            if (!collection.RefreshValidity()) { status = FirstTutorialPreparationStatus.Blocked; error = "현재 Stage 준비 회차가 아닙니다."; return false; }
            if (collection.Stages.Any(s => s.Status != StaffStageRawStatus.Pending && !s.CanApplyLegacy)
                || collection.Applications.Any(a => a.Status == StaffStageApplicationStatus.Failed))
            { status = FirstTutorialPreparationStatus.Blocked; error = "Stage 원본 또는 메모리 적용을 확인하지 못했습니다."; return false; }
            if (collection.RequiredStages.Count != 3 || collection.Applications.Any(a => a.Status != StaffStageApplicationStatus.Succeeded)) return false;
            if (_ordinaryStage1Writes != null && _ordinaryStage1Writes.Values.Any(account => account == query.AccountInDate))
            {
                bool unknown = _ordinaryStage1Writes.Any(p => p.Value == query.AccountInDate && _ordinaryStage1WriteFailures?.Contains(p.Key) == true);
                if (unknown) status = FirstTutorialPreparationStatus.Blocked;
                error = unknown ? "먼저 전송된 Stage1 저장 결과 확인이 필요합니다." : "먼저 요청된 Stage1 저장 결과를 기다리고 있습니다.";
                return false;
            }
            if (StaffRuntime.Mode != StaffAccountRuntimeMode.Common)
            {
                if (CurrentStaffMigrationExecution != null && (CurrentStaffMigrationExecution.Status == StaffMigrationExecutionStatus.Waiting
                    || CurrentStaffMigrationExecution.Status == StaffMigrationExecutionStatus.Sending || CurrentStaffMigrationExecution.Status == StaffMigrationExecutionStatus.Validating)) return false;
                status = FirstTutorialPreparationStatus.Blocked; error = "공용 직원 준비 또는 이전 저장 완료를 확인해 주세요."; return false;
            }
            coordinator = GetGameDataSaveCoordinator();
            if (coordinator == null || !coordinator.CanStartPurchase || !StaffRuntime.CanMutate)
            {
                bool stopped = coordinator != null && (coordinator.State == GameDataSaveCoordinatorState.Indeterminate
                    || coordinator.State == GameDataSaveCoordinatorState.LocalCompletionFailed || coordinator.State == GameDataSaveCoordinatorState.InvalidatedAfterSend);
                status = stopped ? FirstTutorialPreparationStatus.Blocked : FirstTutorialPreparationStatus.Waiting;
                error = "진행 중인 저장·우편·이전·구매 상태를 확인하고 있습니다."; return false;
            }
            var catalog = GameDataTransport.ReadCatalog();
            if (GameDataRestoreContext.TryBuildStaffMaximums(() => catalog, out _, out error) != GameDataRestoreStatus.Ready)
            { status = FirstTutorialPreparationStatus.Blocked; return false; }
            staff = catalog.SingleOrDefault(s => s.Id == "STAFF11");
            if (staff == null || StaffDataManager.GetStaffGroupTypeFromData(staff) != StaffGroupType.Marketer)
            { status = FirstTutorialPreparationStatus.Blocked; error = "기본 홍보 직원 자료가 올바르지 않습니다."; return false; }
            return true;
        }

        public bool TryCompleteFirstTutorial(FirstTutorialExecution start, bool skipped, out FirstTutorialExecution completion, out string error)
        {
            completion = null; error = null;
            if (start == null || !ReferenceEquals(start, _firstTutorialStart) || !start.IsPrepared || !start.IsCurrent || !start.HasPresenter)
            { error = "현재 명시적으로 시작한 튜토리얼이 아닙니다."; return false; }
            if (_firstTutorialCompletion != null) { completion = _firstTutorialCompletion; return !completion.IsFailed; }
            if (!TryReadFirstTutorialPreparation(start.Query, out var collection, out var coordinator, out var staff, out var preparationStatus, out error))
            { if (preparationStatus == FirstTutorialPreparationStatus.Waiting) error = null; return false; }
            try
            {
                completion = new FirstTutorialExecution(this, coordinator, TutorialState, start.Query, collection, start.Identity.Target, staff, true, skipped);
                _firstTutorialCompletion = completion;
                completion.AttachRequest(coordinator.StartFirstTutorial(completion)); return !completion.IsFailed;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        internal bool IsCurrentFirstTutorial(FirstTutorialExecution operation) => operation != null
            && (ReferenceEquals(operation, _firstTutorialStart) || ReferenceEquals(operation, _firstTutorialCompletion))
            && ReferenceEquals(operation.Collection, StageMigrationCollection) && operation.Collection.RefreshValidity()
            && IsCurrentGameDataSaveSession(operation.Query, operation.Identity.Target)
            && StaffRuntime.Mode == StaffAccountRuntimeMode.Common;
        internal Param CreateLatestFirstTutorialValues()
        {
            var values = GameDataTransport.LatestValues();
            if (!StaffRuntime.TryAddSaveField(values, out string error)) throw new InvalidOperationException(error);
            return values;
        }
        private bool ValidateFirstTutorialSave(GameDataSavePayload payload, out string error)
        {
            error = null;
            // Only inspect the new marker unless this owner has an explicit tutorial operation.
            // Older partial saves cannot roll the marker/clear flag/gold back after a confirmed start.
            var fields = JObject.Parse(payload.Json);
            var marker = fields.Property(GameDataRestoreContext.FirstTutorialStartRewardGrantedFieldName);
            if (marker == null && _firstTutorialStart == null) return true;
            var state = TutorialState.Read();
            if (marker != null && (marker.Value.Type != JTokenType.Boolean || !state.RewardGranted.HasValue || (bool)marker.Value != state.RewardGranted.Value))
            { error = "튜토리얼 지급 근거가 현재 복원·확정 상태와 다릅니다."; return false; }
            if (_firstTutorialStart != null && ReferenceEquals(_firstTutorialStart.Query, GameDataRestore.LegacyQuery))
            {
                var expected = new Dictionary<string, object> { ["Money"] = state.Money, ["TotalAddMoney"] = state.Total,
                    ["DailyAddMoney"] = state.Daily, ["WeeklyAddMoney"] = state.Weekly, ["IsFirstTutorialClear"] = state.Clear };
                foreach (var pair in expected)
                    if (fields[pair.Key] != null && !JToken.DeepEquals(fields[pair.Key], JToken.FromObject(pair.Value)))
                    { error = "튜토리얼 이후의 최신 재화·완료 상태와 명시 저장값이 다릅니다."; return false; }
            }
            return true;
        }

        internal void SendFirstTutorialPlacement(FirstTutorialExecution operation, GameDataSaveTarget stageTarget, Param values, Action<GameDataRawResponse> reply)
        {
            if (!IsCurrentFirstTutorial(operation) || !ReferenceEquals(_gameDataSaveCoordinator?.ActiveFirstTutorial, operation)
                || _gameDataSaveCoordinator.State != GameDataSaveCoordinatorState.WaitingForTutorialStage || _gameDataInitializationPolicy?.IsSupported != true)
                throw new InvalidOperationException("튜토리얼 배치의 현재 보호·초기화 근거가 없습니다.");
            TutorialStageTransport.UpdatePlacement(stageTarget, values, response => reply(ToRawResponse(response)));
        }

        // Existing Stage autosaves retain intent, not an old full Stage payload that could erase the saved placement.
        private Func<bool> BindFirstTutorialStageSaveGuard(string table, Param values, Func<bool> existing)
        {
            if (table != "Stage1Data") return existing;
            var query = GameDataRestore.LegacyQuery;
            return () =>
            {
                if (existing != null && !existing()) return false;
                if (_firstTutorialStart == null || !ReferenceEquals(_firstTutorialStart.Query, query)) return true;
                if (!GameDataRestore.IsCurrent(query)) return false;
                bool stale = false;
                try
                {
                    var placement = JObject.Parse(values.GetJson())["EquipStaffDataDic"];
                    stale = placement != null && !JToken.DeepEquals(placement, JObject.Parse(TutorialState.Stage1.CaptureTutorialPlacement()));
                }
                catch { stale = true; }
                if (IsFirstTutorialProtected || stale) { _deferredTutorialStageSave = query; return false; }
                return true;
            };
        }
        private void Update()
        {
            if (_deferredTutorialStageSave == null) return;
            if (!GameDataRestore.IsCurrent(_deferredTutorialStageSave)) { _deferredTutorialStageSave = null; return; }
            if (!StaffRuntime.CanMutate || IsFirstTutorialProtected || IsOfflineOwner) return;
            _deferredTutorialStageSave = null;
            UserInfo.SaveStageDataAsync(EStage.Stage1); // Fresh payload; no lost auto-save intent or stale replay.
        }

        // A Stage write already sent before tutorial protection must finish first. Merely rejecting its
        // late callback would not stop the server from overwriting the later placement. Unknown writes stay pending.
        private void ObserveOrdinaryStageWrite(string table, Action<Action<BackendReturnObject>> send, Action<BackendReturnObject> reply)
        {
            string account = GameDataRestore.LegacyQuery?.AccountInDate;
            if (table != "Stage1Data" || account == null) { send(reply); return; }
            object key = new object();
            if (_ordinaryStage1Writes == null) _ordinaryStage1Writes = new Dictionary<object, string>();
            if (_ordinaryStage1WriteFailures == null) _ordinaryStage1WriteFailures = new HashSet<object>();
            _ordinaryStage1Writes.Add(key, account);
            try { send(response =>
            {
                if (response != null && response.IsSuccess()) { _ordinaryStage1Writes.Remove(key); _ordinaryStage1WriteFailures.Remove(key); }
                else _ordinaryStage1WriteFailures.Add(key);
                reply?.Invoke(response);
            }); }
            catch { _ordinaryStage1WriteFailures.Add(key); throw; }
        }
        private BackendReturnObject ObserveOrdinaryStageWrite(string table, Func<BackendReturnObject> send)
        {
            BackendReturnObject result = null;
            ObserveOrdinaryStageWrite(table, reply => reply(send()), response => result = response);
            return result;
        }

        private sealed class NativeFirstTutorialStageTransport : IFirstTutorialStageTransport
        {
            public void InsertInitial(EStage stage, Param values, Action<BackendReturnObject> response) => Backend.GameData.Insert(stage + "Data", values, bro => response(bro));
            public void UpdatePlacement(GameDataSaveTarget target, Param values, Action<BackendReturnObject> response) => Backend.GameData.UpdateV2("Stage1Data", target.RowInDate, target.AccountInDate, values, bro => response(bro));
        }

        // Called before Capture only for a positively empty SDK page on this session's confirmed new signup.
        // This is not the Legacy migration fallback, and a retry cannot repeat an attempted Insert.
        private bool TryInitializeNewTutorialStage(StaffStageMigrationCollection collection, EStage stage, BackendReturnObject response, Action<BackendReturnObject> receive)
        {
            if (!GameDataRestore.HasConfirmedInitialCreation(collection.Query) || TutorialState.Read().RewardGranted != false
                || TutorialState.Read().Clear || response == null || !response.IsSuccess()) return false;
            JObject raw;
            try { raw = JObject.Parse(response.GetReturnValue()); } catch { return false; }
            if (!(raw["rows"] is JArray rows) || rows.Count != 0 || raw["firstKey"] != null && raw["firstKey"].Type != JTokenType.Null) return false;
            string key = collection.Query.AccountInDate + ":" + stage;
            if (_tutorialStageInitialSends == null) _tutorialStageInitialSends = new Dictionary<string, string>();
            if (_tutorialStageInitialSends.ContainsKey(key)) { RecordInitialStageError(collection.Query.AccountInDate, "신규 Stage 생성 결과가 확인되지 않아 다시 전송하지 않습니다."); return true; }
            if (!collection.RefreshValidity() || _gameDataInitializationPolicy?.IsSupported != true) return true;
            _tutorialStageInitialSends.Add(key, "Sending");
            bool answered = false;
            try
            {
                TutorialStageTransport.InsertInitial(stage, new StageInfo().SaveData().GetParam(), bro =>
                {
                    if (answered) return; answered = true;
                    if (!collection.RefreshValidity() || bro == null || !bro.IsSuccess())
                    { _tutorialStageInitialSends[key] = "Indeterminate"; RecordInitialStageError(collection.Query.AccountInDate, "신규 Stage 생성 결과 확인이 필요합니다. 재전송하지 않습니다."); return; }
                    _tutorialStageInitialSends[key] = "Confirmed";
                    StageDataTransport.Get(stage, collection.Query.AccountInDate, () => collection.RefreshValidity(), receive);
                });
            }
            catch { _tutorialStageInitialSends[key] = "Indeterminate"; RecordInitialStageError(collection.Query.AccountInDate, "신규 Stage 생성 전송 결과가 미확정입니다."); }
            return true;
        }
    }
}
