using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    /// <summary>
    /// 기존 GameData 조회 한 건의 원본 검증과 메모리 복원 완료를 묶는다. 메인 스레드 전용이다.
    /// SDK/UserInfo/Stage/저장에 직접 접근하지 않으며, 저장 조정기의 미확정 잠금을 해제하지 않는다.
    /// </summary>
    public sealed class GameDataRestoreContext
    {
        public const string StaffAccountFieldName = "StaffAccount";

        private readonly Func<string> _readCurrentAccount;
        private GameDataRestoreQuery _query;
        private GameDataRestoreQuery _legacyQuery;
        private GameDataSaveTarget _legacyTarget;
        private GameDataAuthenticationAttempt _authentication;
        private GameDataAuthenticationAttempt _newAccountAuthentication;
        private string _newAccountInDate;
        // 재조회/재로그인으로 최초 생성의 미확정 전송이나 이미 사용한 생성 권한을 지우지 않는다.
        private readonly Dictionary<string, InitialCreationRecord> _initialCreations =
            new Dictionary<string, InitialCreationRecord>(StringComparer.Ordinal);
        private long _generation;
        private long _authenticationGeneration;
        private GameDataRestoreResult _result = new GameDataRestoreResult(GameDataRestoreStatus.Invalidated,
            "검증된 GameData 조회가 없습니다.");

        public GameDataRestoreContext(Func<string> readCurrentAccount)
        {
            _readCurrentAccount = readCurrentAccount ?? throw new ArgumentNullException(nameof(readCurrentAccount));
        }

        public GameDataRestoreResult Result
        {
            get { RefreshAccountValidity(); return _result; }
        }

        public GameDataRestoreEvidence Evidence
        {
            get { RefreshAccountValidity(); return _result.Evidence; }
        }

        public GameDataRestoreQuery LegacyQuery
        {
            get { RefreshAccountValidity(); return HasLegacyRestore ? _legacyQuery : null; }
        }

        public GameDataSaveTarget LegacyTarget
        {
            get { RefreshAccountValidity(); return HasLegacyRestore ? _legacyTarget : null; }
        }

        private bool HasLegacyRestore => ReferenceEquals(_query, _legacyQuery) && _legacyTarget != null
            && (_result.Status == GameDataRestoreStatus.Ready || _result.Status == GameDataRestoreStatus.MigrationRequired);

        public bool IsInitialCreationBlocked
        {
            get
            {
                RefreshAccountValidity();
                string account = ReadCurrentAccount();
                return !string.IsNullOrWhiteSpace(account) && _initialCreations.TryGetValue(account, out var creation)
                    && !creation.Confirmed;
            }
        }

        public GameDataAuthenticationAttempt BeginAuthentication(GameDataAuthenticationKind kind, bool wasLoggedIn)
        {
            InvalidateAccountSession();
            _authentication = new GameDataAuthenticationAttempt(kind, wasLoggedIn);
            return _authentication;
        }

        public bool IsCurrentAuthentication(GameDataAuthenticationAttempt attempt) => attempt != null
            && ReferenceEquals(_authentication, attempt) && !attempt.Consumed;

        public bool CompleteAuthentication(GameDataAuthenticationAttempt attempt, GameDataRawResponse raw,
            string responseAccount)
        {
            if (!IsCurrentAuthentication(attempt)) return false;
            attempt.Consumed = true;
            string currentAccount = ReadCurrentAccount();
            if (!ReferenceEquals(_authentication, attempt) || raw == null || !raw.IsSuccess
                || (raw.StatusCode != "200" && raw.StatusCode != "201")
                || string.IsNullOrWhiteSpace(responseAccount)
                || !string.Equals(responseAccount, currentAccount, StringComparison.Ordinal))
                return false;

            // 201만으로는 로그인 중 Google 계정 연동과 신규 가입을 구분할 수 없다.
            // 호출 종류와 인증 시작 당시 비로그인 상태도 함께 확인한다.
            if (raw.StatusCode == "201" && !attempt.WasLoggedIn
                && (attempt.Kind == GameDataAuthenticationKind.Guest || attempt.Kind == GameDataAuthenticationKind.Google))
            {
                _newAccountAuthentication = attempt;
                _newAccountInDate = responseAccount;
            }
            return true;
        }

        public GameDataRestoreQuery BeginQuery()
        {
            // 토큰의 참조 동일성이 기준이며, 표시용 세대 번호의 순환으로 예전 토큰이 유효해지지 않는다.
            _generation = unchecked(_generation + 1);
            _query = new GameDataRestoreQuery(ReadCurrentAccount(), _generation, _authenticationGeneration);
            _legacyQuery = null;
            _legacyTarget = null;
            _result = new GameDataRestoreResult(GameDataRestoreStatus.QueryPending, "GameData 조회 중입니다.");
            return _query;
        }

        public void InvalidateAccountSession()
        {
            _authenticationGeneration = unchecked(_authenticationGeneration + 1);
            _query = null;
            _legacyQuery = null;
            _legacyTarget = null;
            _authentication = null;
            _newAccountAuthentication = null;
            _newAccountInDate = null;
            _result = new GameDataRestoreResult(GameDataRestoreStatus.Invalidated,
                "계정 세션 또는 조회가 무효화되었습니다.");
        }

        public bool IsCurrent(GameDataRestoreQuery query)
        {
            if (query == null || !ReferenceEquals(_query, query)) return false;
            string currentAccount = ReadCurrentAccount();
            if (!ReferenceEquals(_query, query)) return false;
            if (string.IsNullOrWhiteSpace(query.AccountInDate)
                || !string.Equals(query.AccountInDate, currentAccount, StringComparison.Ordinal))
            {
                InvalidateAccountSession();
                return false;
            }
            return true;
        }

        public bool CanHandle(GameDataRestoreQuery query) => IsCurrent(query)
            && _result.Status == GameDataRestoreStatus.QueryPending;

        public bool TryBeginInitialCreation(GameDataRestoreQuery query, GameDataSdkInitializationPolicy policy,
            out string error)
        {
            error = null;
            if (!IsCurrent(query) || _result.Status != GameDataRestoreStatus.RowMissing)
                error = "현재 조회에서 최초 생성 대상의 행 부재가 확인되지 않았습니다.";
            else if (_newAccountAuthentication == null || !ReferenceEquals(_authentication, _newAccountAuthentication)
                || !string.Equals(_newAccountInDate, query.AccountInDate, StringComparison.Ordinal))
                error = "현재 인증 세션에서 신규 계정 생성이 확인되지 않았습니다.";
            else if (policy == null || !policy.IsSupported)
                error = "실제 초기화에 적용된 최초 생성의 SDK 정책이 확인되지 않았습니다.";
            else if (_initialCreations.ContainsKey(query.AccountInDate))
                error = "이 계정의 최초 생성은 이미 시도되었습니다. 임의로 다시 전송하지 않습니다.";
            if (error != null) return false;

            _initialCreations.Add(query.AccountInDate, new InitialCreationRecord(query));
            _result = new GameDataRestoreResult(GameDataRestoreStatus.CreatingInitialData,
                "확인된 신규 계정의 최초 데이터를 생성 중입니다.");
            return true;
        }

        public GameDataRestoreResult CompleteInitialCreation(GameDataRestoreQuery query, GameDataRawResponse raw)
        {
            if (!IsCurrent(query) || _result.Status != GameDataRestoreStatus.CreatingInitialData
                || !_initialCreations.TryGetValue(query.AccountInDate, out var creation)
                || !ReferenceEquals(creation.Query, query) || creation.ResponseHandled)
                return Ignored();

            // 공개 SDK 응답의 분류를 단발 저장과 공유한다. null/통신 오류를 미반영 확정으로 바꾸지 않는다.
            creation.ResponseHandled = true;
            creation.Confirmed = GameDataSaveReceipt.FromResponse(null, null, raw).Disposition
                == GameDataSaveDisposition.SuccessConfirmed;
            return Publish(query, creation.Confirmed ? GameDataRestoreStatus.InitialCreationConfirmedAwaitingRestore
                    : GameDataRestoreStatus.InitialCreationIndeterminate,
                creation.Confirmed ? "최초 생성 응답을 확인했습니다. 같은 계정의 조회·복원이 더 필요합니다."
                    : "최초 생성의 반영 여부가 미확정입니다. 재전송과 게임 진입을 차단합니다.");
        }

        public GameDataRestoreResult TryRestore(GameDataRestoreQuery query, bool querySucceeded, string rawJson,
            Func<IReadOnlyList<StaffData>> readCatalog, Func<bool> restoreLegacy)
        {
            if (!CanHandle(query)) return Ignored();
            if (IsInitialCreationBlocked)
                return Publish(query, GameDataRestoreStatus.InitialCreationIndeterminate,
                    "기존 최초 생성의 반영 여부가 미확정입니다. 읽은 값만으로 이전 전송을 해제하지 않습니다.");

            // 외부 자료/복원 콜백이 재진입해도 같은 조회를 두 번 처리하지 않는다.
            _result = new GameDataRestoreResult(GameDataRestoreStatus.Restoring, "원본 검증 및 메모리 복원 중입니다.");
            if (!querySucceeded)
                return Publish(query, GameDataRestoreStatus.QueryFailed, "GameData 조회가 성공하지 않았습니다.");

            JObject root;
            try { root = ReadObject(rawJson); }
            catch
            {
                return Publish(query, GameDataRestoreStatus.InvalidResponse,
                    "원본 응답이 JSON 객체가 아니거나 손상/중복 필드/추가 내용을 포함합니다.");
            }

            if (!(root["rows"] is JArray rows))
                return Publish(query, GameDataRestoreStatus.InvalidResponse, "원본 rows 배열이 없습니다.");
            if (rows.Count > 1)
                return Publish(query, GameDataRestoreStatus.AmbiguousRows, "GameData 대상 행이 여러 개입니다. 임의로 선택하지 않습니다.");
            JToken firstKey = root["firstKey"];
            if (firstKey != null && firstKey.Type != JTokenType.Null)
                return Publish(query, GameDataRestoreStatus.PaginationIncomplete,
                    "추가 페이지가 남아 대상 행의 유일성이 확인되지 않았습니다.");
            if (rows.Count == 0)
                return Publish(query, GameDataRestoreStatus.RowMissing, "GameData 대상 행이 없습니다. 빈 계정으로 복원하지 않습니다.");
            if (!(rows[0] is JObject row)
                || !TryWrappedString(row["owner_inDate"], "S", out string owner)
                || string.IsNullOrWhiteSpace(owner))
                return Publish(query, GameDataRestoreStatus.InvalidResponse, "행의 owner_inDate 원본 형식이 잘못되었습니다.");
            if (!string.Equals(owner, query.AccountInDate, StringComparison.Ordinal))
                return Publish(query, GameDataRestoreStatus.AccountMismatch, "행의 소유 계정과 조회 계정이 다릅니다.");
            if (!TryWrappedString(row["inDate"], "S", out string rowInDate)
                || string.IsNullOrWhiteSpace(rowInDate))
                return Publish(query, GameDataRestoreStatus.InvalidResponse, "행의 inDate 원본 형식이 잘못되었습니다.");
            if (!TryReadDiamonds(row, out int diamonds, out string diamondError))
                return Publish(query, GameDataRestoreStatus.InvalidDiamonds, diamondError);

            bool migrationRequired = row.Property(StaffAccountFieldName, StringComparison.Ordinal) == null;
            StaffAccountSaveData staffAccount = null;
            if (!migrationRequired)
            {
                if (!TryWrappedString(row[StaffAccountFieldName], "S", out string staffJson))
                    return Publish(query, GameDataRestoreStatus.InvalidStaffData,
                        "존재하는 공용 직원 필드는 S 문자열이어야 합니다. null/다른 타입은 부재가 아닙니다.");
                StaffAccountLoadResult loaded;
                try { loaded = StaffAccountLoadPlanner.Plan(StaffAccountQueryStatus.SucceededFieldPresent, staffJson); }
                catch
                {
                    return Publish(query, GameDataRestoreStatus.InvalidStaffData, "공용 직원 데이터 형식을 읽을 수 없습니다.");
                }
                if (loaded.Status != StaffAccountLoadStatus.ExistingDataLoaded)
                    return Publish(query, loaded.Status == StaffAccountLoadStatus.UnsupportedVersion
                            ? GameDataRestoreStatus.UnsupportedStaffVersion : GameDataRestoreStatus.InvalidStaffData,
                        "공용 직원 데이터가 손상되었거나 지원하지 않는 형식입니다. 이전으로 우회하지 않습니다.");
                staffAccount = loaded.ExistingData;

                GameDataRestoreStatus catalogStatus;
                string catalogError;
                try { catalogStatus = ValidateCatalog(staffAccount, readCatalog, out catalogError); }
                catch
                {
                    catalogStatus = GameDataRestoreStatus.InvalidStaffCatalog;
                    catalogError = "등록 직원 자료를 읽는 중 오류가 발생했습니다.";
                }
                // 등록 자료 제공자도 호출자 코드이므로, 그 안에서 조회/계정이 바뀔 수 있다.
                if (!IsCurrent(query)) return Ignored();
                if (catalogStatus != GameDataRestoreStatus.Ready)
                    return Publish(query, catalogStatus, catalogError);
            }

            if (!IsCurrent(query)) return Ignored();
            bool restored;
            try { restored = restoreLegacy != null && restoreLegacy(); }
            catch
            {
                return Publish(query, GameDataRestoreStatus.RestoreFailed,
                    "기존 메모리 복원 중 예외가 발생했습니다. 부분 복원을 원상복구했다고 보장하지 않습니다.");
            }
            if (!IsCurrent(query)) return Ignored();
            if (!restored)
                return Publish(query, GameDataRestoreStatus.RestoreFailed, "기존 메모리 복원이 성공하지 않았습니다.");

            _legacyQuery = query;
            _legacyTarget = new GameDataSaveTarget(owner, rowInDate);
            if (migrationRequired)
                return Publish(query, GameDataRestoreStatus.MigrationRequired,
                    "공용 필드가 없어 이전 판단이 필요합니다. 기존 진입·저장의 복원 조건만 충족하며 새 직원 저장은 준비되지 않았습니다.",
                    canContinueLegacy: true);

            var evidence = new GameDataRestoreEvidence(query, new GameDataSaveTarget(owner, rowInDate), diamonds,
                staffAccount, rawJson);
            return Publish(query, GameDataRestoreStatus.Ready, null, true, evidence);
        }

        private GameDataRestoreResult Publish(GameDataRestoreQuery query, GameDataRestoreStatus status, string reason,
            bool canContinueLegacy = false, GameDataRestoreEvidence evidence = null)
        {
            if (!IsCurrent(query)) return Ignored();
            _result = new GameDataRestoreResult(status, reason, canContinueLegacy, evidence);
            return _result;
        }

        private static GameDataRestoreResult Ignored() => new GameDataRestoreResult(GameDataRestoreStatus.Invalidated,
            "현재 처리 가능한 조회가 아니므로 적용하지 않았습니다.");

        private string ReadCurrentAccount()
        {
            try { return _readCurrentAccount(); }
            catch { return null; }
        }

        private void RefreshAccountValidity()
        {
            if (_query != null) IsCurrent(_query);
        }

        private static JObject ReadObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new JsonReaderException();
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                JObject root = JObject.Load(reader,
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonReaderException();
                return root;
            }
        }

        private static bool TryWrappedString(JToken token, string wrapper, out string value)
        {
            value = null;
            if (!(token is JObject obj) || obj.Count != 1
                || obj.Property(wrapper, StringComparison.Ordinal) == null || obj[wrapper].Type != JTokenType.String)
                return false;
            value = (string)obj[wrapper];
            return true;
        }

        private static bool TryReadDiamonds(JObject row, out int diamonds, out string error)
        {
            diamonds = 0;
            error = null;
            if (row.Property("Dia", StringComparison.Ordinal) == null)
                error = "Dia 필드가 누락되었습니다.";
            else if (!TryWrappedString(row["Dia"], "N", out string text))
                error = "Dia는 SDK N 문자열 형식이어야 합니다.";
            else
            {
                int start = text.Length > 0 && (text[0] == '-' || text[0] == '+') ? 1 : 0;
                bool integer = text.Length > start;
                bool nonZero = false;
                for (int i = start; i < text.Length; i++)
                {
                    integer &= text[i] >= '0' && text[i] <= '9';
                    nonZero |= text[i] != '0';
                }
                if (!integer) error = "Dia 원본은 소수/지수/공백이 없는 정수여야 합니다.";
                else if (text[0] == '-' && nonZero) error = "Dia 잔액이 음수입니다.";
                else if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out diamonds))
                    error = "Dia 원본이 int 범위를 초과했습니다.";
            }
            return error == null;
        }

        private static GameDataRestoreStatus ValidateCatalog(StaffAccountSaveData account,
            Func<IReadOnlyList<StaffData>> readCatalog, out string error)
        {
            GameDataRestoreStatus status = TryBuildStaffMaximums(readCatalog, out var maximums, out error);
            return status == GameDataRestoreStatus.Ready
                ? ValidateStaffLevels(account.Staff, maximums, out error) : status;
        }

        // Keep the raw catalog checks shared by existing account restore and pre-correction Stage collection.
        internal static GameDataRestoreStatus TryBuildStaffMaximums(Func<IReadOnlyList<StaffData>> readCatalog,
            out IReadOnlyDictionary<string, int> maximums, out string error)
        {
            maximums = null;
            error = null;
            IReadOnlyList<StaffData> catalog = readCatalog?.Invoke();
            if (catalog == null || catalog.Count == 0)
            {
                error = "검증에 필요한 등록 직원 목록이 준비되지 않았습니다.";
                return GameDataRestoreStatus.CatalogUnavailable;
            }

            var verified = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (StaffData staff in catalog)
            {
                if (staff == null || string.IsNullOrWhiteSpace(staff.Id))
                {
                    error = "등록 직원 목록에 null 또는 빈 ID가 있습니다.";
                    return GameDataRestoreStatus.InvalidStaffCatalog;
                }
                foreach (char c in staff.Id)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        error = "등록 직원 ID에 공백이 있습니다.";
                        return GameDataRestoreStatus.InvalidStaffCatalog;
                    }
                }
                int rawMaximum = staff.MaxLevel;
                if (rawMaximum < 1 || verified.ContainsKey(staff.Id))
                {
                    error = "등록 직원의 최대 레벨 근거가 없거나 ID가 중복되었습니다.";
                    return GameDataRestoreStatus.InvalidStaffCatalog;
                }
                // StaffData.IsMaxLevel의 동일 정책: 역할 레벨 배열과 공식 상한 모두 만족해야 한다.
                // 원본 보유 레벨에는 GetRuntimeLevel/A2/clamp를 적용하지 않는다.
                verified.Add(staff.Id, Math.Min(rawMaximum, StaffData.OfficialMaxLevel));
            }
            maximums = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(verified);
            return GameDataRestoreStatus.Ready;
        }

        internal static GameDataRestoreStatus ValidateStaffLevels(IReadOnlyList<StaffAccountStaffRecord> staffRecords,
            IReadOnlyDictionary<string, int> maximums, out string error)
        {
            error = null;
            foreach (StaffAccountStaffRecord staff in staffRecords)
            {
                if (staff == null || string.IsNullOrWhiteSpace(staff.Id)
                    || !maximums.TryGetValue(staff.Id, out int maximum))
                {
                    error = "공용 보유 목록에 현재 등록되지 않은 직원 ID가 있습니다.";
                    return GameDataRestoreStatus.UnregisteredStaff;
                }
                if (staff.Level < 1 || staff.Level > maximum)
                {
                    error = "공용 직원 원본 레벨이 해당 직원의 허용 범위를 벗어났습니다.";
                    return GameDataRestoreStatus.InvalidStaffLevel;
                }
            }
            return GameDataRestoreStatus.Ready;
        }

        private sealed class InitialCreationRecord
        {
            public GameDataRestoreQuery Query { get; }
            public bool ResponseHandled;
            public bool Confirmed;
            public InitialCreationRecord(GameDataRestoreQuery query) { Query = query; }
        }
    }

    public enum GameDataAuthenticationKind { Guest, Google, Other }

    /// <summary>현재 인증 호출의 수명과 시작 조건만 보관한다. 토큰·인증 원문을 보관하지 않는다.</summary>
    public sealed class GameDataAuthenticationAttempt
    {
        public GameDataAuthenticationKind Kind { get; }
        public bool WasLoggedIn { get; }
        internal bool Consumed { get; set; }
        internal GameDataAuthenticationAttempt(GameDataAuthenticationKind kind, bool wasLoggedIn)
        { Kind = kind; WasLoggedIn = wasLoggedIn; }
    }

    /// <summary>동일 계정 재로그인도 별도 토큰이다. 번호만 비교하지 않고 객체 동일성을 확인한다.</summary>
    public sealed class GameDataRestoreQuery
    {
        public string AccountInDate { get; }
        public long Generation { get; }
        public long AuthenticationGeneration { get; }
        internal GameDataRestoreQuery(string accountInDate, long generation, long authenticationGeneration = 0)
        { AccountInDate = accountInDate; Generation = generation; AuthenticationGeneration = authenticationGeneration; }
    }

    public enum GameDataRestoreStatus
    {
        Invalidated, QueryPending, QueryFailed, InvalidResponse, RowMissing, AmbiguousRows,
        PaginationIncomplete, AccountMismatch, InvalidDiamonds, MigrationRequired, InvalidStaffData,
        UnsupportedStaffVersion, CatalogUnavailable, InvalidStaffCatalog, UnregisteredStaff,
        InvalidStaffLevel, Restoring, RestoreFailed, Ready,
        CreatingInitialData, InitialCreationConfirmedAwaitingRestore, InitialCreationIndeterminate
    }

    public sealed class GameDataRestoreResult
    {
        public GameDataRestoreStatus Status { get; }
        public string Reason { get; }
        public bool CanContinueLegacy { get; }
        public GameDataRestoreEvidence Evidence { get; }
        internal GameDataRestoreResult(GameDataRestoreStatus status, string reason, bool canContinueLegacy = false,
            GameDataRestoreEvidence evidence = null)
        { Status = status; Reason = reason; CanContinueLegacy = canContinueLegacy; Evidence = evidence; }
    }

    /// <summary>
    /// 동일한 검증 원본과 복원 성공에서만 발행되는 불변 메모리 스냅샷이다. 저장/이전 완료 증거가 아니다.
    /// 외부에서 보관한 이전 객체가 아니라 Context.Evidence를 매번 읽어 현재 계정 유효성을 확인한다.
    /// </summary>
    public sealed class GameDataRestoreEvidence
    {
        public GameDataRestoreQuery Query { get; }
        public GameDataSaveTarget Target { get; }
        public int Diamonds { get; }
        public StaffAccountSaveData StaffAccount { get; }
        public string RawJson { get; }
        internal GameDataRestoreEvidence(GameDataRestoreQuery query, GameDataSaveTarget target, int diamonds,
            StaffAccountSaveData staffAccount, string rawJson)
        { Query = query; Target = target; Diamonds = diamonds; StaffAccount = staffAccount; RawJson = rawJson; }
    }
}
