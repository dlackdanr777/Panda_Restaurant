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

        public DateTime LocalTime = DateTime.Now;
        
        public DateTime ServerTime
        {
            get
            {
                BackendReturnObject bro = Backend.Utils.GetServerTime();

                if (bro.IsSuccess())
                {
                    string time = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                    DateTime dateTime = DateTime.Parse(time);
                    return dateTime;
                }
                else
                {
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
                // 치명적인 오류 패턴 확인
                if (IsCriticalError(logString))
                {
                    string truncatedMessage = logString;
                    if (logString.Length > 100)
                        truncatedMessage = logString.Substring(0, 100) + "...";

#if !UNITY_EDITOR
                    ShowPopup("알 수 없는 오류", "오류가 발생하여 게임을 종료합니다.\n게임을 재시작 해주세요.");
                    ShowPopupExitButton();   
#endif

                    DisableSaving($"치명적 오류 감지: {truncatedMessage}");
                    
                    // 오류 로그 업로드 시도 (무한 루프 방지를 위해 try-catch 사용)
                    try
                    {
                        LogUpload("CriticalErrorDetails", 
                            $"오류: {logString}\n스택 트레이스: {stackTrace}");
                    }
                    catch { }
                }
            }
        }
        
        private bool IsCriticalError(string errorMessage)
        {
            string[] criticalPatterns = {
                "NullReferenceException",
                "IndexOutOfRangeException",
                "ArgumentNullException",
                "MissingReferenceException",
                "KeyNotFoundException",
                "OutOfMemoryException",
                "StackOverflowException",
                "AccessViolationException"
            };
            
            foreach (var pattern in criticalPatterns)
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
            try
            {
                BackendReturnObject bro = Backend.Initialize();
                if (bro.IsSuccess())
                {
                    Debug.Log("[BackendManager] 뒤끝 초기화 성공");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] 뒤끝 초기화 실패: {bro.GetMessage()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
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
            bool usePopup = true)
        {
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

            // 콜백 처리 함수
            void HandleCallback(BackendReturnObject bro)
            {
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
                    backendFunction(HandleCallback);
                }
                else
                {
                    string errorMessage = bro.GetMessage();
                    Debug.LogError($"[BackendManager] {operationName} 실패: {errorMessage}");
                    
                    if (usePopup)
                    {
                        ShowPopup("네트워크 에러", 
                            $"{operationName}에 실패했습니다.\n다시 시도해 주세요.\n오류 코드: {bro.GetErrorCode()}");
                        SetPopupButton1("재시도", () => backendFunction(HandleCallback));
                        ShowPopupExitButton();
                    }
                    
                    onFail?.Invoke(state);
                }
            }
            
            // API 호출
            backendFunction(HandleCallback);
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
            bool usePopup = true)
        {
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
                    // API 호출
                    bro = backendFunction();
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
                        string errorMessage = bro.GetMessage();
                        Debug.LogError($"[BackendManager] {operationName} 실패: {errorMessage}");
                        
                        if (usePopup)
                        {
                            ShowPopup("네트워크 에러", 
                                $"{operationName}에 실패했습니다.\n다시 시도해 주세요.\n오류 코드: {bro.GetErrorCode()}");
                        }
                        
                        return bro;
                    }
                } while (state == BackendState.Retry && retryCount <= maxRetries);
                
                return bro;
            }
            catch (Exception ex)
            {
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
                (callback) => Backend.Utils.GetServerTime(),
                (bro) => {
                    string time = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                    DateTime dateTime = DateTime.Parse(time);
                    onSuccess?.Invoke(dateTime);
                },
                onFail,
                1, // 서버 시간은 한 번만 시도
                false // 팝업 표시 안함
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
            
            ProcessBackendAPI(
                "커스텀 로그인",
                (callback) => Backend.BMember.CustomLogin(id, pw, (bro) => callback?.Invoke(bro)),
                (bro) => {
                    _isLogin = true;
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

            void HandleGuestLogin(Action<BackendReturnObject> callback)
            {
                Backend.BMember.GuestLogin((bro) => {
                    // 특수 케이스: 게스트 정보 삭제 후 재시도
                    if (bro.GetStatusCode() == "401")
                    {
                        Debug.Log("[BackendManager] 게스트 정보가 없어 삭제 후 재시도합니다.");
                        Backend.BMember.DeleteGuestInfo();
                        Backend.BMember.GuestLogin();
                        return;
                    }
                    
                    callback(bro);
                });
            }
            
            ProcessBackendAPI(
                "게스트 로그인",
                HandleGuestLogin,
                (bro) => {
                    _isLogin = true;
                    
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


        public void LogOut()
        {
            _isSaveEnabled = false;
            _isLogin = false;
        }
        
        /// <summary>
        /// 회원가입을 비동기적으로 수행합니다
        /// </summary>
        public void CustomSignupAsync(string id, string pw, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            ProcessBackendAPI(
                "회원가입",
                (callback) => Backend.BMember.CustomSignUp(id, pw, (bro) => callback?.Invoke(bro)),
                onSuccess,
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
            
            BackendReturnObject bro = ProcessBackendAPISync(
                "커스텀 로그인",
                () => Backend.BMember.CustomLogin(id, pw),
                3,
                true
            );
            
            if (bro != null && bro.IsSuccess())
            {
                _isLogin = true;
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
            BackendReturnObject bro = Backend.BMember.GuestLogin();
            if (bro.GetStatusCode() == "401")
            {
                Debug.Log("[BackendManager] 게스트 정보가 없어 삭제 후 재시도합니다.");
                Backend.BMember.DeleteGuestInfo();
                
                bro = ProcessBackendAPISync(
                    "게스트 로그인",
                    () => Backend.BMember.GuestLogin(),
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
                _isLogin = true;
                
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
            BackendReturnObject bro = ProcessBackendAPISync(
                "회원가입",
                () => Backend.BMember.CustomSignUp(id, pw),
                3,
                true
            );
            
            return bro != null && bro.IsSuccess();
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
        /// 유저 데이터를 조회합니다
        /// </summary>
        public void GetMyDataAsync(string tableId, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] 로그인이 되어있지 않아 데이터를 조회할 수 없습니다.");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            // 유저 조건 생성
            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);

            ProcessBackendAPI(
                $"{tableId} 데이터 조회",
                (callback) => Backend.GameData.Get(tableId, where, (bro) => callback?.Invoke(bro)),
                (bro) => {
                    Debug.Log($"[BackendManager] {tableId} 데이터 조회 성공");
                    _isLoaded = true;
                    onSuccess?.Invoke(bro);
                },
                onFail,
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
        public void SaveGameDataAsync(string tableId, Param param, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
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
                            true
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
                            true
                        );
                    }
                },
                onFail,
                2,
                true
            );
        }
        
        /// <summary>
        /// 게임 데이터를 삽입합니다
        /// </summary>
        public void InsertGameDataAsync(string tableId, Param param, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
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
        /// </summary>
        public bool SaveGameData(string tableId, Param param)
        {
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
                true
            );
            
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
                    true
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
                    true
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
        /// </summary>
        public bool UpdateGameData(string tableId, string inDate, Param param)
        {
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

        /// <summary>
        /// 백엔드 오류를 분류하고 적절한 처리 방향을 결정합니다
        /// </summary>
        public BackendState HandleError(BackendReturnObject bro)
        {
            if (bro == null)
                return BackendState.Failure;
                
            if (bro.IsSuccess())
                return BackendState.Success;
            
            try
            {
                // 오류 로그 업로드 (실패해도 계속 진행)
                ErrorLogUpload(bro);
            }
            catch {}
            
            // 오류 유형 분석
            string errorCode = bro.GetErrorCode();
            string statusCode = bro.GetStatusCode();
            
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
                return RefreshTheBackendToken(3) ? BackendState.Retry : BackendState.Failure;
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

