using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    /// <summary>
    /// One fixed GameData query/collection round's pre-correction Stage responses.
    /// This object neither loads legacy state nor saves/applies its migration candidate.
    /// Main-thread only; the caller must check the fixed query again before applying legacy data.
    /// </summary>
    public sealed class StaffStageMigrationCollection
    {
        private static readonly EStage[] Scope = { EStage.Stage1, EStage.Stage2, EStage.Stage3 };
        private readonly bool _migrationRequired;
        private readonly Func<bool> _isCurrent;
        private readonly StaffStageRawResult[] _stages = new StaffStageRawResult[Scope.Length];
        private readonly IReadOnlyList<EStage> _requiredStages = Array.AsReadOnly((EStage[])Scope.Clone());
        private IReadOnlyDictionary<string, int> _maximums;
        private GameDataRestoreStatus _catalogStatus;
        private string _catalogError;
        private StaffStageMigrationCollectionStatus _status;
        private StaffAccountLoadResult _result;
        private IReadOnlyList<StaffAccountMigrationIssue> _migrationIssues =
            Array.AsReadOnly(Array.Empty<StaffAccountMigrationIssue>());
        private string _error;
        private bool _invalidScopeResponse;

        public GameDataRestoreQuery Query { get; }
        public long Round { get; }
        public IReadOnlyList<EStage> RequiredStages => _requiredStages;
        public int CompletedStageCount { get; private set; }
        // A required round finalizes once, including a round blocked before calling the planner.
        public int CompletionCount { get; private set; }
        public int PlanCount { get; private set; }
        public StaffStageMigrationCollectionStatus Status { get { RefreshCurrent(); return _status; } }
        public StaffAccountLoadResult Result { get { RefreshCurrent(); return _result; } }
        public string Error { get { RefreshCurrent(); return _error; } }
        public IReadOnlyList<StaffAccountMigrationIssue> MigrationIssues
        {
            get { RefreshCurrent(); return _migrationIssues; }
        }
        public IReadOnlyList<StaffStageRawResult> Stages
        {
            get { RefreshCurrent(); return Array.AsReadOnly((StaffStageRawResult[])_stages.Clone()); }
        }

        public StaffStageMigrationCollection(GameDataRestoreQuery query, long round, bool migrationRequired,
            Func<bool> isCurrent, Func<IReadOnlyList<StaffData>> readCatalog)
        {
            Query = query ?? throw new ArgumentNullException(nameof(query));
            Round = round;
            _migrationRequired = migrationRequired;
            _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
            _status = migrationRequired ? StaffStageMigrationCollectionStatus.Collecting
                : StaffStageMigrationCollectionStatus.NotRequired;
            for (int index = 0; index < Scope.Length; index++)
                _stages[index] = new StaffStageRawResult(Scope[index], StaffStageRawStatus.Pending);
            if (!RefreshCurrent() || !migrationRequired) return;
            try
            {
                _catalogStatus = GameDataRestoreContext.TryBuildStaffMaximums(readCatalog,
                    out _maximums, out _catalogError);
            }
            catch
            {
                _maximums = null;
                _catalogStatus = GameDataRestoreStatus.InvalidStaffCatalog;
                _catalogError = "등록 직원의 최대 레벨 근거를 읽을 수 없습니다.";
            }
            if (!RefreshCurrent()) return;
            if (_catalogStatus != GameDataRestoreStatus.Ready)
            {
                _status = StaffStageMigrationCollectionStatus.Blocked;
                _error = _catalogError;
            }
        }

        /// <summary>
        /// The stage must be captured from the requested StageNData table, not inferred from the row or floor.
        /// A duplicate or stale response returns null and cannot replace the first terminal response.
        /// </summary>
        public StaffStageRawResult Capture(EStage stage, bool querySucceeded, string rawJson)
        {
            if (!RefreshCurrent()) return null;
            int index = Array.IndexOf(Scope, stage);
            if (index < 0)
            {
                _invalidScopeResponse = true;
                _result = null;
                _error = "요청한 Stage1/2/3 범위 밖의 응답입니다.";
                if (_migrationRequired) _status = StaffStageMigrationCollectionStatus.Blocked;
                return new StaffStageRawResult(stage, StaffStageRawStatus.InvalidStage, error: _error, rawJson: rawJson);
            }
            if (_stages[index].Status != StaffStageRawStatus.Pending) return null;
            StaffStageRawResult captured = ReadResponse(stage, querySucceeded, rawJson);
            // A supplied current-session predicate may itself reenter or invalidate this round.
            if (!RefreshCurrent() || _stages[index].Status != StaffStageRawStatus.Pending) return null;
            _stages[index] = captured;
            CompletedStageCount++;
            if (_migrationRequired && !captured.HasVerifiedRawRecords)
            {
                _status = StaffStageMigrationCollectionStatus.Blocked;
                if (_error == null) _error = captured.Error;
            }
            CompleteIfReady();
            return RefreshCurrent() ? captured : null;
        }

        // Blocked/NotRequired rounds may still load validated legacy envelopes; only stale rounds return false.
        public bool RefreshValidity() => RefreshCurrent();

        public void Invalidate()
        {
            _status = StaffStageMigrationCollectionStatus.Invalidated;
            _result = null;
            _maximums = null;
            if (_error == null)
                _error = "계정·인증·GameData 조회 또는 Stage 수집 회차가 변경되었습니다.";
        }

        private bool RefreshCurrent()
        {
            if (_status == StaffStageMigrationCollectionStatus.Invalidated) return false;
            bool current;
            try { current = _isCurrent(); }
            catch { current = false; }
            if (!current) Invalidate();
            return _status != StaffStageMigrationCollectionStatus.Invalidated;
        }

        private void CompleteIfReady()
        {
            if (!_migrationRequired || CompletedStageCount != Scope.Length || CompletionCount != 0) return;
            CompletionCount = 1;
            if (_invalidScopeResponse || _catalogStatus != GameDataRestoreStatus.Ready)
            {
                _status = StaffStageMigrationCollectionStatus.Blocked;
                return;
            }
            var sources = new List<StaffStageOwnershipSnapshot>();
            foreach (StaffStageRawResult stage in _stages)
            {
                if (!stage.HasVerifiedRawRecords)
                {
                    _status = StaffStageMigrationCollectionStatus.Blocked;
                    return;
                }
                // SaveStaffData is mutable: keep fresh instances private to this one planner call.
                var staff = new List<SaveStaffData>();
                foreach (StaffStageRawRecord record in stage.Records)
                {
                    var saved = new SaveStaffData(record.Id, record.Level.Value);
                    saved.SetSkinId(record.SkinId);
                    staff.Add(saved);
                }
                sources.Add(new StaffStageOwnershipSnapshot(stage.Stage, true, true, staff.AsReadOnly()));
            }
            if (!RefreshCurrent()) return;
            PlanCount = 1;
            StaffAccountLoadResult plan;
            try
            {
                plan = StaffAccountLoadPlanner.Plan(StaffAccountQueryStatus.SucceededFieldAbsent, null,
                    _requiredStages, sources.AsReadOnly(), _maximums);
            }
            catch
            {
                if (RefreshCurrent())
                {
                    _status = StaffStageMigrationCollectionStatus.Blocked;
                    _error = "기존 이전 계획을 계산할 수 없습니다. 후보를 반환하지 않습니다.";
                }
                return;
            }
            if (!RefreshCurrent()) return;
            _migrationIssues = plan.MigrationIssues;
            if (plan.Status == StaffAccountLoadStatus.UnsavedMigrationCandidate)
            {
                _result = plan;
                _status = StaffStageMigrationCollectionStatus.UnsavedMigrationCandidate;
            }
            else
            {
                _status = StaffStageMigrationCollectionStatus.Blocked;
                _error = plan.Error;
            }
        }

        private StaffStageRawResult ReadResponse(EStage stage, bool succeeded, string rawJson)
        {
            if (!succeeded)
                return Failure(stage, StaffStageRawStatus.QueryFailed, "Stage 조회가 성공하지 않았습니다.", rawJson);
            JObject root;
            try
            {
                if (string.IsNullOrWhiteSpace(rawJson)) throw new JsonReaderException();
                using (var text = new StringReader(rawJson))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader,
                        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new JsonReaderException();
                }
            }
            catch
            {
                return Failure(stage, StaffStageRawStatus.InvalidResponse,
                    "원본 응답이 손상되었거나 중복 필드/추가 내용을 포함합니다.", rawJson);
            }
            if (!(root["rows"] is JArray rows))
                return Failure(stage, StaffStageRawStatus.InvalidResponse, "원본 rows 배열이 없습니다.", rawJson);
            if (rows.Count > 1)
                return Failure(stage, StaffStageRawStatus.AmbiguousRows, "Stage 행이 여러 개입니다.", rawJson);
            JToken firstKey = root["firstKey"];
            if (firstKey != null && firstKey.Type != JTokenType.Null)
                return Failure(stage, StaffStageRawStatus.PaginationIncomplete, "추가 페이지가 남아 있습니다.", rawJson);
            if (rows.Count == 0)
                return Failure(stage, StaffStageRawStatus.MissingRow, "Stage 행이 없습니다. 빈 보유로 간주하지 않습니다.", rawJson);
            if (!(rows[0] is JObject row)
                || !TryString(row["owner_inDate"], "S", out string owner) || string.IsNullOrWhiteSpace(owner))
                return Failure(stage, StaffStageRawStatus.InvalidResponse, "owner_inDate 원본 형식이 잘못되었습니다.", rawJson);
            if (!string.Equals(owner, Query.AccountInDate, StringComparison.Ordinal))
                return Failure(stage, StaffStageRawStatus.AccountMismatch, "Stage 소유 계정이 다릅니다.", rawJson);
            if (!TryString(row["inDate"], "S", out string rowInDate) || string.IsNullOrWhiteSpace(rowInDate))
                return Failure(stage, StaffStageRawStatus.InvalidResponse, "inDate 원본 형식이 잘못되었습니다.", rawJson);
            if (!_migrationRequired)
                return new StaffStageRawResult(stage, StaffStageRawStatus.LegacyOnly, rowInDate, true, rawJson: rawJson);

            if (row.Property("GiveStaffList", StringComparison.Ordinal) == null)
                return Failure(stage, StaffStageRawStatus.MissingStaffField,
                    "GiveStaffList가 없습니다. 확인된 빈 목록과 구분합니다.", rawJson, rowInDate);
            if (!TryWrapped(row["GiveStaffList"], "L", out JToken listValue) || !(listValue is JArray list))
                return Failure(stage, StaffStageRawStatus.InvalidStaffRecords,
                    "GiveStaffList는 SDK L 배열이어야 합니다.", rawJson, rowInDate);

            var records = new List<StaffStageRawRecord>();
            string recordError = null;
            for (int index = 0; index < list.Count; index++)
            {
                JToken original = list[index];
                JObject fields = TryWrapped(original, "M", out JToken map) ? map as JObject : null;
                JToken idToken = fields?["Id"];
                JToken levelToken = fields?["Level"];
                bool skinPresent = fields?.Property("SkinId", StringComparison.Ordinal) != null;
                JToken skinToken = skinPresent ? fields["SkinId"] : null;
                bool validId = TryString(idToken, "S", out string id);
                bool validLevel = TryInteger(levelToken, out int level);
                string skin = null;
                bool validSkin = !skinPresent || TryString(skinToken, "S", out skin);
                records.Add(new StaffStageRawRecord(index, Raw(original), Raw(idToken), Raw(levelToken),
                    validId ? id : null, validLevel ? (int?)level : null, skinPresent, Raw(skinToken), skin));
                if (recordError == null && (fields == null || !validId || !validLevel || !validSkin))
                    recordError = "GiveStaffList[" + index + "]의 M/Id(S)/Level(N 정수)/SkinId(S) 원본 형식을 확인해야 합니다.";
            }
            if (recordError != null)
                return Failure(stage, StaffStageRawStatus.InvalidStaffRecords, recordError, rawJson, rowInDate, records);
            if (_catalogStatus != GameDataRestoreStatus.Ready)
                return Failure(stage, StaffStageRawStatus.InvalidStaffCatalog, _catalogError, rawJson, rowInDate, records);
            var levels = new List<StaffAccountStaffRecord>();
            foreach (StaffStageRawRecord record in records)
                levels.Add(new StaffAccountStaffRecord(record.Id, record.Level.Value));
            GameDataRestoreStatus validation = GameDataRestoreContext.ValidateStaffLevels(levels, _maximums, out string error);
            if (validation != GameDataRestoreStatus.Ready)
                return Failure(stage, validation == GameDataRestoreStatus.UnregisteredStaff
                    ? StaffStageRawStatus.UnregisteredStaff : StaffStageRawStatus.InvalidStaffLevel,
                    error, rawJson, rowInDate, records);
            return new StaffStageRawResult(stage, StaffStageRawStatus.VerifiedRawRecords, rowInDate, true, true,
                records: records, rawJson: rawJson);
        }

        private static StaffStageRawResult Failure(EStage stage, StaffStageRawStatus status, string error,
            string rawJson, string rowInDate = null, List<StaffStageRawRecord> records = null)
        {
            // A valid envelope can still follow the unchanged legacy loader; never use that loader's repaired records here.
            return new StaffStageRawResult(stage, status, rowInDate, rowInDate != null,
                error: error, records: records, rawJson: rawJson);
        }

        private static string Raw(JToken value) => value?.ToString(Formatting.None);

        private static bool TryWrapped(JToken value, string wrapper, out JToken inner)
        {
            inner = null;
            if (!(value is JObject obj) || obj.Count != 1 || obj.Property(wrapper, StringComparison.Ordinal) == null)
                return false;
            inner = obj[wrapper];
            return true;
        }

        private static bool TryString(JToken value, string wrapper, out string text)
        {
            text = null;
            if (!TryWrapped(value, wrapper, out JToken inner) || inner.Type != JTokenType.String) return false;
            text = (string)inner;
            return true;
        }

        private static bool TryInteger(JToken value, out int number)
        {
            number = 0;
            if (!TryString(value, "N", out string text)) return false;
            int start = text.Length > 0 && (text[0] == '-' || text[0] == '+') ? 1 : 0;
            if (text.Length == start) return false;
            for (int index = start; index < text.Length; index++)
                if (text[index] < '0' || text[index] > '9') return false;
            return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
        }
    }

    public enum StaffStageMigrationCollectionStatus
    {
        Collecting, NotRequired, Blocked, UnsavedMigrationCandidate, Invalidated
    }

    public enum StaffStageRawStatus
    {
        Pending, QueryFailed, InvalidStage, InvalidResponse, MissingRow, AmbiguousRows, PaginationIncomplete,
        AccountMismatch, MissingStaffField, InvalidStaffRecords, InvalidStaffCatalog, UnregisteredStaff,
        InvalidStaffLevel, VerifiedRawRecords, LegacyOnly
    }

    /// <summary>Immutable diagnostic only. Tokens retain missing/null/type distinctions; no mutable save object is exposed.</summary>
    public sealed class StaffStageRawRecord
    {
        public int RecordIndex { get; }
        public string RawRecordJson { get; }
        public string IdRawJson { get; }
        public string LevelRawJson { get; }
        public string Id { get; }
        public int? Level { get; }
        public bool SkinIdPresent { get; }
        public string SkinIdRawJson { get; }
        public string SkinId { get; }
        internal StaffStageRawRecord(int index, string rawRecord, string idRaw, string levelRaw, string id,
            int? level, bool skinPresent, string skinRaw, string skin)
        {
            RecordIndex = index;
            RawRecordJson = rawRecord;
            IdRawJson = idRaw;
            LevelRawJson = levelRaw;
            Id = id;
            Level = level;
            SkinIdPresent = skinPresent;
            SkinIdRawJson = skinRaw;
            SkinId = skin;
        }
    }

    public sealed class StaffStageRawResult
    {
        public EStage Stage { get; }
        public StaffStageRawStatus Status { get; }
        public string RowInDate { get; }
        public bool CanApplyLegacy { get; }
        public bool HasVerifiedRawRecords { get; }
        public string Error { get; }
        public string RawJson { get; }
        public IReadOnlyList<StaffStageRawRecord> Records { get; }
        internal StaffStageRawResult(EStage stage, StaffStageRawStatus status, string rowInDate = null,
            bool canApplyLegacy = false, bool verified = false, string error = null,
            List<StaffStageRawRecord> records = null, string rawJson = null)
        {
            Stage = stage;
            Status = status;
            RowInDate = rowInDate;
            CanApplyLegacy = canApplyLegacy;
            HasVerifiedRawRecords = verified;
            Error = error;
            RawJson = rawJson;
            Records = Array.AsReadOnly(records == null ? Array.Empty<StaffStageRawRecord>() : records.ToArray());
        }
    }
}
