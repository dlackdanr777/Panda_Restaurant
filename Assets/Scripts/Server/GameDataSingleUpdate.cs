using System;
using System.Collections.Generic;
using System.IO;
using BackEnd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Muks.BackEnd
{
    /// <summary>
    /// GameData 공개 API를 한 번 호출한다. Get/Insert/프로젝트 오류 처리기/오류 재시도를 사용하지 않는다.
    /// SDK의 명시적 인증 거절 후 자동 갱신은 별개이며, HTTP 전송 횟수나 서버 반영 횟수 보장이 아니다.
    /// </summary>
    public sealed class GameDataSingleUpdate
    {
        private readonly Func<GameDataSaveReadiness> _readReadiness;
        private readonly IGameDataUpdateTransport _transport;

        public GameDataSingleUpdate(Func<GameDataSaveReadiness> readReadiness, IGameDataUpdateTransport transport)
        {
            _readReadiness = readReadiness ?? throw new ArgumentNullException(nameof(readReadiness));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public bool CanSend(GameDataSaveIdentity identity, out string error)
        {
            error = null;
            if (identity == null || !identity.IsValid)
                error = "명시적인 요청 ID, 계정 inDate, GameData 행 inDate가 필요합니다.";
            else
            {
                try
                {
                    GameDataSaveReadiness ready = _readReadiness();
                    if (ready == null) error = "저장 준비 상태가 없습니다.";
                    else if (!ready.SaveEnabled) error = "저장이 비활성화되어 있습니다.";
                    else if (!ready.LoggedIn) error = "로그인이 완료되지 않았습니다.";
                    else if (!ready.Loaded || !identity.Target.Matches(ready.RestoredTarget))
                        error = "해당 계정과 GameData 행의 원본 복원이 완료되지 않았습니다.";
                    else if (!string.Equals(identity.Target.AccountInDate, ready.CurrentAccountInDate, StringComparison.Ordinal))
                        error = "현재 로그인 계정과 저장 대상 계정이 다릅니다.";
                    else if (!ready.GameplaySaveAllowed) error = "기존 튜토리얼 저장 허용 조건을 충족하지 않았습니다.";
                    else if (!string.IsNullOrEmpty(ready.TransportBlockReason)) error = ready.TransportBlockReason;
                    else if (ready.InitializationPolicy == null) error = "실제 SDK 초기화에 적용된 저장 정책이 확인되지 않았습니다.";
                    else if (!ready.InitializationPolicy.IsSupported) error = ready.InitializationPolicy.BlockReason;
                }
                catch (Exception ex) { error = "전송 전 준비 확인 실패: " + ex.Message; }
            }
            return error == null;
        }

        public void Send(GameDataSaveIdentity identity, GameDataSavePayload payload, Action<GameDataSaveReceipt> onResponse)
        {
            if (onResponse == null) throw new ArgumentNullException(nameof(onResponse));
            if (!CanSend(identity, out string error) || payload == null)
            {
                onResponse(GameDataSaveReceipt.Rejected(identity, payload, error ?? "전송 자료가 없습니다."));
                return;
            }

            Param isolatedValues;
            try { isolatedValues = payload.CreateParamCopy(); }
            catch (Exception ex)
            {
                onResponse(GameDataSaveReceipt.Rejected(identity, payload, "전송 자료 생성 실패: " + ex.Message));
                return;
            }

            // 복사 이후에도 gate를 확인한다. 이 객체와 콜백은 메인 스레드에서 사용한다.
            if (!CanSend(identity, out error))
            {
                onResponse(GameDataSaveReceipt.Rejected(identity, payload, error));
                return;
            }

            Exception listenerException = null;
            bool confirmedSuccess = false;
            try
            {
                // 이 경계를 지난 예외는 요청 미전송이라고 증명할 수 없다. 재호출하지 않는다.
                _transport.UpdateOnce(identity, isolatedValues, (responseIdentity, raw) =>
                {
                    if (!identity.Matches(responseIdentity)) return;
                    confirmedSuccess |= raw != null && raw.IsSuccess;
                    try { onResponse(GameDataSaveReceipt.FromResponse(identity, payload, raw)); }
                    catch (Exception ex) { listenerException = ex; throw; }
                });
            }
            catch (Exception ex)
            {
                // 소비자 콜백 예외를 통신 실패로 오분류하지 않는다. 조정기는 자체적으로 이를 격리한다.
                if (ReferenceEquals(ex, listenerException)) throw;
                if (!confirmedSuccess)
                    onResponse(GameDataSaveReceipt.Unknown(identity, payload, "전송 시작 이후 예외: " + ex.Message));
            }
        }
    }

    /// <summary>테스트에서만 가짜 구현을 주입한다. 실제 구현은 명시한 행의 UpdateV2만 호출한다.</summary>
    public interface IGameDataUpdateTransport
    {
        void UpdateOnce(GameDataSaveIdentity identity, Param values,
            Action<GameDataSaveIdentity, GameDataRawResponse> onResponse);
    }

    internal sealed class BackendGameDataUpdateTransport : IGameDataUpdateTransport
    {
        public void UpdateOnce(GameDataSaveIdentity identity, Param values,
            Action<GameDataSaveIdentity, GameDataRawResponse> onResponse)
        {
            Backend.GameData.UpdateV2("GameData", identity.Target.RowInDate, identity.Target.AccountInDate, values,
                bro => onResponse(identity, bro == null ? null : new GameDataRawResponse(
                    bro.IsSuccess(), bro.GetStatusCode(), bro.GetErrorCode(), bro.GetMessage())));
        }
    }

    public sealed class GameDataSaveTarget
    {
        public string AccountInDate { get; }
        public string RowInDate { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(AccountInDate) && !string.IsNullOrWhiteSpace(RowInDate);
        public GameDataSaveTarget(string accountInDate, string rowInDate)
        { AccountInDate = accountInDate; RowInDate = rowInDate; }
        public bool Matches(GameDataSaveTarget other) => other != null
            && string.Equals(AccountInDate, other.AccountInDate, StringComparison.Ordinal)
            && string.Equals(RowInDate, other.RowInDate, StringComparison.Ordinal);
    }

    /// <summary>ID는 로컬 응답 상관관계용이며 서버 중복 방지 키가 아니다.</summary>
    public sealed class GameDataSaveIdentity
    {
        public string RequestId { get; }
        public GameDataSaveTarget Target { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(RequestId) && Target != null && Target.IsValid;
        public GameDataSaveIdentity(string requestId, GameDataSaveTarget target)
        { RequestId = requestId; Target = target; }
        public bool Matches(GameDataSaveIdentity other) => other != null && Target != null
            && Target.Matches(other.Target) && string.Equals(RequestId, other.RequestId, StringComparison.Ordinal);
    }

    public sealed class GameDataSaveReadiness
    {
        public bool SaveEnabled { get; }
        public bool LoggedIn { get; }
        public bool Loaded { get; }
        public bool GameplaySaveAllowed { get; }
        public string CurrentAccountInDate { get; }
        public GameDataSaveTarget RestoredTarget { get; }
        public string TransportBlockReason { get; }
        public GameDataSdkInitializationPolicy InitializationPolicy { get; }

        // RestoredTarget은 해당 원본 복원 완료를 확인한 호출자가 제공한다. 단순 IsLoaded로 대체하지 않는다.
        public GameDataSaveReadiness(bool saveEnabled, bool loggedIn, bool loaded, bool gameplaySaveAllowed,
            string currentAccountInDate, GameDataSaveTarget restoredTarget, string transportBlockReason = null,
            GameDataSdkInitializationPolicy initializationPolicy = null)
        {
            SaveEnabled = saveEnabled; LoggedIn = loggedIn; Loaded = loaded;
            GameplaySaveAllowed = gameplaySaveAllowed; CurrentAccountInDate = currentAccountInDate;
            RestoredTarget = restoredTarget; TransportBlockReason = transportBlockReason;
            InitializationPolicy = initializationPolicy;
        }
    }

    /// <summary>인증키나 계정 원문 없이 초기화에 필요한 재시도 정책 필드만 값으로 복사한다.</summary>
    public sealed class GameDataSdkRetrySettings
    {
        public Version SdkVersion { get; }
        public bool RetryWhenClientRequestFailError { get; }
        public bool RetryWhenServerError { get; }
        public bool AutoRefreshToken { get; }

        public GameDataSdkRetrySettings(Version sdkVersion, bool retryWhenClientRequestFailError,
            bool retryWhenServerError, bool autoRefreshToken)
        {
            SdkVersion = sdkVersion;
            RetryWhenClientRequestFailError = retryWhenClientRequestFailError;
            RetryWhenServerError = retryWhenServerError;
            AutoRefreshToken = autoRefreshToken;
        }

        internal bool Matches(GameDataSdkRetrySettings other) => other != null
            && Equals(SdkVersion, other.SdkVersion)
            && RetryWhenClientRequestFailError == other.RetryWhenClientRequestFailError
            && RetryWhenServerError == other.RetryWhenServerError && AutoRefreshToken == other.AutoRefreshToken;
    }

    /// <summary>
    /// 설정 파일이나 외부 bool만으로 생성할 수 없는 초기화 관찰 결과. 생성자는 비공개이다.
    /// 유일한 런타임 호출자는 BackendManager.InitializeBackend이며 테스트만 가짜 초기화 함수를 사용한다.
    /// SDK 5.15.0은 기본 Initialize 안에서 Resource를 값 복사한다. 이후 Inspector 변경은 적용 증거가 아니다.
    /// </summary>
    public sealed class GameDataSdkInitializationPolicy
    {
        public GameDataSdkRetrySettings Settings { get; }
        public bool IsSupported => BlockReason == null;
        public string BlockReason
        {
            get
            {
                if (!Equals(Settings.SdkVersion, new Version(5, 15, 0, 0)))
                    return "설치된 SDK 버전의 재시도/인증 분기 검증이 필요합니다.";
                if (Settings.RetryWhenClientRequestFailError || Settings.RetryWhenServerError)
                    return "SDK 초기화에 오류 재시도가 적용되어 있어 중간 미확정 시도를 배제할 수 없습니다.";
                // 5.15.0: !IsSuccess && 401 && message.StartsWith("bad bad,accessToken")에서만 갱신.
                // 400/HttpRequestException, 408, null 등 결과 미확정은 이 인증 거절 분기에 해당하지 않는다.
                // 자동 갱신 성공 시 동일 본문/갱신된 인증 헤더로 재호출하며 마지막 응답만 받는다.
                return null;
            }
        }

        private GameDataSdkInitializationPolicy(GameDataSdkRetrySettings settings) { Settings = settings; }

        internal static GameDataRawResponse ObserveInitialization(Func<GameDataSdkRetrySettings> readSettings,
            Func<bool> isSdkInitialized, Func<GameDataRawResponse> initializeSdk,
            out GameDataSdkInitializationPolicy appliedPolicy)
        {
            appliedPolicy = null; // 실패한 재초기화가 이전 증거를 유지하지 못하게 한다.
            if (readSettings == null || isSdkInitialized == null || initializeSdk == null)
                throw new ArgumentNullException("초기화 관찰 함수가 필요합니다.");
            bool? initializedBefore = ReadInitialized(isSdkInitialized);
            GameDataSdkRetrySettings before = ReadSettings(readSettings);
            GameDataRawResponse response = initializeSdk(); // 실제 기본 초기화 호출을 건너뛰거나 대체하지 않는다.
            bool? initializedAfter = ReadInitialized(isSdkInitialized);
            GameDataSdkRetrySettings after = ReadSettings(readSettings);
            if (initializedBefore == false && initializedAfter == true && response != null && response.IsSuccess
                && before != null && before.Matches(after))
                appliedPolicy = new GameDataSdkInitializationPolicy(before);
            return response;
        }

        // 증거 조회 실패 때문에 기존 인증 초기화 자체를 중단하지는 않는다. 새 저장만 계속 거절한다.
        private static GameDataSdkRetrySettings ReadSettings(Func<GameDataSdkRetrySettings> read)
        { try { return read(); } catch { return null; } }
        private static bool? ReadInitialized(Func<bool> read)
        { try { return read(); } catch { return null; } }
    }

    /// <summary>
    /// SDK 5.15 Param.GetJson()은 값 자체를 직렬화한다. Clone/GetValue는 깊은 복사가 아니므로 사용하지 않는다.
    /// 문자열로 고정하고 새 Param에는 일반 사전/배열/스칼라만 넣어 SDK와 다른 JToken 타입을 섞지 않는다.
    /// Version 1 직원 JSON은 단지 문자열 값이며 형식/정책을 변경하지 않는다.
    /// </summary>
    public sealed class GameDataSavePayload
    {
        public string Json { get; }
        private GameDataSavePayload(string json) { Json = json; }

        public static bool TryCapture(Param values, out GameDataSavePayload payload, out string error)
        {
            payload = null; error = null;
            if (values == null) { error = "저장 값이 없습니다."; return false; }
            try
            {
                string json = values.GetJson();
                JObject root = ReadObject(json);
                if (!root.HasValues) { error = "저장할 필드가 없습니다."; return false; }
                foreach (JProperty field in root.Properties())
                {
                    // SortedList의 indexer로 SDK Add 검사를 우회한 입력도 전체 거절한다.
                    // SDK Add는 잘못된 키를 예외 없이 버릴 수 있으므로 부분 저장 전에 검사해야 한다.
                    if (string.IsNullOrEmpty(field.Name) || char.IsDigit(field.Name[0]))
                    { error = "SDK가 저장할 수 없는 필드 이름입니다: " + field.Name; return false; }
                    // SDK AddCalculation이 만드는 모양. 이 경로는 증분 연산이 아닌 확정된 최종 값 전용이다.
                    if (field.Value is JObject operation &&
                        operation.Property("operator") != null && operation.Property("number") != null)
                    { error = "증분 연산 자료는 단발 최종 값 저장에 사용할 수 없습니다: " + field.Name; return false; }
                }
                var candidate = new GameDataSavePayload(json);
                candidate.CreateParamCopy(); // 변환할 수 없는 값은 전송 이전에 전체 거절한다.
                payload = candidate;
                return true;
            }
            catch (Exception ex) { error = "저장 값 고정 실패: " + ex.Message; return false; }
        }

        public Param CreateParamCopy()
        {
            var result = new Param();
            foreach (JProperty field in ReadObject(Json).Properties()) result.Add(field.Name, PlainValue(field.Value));
            return result;
        }

        private static JObject ReadObject(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }

        private static object PlainValue(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (JProperty field in obj.Properties()) result.Add(field.Name, PlainValue(field.Value));
                return result;
            }
            if (token is JArray array)
            {
                var result = new object[array.Count];
                for (int i = 0; i < result.Length; i++) result[i] = PlainValue(array[i]);
                return result;
            }
            if (token is JValue value) return value.Value;
            throw new ArgumentException("지원하지 않는 저장 값입니다.");
        }
    }

    public sealed class GameDataRawResponse
    {
        public bool IsSuccess { get; }
        public string StatusCode { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public GameDataRawResponse(bool isSuccess, string statusCode, string errorCode, string message)
        { IsSuccess = isSuccess; StatusCode = statusCode; ErrorCode = errorCode; Message = message; }
    }

    public enum GameDataSaveDisposition
    {
        RejectedBeforeSend,
        SuccessConfirmed,
        // 미반영을 증명하는 API 근거가 아직 없으므로 어떤 SDK 오류 코드에도 배정하지 않는다.
        UnappliedFailureConfirmed,
        Indeterminate
    }

    public sealed class GameDataSaveReceipt
    {
        public GameDataSaveIdentity Identity { get; }
        public GameDataSavePayload Payload { get; }
        public bool SendStarted { get; }
        public GameDataRawResponse RawResponse { get; }
        public GameDataSaveDisposition Disposition { get; }
        public string Reason { get; }

        private GameDataSaveReceipt(GameDataSaveIdentity identity, GameDataSavePayload payload, bool sendStarted,
            GameDataRawResponse raw, GameDataSaveDisposition disposition, string reason)
        { Identity = identity; Payload = payload; SendStarted = sendStarted; RawResponse = raw; Disposition = disposition; Reason = reason; }

        internal static GameDataSaveReceipt Rejected(GameDataSaveIdentity identity, GameDataSavePayload payload, string reason) =>
            new GameDataSaveReceipt(identity, payload, false, null, GameDataSaveDisposition.RejectedBeforeSend, reason);
        internal static GameDataSaveReceipt Unknown(GameDataSaveIdentity identity, GameDataSavePayload payload, string reason) =>
            new GameDataSaveReceipt(identity, payload, true, null, GameDataSaveDisposition.Indeterminate, reason);
        internal static GameDataSaveReceipt FromResponse(GameDataSaveIdentity identity, GameDataSavePayload payload, GameDataRawResponse raw) =>
            new GameDataSaveReceipt(identity, payload, true, raw,
                raw != null && raw.IsSuccess ? GameDataSaveDisposition.SuccessConfirmed : GameDataSaveDisposition.Indeterminate,
                raw != null && raw.IsSuccess ? null : "미반영이 입증되지 않았습니다. 재전송 및 후속 저장을 차단합니다.");
    }
}
