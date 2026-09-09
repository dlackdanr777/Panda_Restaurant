using BackEnd;
using LitJson;
using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

namespace Muks.BackEnd
{
    /// <summary>서버와의 연결 상태을 확인 하는 열거형</summary>
    public enum BackendState
    {
        NotSave,
        NotLogin,
        Failure,
        Maintainance,
        Retry,
        Success,
    }

    /// <summary>GameData SDK boundary. Tests inject a memory fake without constructing/initializing a Unity singleton.</summary>
    public interface IGameDataBackendTransport
    {
        bool LoggedIn { get; }
        bool NativeLoggedIn { get; }
        string AccountInDate { get; }
        void Get(string accountInDate, Action<BackendReturnObject> callback);
        void Insert(Param values, Action<BackendReturnObject> callback);
        void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback);
        Param LatestValues();
        bool GameplaySaveAllowed { get; }
        IReadOnlyList<StaffData> ReadCatalog();
        bool Restore(BackendReturnObject response);
        Param InitialValues();
    }

    /// <summary>Existing Stage reads and legacy application boundary; no migration writes.</summary>
    public interface IStageDataLoadTransport
    {
        void Get(EStage stage, string accountInDate, Func<bool> isCurrent, Action<BackendReturnObject> callback);
        BackendReturnObject Get(EStage stage, string accountInDate, Func<bool> isCurrent);
        void Apply(EStage stage, BackendReturnObject response, Func<bool> isCurrent, bool asynchronous);
    }

    /// <summary>뒤끝과 연동할 수 있게 해주는 싱글톤 클래스</summary>
    public class BackendManager : MonoBehaviour
    {
        public static event Action OnGuestSignupHandler;
        public static event Action OnGuestLoginHandler;
        public static event Action<BackendReturnObject> OnInsertGameDataHandler;

        public static event Action OnPauseHandler;
        public static event Action OnResumeHandler;
        public static event Action OnExitHandler;

        public static BackendManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("BackendManager");
                    _instance = obj.AddComponent<BackendManager>();
                    if (Application.isPlaying)
                        DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        private static BackendManager _instance;

        // 저장 가능 상태를 추적하는 플래그
        private static bool _isSaveEnabled = true;
        public static bool IsSaveEnabled => _isSaveEnabled;

        /// <summary>이 값이 참일 때만 서버에 정보를 보냅니다.(로그인 실패인데 정보를 보내면 서버 정보가 초기화)</summary> 
        private bool _isLogin = false;
        public bool IsLogin => _isLogin;

        private bool _isLoaded = false;
        public bool IsLoaded => _isLoaded;

        // 실제 초기화 경계에서 얻은 증거만 보관한다. Inspector의 현재 값으로 대체하지 않는다.
        private GameDataSdkInitializationPolicy _gameDataInitializationPolicy;

        // DontDestroyOnLoad 수명: 씬 해제와 계정/조회 세대 무효화는 별개이다.
        private GameDataRestoreContext _gameDataRestoreContext;
        private GameDataAuthenticationAttempt _authenticationInFlight;
        private IGameDataBackendTransport _gameDataTransport;
        private GameDataSaveCoordinator _gameDataSaveCoordinator;
        private GameDataRestoreQuery _gameDataSaveQuery;
        private Func<bool> _gameDataGameplayGate;
        private IStageDataLoadTransport _stageDataTransport;
        private StaffStageMigrationCollection _stageMigrationCollection;
        private long _stageRoundSerial;
        public StaffStageMigrationCollection StageMigrationCollection
        {
            get { _stageMigrationCollection?.RefreshValidity(); return _stageMigrationCollection; }
        }
        private IStageDataLoadTransport StageDataTransport => _stageDataTransport ??
            (_stageDataTransport = new SdkStageDataLoadTransport(this));
        public GameDataSaveCoordinator CurrentGameDataSaveCoordinator => _gameDataSaveCoordinator;
        private IGameDataBackendTransport GameDataTransport => _gameDataTransport ??
            (_gameDataTransport = new SdkGameDataBackendTransport(this));

        private sealed class SdkGameDataBackendTransport : IGameDataBackendTransport
        {
            private readonly BackendManager _owner;
            public SdkGameDataBackendTransport(BackendManager owner) { _owner = owner; }
            public bool LoggedIn => _owner._isLogin && Backend.IsLogin;
            public bool NativeLoggedIn => Backend.IsLogin;
            public string AccountInDate => Backend.UserInDate;
            public void Get(string accountInDate, Action<BackendReturnObject> callback)
            {
                var where = new Where();
                where.Equal("owner_inDate", accountInDate);
                Backend.GameData.Get("GameData", where, bro => callback(bro));
            }
            public void Insert(Param values, Action<BackendReturnObject> callback) =>
                Backend.GameData.Insert("GameData", values, bro => callback(bro));
            public void Update(GameDataSaveTarget target, Param values, Action<BackendReturnObject> callback) =>
                Backend.GameData.UpdateV2("GameData", target.RowInDate, target.AccountInDate, values, bro => callback(bro));
            public Param LatestValues()
            {
                UserInfo.ApplyDailyWeeklyResetIfNeeded();
                return UserInfo.GetSaveUserData();
            }
            public bool GameplaySaveAllowed => UserInfo.IsFirstTutorialClear && !UserInfo.IsTutorialStart;
            public IReadOnlyList<StaffData> ReadCatalog() => Resources.LoadAll<StaffData>("StaffData");
            public bool Restore(BackendReturnObject response) => UserInfo.TryLoadGameData(response);
            public Param InitialValues() => LoadUserData.CreateInitialGameData(_owner.ServerTime);
        }

        private sealed class SdkStageDataLoadTransport : IStageDataLoadTransport
        {
            private readonly BackendManager _owner;
            public SdkStageDataLoadTransport(BackendManager owner) { _owner = owner; }
            public void Get(EStage stage, string accountInDate, Func<bool> isCurrent,
                Action<BackendReturnObject> callback)
            {
                if (!isCurrent()) return;
                var where = new Where();
                where.Equal("owner_inDate", accountInDate);
                BackendReturnObject lastResponse = null;
                GameDataRestoreQuery query = _owner.GameDataRestore.LegacyQuery;
                long round = _owner._stageRoundSerial;
                _owner.ProcessBackendAPI(stage + "Data 데이터 조회", next =>
                {
                    if (!isCurrent()) return; // Includes delayed popup/automatic read retries.
                    Backend.GameData.Get(stage + "Data", where, bro =>
                    {
                        if (!isCurrent()) return; // Before HandleError/retry/legacy application.
                        lastResponse = bro;
                        next(bro);
                    });
                }, bro => { if (isCurrent()) callback(bro); },
                state =>
                {
                    if (!isCurrent()) return;
                    callback(lastResponse);
                    // A terminal response cannot be replaced in the same collection. An explicit UI retry
                    // starts a fresh all-Stage round, retaining the existing read retry affordance.
                    _owner.SetPopupButton1("재시도", () => _owner.RetryStageCollection(query, round));
                });
            }
            public BackendReturnObject Get(EStage stage, string accountInDate, Func<bool> isCurrent)
            {
                if (!isCurrent()) return null;
                var where = new Where();
                where.Equal("owner_inDate", accountInDate);
                return _owner.ProcessBackendAPISync(stage + "Data 데이터 조회",
                    () => Backend.GameData.Get(stage + "Data", where), isCurrent: isCurrent);
            }
            public void Apply(EStage stage, BackendReturnObject response, Func<bool> isCurrent, bool asynchronous) =>
                UserInfo.ApplyLoadedStageData(stage, response, isCurrent, asynchronous);
        }

        // The existing initialization creates/queries Stage1, Stage2 and Stage3, regardless of unlock state.
        // Single-stage reloads start a separate round; they never borrow the other stages from an older round.
        public void LoadAllStageData(bool asynchronous)
        {
            StaffStageMigrationCollection collection = BeginStageCollection();
            if (collection == null) return;
            foreach (EStage stage in collection.RequiredStages) LoadStageData(collection, stage, asynchronous);
        }
        public void LoadStageData(EStage stage, bool asynchronous)
        {
            if (stage < EStage.Stage1 || stage >= EStage.Length) return;
            StaffStageMigrationCollection collection = BeginStageCollection();
            if (collection != null) LoadStageData(collection, stage, asynchronous);
        }
        internal void RetryStageCollection(GameDataRestoreQuery query, long round)
        {
            if (round != _stageRoundSerial || !GameDataRestore.IsCurrent(query)
                || !ReferenceEquals(GameDataRestore.LegacyQuery, query)) return;
            LoadAllStageData(true);
        }
        private StaffStageMigrationCollection BeginStageCollection()
        {
            long round = _stageRoundSerial = unchecked(_stageRoundSerial + 1);
            _stageMigrationCollection?.RefreshValidity();
            GameDataRestoreQuery query = GameDataRestore.LegacyQuery;
            GameDataSaveTarget target = GameDataRestore.LegacyTarget;
            if (!IsCurrentStageRound(query, target, round)) return null;
            bool migrationRequired = GameDataRestore.Result.Status == GameDataRestoreStatus.MigrationRequired;
            var collection = new StaffStageMigrationCollection(query, round, migrationRequired,
                () => IsCurrentStageRound(query, target, round), () => GameDataTransport.ReadCatalog());
            if (!IsCurrentStageRound(query, target, round)) return null;
            _stageMigrationCollection = collection;
            return collection;
        }
        private bool IsCurrentStageRound(GameDataRestoreQuery query, GameDataSaveTarget target, long round) =>
            round == _stageRoundSerial && query != null && target != null && GameDataTransport.LoggedIn
            && GameDataRestore.IsCurrent(query) && ReferenceEquals(GameDataRestore.LegacyQuery, query)
            && ReferenceEquals(GameDataRestore.LegacyTarget, target);

        private void LoadStageData(StaffStageMigrationCollection collection, EStage stage, bool asynchronous)
        {
            bool handled = false;
            Func<bool> current = () => collection.RefreshValidity();
            Func<bool> canReceive = () => !handled && current();
            void Receive(BackendReturnObject response)
            {
                if (!canReceive()) return;
                handled = true;
                // Preserve raw types/values BEFORE FlattenRows, SetData, A2 and StageInfo.LoadData.
                var raw = collection.Capture(stage, response != null && response.IsSuccess(),
                    response == null ? null : response.GetReturnValue());
                if (raw == null || !raw.CanApplyLegacy || !current()) return;
                _isLoaded = true;
                StageDataTransport.Apply(stage, response, current, asynchronous);
            }
            if (!canReceive()) return;
            try
            {
                if (asynchronous) StageDataTransport.Get(stage, collection.Query.AccountInDate, canReceive, Receive);
                else Receive(StageDataTransport.Get(stage, collection.Query.AccountInDate, canReceive));
            }
            catch (Exception)
            {
                // A throwing/failed query is not an empty owned-staff list. No additional lookup/write.
                if (canReceive()) Receive(null);
            }
        }
        private GameDataRestoreContext GameDataRestore => _gameDataRestoreContext ??
            (_gameDataRestoreContext = new GameDataRestoreContext(
                () => GameDataTransport.LoggedIn ? GameDataTransport.AccountInDate : null));
        public GameDataRestoreEvidence RestoredGameData => GameDataRestore.Evidence;
        public GameDataRestoreResult GameDataRestoreResult => GameDataRestore.Result;

        // 읽기/대기 세션은 무효화하되 이미 보낸 작업의 대상 소유권과 미해결 기록은 유지한다.
        public void InvalidateGameDataRestore()
        {
            InvalidateStageCollection();
            InvalidateGameDataSaveSession();
            GameDataRestore.InvalidateAccountSession();
        }
        private void InvalidateStageCollection()
        {
            _stageRoundSerial = unchecked(_stageRoundSerial + 1);
            _stageMigrationCollection?.RefreshValidity();
        }
        private void InvalidateGameDataSaveSession()
        {
            _gameDataSaveCoordinator?.InvalidateSession();
            _gameDataSaveCoordinator = null;
            _gameDataSaveQuery = null;
        }
        private GameDataRestoreQuery BeginGameDataQuery()
        {
            InvalidateStageCollection();
            InvalidateGameDataSaveSession();
            return GameDataRestore.BeginQuery();
        }
        public bool IsCurrentGameDataQuery(GameDataRestoreQuery query) => GameDataRestore.IsCurrent(query);

        public GameDataAuthenticationAttempt BeginGameDataAuthentication(GameDataAuthenticationKind kind)
        {
            // SDK updates its shared account before callbacks. Do not overlap real authentication calls.
            if (_authenticationInFlight != null) return null;
            InvalidateStageCollection();
            InvalidateGameDataSaveSession();
            return _authenticationInFlight = GameDataRestore.BeginAuthentication(kind, GameDataTransport.NativeLoggedIn);
        }
        public bool IsCurrentGameDataAuthentication(GameDataAuthenticationAttempt attempt) =>
            GameDataRestore.IsCurrentAuthentication(attempt);
        public bool ObserveGameDataAuthenticationResponse(GameDataAuthenticationAttempt attempt)
        {
            if (ReferenceEquals(_authenticationInFlight, attempt)) _authenticationInFlight = null;
            return GameDataRestore.IsCurrentAuthentication(attempt);
        }

        // SDK 5.15 인증 후처리는 원본 gamerInDate로 UserInDate를 갱신한 뒤,
        // status/error/message만 복제한 콜백을 준다(ReturnValue는 비어 있음).
        // 콜백 직후의 SDK 계정 값과 현재 인증 시도를 묶고 인증 원문/토큰은 읽거나 보관하지 않는다.
        public bool CompleteGameDataAuthentication(GameDataAuthenticationAttempt attempt, BackendReturnObject bro)
        {
            if (!ObserveGameDataAuthenticationResponse(attempt)) return false;
            string account = GameDataTransport.AccountInDate;
            _isLogin = true;
            if (!GameDataRestore.CompleteAuthentication(attempt, ToRawResponse(bro), account))
            {
                _isLogin = false;
                return false;
            }
            // A fresh authenticated session still cannot save until its own GameData restoration succeeds.
            _isSaveEnabled = true;
            return true;
        }

        public DateTime LocalTime = DateTime.Now;

        // ServerTime 캐시 (동기 네트워크 호출 빈도 제한)
        private DateTime _cachedServerTime;
        private float _serverTimeCachedAt = -9999f;
        private const float ServerTimeCacheSeconds = 60f;

        public DateTime ServerTime
        {
            get
            {
                if (Time.realtimeSinceStartup - _serverTimeCachedAt < ServerTimeCacheSeconds)
                    return _cachedServerTime;

                BackendReturnObject bro = Backend.Utils.GetServerTime();

                if (bro != null && bro.IsSuccess())
                {
                    string time = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                    _cachedServerTime = DateTime.Parse(time);
                    _serverTimeCachedAt = Time.realtimeSinceStartup;
                    return _cachedServerTime;
                }
                else
                {
                    // 캐시된 서버 시간이 있으면 사용, 없으면 로컬 시간 반환
                    if (_serverTimeCachedAt > 0)
                    {
                        float elapsed = Time.realtimeSinceStartup - _serverTimeCachedAt;
                        return _cachedServerTime.AddSeconds(elapsed);
                    }
                    return LocalTime;
                }
            }
        }

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 전역 오류 처리기 설정
            SetupGlobalErrorHandler();
            
            // 초기화
            Init();
        }
        
        // 전역 예외 핸들러 설정
        private void SetupGlobalErrorHandler()
        {
            Application.logMessageReceived += HandleLog;
            Debug.Log("[BackendManager] 전역 오류 감지 시스템이 활성화되었습니다.");
        }
        
        private void OnDestroy()
        {
            // 이벤트 구독 해제
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                bool isFatal = IsFatalError(logString);
                bool isSevere = !isFatal && IsSevereError(logString);

                if (!isFatal && !isSevere)
                    return;

                // 광고 재생 중 발생한 오류는 ad SDK 자체 오류일 수 있으므로 억제
                if (AdManager.HasInstance && AdManager.IsAdPlaying)
                {
                    Debug.LogWarning($"[BackendManager] 광고 재생 중 예외 감지 (억제됨): {logString.Substring(0, Mathf.Min(logString.Length, 120))}");
                    return;
                }

                // 오류 로그 업로드 시도 (무한 루프 방지를 위해 try-catch 사용)
                try
                {
                    LogUpload("CriticalErrorDetails",
                        $"오류: {logString}\n스택 트레이스: {stackTrace}");
                }
                catch { }

                // 게임을 중단시킬 치명적 오류(메모리 부족, 스택 오버플로우 등)만 팝업+저장 비활성화
                if (isFatal)
                {
                    string truncatedMessage = logString.Length > 100 ? logString.Substring(0, 100) + "..." : logString;

#if !UNITY_EDITOR
                    ShowPopup("알 수 없는 오류", "오류가 발생하여 게임을 종료합니다.\n게임을 재시작 해주세요.");
                    ShowPopupExitButton();
#endif

                    DisableSaving($"치명적 오류 감지: {truncatedMessage}");
                }
            }
        }

        // 즉시 게임을 중단시켜야 하는 치명적 오류 (저장 비활성화 + 팝업 표시)
        private bool IsFatalError(string errorMessage)
        {
            string[] fatalPatterns = {
                "OutOfMemoryException",
                "StackOverflowException",
                "AccessViolationException"
            };

            foreach (var pattern in fatalPatterns)
            {
                if (errorMessage.Contains(pattern))
                    return true;
            }

            return false;
        }

        // 로그 업로드는 하지만 게임 진행을 막지 않는 심각한 오류
        private bool IsSevereError(string errorMessage)
        {
            string[] severePatterns = {
                "NullReferenceException",
                "IndexOutOfRangeException",
                "ArgumentNullException",
                "MissingReferenceException",
                "KeyNotFoundException"
            };

            foreach (var pattern in severePatterns)
            {
                if (errorMessage.Contains(pattern))
                    return true;
            }

            return false;
        }
        
        // 전역 오류 플래그 관리 메서드
        public static void DisableSaving(string reason)
        {
            if (_isSaveEnabled)
            {
                _isSaveEnabled = false;
                Debug.LogError($"[BackendManager] 심각한 오류 발생으로 데이터 저장이 비활성화되었습니다! 이유: {reason}");
                
                // 안전하게 로그 업로드 시도
                try
                {
                    if (Instance != null && Instance._isLogin)
                    {
                        Instance.LogUpload("CriticalError", $"저장 기능 비활성화: {reason}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BackendManager] 오류 로그 업로드 중 추가 예외 발생: {ex.Message}");
                }
            }
        }
        
        public static void EnableSaving()
        {
            _isSaveEnabled = true;
            Debug.Log("[BackendManager] 데이터 저장이 다시 활성화되었습니다.");
        }
        
        private void Init()
        {
            // 동기식 초기화 호출
            bool isSuccess = InitializeBackend();
            if (!isSuccess)
            {
                Debug.LogError("[BackendManager] 뒤끝 초기화 실패");
            }
        }
        
        /// <summary>
        /// 뒤끝 초기화 (동기식)
        /// </summary>
        private bool InitializeBackend()
        {
            _gameDataInitializationPolicy = null;
            try
            {
                GameDataRawResponse response = GameDataSdkInitializationPolicy.ObserveInitialization(
                    ReadGameDataSdkRetrySettings, () => Backend.IsInitialized,
                    () =>
                    {
                        // 기본 초기화를 유지해 인증/시간 제한/국가/콜백 등 기존 설정 전체가 적용되게 한다.
                        BackendReturnObject bro = Backend.Initialize();
                        return bro == null ? null : new GameDataRawResponse(
                            bro.IsSuccess(), bro.GetStatusCode(), bro.GetErrorCode(), bro.GetMessage());
                    }, out _gameDataInitializationPolicy);
                if (response != null && response.IsSuccess)
                {
                    Debug.Log("[BackendManager] 뒤끝 초기화 성공");
                    return true;
                }
                else
                {
                    Debug.LogError("[BackendManager] 뒤끝 초기화 실패");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        private static GameDataSdkRetrySettings ReadGameDataSdkRetrySettings()
        {
            // SDK 5.15.0의 기본 Initialize가 읽는 바로 그 Resource. 민감한 값은 읽거나 출력하지 않는다.
            var settings = Resources.Load<TheBackendSettings>("TheBackendSettings");
            return settings == null ? null : new GameDataSdkRetrySettings(typeof(Backend).Assembly.GetName().Version,
                settings.retryWhenClientRequestFailError, settings.retryWhenServerError, settings.autoRefreshToken);
        }

        #region 비동기/동기 작업 처리를 위한 공통 메서드

        /// <summary>
        /// 백엔드 API 호출을 처리하는 중앙 함수 (비동기)
        /// </summary>
        internal void ProcessBackendAPI(
            string operationName,
            Action<Action<BackendReturnObject>> backendFunction, 
            Action<BackendReturnObject> onSuccess = null,
            Action<BackendState> onFail = null, 
            int maxRetries = 3,
            bool usePopup = true,
            Func<bool> isCurrent = null)
        {
            if (isCurrent != null && !isCurrent()) return;
            if (!_isSaveEnabled && operationName.Contains("저장"))
            {
                Debug.LogWarning($"[BackendManager] 저장이 비활성화되어 있어 {operationName}이 중단되었습니다.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }

            // 로그인 검사 (초기화, 로그인 관련 작업은 제외)
            if (!IsLogin && !operationName.Contains("초기화") && !operationName.Contains("로그인") && !operationName.Contains("버전"))
            {
                Debug.LogError($"[BackendManager] 로그인이 필요한 작업({operationName})이 로그인 없이 시도되었습니다.");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            int retryCount = 0;

            void Send(Action<BackendReturnObject> callback)
            {
                if (isCurrent == null || isCurrent()) backendFunction(callback);
            }

            // 콜백 처리 함수
            void HandleCallback(BackendReturnObject bro)
            {
                if (isCurrent != null && !isCurrent()) return;
                BackendState state = HandleError(bro);
                
                if (state == BackendState.Success)
                {
                    Debug.Log($"[BackendManager] {operationName} 성공");
                    onSuccess?.Invoke(bro);
                }
                else if (state == BackendState.Retry && retryCount < maxRetries)
                {
                    retryCount++;
                    Debug.Log($"[BackendManager] {operationName} 재시도({retryCount}/{maxRetries})");
                    Send(HandleCallback);
                }
                else
                {
                    string errorMessage = bro != null ? bro.GetMessage() : "BackendReturnObject is null";
                    string errorCode = bro != null ? bro.GetErrorCode() : "NULL_RESPONSE";
                    Debug.LogError($"[BackendManager] {operationName} 실패: {errorMessage}");
                    
                    if (usePopup)
                    {
                        ShowPopup("네트워크 에러", 
                            $"{operationName}에 실패했습니다.\n다시 시도해 주세요.\n오류 코드: {errorCode}");
                        SetPopupButton1("재시도", () => Send(HandleCallback));
                        ShowPopupExitButton();
                    }
                    
                    onFail?.Invoke(state);
                }
            }
            
            // API 호출
            Send(HandleCallback);
        }

        /// <summary>
        /// 백엔드 API 호출을 처리하는 중앙 함수 (동기)
        /// </summary>
        /// <param name="operationName">작업 이름</param>
        /// <param name="backendFunction">동기 백엔드 함수를 호출하는 람다식</param>
        /// <param name="maxRetries">최대 재시도 횟수</param>
        /// <param name="usePopup">실패 시 팝업 표시 여부</param>
        /// <returns>처리 결과 (백엔드 반환 객체)</returns>
        internal BackendReturnObject ProcessBackendAPISync(
            string operationName,
            Func<BackendReturnObject> backendFunction,
            int maxRetries = 3,
            bool usePopup = true,
            Func<bool> isCurrent = null)
        {
            if (isCurrent != null && !isCurrent()) return null;
            if (!_isSaveEnabled && operationName.Contains("저장"))
            {
                Debug.LogWarning($"[BackendManager] 저장이 비활성화되어 있어 {operationName}이 중단되었습니다.");
                return null;
            }

            // 로그인 검사 (초기화, 로그인 관련 작업은 제외)
            if (!IsLogin && !operationName.Contains("초기화") && !operationName.Contains("로그인"))
            {
                Debug.LogError($"[BackendManager] 로그인이 필요한 작업({operationName})이 로그인 없이 시도되었습니다.");
                return null;
            }

            try
            {
                int retryCount = 0;
                BackendReturnObject bro = null;
                BackendState state = BackendState.Failure;
                
                do
                {
                    if (isCurrent != null && !isCurrent()) return null;
                    // API 호출
                    bro = backendFunction();
                    if (isCurrent != null && !isCurrent()) return null;
                    state = HandleError(bro);

                    if (state == BackendState.Success)
                    {
                        Debug.Log($"[BackendManager] {operationName} 성공");
                        return bro;
                    }
                    else if (state == BackendState.Retry && retryCount < maxRetries)
                    {
                        retryCount++;
                        Debug.Log($"[BackendManager] {operationName} 재시도({retryCount}/{maxRetries})");
                        continue;
                    }
                    else
                    {
                        string errorMessage = bro != null ? bro.GetMessage() : "BackendReturnObject is null";
                        string errorCode = bro != null ? bro.GetErrorCode() : "NULL_RESPONSE";
                        Debug.LogError($"[BackendManager] {operationName} 실패: {errorMessage}");
                        
                        if (usePopup)
                        {
                            ShowPopup("네트워크 에러", 
                                $"{operationName}에 실패했습니다.\n다시 시도해 주세요.\n오류 코드: {errorCode}");
                        }
                        
                        return bro;
                    }
                } while (state == BackendState.Retry && retryCount <= maxRetries);
                
                return bro;
            }
            catch (Exception ex)
            {
                if (isCurrent != null && !isCurrent()) return null;
                Debug.LogException(ex);
                Debug.LogError($"[BackendManager] {operationName} 처리 중 예외 발생: {ex.Message}");
                
                if (usePopup)
                {
                    ShowPopup("오류 발생", $"{operationName} 실행 중 오류가 발생했습니다: {ex.Message}");
                }
                
                return null;
            }
        }

        #endregion

        #region 사용자 인증 관련 메서드 (비동기)

        /// <summary>
        /// 서버 시간을 비동기적으로 가져옵니다
        /// </summary>
        public void GetServerTimeAsync(Action<DateTime> onSuccess = null, Action<BackendState> onFail = null)
        {
            ProcessBackendAPI(
                "서버 시간 조회",
                cb => cb(Backend.Utils.GetServerTime()), // ← 결과를 cb로 전달
                bro =>
                {
                    try
                    {
                        string time = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                        var dt = DateTime.Parse(time, null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal);
                        onSuccess?.Invoke(dt);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        onFail?.Invoke(BackendState.Failure);
                    }
                },
                onFail,
                1,
                false
            );
        }
        
        /// <summary>
        /// 커스텀 로그인을 비동기적으로 수행합니다
        /// </summary>
        public void CustomLoginAsync(string id, string pw, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] 이미 로그인되어 있습니다.");
                return;
            }
            
            GameDataAuthenticationAttempt attempt = null;
            ProcessBackendAPI(
                "커스텀 로그인",
                (callback) => {
                    var started = attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Other);
                    if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                    Backend.BMember.CustomLogin(id, pw, bro => {
                        if (ObserveGameDataAuthenticationResponse(started)) callback?.Invoke(bro);
                    });
                },
                (bro) => {
                    if (!CompleteGameDataAuthentication(attempt, bro)) return;
                    Debug.Log("[BackendManager] 커스텀 로그인 성공");
                    onSuccess?.Invoke(bro);
                },
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// 게스트 로그인을 비동기적으로 수행합니다
        /// </summary>
        public void GuestLoginAsync(Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] 이미 로그인되어 있습니다.");
                return;
            }

            GameDataAuthenticationAttempt attempt = null;
            void HandleGuestLogin(Action<BackendReturnObject> callback)
            {
                var started = attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
                if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                Backend.BMember.GuestLogin((bro) => {
                    if (!ObserveGameDataAuthenticationResponse(started)) return;
                    // 특수 케이스: bro null 또는 실패
                    if (bro == null)
                    {
                        Debug.LogError("[BackendManager] 게스트 로그인 실패: bro == null");
                        onFail?.Invoke(BackendState.Failure);
                        return;
                    }

                    if (!bro.IsSuccess())
                    {
                        Debug.LogError("[BackendManager] 게스트 로그인 실패: " + bro.GetMessage());
                        if (bro.GetStatusCode() == "403")
                        {
                            Debug.LogWarning("[BackendManager] 접근 차단된 계정입니다.");
                            ShowPopup("접근 차단", "이 계정은 접근이 차단되었습니다.\n자세한 문의는 고객센터를 이용해주세요.");
                            ShowPopupExitButton();
                            return; // 재시도 팝업 없이 종료 버튼만 노출
                        }

                        if (bro.GetStatusCode() == "401")
                        {
                            Debug.Log("[BackendManager] 게스트 정보가 없어 삭제 후 재시도합니다.");
                            Backend.BMember.DeleteGuestInfo();
                        }

                        onFail?.Invoke(BackendState.Failure);
                        return;
                    }


                    
                    callback(bro);
                });
            }
            
            ProcessBackendAPI(
                "게스트 로그인",
                HandleGuestLogin,
                (bro) => {
                    if (!CompleteGameDataAuthentication(attempt, bro)) return;
                    
                    // 신규 가입 또는 기존 로그인 처리
                    if (bro.GetStatusCode() == "201")
                    {
                        Debug.Log("[BackendManager] 게스트 신규 가입 성공");
                        OnGuestSignupHandler?.Invoke();
                    }
                    else if (bro.GetStatusCode() == "200")
                    {
                        Debug.Log("[BackendManager] 게스트 로그인 성공");
                        OnGuestLoginHandler?.Invoke();
                    }
                    
                    onSuccess?.Invoke(bro);
                },
                onFail,
                3,
                true
            );
        }

        /// <summary>
        /// 뒤끝 서버에서 유저의 gamerId(UUID)를 조회해 UserInfo.GamerId에 저장합니다.
        /// 로그인 직후 한 번 호출하면 이후 모든 UI에서 재사용할 수 있습니다.
        /// </summary>
        public void FetchGamerIdAsync(Action onSuccess = null, Action onFail = null, Func<bool> canApply = null)
        {
            if (canApply != null && !canApply()) return;
            Backend.BMember.GetUserInfo((bro) =>
            {
                if (canApply != null && !canApply()) return;
                if (bro.IsSuccess())
                {
                    string gamerId = bro.GetReturnValuetoJSON()["row"]["gamerId"]?.ToString();
                    if (!string.IsNullOrEmpty(gamerId))
                        UserInfo.SetGamerId(gamerId);
                    Debug.Log($"[BackendManager] GamerId 조회 완료: {gamerId}");
                    onSuccess?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[BackendManager] GamerId 조회 실패: " + bro.GetMessage());
                    onFail?.Invoke();
                }
            });
        }


        public void LogOut()
        {
            InvalidateGameDataRestore();
            _isSaveEnabled = false;
            _isLogin = false;
        }

        /// <summary>
        /// 페더레이션 로그인 성공을 외부에서 통보받아 로그인 상태를 활성화합니다.
        /// GoogleLoginManager 등 외부에서 Backend.BMember.AuthorizeFederation 직접 호출 후 사용합니다.
        /// </summary>
        public bool NotifyFederationLoginSuccess(GameDataAuthenticationAttempt attempt, BackendReturnObject bro)
        {
            if (!CompleteGameDataAuthentication(attempt, bro)) return false;
            _isSaveEnabled = true;
            Debug.Log("[BackendManager] 페더레이션 로그인 상태 활성화");
            return true;
        }

        /// <summary>
        /// 이미 획득한 GPGS2 AccessToken으로 뒤끝 연동 로그인을 수행합니다 (계정 전환 시 사용).
        /// LogOut() 호출 후 이 메서드를 사용하여 다른 구글 연동 계정으로 전환합니다.
        /// </summary>
        public void FederationLoginWithAccessTokenAsync(string accessToken, FederationType federationType, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            
            if (IsLogin)
            {
                Debug.LogWarning("[BackendManager] FederationLoginWithAccessToken: 이미 로그인 상태입니다.");
                return;
            }

            GameDataAuthenticationAttempt attempt = null;
            ProcessBackendAPI(
                "구글 연동 계정 전환 로그인",
                (callback) => {
                    var started = attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Other);
                    if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                    Backend.BMember.AuthorizeFederation(accessToken, federationType, bro => {
                        if (ObserveGameDataAuthenticationResponse(started)) callback?.Invoke(bro);
                    });
                },
                (bro) =>
                {
                    Debug.Log($"[BackendManager] AuthorizeFederation 응답 statusCode: {bro.GetStatusCode()}, message: {bro.GetMessage()}");
                    if (bro.GetStatusCode() == "201")
                    {
                        // 201 = 신규 계정 생성 → 연동된 계정으로 전환 실패 (토큰이 만료되었거나 잘못된 경우)
                        Debug.LogError("[BackendManager] 연동 계정 전환 실패: 201 신규 계정 생성됨. accessToken이 이미 만료되었을 수 있습니다.");
                        _isLogin = false;
                        _isSaveEnabled = false;
                        Backend.BMember.Logout();
                        onFail?.Invoke(BackendState.Failure);
                        return;
                    }
                    if (!CompleteGameDataAuthentication(attempt, bro)) return;
                    _isSaveEnabled = true;
                    Debug.Log($"[BackendManager] 구글 연동 계정 전환 로그인 성공 (statusCode: {bro.GetStatusCode()})");
                    onSuccess?.Invoke(bro);
                },
                onFail,
                0,
                false
            );
        }

        /// <summary>
        /// 서버 인증 코드(또는 IdToken)로 뒤끝 연동 로그인을 비동기적으로 수행합니다
        /// </summary>
        public void GoogleFederationLoginAsync(string authCode, FederationType federationType, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] 이미 로그인되어 있습니다.");
                return;
            }

            GameDataAuthenticationAttempt attempt = null;
            if (federationType == FederationType.GPGS2)
            {
                // GPGS2: authCode → GetGPGS2AccessToken → AuthorizeFederation 2단계
                ProcessBackendAPI(
                    "GPGS2 로그인 액세스 토큰 획득",
                    (callback) => {
                        var started = attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Other);
                        if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                        Backend.BMember.GetGPGS2AccessToken(authCode, bro => {
                            if (ObserveGameDataAuthenticationResponse(started)) callback?.Invoke(bro);
                        });
                    },
                    (bro) =>
                    {
                        string accessToken = bro.GetReturnValuetoJSON()["access_token"].ToString();
                        Debug.Log("[BackendManager] GetGPGS2AccessToken 성공, 뒤끝 연동 로그인 시도");
                        ProcessBackendAPI(
                            "GPGS2 연동 로그인",
                            (callback2) => {
                                var started = attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Google);
                                if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                                Backend.BMember.AuthorizeFederation(accessToken, federationType, bro2 => {
                                    if (ObserveGameDataAuthenticationResponse(started)) callback2?.Invoke(bro2);
                                });
                            },
                            (bro2) =>
                            {
                                if (!CompleteGameDataAuthentication(attempt, bro2)) return;
                                if (bro2.GetStatusCode() == "201")
                                    Debug.Log("[BackendManager] GPGS2 연동 신규 가입 성공");
                                else
                                    Debug.Log("[BackendManager] GPGS2 연동 로그인 성공");
                                onSuccess?.Invoke(bro2);
                            },
                            onFail,
                            0,
                            false
                        );
                    },
                    onFail,
                    0,
                    false
                );
            }
            else
            {
                ProcessBackendAPI(
                    "구글 연동 로그인",
                    (callback) => {
                        var started = attempt = BeginGameDataAuthentication(federationType == FederationType.Google
                            ? GameDataAuthenticationKind.Google : GameDataAuthenticationKind.Other);
                        if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                        Backend.BMember.AuthorizeFederation(authCode, federationType, bro => {
                            if (ObserveGameDataAuthenticationResponse(started)) callback?.Invoke(bro);
                        });
                    },
                    (bro) =>
                    {
                        if (!CompleteGameDataAuthentication(attempt, bro)) return;
                        if (bro.GetStatusCode() == "201")
                            Debug.Log($"[BackendManager] {federationType} 연동 신규 가입 성공");
                        else
                            Debug.Log($"[BackendManager] {federationType} 연동 로그인 성공");
                        onSuccess?.Invoke(bro);
                    },
                    onFail,
                    0,
                    false
                );
            }
        }

        /// <summary>
        /// 뒤끝 토큰으로 자동 로그인을 비동기적으로 시도합니다
        /// </summary>
        public void TokenLoginAsync(Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] 이미 로그인되어 있습니다.");
                return;
            }

            GameDataAuthenticationAttempt attempt = null;
            ProcessBackendAPI(
                "토큰 자동 로그인",
                (callback) => {
                    var started = attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Other);
                    if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                    Backend.BMember.LoginWithTheBackendToken(bro => {
                        if (ObserveGameDataAuthenticationResponse(started)) callback?.Invoke(bro);
                    });
                },
                (bro) =>
                {
                    if (!CompleteGameDataAuthentication(attempt, bro)) return;
                    _isSaveEnabled = true;
                    Debug.Log("[BackendManager] 토큰 자동 로그인 성공");
                    onSuccess?.Invoke(bro);
                },
                onFail,
                1,
                false
            );
        }
        
        /// <summary>
        /// 회원가입을 비동기적으로 수행합니다
        /// </summary>
        public void CustomSignupAsync(string id, string pw, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            GameDataAuthenticationAttempt attempt = null;
            ProcessBackendAPI(
                "회원가입",
                callback => {
                    var started = attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Other);
                    if (started == null) { onFail?.Invoke(BackendState.Failure); return; }
                    Backend.BMember.CustomSignUp(id, pw, bro => {
                        if (ObserveGameDataAuthenticationResponse(started)) callback?.Invoke(bro);
                    });
                },
                bro => { if (CompleteGameDataAuthentication(attempt, bro)) onSuccess?.Invoke(bro); },
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// 닉네임을 생성합니다
        /// </summary>
        public void CreateNickNameAsync(string nickName, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            // 중복 체크 먼저 수행
            ProcessBackendAPI(
                "닉네임 중복 체크",
                (callback) => Backend.BMember.CheckNicknameDuplication(nickName, (bro) => callback?.Invoke(bro)),
                (checkBro) => {
                    // 닉네임 생성
                    ProcessBackendAPI(
                        "닉네임 생성",
                        (callback) => Backend.BMember.CreateNickname(nickName, (bro) => callback?.Invoke(bro)),
                        onSuccess,
                        onFail,
                        2,
                        true
                    );
                },
                onFail,
                1,
                true
            );
        }
        
        /// <summary>
        /// 닉네임을 업데이트합니다
        /// </summary>
        public void UpdateNickNameAsync(string nickName, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            // 중복 체크 먼저 수행
            ProcessBackendAPI(
                "닉네임 중복 체크",
                (callback) => Backend.BMember.CheckNicknameDuplication(nickName, (bro) => callback?.Invoke(bro)),
                (checkBro) => {
                    // 닉네임 업데이트
                    ProcessBackendAPI(
                        "닉네임 업데이트",
                        (callback) => Backend.BMember.UpdateNickname(nickName, (bro) => callback?.Invoke(bro)),
                        onSuccess,
                        onFail,
                        2,
                        true
                    );
                },
                onFail,
                1,
                true
            );
        }

        #endregion
        
        #region 사용자 인증 관련 메서드 (동기)

        /// <summary>
        /// 커스텀 로그인을 동기적으로 수행합니다
        /// </summary>
        public bool CustomLogin(string id, string pw)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] 이미 로그인되어 있습니다.");
                return true;
            }
            
            GameDataAuthenticationAttempt attempt = null;
            BackendReturnObject bro = ProcessBackendAPISync(
                "커스텀 로그인",
                () => {
                    attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Other);
                    if (attempt == null) return null;
                    var response = Backend.BMember.CustomLogin(id, pw);
                    ObserveGameDataAuthenticationResponse(attempt);
                    return response;
                },
                3,
                true
            );
            
            if (bro != null && bro.IsSuccess())
            {
                if (!CompleteGameDataAuthentication(attempt, bro)) return false;
                Debug.Log("[BackendManager] 커스텀 로그인 성공");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 게스트 로그인을 동기적으로 수행합니다
        /// </summary>
        public bool GuestLogin()
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] 이미 로그인되어 있습니다.");
                return true;
            }
            
            // 특수 케이스: 게스트 정보가 없는 경우 처리
            var attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
            if (attempt == null) return false;
            BackendReturnObject bro = Backend.BMember.GuestLogin();
            ObserveGameDataAuthenticationResponse(attempt);
            if (bro.GetStatusCode() == "401")
            {
                Debug.Log("[BackendManager] 게스트 정보가 없어 삭제 후 재시도합니다.");
                Backend.BMember.DeleteGuestInfo();
                
                bro = ProcessBackendAPISync(
                    "게스트 로그인",
                    () => {
                        attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Guest);
                        if (attempt == null) return null;
                        var response = Backend.BMember.GuestLogin();
                        ObserveGameDataAuthenticationResponse(attempt);
                        return response;
                    },
                    3,
                    true
                );
            }
            else
            {
                // 정상적인 처리 과정
                bro = ProcessBackendAPISync(
                    "게스트 로그인",
                    () => bro,  // 이미 호출된 결과 사용
                    3,
                    true
                );
            }
            
            if (bro != null && bro.IsSuccess())
            {
                if (!CompleteGameDataAuthentication(attempt, bro)) return false;
                
                // 신규 가입 또는 기존 로그인 처리
                if (bro.GetStatusCode() == "201")
                {
                    Debug.Log("[BackendManager] 게스트 신규 가입 성공");
                    OnGuestSignupHandler?.Invoke();
                }
                else if (bro.GetStatusCode() == "200")
                {
                    Debug.Log("[BackendManager] 게스트 로그인 성공");
                    OnGuestLoginHandler?.Invoke();
                }
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 회원가입을 동기적으로 수행합니다
        /// </summary>
        public bool CustomSignup(string id, string pw)
        {
            GameDataAuthenticationAttempt attempt = null;
            BackendReturnObject bro = ProcessBackendAPISync(
                "회원가입",
                () => {
                    attempt = BeginGameDataAuthentication(GameDataAuthenticationKind.Other);
                    if (attempt == null) return null;
                    var response = Backend.BMember.CustomSignUp(id, pw);
                    ObserveGameDataAuthenticationResponse(attempt);
                    return response;
                },
                3,
                true
            );
            
            return bro != null && bro.IsSuccess() && CompleteGameDataAuthentication(attempt, bro);
        }
        
        /// <summary>
        /// 닉네임을 생성합니다 (동기식)
        /// </summary>
        public bool CreateNickName(string nickName)
        {
            // 중복 체크
            BackendReturnObject checkBro = ProcessBackendAPISync(
                "닉네임 중복 체크",
                () => Backend.BMember.CheckNicknameDuplication(nickName),
                1,
                true
            );
            
            if (checkBro == null || !checkBro.IsSuccess())
                return false;
            
            // 닉네임 생성
            BackendReturnObject createBro = ProcessBackendAPISync(
                "닉네임 생성",
                () => Backend.BMember.CreateNickname(nickName),
                2,
                true
            );
            
            return createBro != null && createBro.IsSuccess();
        }
        
        /// <summary>
        /// 닉네임을 업데이트합니다 (동기식)
        /// </summary>
        public bool UpdateNickName(string nickName)
        {
            // 중복 체크
            BackendReturnObject checkBro = ProcessBackendAPISync(
                "닉네임 중복 체크",
                () => Backend.BMember.CheckNicknameDuplication(nickName),
                1,
                true
            );
            
            if (checkBro == null || !checkBro.IsSuccess())
                return false;
            
            // 닉네임 업데이트
            BackendReturnObject updateBro = ProcessBackendAPISync(
                "닉네임 업데이트",
                () => Backend.BMember.UpdateNickname(nickName),
                2,
                true
            );
            
            return updateBro != null && updateBro.IsSuccess();
        }

        #endregion

        #region 데이터 관련 메서드 (비동기)

        /// <summary>
        /// 생성 당시 계정/행/인증·복원 조회 객체에 고정한다. 같은 계정 재로그인도 재사용할 수 없다.
        /// 두 오류 재시도 OFF가 실제 기본 초기화에 적용된 증거가 있어야 전송한다.
        /// 인증 거절 후 자동 토큰 갱신은 허용하지만, 결과 미확정의 재전송은 허용하지 않는다.
        /// </summary>
        public GameDataSingleUpdate CreateGameDataSingleUpdate()
        {
            return CreateBoundGameDataSingleUpdate(GameDataRestore.LegacyQuery, GameDataRestore.LegacyTarget,
                () => null, requireStaffReady: true);
        }

        private GameDataSingleUpdate CreateBoundGameDataSingleUpdate(GameDataRestoreQuery query,
            GameDataSaveTarget target, Func<GameDataSaveCoordinator> readOwner, bool requireStaffReady)
        {
            GameDataSingleUpdate updater = null;
            updater = new GameDataSingleUpdate(() =>
            {
                bool current = IsCurrentGameDataSaveSession(query, target);
                bool staffReady = !requireStaffReady || ReferenceEquals(RestoredGameData?.Query, query);
                return new GameDataSaveReadiness(
                    _isSaveEnabled, GameDataTransport.LoggedIn, current && staffReady,
                    !requireStaffReady || GameDataTransport.GameplaySaveAllowed,
                    GameDataTransport.AccountInDate, target,
                    transportBlockReason: GameDataSaveCoordinator.IsTargetOwnedByOther(target, readOwner() ?? updater.Owner)
                        ? "이 GameData 행은 다른 진행 중/미해결 저장이 소유하고 있습니다." : null,
                    initializationPolicy: _gameDataInitializationPolicy);
            }, new BackendGameDataUpdateTransport((identity, values, response) =>
            {
                // Same injected boundary as read/bootstrap. SDK 5.15 default initialization uses
                // UnityWebRequest + CallbackUpdateManager.Update (main thread); fakes may reply inline.
                GameDataTransport.Update(identity.Target, values, bro => response(identity, ToRawResponse(bro)));
            }), isSessionCurrent: () => IsCurrentGameDataSaveSession(query, target));
            return updater;
        }

        /// <summary>기존 단일 Get을 재사용한다. 공용 필드 부재만 기존 게임 복원을 허용한다.</summary>
        public void GetAndRestoreGameDataAsync(Action<GameDataRestoreQuery, GameDataRestoreResult> onComplete,
            Action<BackendState> onFail = null)
        {
            GameDataRestoreQuery query = BeginGameDataQuery();
            GetMyDataAsyncCore("GameData", bro =>
            {
                if (!GameDataRestore.CanHandle(query)) return;
                GameDataRestoreResult result = GameDataRestore.TryRestore(query, bro != null && bro.IsSuccess(),
                    bro?.GetReturnValue(), GameDataTransport.ReadCatalog, () => GameDataTransport.Restore(bro));
                if (result.Status == GameDataRestoreStatus.RowMissing
                    && TryCreateInitialGameData(query, onComplete, onFail)) return;
                if (GameDataRestore.IsCurrent(query))
                {
                    // Compatibility for other tables; GameData writers never use this flag as restore proof.
                    if (result.CanContinueLegacy) _isLoaded = true;
                    onComplete?.Invoke(query, result);
                }
            }, state =>
            {
                if (!GameDataRestore.CanHandle(query)) return;
                GameDataRestore.TryRestore(query, false, null, null, null);
                if (GameDataRestore.IsCurrent(query)) onFail?.Invoke(state);
            }, query);
        }

        private bool TryCreateInitialGameData(GameDataRestoreQuery query,
            Action<GameDataRestoreQuery, GameDataRestoreResult> onComplete, Action<BackendState> onFail)
        {
            if (!_isSaveEnabled || !GameDataTransport.LoggedIn
                || !GameDataRestore.TryBeginInitialCreation(query, _gameDataInitializationPolicy, out _)) return false;
            try
            {
                // Detached initial defaults, never the previous account's UserInfo snapshot.
                if (!GameDataSavePayload.TryCapture(GameDataTransport.InitialValues(), out var payload, out _))
                    throw new InvalidOperationException("Initial GameData payload validation failed.");
                Param values = payload.CreateParamCopy();
                if (!GameDataRestore.IsCurrent(query) || !GameDataTransport.LoggedIn || !_isSaveEnabled
                    || _gameDataInitializationPolicy == null || !_gameDataInitializationPolicy.IsSupported) return true;
                // Exactly one public SDK invocation. No ProcessBackendAPI/automatic/popup retry.
                GameDataTransport.Insert(values, bro =>
                {
                    GameDataRestoreResult result = GameDataRestore.CompleteInitialCreation(query, ToRawResponse(bro));
                    if (!GameDataRestore.IsCurrent(query)) return;
                    if (result.Status == GameDataRestoreStatus.InitialCreationConfirmedAwaitingRestore)
                        GetAndRestoreGameDataAsync(onComplete, onFail);
                    else if (result.Status == GameDataRestoreStatus.InitialCreationIndeterminate)
                        onComplete?.Invoke(query, result);
                });
            }
            catch
            {
                var result = GameDataRestore.CompleteInitialCreation(query, null);
                if (GameDataRestore.IsCurrent(query)
                    && result.Status == GameDataRestoreStatus.InitialCreationIndeterminate)
                    onComplete?.Invoke(query, result);
            }
            return true;
        }

        private static GameDataRawResponse ToRawResponse(BackendReturnObject bro) => bro == null ? null
            : new GameDataRawResponse(bro.IsSuccess(), bro.GetStatusCode(), bro.GetErrorCode(), bro.GetMessage(), bro);

        public bool CanSaveLegacyGameData
        {
            get
            {
                var query = GameDataRestore.LegacyQuery;
                var target = GameDataRestore.LegacyTarget;
                var owner = ReferenceEquals(_gameDataSaveQuery, query) ? _gameDataSaveCoordinator : null;
                return IsCurrentGameDataSaveSession(query, target)
                    && _gameDataInitializationPolicy != null && _gameDataInitializationPolicy.IsSupported
                    && !GameDataSaveCoordinator.IsTargetOwnedByOther(target, owner)
                    && (owner == null || (owner.State != GameDataSaveCoordinatorState.Indeterminate
                        && owner.State != GameDataSaveCoordinatorState.LocalCompletionFailed
                        && owner.State != GameDataSaveCoordinatorState.InvalidatedAfterSend));
            }
        }

        private bool IsCurrentGameDataSaveSession(GameDataRestoreQuery query, GameDataSaveTarget target)
        {
            return _isSaveEnabled && GameDataTransport.LoggedIn && target != null
                && GameDataRestore.IsCurrent(query) && ReferenceEquals(query, GameDataRestore.LegacyQuery)
                && target.Matches(GameDataRestore.LegacyTarget) && !GameDataRestore.IsInitialCreationBlocked;
        }

        private GameDataSaveCoordinator GetGameDataSaveCoordinator()
        {
            if (!CanSaveLegacyGameData) return null;
            var query = GameDataRestore.LegacyQuery;
            var target = GameDataRestore.LegacyTarget;
            if (ReferenceEquals(_gameDataSaveQuery, query) && _gameDataSaveCoordinator != null)
                return _gameDataSaveCoordinator;

            InvalidateGameDataSaveSession();
            GameDataSaveCoordinator owner = null;
            var updater = CreateBoundGameDataSingleUpdate(query, target, () => owner, requireStaffReady: false);
            owner = new GameDataSaveCoordinator(target, updater, () =>
            {
                if (!IsCurrentGameDataSaveSession(query, target))
                    throw new InvalidOperationException("오래된 세션의 최신 자료를 생성할 수 없습니다.");
                return GameDataTransport.LatestValues();
            }, isSessionCurrent: () => IsCurrentGameDataSaveSession(query, target));
            _gameDataSaveQuery = query;
            _gameDataSaveCoordinator = owner;
            return owner;
        }

        /// <summary>접수/전송/성공은 ticket에서 구분한다. 앞선 완료 후 전체 최신 자료를 생성하며 병합 시 Param을 보관하지 않는다.</summary>
        public GameDataSaveRequest RequestGameDataAutosave(Action<BackendReturnObject> onSuccess = null,
            Action<BackendState> onFail = null, bool requireGameplay = false)
        {
            var coordinator = GetGameDataSaveCoordinator();
            if (coordinator == null) return RejectGameDataRequest("현재 복원 세션/정책/대상 소유권으로 저장을 접수할 수 없습니다.", onFail);
            Func<bool> guard = requireGameplay
                ? (_gameDataGameplayGate ?? (_gameDataGameplayGate = () => GameDataTransport.GameplaySaveAllowed)) : null;
            return SubmitGameDataRequest(coordinator, null, true, onSuccess, onFail, guard);
        }

        /// <summary>명시된 변경값은 고정된 별도 FIFO 작업이다. 부분 필드와 성공 통지를 자동 저장으로 대체하지 않는다.</summary>
        public GameDataSaveRequest RequestGameDataSave(Param values, string expectedRow = null,
            Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            var coordinator = GetGameDataSaveCoordinator();
            var query = _gameDataSaveQuery;
            var target = GameDataRestore.LegacyTarget;
            if (coordinator == null || target == null || (expectedRow != null
                && !string.Equals(expectedRow, target.RowInDate, StringComparison.Ordinal)))
                return RejectGameDataRequest("현재 복원된 저장 대상과 요청이 일치하지 않습니다.", onFail);
            if (!GameDataSavePayload.TryCapture(values, out var payload, out string error))
                return RejectGameDataRequest(error, onFail);
            if (!IsCurrentGameDataSaveSession(query, target))
                return GameDataSaveRequest.Rejected("자료 고정 중 저장 세션이 무효화되었습니다.");
            return SubmitGameDataRequest(coordinator, () => payload.CreateParamCopy(), false, onSuccess, onFail);
        }

        private static GameDataSaveRequest RejectGameDataRequest(string error, Action<BackendState> onFail)
        {
            onFail?.Invoke(BackendState.NotSave);
            return GameDataSaveRequest.Rejected(error);
        }

        private GameDataSaveRequest SubmitGameDataRequest(GameDataSaveCoordinator coordinator, Func<Param> createValues,
            bool autosave, Action<BackendReturnObject> onSuccess, Action<BackendState> onFail,
            Func<bool> canCreateValues = null)
        {
            var query = _gameDataSaveQuery;
            var target = GameDataRestore.LegacyTarget;
            bool failureReported = false;
            Action<GameDataSaveReceipt> confirmed = receipt =>
            {
                if (IsCurrentGameDataSaveSession(query, target)) onSuccess?.Invoke(receipt.RawResponse?.NativeResponse);
            };
            Action<GameDataSaveRequest> changed = request =>
            {
                if (failureReported || !IsCurrentGameDataSaveSession(query, target)) return;
                if (request.Status == GameDataSaveRequestStatus.RejectedBeforeSend
                    || request.Status == GameDataSaveRequestStatus.Indeterminate
                    || request.Status == GameDataSaveRequestStatus.LocalCompletionFailed)
                {
                    failureReported = true;
                    onFail?.Invoke(request.Status == GameDataSaveRequestStatus.RejectedBeforeSend
                        ? BackendState.NotSave : BackendState.Failure);
                }
            };
            return autosave ? coordinator.RequestAutosave(confirmed, changed, canCreateValues)
                : coordinator.EnqueueSave(createValues, confirmed, changed);
        }

        private void SaveLegacyGameDataAsync(Param values, string expectedRow,
            Action<BackendReturnObject> onSuccess, Action<BackendState> onFail) =>
            RequestGameDataSave(values, expectedRow, onSuccess, onFail);

        // Compatibility only: false may mean accepted but still pending, never an assertion of non-application.
        // Runtime callers use RequestGameDataAutosave's success callback. No synchronous SDK call/wait is made.
        private bool SaveLegacyGameData(Param values, string expectedRow)
        {
            return RequestGameDataSave(values, expectedRow).Status == GameDataSaveRequestStatus.SuccessConfirmed;
        }
        
        /// <summary>
        /// 유저 데이터를 조회합니다
        /// </summary>
        public void GetMyDataAsync(string tableId, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            // 호환 조회도 새 GameData 조회이면 이전 증거를 폐기한다. Stage 조회와는 수명을 공유하지 않는다.
            GameDataRestoreQuery query = tableId == "GameData" ? BeginGameDataQuery() : null;
            GetMyDataAsyncCore(tableId, onSuccess, onFail, query);
        }

        private void GetMyDataAsyncCore(string tableId, Action<BackendReturnObject> onSuccess,
            Action<BackendState> onFail, GameDataRestoreQuery query)
        {
            if (tableId == "GameData")
            {
                if (!GameDataRestore.CanHandle(query) || !GameDataTransport.LoggedIn) return;
                try
                {
                    // Reuse the same Get; never retain a failed query's old popup retry closure.
                    GameDataTransport.Get(query.AccountInDate, bro =>
                    {
                        if (!GameDataRestore.CanHandle(query)) return;
                        if (bro != null && bro.IsSuccess()) onSuccess?.Invoke(bro);
                        else onFail?.Invoke(BackendState.Failure);
                    });
                }
                catch { if (GameDataRestore.CanHandle(query)) onFail?.Invoke(BackendState.Failure); }
                return;
            }
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] 로그인이 되어있지 않아 데이터를 조회할 수 없습니다.");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            // 유저 조건 생성
            Where where = new Where();
            where.Equal("owner_inDate", query != null ? query.AccountInDate : Backend.UserInDate);

            ProcessBackendAPI(
                $"{tableId} 데이터 조회",
                (callback) =>
                {
                    if (query != null && !GameDataRestore.IsCurrent(query)) return;
                    Backend.GameData.Get(tableId, where, bro =>
                    {
                        // 구세대 응답은 오류 재시도 처리기/메모리 적용/후속 초기화보다 먼저 차단한다.
                        if (query == null || GameDataRestore.IsCurrent(query)) callback?.Invoke(bro);
                    });
                },
                (bro) => {
                    if (query != null && !GameDataRestore.IsCurrent(query)) return;
                    Debug.Log($"[BackendManager] {tableId} 데이터 조회 성공");
                    _isLoaded = true;
                    onSuccess?.Invoke(bro);
                },
                state => { if (query == null || GameDataRestore.IsCurrent(query)) onFail?.Invoke(state); },
                3,
                true
            );
        }
        
        /// <summary>
        /// 차트 데이터를 조회합니다
        /// </summary>
        public void GetChartDataAsync(string chartId, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] 로그인이 되어있지 않아 차트 데이터를 조회할 수 없습니다.");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            ProcessBackendAPI(
                $"{chartId} 차트 조회",
                (callback) => Backend.Chart.GetChartContents(chartId, (bro) => callback?.Invoke(bro)),
                onSuccess,
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// 게임 데이터를 안전하게 저장합니다
        /// </summary>
        public void SaveGameDataAsync(string tableId, Param param, Action<BackendReturnObject> onSuccess = null,
            Action<BackendState> onFail = null, Func<bool> isCurrent = null)
        {
            if (isCurrent != null && !isCurrent()) return;
            if (tableId == "GameData") { SaveLegacyGameDataAsync(param, null, onSuccess, onFail); return; }
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] 저장이 비활성화되어 있어 데이터가 저장되지 않습니다.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] 로그인 또는 데이터 로드가 필요합니다");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }
            
            // 유저 정보 조회를 위한 조건
            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);
            
            // 데이터 존재 확인 후 업데이트 또는 삽입
            ProcessBackendAPI(
                $"{tableId} 데이터 확인",
                (callback) => Backend.GameData.Get(tableId, where, (bro) => callback?.Invoke(bro)),
                (getBro) => {
                    if (isCurrent != null && !isCurrent()) return;
                    var rows = getBro.FlattenRows();
                    
                    // 결과에 따라 삽입 또는 업데이트
                    if (rows != null && rows.Count > 0)
                    {
                        string inDate = getBro.GetInDate();
                        
                        // 업데이트 수행
                        ProcessBackendAPI(
                            $"{tableId} 데이터 업데이트",
                            (callback) => Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param, (bro) => callback?.Invoke(bro)),
                            onSuccess,
                            onFail,
                            3,
                            true,
                            isCurrent
                        );
                    }
                    else
                    {
                        // 삽입 수행
                        ProcessBackendAPI(
                            $"{tableId} 데이터 삽입",
                            (callback) => Backend.GameData.Insert(tableId, param, (bro) => callback?.Invoke(bro)),
                            (insertBro) => {
                                OnInsertGameDataHandler?.Invoke(insertBro);
                                onSuccess?.Invoke(insertBro);
                            },
                            onFail,
                            3,
                            true,
                            isCurrent
                        );
                    }
                },
                onFail,
                2,
                true,
                isCurrent
            );
        }
        
        /// <summary>
        /// 게임 데이터를 삽입합니다
        /// </summary>
        public void InsertGameDataAsync(string tableId, Param param, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (tableId == "GameData") { onFail?.Invoke(BackendState.NotSave); return; }
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] 저장이 비활성화되어 있어 데이터가 저장되지 않습니다.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] 로그인 또는 데이터 로드가 필요합니다");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }
            
            ProcessBackendAPI(
                $"{tableId} 데이터 삽입",
                (callback) => Backend.GameData.Insert(tableId, param, (bro) => callback?.Invoke(bro)),
                (insertBro) => {
                    OnInsertGameDataHandler?.Invoke(insertBro);
                    onSuccess?.Invoke(insertBro);
                },
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// 게임 데이터를 업데이트합니다
        /// </summary>
        public void UpdateGameDataAsync(string tableId, string inDate, Param param, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (tableId == "GameData") { SaveLegacyGameDataAsync(param, inDate, onSuccess, onFail); return; }
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] 저장이 비활성화되어 있어 데이터가 저장되지 않습니다.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] 로그인 또는 데이터 로드가 필요합니다");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }
            
            ProcessBackendAPI(
                $"{tableId} 데이터 업데이트",
                (callback) => Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param, (bro) => callback?.Invoke(bro)),
                onSuccess,
                onFail,
                3,
                true
            );
        }

        #endregion
        
        #region 데이터 관련 메서드 (동기)

        /// <summary>
        /// 유저 데이터를 조회합니다 (동기식)
        /// </summary>
        public BackendReturnObject GetMyData(string tableId)
        {
            if (tableId == "GameData") InvalidateGameDataRestore();
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] 로그인이 되어있지 않아 데이터를 조회할 수 없습니다.");
                return null;
            }
            
            // 유저 조건 생성
            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);
            
            BackendReturnObject bro = ProcessBackendAPISync(
                $"{tableId} 데이터 조회",
                () => Backend.GameData.Get(tableId, where),
                3,
                true
            );
            
            if (bro != null && bro.IsSuccess())
            {
                _isLoaded = true;
            }
            
            return bro;
        }

        /// <summary>
        /// 차트 데이터를 조회합니다 (동기식)
        /// </summary>
        public BackendReturnObject GetChartData(string chartId)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] 로그인이 되어있지 않아 차트 데이터를 조회할 수 없습니다.");
                return null;
            }
            
            return ProcessBackendAPISync(
                $"{chartId} 차트 조회",
                () => Backend.Chart.GetChartContents(chartId),
                3,
                true
            );
        }

        /// <summary>
        /// 게임 데이터를 안전하게 저장합니다 (동기식)
        /// GameData만 공통 비동기 큐를 사용한다. true는 반환 전에 성공 확정된 경우뿐이며,
        /// false는 대기/미확정일 수도 있다. 완료 확인이 필요하면 RequestGameDataSave를 사용한다.
        /// 다른 테이블의 기존 동기 계약은 유지한다.
        /// </summary>
        public bool SaveGameData(string tableId, Param param, Func<bool> isCurrent = null)
        {
            if (isCurrent != null && !isCurrent()) return false;
            if (tableId == "GameData") return SaveLegacyGameData(param, null);

            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] 저장이 비활성화되어 있어 데이터가 저장되지 않습니다.");
                return false;
            }

            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] 로그인 또는 데이터 로드가 필요합니다");
                return false;
            }

            // 유저 정보 조회를 위한 조건
            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);
            
            // 데이터 존재 확인
            BackendReturnObject getBro = ProcessBackendAPISync(
                $"{tableId} 데이터 확인",
                () => Backend.GameData.Get(tableId, where),
                2,
                true,
                isCurrent
            );

            if (isCurrent != null && !isCurrent()) return false;
            
            if (getBro == null || !getBro.IsSuccess())
            {
                Debug.LogError($"[BackendManager] {tableId} 데이터 조회 실패");
                return false;
            }
            
            var rows = getBro.FlattenRows();
            
            // 결과에 따라 삽입 또는 업데이트
            if (rows != null && rows.Count > 0)
            {
                string inDate = getBro.GetInDate();
                
                // 업데이트 수행
                BackendReturnObject updateBro = ProcessBackendAPISync(
                    $"{tableId} 데이터 업데이트",
                    () => Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param),
                    3,
                    true,
                    isCurrent
                );
                
                return updateBro != null && updateBro.IsSuccess();
            }
            else
            {
                // 삽입 수행
                BackendReturnObject insertBro = ProcessBackendAPISync(
                    $"{tableId} 데이터 삽입",
                    () => Backend.GameData.Insert(tableId, param),
                    3,
                    true,
                    isCurrent
                );
                
                if (insertBro != null && insertBro.IsSuccess())
                {
                    OnInsertGameDataHandler?.Invoke(insertBro);
                    return true;
                }
                
                return false;
            }
        }

        /// <summary>
        /// 게임 데이터를 삽입합니다 (동기식)
        /// </summary>
        public bool InsertGameData(string tableId, Param param)
        {
            if (tableId == "GameData") return false;
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] 저장이 비활성화되어 있어 데이터가 저장되지 않습니다.");
                return false;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] 로그인 또는 데이터 로드가 필요합니다");
                return false;
            }
            
            BackendReturnObject insertBro = ProcessBackendAPISync(
                $"{tableId} 데이터 삽입",
                () => Backend.GameData.Insert(tableId, param),
                3,
                true
            );
            
            if (insertBro != null && insertBro.IsSuccess())
            {
                OnInsertGameDataHandler?.Invoke(insertBro);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 게임 데이터를 업데이트합니다 (동기식)
        /// GameData는 공통 비동기 큐에 접수하며, 대기 중이면 false다(미반영 확정의 뜻이 아님).
        /// 완료 확인이 필요하면 RequestGameDataSave를 사용한다. 다른 테이블은 기존 동기식이다.
        /// </summary>
        public bool UpdateGameData(string tableId, string inDate, Param param)
        {
            if (tableId == "GameData") return SaveLegacyGameData(param, inDate);
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] 저장이 비활성화되어 있어 데이터가 저장되지 않습니다.");
                return false;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] 로그인 또는 데이터 로드가 필요합니다");
                return false;
            }
            
            BackendReturnObject updateBro = ProcessBackendAPISync(
                $"{tableId} 데이터 업데이트",
                () => Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param),
                3,
                true
            );
            
            return updateBro != null && updateBro.IsSuccess();
        }

        #endregion

        #region 로그 및 오류 처리

        // 오류 로그 업로드 재귀 방지 플래그
        private bool _isUploadingErrorLog;

        /// <summary>
        /// 백엔드 오류를 분류하고 적절한 처리 방향을 결정합니다
        /// </summary>
        public BackendState HandleError(BackendReturnObject bro)
        {
            if (bro == null)
                return BackendState.Failure;
                
            if (bro.IsSuccess())
                return BackendState.Success;
            
            if (!_isUploadingErrorLog)
            {
                try
                {
                    _isUploadingErrorLog = true;
                    // 오류 로그 업로드 (실패해도 계속 진행)
                    ErrorLogUpload(bro);
                }
                catch {}
                finally
                {
                    _isUploadingErrorLog = false;
                }
            }
            
            // 오류 유형 분석
            string errorCode = bro.GetErrorCode();
            string statusCode = bro.GetStatusCode();
            
            // GPGS2 인증 코드 관련 에러는 어떤 상태 코드여도 토큰 갱신 불가 → 즉시 실패
            string broMessage = bro.GetMessage() ?? "";
            if (broMessage.Contains("GPGS2"))
            {
                Debug.LogError($"[BackendManager] GPGS2 인증 코드 오류 (서버 설정 확인 필요): {broMessage}");
                return BackendState.Failure;
            }

            // 상태 코드별 처리
            switch (statusCode)
            {
                case "401": // 인증 오류
                    if (errorCode == "BadUnauthorizedException" || 
                        errorCode == "UnauthorizedSessionException")
                    {
                        // 토큰 갱신 시도
                        if (RefreshTheBackendToken(1))
                            return BackendState.Retry;
                        else
                            return BackendState.Failure;
                    }
                    break;
                    
                case "400": // 요청 오류 (일반적으로 재시도해도 동일 결과)
                    return BackendState.Failure;
                    
                case "403": // 권한 오류
                    if (bro.IsTooManyRequestError())
                    {
                        Debug.LogWarning("[BackendManager] 과도한 요청으로 잠시 차단됨. 5분 후 다시 시도해주세요.");
                        return BackendState.Failure;
                    }
                    break;
                    
                case "408": // 타임아웃
                case "500": // 서버 오류
                case "502": // 게이트웨이 오류
                case "503": // 서비스 일시 중지
                    return BackendState.Retry;
                    
                case "429": // 요청 제한 초과
                    Debug.LogWarning("[BackendManager] 요청 제한을 초과했습니다. 잠시 후 다시 시도해주세요.");
                    return BackendState.Retry;
            }
            
            // 뒤끝의 확장된 오류 확인 메서드 사용
            if (bro.IsClientRequestFailError())
            {
                Debug.Log("[BackendManager] 일시적인 네트워크 문제. 재시도합니다.");
                return BackendState.Retry;
            }
            else if (bro.IsServerError())
            {
                Debug.Log("[BackendManager] 서버 오류. 재시도합니다.");
                return BackendState.Retry;
            }
            else if (bro.IsMaintenanceError())
            {
                ShowPopup("서버 점검중", "현재 서버 점검중 입니다. 점검이 끝난 후 접속해 주세요.");
                ShowPopupExitButton();
                return BackendState.Maintainance;
            }
            else if (bro.IsBadAccessTokenError())
            {
                // GPGS2 serverAuthCode 관련 에러는 토큰 갱신 불가 - 즉시 실패 처리
                string msg = bro.GetMessage();
                if (msg != null && msg.Contains("GPGS2"))
                {
                    Debug.LogError($"[BackendManager] GPGS2 인증 코드 오류 (토큰 갱신 불가): {msg}");
                    return BackendState.Failure;
                }
                return IsLogin && RefreshTheBackendToken(3) ? BackendState.Retry : BackendState.Failure;
            }
            
            // 기타 오류
            Debug.LogError($"[BackendManager] 처리되지 않은 오류: {errorCode}, {bro.GetMessage()}");
            return BackendState.Failure;
        }
        
        /// <summary>
        /// 뒤끝 토큰 갱신을 시도합니다
        /// </summary>
        public bool RefreshTheBackendToken(int maxRetries)
        {
            if (maxRetries <= 0)
            {
                Debug.Log("[BackendManager] 토큰 갱신 실패");
                return false;
            }
            
            BackendReturnObject callback = Backend.BMember.RefreshTheBackendToken();

            if (callback == null)
            {
                Debug.LogError("[BackendManager] 토큰 갱신 실패: 응답 없음");
                return false;
            }

            if (callback.IsSuccess())
            {
                Debug.Log("[BackendManager] 토큰 갱신 성공");
                return true;
            }
            else
            {
                if (callback.IsClientRequestFailError() || callback.IsServerError())
                {
                    return RefreshTheBackendToken(maxRetries - 1);
                }
                
                Debug.LogError($"[BackendManager] 토큰 갱신 실패: {callback.GetMessage()}");
                return false;
            }
        }
        
        /// <summary>
        /// 오류 로그를 서버에 업로드합니다 (동기)
        /// </summary>
        public bool ErrorLogUpload(BackendReturnObject errorBro)
        {
            if (!Backend.IsLogin || !_isLogin)
                return false;

            try
            {
                Param logParam = new Param();
                logParam.Add("ErrorLog", errorBro.ToString());
                
                // 오류 발생 시간 추가
                logParam.Add("Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                
                // 디바이스 정보 추가
                logParam.Add("Device", SystemInfo.deviceModel);
                logParam.Add("OS", SystemInfo.operatingSystem);
                
                BackendReturnObject bro = ProcessBackendAPISync(
                    "오류 로그 업로드",
                    () => Backend.GameLog.InsertLogV2("ErrorLogs", logParam),
                    1,  // 한 번만 시도
                    false // 팝업 표시 안 함
                );
                
                return bro != null && bro.IsSuccess();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackendManager] 오류 로그 업로드 중 예외 발생: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 일반 로그를 서버에 업로드합니다 (동기)
        /// </summary>
        public bool LogUpload(string logName, string logDescription)
        {
            if (!Backend.IsLogin || !_isLogin)
                return false;

            try
            {
                Param logParam = new Param();
                logParam.Add(logName, logDescription);
                logParam.Add("Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                
                BackendReturnObject bro = ProcessBackendAPISync(
                    "일반 로그 업로드",
                    () => Backend.GameLog.InsertLogV2("UserLogs", logParam),
                    1,  // 한 번만 시도
                    false // 팝업 표시 안 함
                );
                
                return bro != null && bro.IsSuccess();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackendManager] 로그 업로드 중 예외 발생: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 버그 리포트를 서버에 업로드합니다 (비동기)
        /// </summary>
        public void BugReportUploadAsync(string userId, string email, string logDescription, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!Backend.IsLogin || !_isLogin)
            {
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            Param logParam = new Param();
            logParam.Add("Description", logDescription);
            logParam.Add("Email", email);
            logParam.Add("UserId", userId);
            logParam.Add("Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            
            ProcessBackendAPI(
                "버그 리포트 업로드",
                (callback) => Backend.GameLog.InsertLogV2("BugReport", logParam, (bro) => callback?.Invoke(bro)),
                onSuccess,
                onFail,
                1,
                false
            );
        }
        
        /// <summary>
        /// 버그 리포트를 서버에 업로드합니다 (동기)
        /// </summary>
        public bool BugReportUpload(string userId, string email, string logDescription)
        {
            if (!Backend.IsLogin || !_isLogin)
            {
                Debug.LogError("[BackendManager] 로그인이 되어있지 않아 버그 리포트를 업로드할 수 없습니다.");
                return false;
            }

            Param logParam = new Param();
            logParam.Add("Description", logDescription);
            logParam.Add("Email", email);
            logParam.Add("UserId", userId);
            logParam.Add("Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            
            BackendReturnObject bro = ProcessBackendAPISync(
                "버그 리포트 업로드",
                () => Backend.GameLog.InsertLogV2("BugReport", logParam),
                1,  // 한 번만 시도
                false // 팝업 표시 안 함
            );
            
            return bro != null && bro.IsSuccess();
        }

        #endregion

        #region 애플리케이션 생명주기

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                // 앱이 백그라운드로 전환될 때
                Debug.Log("[BackendManager] 앱 일시정지, 중요 데이터 저장 시도...");
                
                if (_isLogin && _isSaveEnabled)
                {
                    DebugLog.Log("11");
                    OnPauseHandler?.Invoke();
                }
            }
            else
            {
                // 앱이 포그라운드로 돌아올 때
                Debug.Log("[BackendManager] 앱 재시작");
                OnResumeHandler?.Invoke();
                // 토큰 유효성 검증
                CheckTokenValidity();
            }
        }
        
        private void OnApplicationQuit()
        {
            // 앱 종료 시
            if (_isLogin && _isSaveEnabled)
            {
                Debug.Log("[BackendManager] 앱 종료, 중요 데이터 저장 시도...");
                OnExitHandler?.Invoke();
            }
        }

        private void CheckTokenValidity()
        {
            // 광고 재생 중에는 토큰 검사 생략 (ad 오버레이로 인한 일시적 네트워크 실패 → 오탐 방지)
            if (AdManager.HasInstance && AdManager.IsAdPlaying)
            {
                Debug.Log("[BackendManager] 광고 재생 중 - 토큰 유효성 검사 건너뜀");
                return;
            }

            if (_isLogin)
            {
                // 토큰 유효성 검사를 위한 API 호출
                BackendReturnObject bro = ProcessBackendAPISync(
                    "토큰 유효성 검사",
                    () => Backend.BMember.GetUserInfo(),
                    1,  // 한 번만 시도
                    false // 팝업 표시 안 함
                );

                if (bro == null || !bro.IsSuccess())
                {
                    if (bro != null && bro.IsBadAccessTokenError() && !RefreshTheBackendToken(1))
                    {
                        // 토큰 갱신 실패 시 로그아웃 처리
                        Debug.LogWarning("[BackendManager] 세션이 만료되어 로그아웃합니다.");
                        LogOut();
                        ShowPopup("세션 만료", "세션이 만료되었습니다. 다시 접속해 주세요.");
                        ShowPopupExitButton();
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// 팝업을 표시합니다
        /// </summary>
        public void ShowPopup(string title, string description)
        {
            // 사용자 팝업 매니저 연동
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowPopup(title, description);
            else
                Debug.LogWarning($"[BackendManager] 팝업 표시: {title} - {description}");
        }

        public void ShowPopupExitButton()
        {
            // 사용자 팝업 매니저 연동
            if (PopupManager.Instance != null)
                PopupManager.Instance.SetPopupButton2("종료", ExitApp);
            else
                Debug.LogWarning("[BackendManager] 팝업 종료 버튼 표시");
        }

        public void SetPopupButton1(string buttonText, Action buttonClicked)
        {
            if (PopupManager.Instance != null)
                PopupManager.Instance.SetPopupButton1(buttonText, buttonClicked);
            else
                Debug.LogWarning($"[BackendManager] 버튼 설정: {buttonText}");
        }

        public void SetPopupButton2(string buttonText, Action buttonClicked)
        {
            if (PopupManager.Instance != null)
                PopupManager.Instance.SetPopupButton2(buttonText, buttonClicked);
            else
                Debug.LogWarning($"[BackendManager] 버튼 설정: {buttonText}");
        }
        
        /// <summary>
        /// 앱을 종료합니다
        /// </summary>
        private void ExitApp()
        {
            Application.Quit();
        }
    }
}

