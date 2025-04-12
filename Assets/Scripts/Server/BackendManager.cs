using BackEnd;
using LitJson;
using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

namespace Muks.BackEnd
{
    /// <summary>�������� ���� ������ Ȯ�� �ϴ� ������</summary>
    public enum BackendState
    {
        NotSave,
        NotLogin,
        Failure,
        Maintainance,
        Retry,
        Success,
    }

    /// <summary>�ڳ��� ������ �� �ְ� ���ִ� �̱��� Ŭ����</summary>
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

        // ���� ���� ���¸� �����ϴ� �÷���
        private static bool _isSaveEnabled = true;
        public static bool IsSaveEnabled => _isSaveEnabled;

        /// <summary>�� ���� ���� ���� ������ ������ �����ϴ�.(�α��� �����ε� ������ ������ ���� ������ �ʱ�ȭ)</summary> 
        public bool _isLogin = false;
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
            
            // ���� ���� ó���� ����
            SetupGlobalErrorHandler();
            
            // �ʱ�ȭ
            Init();
        }
        
        // ���� ���� �ڵ鷯 ����
        private void SetupGlobalErrorHandler()
        {
            Application.logMessageReceived += HandleLog;
            Debug.Log("[BackendManager] ���� ���� ���� �ý����� Ȱ��ȭ�Ǿ����ϴ�.");
        }
        
        private void OnDestroy()
        {
            // �̺�Ʈ ���� ����
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                // ġ������ ���� ���� Ȯ��
                if (IsCriticalError(logString))
                {
                    string truncatedMessage = logString;
                    if (logString.Length > 100)
                        truncatedMessage = logString.Substring(0, 100) + "...";
                        
                    DisableSaving($"ġ���� ���� ����: {truncatedMessage}");
                    
                    // ���� �α� ���ε� �õ� (���� ���� ������ ���� try-catch ���)
                    try
                    {
                        LogUpload("CriticalErrorDetails", 
                            $"����: {logString}\n���� Ʈ���̽�: {stackTrace}");
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
        
        // ���� ���� �÷��� ���� �޼���
        public static void DisableSaving(string reason)
        {
            if (_isSaveEnabled)
            {
                _isSaveEnabled = false;
                Debug.LogError($"[BackendManager] �ɰ��� ���� �߻����� ������ ������ ��Ȱ��ȭ�Ǿ����ϴ�! ����: {reason}");
                
                // �����ϰ� �α� ���ε� �õ�
                try
                {
                    if (Instance != null && Instance._isLogin)
                    {
                        Instance.LogUpload("CriticalError", $"���� ��� ��Ȱ��ȭ: {reason}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BackendManager] ���� �α� ���ε� �� �߰� ���� �߻�: {ex.Message}");
                }
            }
        }
        
        public static void EnableSaving()
        {
            _isSaveEnabled = true;
            Debug.Log("[BackendManager] ������ ������ �ٽ� Ȱ��ȭ�Ǿ����ϴ�.");
        }
        
        private void Init()
        {
            // ����� �ʱ�ȭ ȣ��
            bool isSuccess = InitializeBackend();
            if (!isSuccess)
            {
                Debug.LogError("[BackendManager] �ڳ� �ʱ�ȭ ����");
            }
        }
        
        /// <summary>
        /// �ڳ� �ʱ�ȭ (�����)
        /// </summary>
        private bool InitializeBackend()
        {
            try
            {
                BackendReturnObject bro = Backend.Initialize();
                if (bro.IsSuccess())
                {
                    Debug.Log("[BackendManager] �ڳ� �ʱ�ȭ ����");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] �ڳ� �ʱ�ȭ ����: {bro.GetMessage()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        

        #region �񵿱� �۾� ó���� ���� ���� �޼���

        /// <summary>
        /// �鿣�� API ȣ���� ó���ϴ� �߾� �Լ�
        /// </summary>
        private void ProcessBackendAPI(
            string operationName,
            Action<Action<BackendReturnObject>> backendFunction, 
            Action<BackendReturnObject> onSuccess = null,
            Action<BackendState> onFail = null, 
            int maxRetries = 3,
            bool usePopup = true)
        {
            if (!_isSaveEnabled && operationName.Contains("����"))
            {
                Debug.LogWarning($"[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� {operationName}�� �ߴܵǾ����ϴ�.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }

            // �α��� �˻� (�ʱ�ȭ, �α��� ���� �۾��� ����)
            if (!IsLogin && !operationName.Contains("�ʱ�ȭ") && !operationName.Contains("�α���"))
            {
                Debug.LogError($"[BackendManager] �α����� �ʿ��� �۾�({operationName})�� �α��� ���� �õ��Ǿ����ϴ�.");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            int retryCount = 0;

            // �ݹ� ó�� �Լ�
            void HandleCallback(BackendReturnObject bro)
            {
                BackendState state = HandleError(bro);
                
                if (state == BackendState.Success)
                {
                    Debug.Log($"[BackendManager] {operationName} ����");
                    onSuccess?.Invoke(bro);
                }
                else if (state == BackendState.Retry && retryCount < maxRetries)
                {
                    retryCount++;
                    Debug.Log($"[BackendManager] {operationName} ��õ�({retryCount}/{maxRetries})");
                    backendFunction(HandleCallback);
                }
                else
                {
                    string errorMessage = bro.GetMessage();
                    Debug.LogError($"[BackendManager] {operationName} ����: {errorMessage}");
                    
                    if (usePopup)
                    {
                        ShowPopup("��Ʈ��ũ ����", 
                            $"{operationName}�� �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {bro.GetErrorCode()}",
                            () => backendFunction(HandleCallback));
                    }
                    
                    onFail?.Invoke(state);
                }
            }
            
            // API ȣ��
            backendFunction(HandleCallback);
        }

        #endregion

        #region ����� ���� ���� �޼��� (�񵿱�)

        /// <summary>
        /// ���� �ð��� �񵿱������� �����ɴϴ�
        /// </summary>
        public void GetServerTimeAsync(Action<DateTime> onSuccess = null, Action<BackendState> onFail = null)
        {
            ProcessBackendAPI(
                "���� �ð� ��ȸ",
                (callback) => Backend.Utils.GetServerTime(),
                (bro) => {
                    string time = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                    DateTime dateTime = DateTime.Parse(time);
                    onSuccess?.Invoke(dateTime);
                },
                onFail,
                1, // ���� �ð��� �� ���� �õ�
                false // �˾� ǥ�� ����
            );
        }
        
        /// <summary>
        /// Ŀ���� �α����� �񵿱������� �����մϴ�
        /// </summary>
        public void CustomLoginAsync(string id, string pw, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] �̹� �α��εǾ� �ֽ��ϴ�.");
                return;
            }
            
            ProcessBackendAPI(
                "Ŀ���� �α���",
                (callback) => Backend.BMember.CustomLogin(id, pw, (bro) => callback?.Invoke(bro)),
                (bro) => {
                    _isLogin = true;
                    Debug.Log("[BackendManager] Ŀ���� �α��� ����");
                    onSuccess?.Invoke(bro);
                },
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// �Խ�Ʈ �α����� �񵿱������� �����մϴ�
        /// </summary>
        public void GuestLoginAsync(Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] �̹� �α��εǾ� �ֽ��ϴ�.");
                return;
            }

            void HandleGuestLogin(Action<BackendReturnObject> callback)
            {
                Backend.BMember.GuestLogin((bro) => {
                    // Ư�� ���̽�: �Խ�Ʈ ���� ���� �� ��õ�
                    if (bro.GetStatusCode() == "401")
                    {
                        Debug.Log("[BackendManager] �Խ�Ʈ ������ ���� ���� �� ��õ��մϴ�.");
                        Backend.BMember.DeleteGuestInfo();
                        Backend.BMember.GuestLogin();
                        return;
                    }
                    
                    callback(bro);
                });
            }
            
            ProcessBackendAPI(
                "�Խ�Ʈ �α���",
                HandleGuestLogin,
                (bro) => {
                    _isLogin = true;
                    
                    // �ű� ���� �Ǵ� ���� �α��� ó��
                    if (bro.GetStatusCode() == "201")
                    {
                        Debug.Log("[BackendManager] �Խ�Ʈ �ű� ���� ����");
                        OnGuestSignupHandler?.Invoke();
                    }
                    else if (bro.GetStatusCode() == "200")
                    {
                        Debug.Log("[BackendManager] �Խ�Ʈ �α��� ����");
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
        /// ȸ�������� �񵿱������� �����մϴ�
        /// </summary>
        public void CustomSignupAsync(string id, string pw, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            ProcessBackendAPI(
                "ȸ������",
                (callback) => Backend.BMember.CustomSignUp(id, pw, (bro) => callback?.Invoke(bro)),
                onSuccess,
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// �г����� �����մϴ�
        /// </summary>
        public void CreateNickNameAsync(string nickName, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            // �ߺ� üũ ���� ����
            ProcessBackendAPI(
                "�г��� �ߺ� üũ",
                (callback) => Backend.BMember.CheckNicknameDuplication(nickName, (bro) => callback?.Invoke(bro)),
                (checkBro) => {
                    // �г��� ����
                    ProcessBackendAPI(
                        "�г��� ����",
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
        /// �г����� ������Ʈ�մϴ�
        /// </summary>
        public void UpdateNickNameAsync(string nickName, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            // �ߺ� üũ ���� ����
            ProcessBackendAPI(
                "�г��� �ߺ� üũ",
                (callback) => Backend.BMember.CheckNicknameDuplication(nickName, (bro) => callback?.Invoke(bro)),
                (checkBro) => {
                    // �г��� ������Ʈ
                    ProcessBackendAPI(
                        "�г��� ������Ʈ",
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
        
        #region ����� ���� ���� �޼��� (����)
        
        /// <summary>
        /// Ŀ���� �α����� ���������� �����մϴ�
        /// </summary>
        public bool CustomLogin(string id, string pw)
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] �̹� �α��εǾ� �ֽ��ϴ�.");
                return true;
            }
            
            try
            {
                if (!_isSaveEnabled)
                {
                    Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �α����� �ߴܵǾ����ϴ�.");
                    return false;
                }
                
                BackendReturnObject bro = Backend.BMember.CustomLogin(id, pw);
                BackendState state = HandleError(bro);
                
                if (state == BackendState.Success)
                {
                    _isLogin = true;
                    Debug.Log("[BackendManager] Ŀ���� �α��� ����");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] Ŀ���� �α��� ����: {bro.GetMessage()}");
                    ShowPopup("�α��� ����", $"�α��ο� �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {bro.GetErrorCode()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// �Խ�Ʈ �α����� ���������� �����մϴ�
        /// </summary>
        public bool GuestLogin()
        {
            if (IsLogin)
            {
                Debug.Log("[BackendManager] �̹� �α��εǾ� �ֽ��ϴ�.");
                return true;
            }
            
            try
            {
                if (!_isSaveEnabled)
                {
                    Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �α����� �ߴܵǾ����ϴ�.");
                    return false;
                }
                
                BackendReturnObject bro = Backend.BMember.GuestLogin();
                
                // Ư�� ���̽�: �Խ�Ʈ ���� ���� �� ��õ�
                if (bro.GetStatusCode() == "401")
                {
                    Debug.Log("[BackendManager] �Խ�Ʈ ������ ���� ���� �� ��õ��մϴ�.");
                    Backend.BMember.DeleteGuestInfo();
                    bro = Backend.BMember.GuestLogin();
                }
                
                BackendState state = HandleError(bro);
                
                if (state == BackendState.Success)
                {
                    _isLogin = true;
                    
                    // �ű� ���� �Ǵ� ���� �α��� ó��
                    if (bro.GetStatusCode() == "201")
                    {
                        Debug.Log("[BackendManager] �Խ�Ʈ �ű� ���� ����");
                        OnGuestSignupHandler?.Invoke();
                    }
                    else if (bro.GetStatusCode() == "200")
                    {
                        Debug.Log("[BackendManager] �Խ�Ʈ �α��� ����");
                        OnGuestLoginHandler?.Invoke();
                    }
                    
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] �Խ�Ʈ �α��� ����: {bro.GetMessage()}");
                    ShowPopup("�α��� ����", $"�Խ�Ʈ �α��ο� �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {bro.GetErrorCode()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// �α׾ƿ��� �����մϴ�
        /// </summary>
        public void LogOut()
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] �α����� �Ǿ����� �ʾ� �α׾ƿ��� ������ �� �����ϴ�.");
                return;
            }

            _isLogin = false;
            _isLoaded = false;
            
            // �α׾ƿ� API ȣ���� ���� (�ʿ�� �߰�)
            Debug.Log("[BackendManager] �α׾ƿ� �Ϸ�");
        }
        
        /// <summary>
        /// ȸ�������� ���������� �����մϴ�
        /// </summary>
        public bool CustomSignup(string id, string pw)
        {
            try
            {
                BackendReturnObject bro = Backend.BMember.CustomSignUp(id, pw);
                BackendState state = HandleError(bro);
                
                if (state == BackendState.Success)
                {
                    Debug.Log("[BackendManager] ȸ������ ����");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] ȸ������ ����: {bro.GetMessage()}");
                    ShowPopup("ȸ������ ����", $"ȸ�����Կ� �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {bro.GetErrorCode()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// �г����� �����մϴ� (�����)
        /// </summary>
        public bool CreateNickName(string nickName)
        {
            try
            {
                // �ߺ� üũ
                BackendReturnObject checkBro = Backend.BMember.CheckNicknameDuplication(nickName);
                BackendState checkState = HandleError(checkBro);
                
                if (checkState != BackendState.Success)
                {
                    Debug.LogError($"[BackendManager] �г��� �ߺ� üũ ����: {checkBro.GetMessage()}");
                    ShowPopup("�г��� �ߺ� üũ ����", $"�ߺ� üũ�� �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {checkBro.GetErrorCode()}");
                    return false;
                }
                
                // �г��� ����
                BackendReturnObject createBro = Backend.BMember.CreateNickname(nickName);
                BackendState createState = HandleError(createBro);
                
                if (createState == BackendState.Success)
                {
                    Debug.Log("[BackendManager] �г��� ���� ����");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] �г��� ���� ����: {createBro.GetMessage()}");
                    ShowPopup("�г��� ���� ����", $"�г��� ������ �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {createBro.GetErrorCode()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// �г����� ������Ʈ�մϴ� (�����)
        /// </summary>
        public bool UpdateNickName(string nickName)
        {
            try
            {
                // �ߺ� üũ
                BackendReturnObject checkBro = Backend.BMember.CheckNicknameDuplication(nickName);
                BackendState checkState = HandleError(checkBro);
                
                if (checkState != BackendState.Success)
                {
                    Debug.LogError($"[BackendManager] �г��� �ߺ� üũ ����: {checkBro.GetMessage()}");
                    ShowPopup("�г��� �ߺ� üũ ����", $"�ߺ� üũ�� �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {checkBro.GetErrorCode()}");
                    return false;
                }
                
                // �г��� ������Ʈ
                BackendReturnObject updateBro = Backend.BMember.UpdateNickname(nickName);
                BackendState updateState = HandleError(updateBro);
                
                if (updateState == BackendState.Success)
                {
                    Debug.Log("[BackendManager] �г��� ������Ʈ ����");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] �г��� ������Ʈ ����: {updateBro.GetMessage()}");
                    ShowPopup("�г��� ������Ʈ ����", $"�г��� ������Ʈ�� �����߽��ϴ�.\n�ٽ� �õ��� �ּ���.\n���� �ڵ�: {updateBro.GetErrorCode()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        #endregion

        #region ������ ���� �޼��� (�񵿱�)
        
        /// <summary>
        /// ���� �����͸� ��ȸ�մϴ�
        /// </summary>
        public void GetMyDataAsync(string tableId, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] �α����� �Ǿ����� �ʾ� �����͸� ��ȸ�� �� �����ϴ�.");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            // ���� ���� ����
            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);

            ProcessBackendAPI(
                $"{tableId} ������ ��ȸ",
                (callback) => Backend.GameData.Get(tableId, where, (bro) => callback?.Invoke(bro)),
                (bro) => {
                    Debug.Log($"[BackendManager] {tableId} ������ ��ȸ ����");
                    _isLoaded = true;
                    onSuccess?.Invoke(bro);
                },
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// ��Ʈ �����͸� ��ȸ�մϴ�
        /// </summary>
        public void GetChartDataAsync(string chartId, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] �α����� �Ǿ����� �ʾ� ��Ʈ �����͸� ��ȸ�� �� �����ϴ�.");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }

            ProcessBackendAPI(
                $"{chartId} ��Ʈ ��ȸ",
                (callback) => Backend.Chart.GetChartContents(chartId, (bro) => callback?.Invoke(bro)),
                onSuccess,
                onFail,
                3,
                true
            );
        }
        
        /// <summary>
        /// ���� �����͸� �����ϰ� �����մϴ�
        /// </summary>
        public void SaveGameDataAsync(string tableId, Param param, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �����Ͱ� ������� �ʽ��ϴ�.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] �α��� �Ǵ� ������ �ε尡 �ʿ��մϴ�");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }
            
            // ���� ���� ��ȸ�� ���� ����
            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);
            
            // ������ ���� Ȯ�� �� ������Ʈ �Ǵ� ����
            ProcessBackendAPI(
                $"{tableId} ������ Ȯ��",
                (callback) => Backend.GameData.Get(tableId, where, (bro) => callback?.Invoke(bro)),
                (getBro) => {
                    var rows = getBro.FlattenRows();
                    
                    // ����� ���� ���� �Ǵ� ������Ʈ
                    if (rows != null && rows.Count > 0)
                    {
                        string inDate = getBro.GetInDate();
                        
                        // ������Ʈ ����
                        ProcessBackendAPI(
                            $"{tableId} ������ ������Ʈ",
                            (callback) => Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param, (bro) => callback?.Invoke(bro)),
                            onSuccess,
                            onFail,
                            3,
                            true
                        );
                    }
                    else
                    {
                        // ���� ����
                        ProcessBackendAPI(
                            $"{tableId} ������ ����",
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
        /// ���� �����͸� �����մϴ�
        /// </summary>
        public void InsertGameDataAsync(string tableId, Param param, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �����Ͱ� ������� �ʽ��ϴ�.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] �α��� �Ǵ� ������ �ε尡 �ʿ��մϴ�");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }
            
            ProcessBackendAPI(
                $"{tableId} ������ ����",
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
        /// ���� �����͸� ������Ʈ�մϴ�
        /// </summary>
        public void UpdateGameDataAsync(string tableId, string inDate, Param param, Action<BackendReturnObject> onSuccess = null, Action<BackendState> onFail = null)
        {
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �����Ͱ� ������� �ʽ��ϴ�.");
                onFail?.Invoke(BackendState.NotSave);
                return;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] �α��� �Ǵ� ������ �ε尡 �ʿ��մϴ�");
                onFail?.Invoke(BackendState.NotLogin);
                return;
            }
            
            ProcessBackendAPI(
                $"{tableId} ������ ������Ʈ",
                (callback) => Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param, (bro) => callback?.Invoke(bro)),
                onSuccess,
                onFail,
                3,
                true
            );
        }

        #endregion
        
        #region ������ ���� �޼��� (����)

        /// <summary>
        /// ���� �����͸� ��ȸ�մϴ� (�����)
        /// </summary>
        public BackendReturnObject GetMyData(string tableId)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] �α����� �Ǿ����� �ʾ� �����͸� ��ȸ�� �� �����ϴ�.");
                return null;
            }
            
            try
            {
                // ���� ���� ����
                Where where = new Where();
                where.Equal("owner_inDate", Backend.UserInDate);
                
                BackendReturnObject bro = Backend.GameData.Get(tableId, where);
                BackendState state = HandleError(bro);
                
                if (state == BackendState.Success)
                {
                    Debug.Log($"[BackendManager] {tableId} ������ ��ȸ ����");
                    _isLoaded = true;
                    return bro;
                }
                else
                {
                    Debug.LogError($"[BackendManager] {tableId} ������ ��ȸ ����: {bro.GetMessage()}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }
        
        /// <summary>
        /// ��Ʈ �����͸� ��ȸ�մϴ� (�����)
        /// </summary>
        public BackendReturnObject GetChartData(string chartId)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("[BackendManager] �α����� �Ǿ����� �ʾ� ��Ʈ �����͸� ��ȸ�� �� �����ϴ�.");
                return null;
            }
            
            try
            {
                BackendReturnObject bro = Backend.Chart.GetChartContents(chartId);
                BackendState state = HandleError(bro);
                
                if (state == BackendState.Success)
                {
                    Debug.Log($"[BackendManager] {chartId} ��Ʈ ������ ��ȸ ����");
                    return bro;
                }
                else
                {
                    Debug.LogError($"[BackendManager] {chartId} ��Ʈ ������ ��ȸ ����: {bro.GetMessage()}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }
        
        /// <summary>
        /// ���� �����͸� �����ϰ� �����մϴ� (�����)
        /// </summary>
        public bool SaveGameData(string tableId, Param param)
        {
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �����Ͱ� ������� �ʽ��ϴ�.");
                return false;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] �α��� �Ǵ� ������ �ε尡 �ʿ��մϴ�");
                return false;
            }
            
            try
            {
                // ���� ���� ��ȸ�� ���� ����
                Where where = new Where();
                where.Equal("owner_inDate", Backend.UserInDate);
                
                // ������ ���� Ȯ��
                BackendReturnObject getBro = Backend.GameData.Get(tableId, where);
                BackendState getState = HandleError(getBro);
                
                if (getState != BackendState.Success)
                {
                    Debug.LogError($"[BackendManager] {tableId} ������ ��ȸ ����: {getBro.GetMessage()}");
                    return false;
                }
                
                var rows = getBro.FlattenRows();
                
                // ����� ���� ���� �Ǵ� ������Ʈ
                if (rows != null && rows.Count > 0)
                {
                    string inDate = getBro.GetInDate();
                    
                    // ������Ʈ ����
                    BackendReturnObject updateBro = Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param);
                    BackendState updateState = HandleError(updateBro);
                    
                    if (updateState == BackendState.Success)
                    {
                        Debug.Log($"[BackendManager] {tableId} ������ ������Ʈ ����");
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"[BackendManager] {tableId} ������ ������Ʈ ����: {updateBro.GetMessage()}");
                        return false;
                    }
                }
                else
                {
                    // ���� ����
                    BackendReturnObject insertBro = Backend.GameData.Insert(tableId, param);
                    BackendState insertState = HandleError(insertBro);
                    
                    if (insertState == BackendState.Success)
                    {
                        Debug.Log($"[BackendManager] {tableId} ������ ���� ����");
                        OnInsertGameDataHandler?.Invoke(insertBro);
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"[BackendManager] {tableId} ������ ���� ����: {insertBro.GetMessage()}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// ���� �����͸� �����մϴ� (�����)
        /// </summary>
        public bool InsertGameData(string tableId, Param param)
        {
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �����Ͱ� ������� �ʽ��ϴ�.");
                return false;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] �α��� �Ǵ� ������ �ε尡 �ʿ��մϴ�");
                return false;
            }
            
            try
            {
                BackendReturnObject insertBro = Backend.GameData.Insert(tableId, param);
                BackendState insertState = HandleError(insertBro);
                
                if (insertState == BackendState.Success)
                {
                    Debug.Log($"[BackendManager] {tableId} ������ ���� ����");
                    OnInsertGameDataHandler?.Invoke(insertBro);
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] {tableId} ������ ���� ����: {insertBro.GetMessage()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// ���� �����͸� ������Ʈ�մϴ� (�����)
        /// </summary>
        public bool UpdateGameData(string tableId, string inDate, Param param)
        {
            if (!_isSaveEnabled)
            {
                Debug.LogWarning("[BackendManager] ������ ��Ȱ��ȭ�Ǿ� �־� �����Ͱ� ������� �ʽ��ϴ�.");
                return false;
            }
            
            if (!IsLogin || !_isLoaded)
            {
                Debug.LogError("[BackendManager] �α��� �Ǵ� ������ �ε尡 �ʿ��մϴ�");
                return false;
            }
            
            try
            {
                BackendReturnObject updateBro = Backend.GameData.UpdateV2(tableId, inDate, Backend.UserInDate, param);
                BackendState updateState = HandleError(updateBro);
                
                if (updateState == BackendState.Success)
                {
                    Debug.Log($"[BackendManager] {tableId} ������ ������Ʈ ����");
                    return true;
                }
                else
                {
                    Debug.LogError($"[BackendManager] {tableId} ������ ������Ʈ ����: {updateBro.GetMessage()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        #endregion

        #region �α� �� ���� ó��

        /// <summary>
        /// �鿣�� ������ �з��ϰ� ������ ó�� ������ �����մϴ�
        /// </summary>
        public BackendState HandleError(BackendReturnObject bro)
        {
            if (bro == null)
                return BackendState.Failure;
                
            if (bro.IsSuccess())
                return BackendState.Success;
            
            try
            {
                // ���� �α� ���ε� (�����ص� ��� ����)
                ErrorLogUpload(bro);
            }
            catch {}
            
            // ���� ���� �м�
            string errorCode = bro.GetErrorCode();
            string statusCode = bro.GetStatusCode();
            
            // ���� �ڵ庰 ó��
            switch (statusCode)
            {
                case "401": // ���� ����
                    if (errorCode == "BadUnauthorizedException" || 
                        errorCode == "UnauthorizedSessionException")
                    {
                        // ��ū ���� �õ�
                        if (RefreshTheBackendToken(1))
                            return BackendState.Retry;
                        else
                            return BackendState.Failure;
                    }
                    break;
                    
                case "400": // ��û ���� (�Ϲ������� ��õ��ص� ���� ���)
                    return BackendState.Failure;
                    
                case "403": // ���� ����
                    if (bro.IsTooManyRequestError())
                    {
                        Debug.LogWarning("[BackendManager] ������ ��û���� ��� ���ܵ�. 5�� �� �ٽ� �õ����ּ���.");
                        return BackendState.Failure;
                    }
                    break;
                    
                case "408": // Ÿ�Ӿƿ�
                case "500": // ���� ����
                case "502": // ����Ʈ���� ����
                case "503": // ���� �Ͻ� ����
                    return BackendState.Retry;
                    
                case "429": // ��û ���� �ʰ�
                    Debug.LogWarning("[BackendManager] ��û ������ �ʰ��߽��ϴ�. ��� �� �ٽ� �õ����ּ���.");
                    return BackendState.Retry;
            }
            
            // �ڳ��� Ȯ��� ���� Ȯ�� �޼��� ���
            if (bro.IsClientRequestFailError())
            {
                Debug.Log("[BackendManager] �Ͻ����� ��Ʈ��ũ ����. ��õ��մϴ�.");
                return BackendState.Retry;
            }
            else if (bro.IsServerError())
            {
                Debug.Log("[BackendManager] ���� ����. ��õ��մϴ�.");
                return BackendState.Retry;
            }
            else if (bro.IsMaintenanceError())
            {
                ShowPopup("���� ������", "���� ���� ������ �Դϴ�. ������ ���� �� ������ �ּ���.", Application.Quit);
                return BackendState.Maintainance;
            }
            else if (bro.IsBadAccessTokenError())
            {
                return RefreshTheBackendToken(3) ? BackendState.Retry : BackendState.Failure;
            }
            
            // ��Ÿ ����
            Debug.LogError($"[BackendManager] ó������ ���� ����: {errorCode}, {bro.GetMessage()}");
            return BackendState.Failure;
        }
        
        /// <summary>
        /// �ڳ� ��ū ������ �õ��մϴ�
        /// </summary>
        public bool RefreshTheBackendToken(int maxRetries)
        {
            if (maxRetries <= 0)
            {
                Debug.Log("[BackendManager] ��ū ���� ����");
                return false;
            }
            
            BackendReturnObject callback = Backend.BMember.RefreshTheBackendToken();

            if (callback.IsSuccess())
            {
                Debug.Log("[BackendManager] ��ū ���� ����");
                return true;
            }
            else
            {
                if (callback.IsClientRequestFailError() || callback.IsServerError())
                {
                    return RefreshTheBackendToken(maxRetries - 1);
                }
                
                Debug.LogError($"[BackendManager] ��ū ���� ����: {callback.GetMessage()}");
                return false;
            }
        }
        
        /// <summary>
        /// ���� �α׸� ������ ���ε��մϴ�
        /// </summary>
        public void ErrorLogUpload(BackendReturnObject bro)
        {
            if (!Backend.IsLogin || !_isLogin)
                return;

            try
            {
                Param logParam = new Param();
                logParam.Add("ErrorLog", bro.ToString());
                
                // ���� �߻� �ð� �߰�
                logParam.Add("Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                
                // ����̽� ���� �߰�
                logParam.Add("Device", SystemInfo.deviceModel);
                logParam.Add("OS", SystemInfo.operatingSystem);
                
                Backend.GameLog.InsertLogV2("ErrorLogs", logParam);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackendManager] ���� �α� ���ε� �� ���� �߻�: {ex.Message}");
            }
        }
        
        /// <summary>
        /// �Ϲ� �α׸� ������ ���ε��մϴ�
        /// </summary>
        public void LogUpload(string logName, string logDescription)
        {
            if (!Backend.IsLogin || !_isLogin)
                return;

            try
            {
                Param logParam = new Param();
                logParam.Add(logName, logDescription);
                logParam.Add("Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Backend.GameLog.InsertLogV2("UserLogs", logParam);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BackendManager] �α� ���ε� �� ���� �߻�: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ���� ����Ʈ�� ������ ���ε��մϴ� (�񵿱�)
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
                "���� ����Ʈ ���ε�",
                (callback) => Backend.GameLog.InsertLogV2("BugReport", logParam, (bro) => callback?.Invoke(bro)),
                onSuccess,
                onFail,
                1,
                false
            );
        }
        
        /// <summary>
        /// ���� ����Ʈ�� ������ ���ε��մϴ� (����)
        /// </summary>
        public bool BugReportUpload(string userId, string email, string logDescription)
        {
            if (!Backend.IsLogin || !_isLogin)
            {
                Debug.LogError("[BackendManager] �α����� �Ǿ����� �ʾ� ���� ����Ʈ�� ���ε��� �� �����ϴ�.");
                return false;
            }

            try
            {
                Param logParam = new Param();
                logParam.Add("Description", logDescription);
                logParam.Add("Email", email);
                logParam.Add("UserId", userId);
                logParam.Add("Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                
                BackendReturnObject bro = Backend.GameLog.InsertLogV2("BugReport", logParam);
                return bro.IsSuccess();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        #endregion

        #region ���ø����̼� �����ֱ�

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                // ���� ��׶���� ��ȯ�� ��
                Debug.Log("[BackendManager] �� �Ͻ�����, �߿� ������ ���� �õ�...");
                
                if (_isLogin && _isSaveEnabled)
                {
                    OnPauseHandler?.Invoke();
                }
            }
            else
            {
                // ���� ���׶���� ���ƿ� ��
                Debug.Log("[BackendManager] �� �����");
                OnResumeHandler?.Invoke();
                // ��ū ��ȿ�� ����
                CheckTokenValidity();
            }
        }
        
        private void OnApplicationQuit()
        {
            // �� ���� ��
            if (_isLogin && _isSaveEnabled)
            {
                Debug.Log("[BackendManager] �� ����, �߿� ������ ���� �õ�...");
                OnExitHandler?.Invoke();
            }
        }
        
        private void CheckTokenValidity()
        {
            if (_isLogin)
            {
                // ��ū ��ȿ�� �˻縦 ���� ������ API ȣ��
                Backend.BMember.GetUserInfo((bro) => {
                    if (!bro.IsSuccess())
                    {
                        if (bro.IsBadAccessTokenError() && !RefreshTheBackendToken(1))
                        {
                            // ��ū ���� ���� �� �α׾ƿ� ó��
                            Debug.LogWarning("[BackendManager] ������ ����Ǿ� �α׾ƿ��մϴ�.");
                            LogOut();
                            ShowPopup("���� ����", "������ ����Ǿ����ϴ�. �ٽ� �α������ּ���.");
                        }
                    }
                });
            }
        }

        #endregion

        /// <summary>
        /// �˾��� ǥ���մϴ�
        /// </summary>
        public void ShowPopup(string title, string description, Action onButtonClicked = null)
        {
            // ����� �˾� �Ŵ��� ����
            if (PopupManager.Instance != null)
                PopupManager.Instance.ShowPopup(title, description, onButtonClicked);
            else
                Debug.LogWarning($"[BackendManager] �˾� ǥ��: {title} - {description}");
        }
        
        /// <summary>
        /// ���� �����մϴ�
        /// </summary>
        private void ExitApp()
        {
            Application.Quit();
        }
    }
}

