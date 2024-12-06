using BackEnd;
using LitJson;
using System;
using System.Data;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Muks.BackEnd
{
    /// <summary>�������� ���� ������ Ȯ�� �ϴ� ������</summary>
    public enum BackendState
    {
        Failure,
        Maintainance,
        Retry,
        Success,
    }


    /// <summary> �ڳ��� ������ �� �ְ� ���ִ� �̱��� Ŭ���� </summary>
    public class BackendManager : MonoBehaviour
    {
        public static event Action OnGuestSignupHandler;
        public static event Action OnGuestLoginHandler;
        public static event Action<BackendReturnObject> OnInsertGameDataHandler;

        public static BackendManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("ChallengeManager");
                    _instance = obj.AddComponent<BackendManager>();
                    DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        private static BackendManager _instance;



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
                return;

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }

        private async void Init()
        {
            await BackendInit(10);
        }


        /// <summary>�ڳ� �ʱ� ����</summary>

        private async Task BackendInit(int maxRepeatCount = 10)
        {
            await ExecuteWithRetryAsync(maxRepeatCount, Backend.Initialize, (bro) =>
            {
                Debug.Log("�ʱ�ȭ ����");
            }, 
            (BackendState state) =>
            {
                Debug.LogError("�ڳ��� �ʱ�ȭ���� ���߽��ϴ�. �ٽ� ����:" + state);
            });

        }

        public void GetServerTimeAsync(Action<DateTime> onCompleted = null, Action<BackendReturnObject> onFailed = null)
        {
            Backend.Utils.GetServerTime((bro) =>
            {
                if (bro.IsSuccess())
                {
                    string time = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                    DateTime dateTime = DateTime.Parse(time);
                    onCompleted?.Invoke(dateTime);
                }
                else
                {
                    onCompleted?.Invoke(LocalTime);
                    onFailed?.Invoke(bro);
                }
            });
        }


        /// <summary> id, pw, ���� ���� ���н� �ݺ�Ƚ��, �Ϸ� �� ������ �Լ��� �޾� �α����� �����ϴ� �Լ� </summary>
        public async Task CustomLogin(string id, string pw, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.BMember.CustomLogin(id, pw), (bro) =>
            {
                Debug.Log("Ŀ���� �α��� ����");
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("Ŀ���� �α��� ���� �߻�:" + state);
                onFailed?.Invoke(state);
            });
        }


        /// <summary>�Խ�Ʈ �α����� �����ϴ� �Լ� </summary>
        public async Task GuestLogin(int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendReturnObject> onFailed = null)
        {
            if (IsLogin)
                return;

            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.BMember.GuestLogin(), async (bro) =>
            {
                if(!Backend.IsLogin)
                {
                    if (bro.GetStatusCode() == "401")
                    {
                        Debug.Log("�Խ�Ʈ �α��� ����: ������ ������ ����");
                        Backend.BMember.DeleteGuestInfo();
                        await GuestLogin(maxRepeatCount - 1, onCompleted, onFailed);
                    }
                    else
                    {
                        Debug.Log("�Խ�Ʈ �α��� ����: " + bro.GetMessage());
                    }

                    return;
                }



                Debug.Log("�Խ�Ʈ �α��� ����");
                if(bro.GetStatusCode() == "201")
                {
                    UserInfo.SetFirstAccessTime(ServerTime);
                    OnGuestSignupHandler?.Invoke();
                }

                else if(bro.GetStatusCode() == "200")
                {
                    OnGuestLoginHandler?.Invoke();
                }
                onCompleted?.Invoke(bro);
                _isLogin = true;

            },
            async (BackendReturnObject bro) =>
            {
                if (bro.GetStatusCode() == "401")
                {
                    Debug.Log("�Խ�Ʈ �α��� ����: ������ ������ ����");
                    Backend.BMember.DeleteGuestInfo();
                    await GuestLogin(maxRepeatCount - 1, onCompleted, onFailed);
                }
                else
                {
                    Debug.Log("�Խ�Ʈ �α��� ����: " + bro.GetMessage());
                }
            });
        }

        public void LogOut()
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            _isLogin = false;
            _isLoaded = false;
        }


        /// <summary> id, pw, ���� ���� ���н� �ݺ�Ƚ��, �Ϸ� �� ������ �Լ��� �޾� ȸ�������� �����ϴ� �Լ� </summary>
        public async Task CustomSignup(string id, string pw, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.BMember.CustomSignUp(id, pw), (bro) =>
            {
                Debug.Log("���� ���� ����");
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("���� ���� ���� �߻�: " + state);
                onFailed?.Invoke(state);
            });
        }


        /// <summary> �� ������ ID�� �޾� ���� ���� Ȯ�� �� ���� �Լ��� ó�����ִ� �Լ� </summary>
        public void GetMyData(string selectedProbabilityFileId, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);

            ExecuteWithRetry(maxRepeatCount, () => Backend.GameData.Get(selectedProbabilityFileId, where), (bro) =>
            {
                Debug.Log("�� ���� �ҷ����� ����:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
                _isLoaded = true;
            },
            (state) =>
            {
                Debug.LogError("�� ���� �ҷ����� ���� �߻�:" + selectedProbabilityFileId + "  State: " + state);
                onFailed?.Invoke(state);
                _isLoaded = false;
            });
        }


        /// <summary> ��Ʈ ID�� �ݺ� Ƚ��, ������ ���� ��� ������ �Լ��� �޾� �ڳ����� ChartData�� �޾ƿ��� �Լ� </summary>
        public async Task GetChartData(string selectedProbabilityFileId, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.Chart.GetOneChartAndSave(selectedProbabilityFileId), (bro) =>
            {
                Debug.LogError("��Ʈ �ҷ����� ����:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("��Ʈ �ҷ����� ���� �߻�:" + selectedProbabilityFileId);
                onFailed?.Invoke(state);
            });
        }


        public void SaveGameData(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("���� ������ �����Ϸ� ������ ���� �ε尡 �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);
            ExecuteWithRetry(maxRepeatCount, () => Backend.GameData.Get(selectedProbabilityFileId, where), (bro) =>
            {
                var rows = bro.FlattenRows();
                if (rows != null && 0 < rows.Count)
                {
                    UpdateGameData(selectedProbabilityFileId, bro.GetInDate(), maxRepeatCount, param, onCompleted, onFailed);
                    return;
                }

                else
                {
                    InsertGameData(selectedProbabilityFileId, maxRepeatCount, param, onCompleted, onFailed);
                    return;
                }

            },
            (state) =>
            {
                 Debug.LogError("���� ���� ���� ���� �߻�:" + selectedProbabilityFileId + "  State: " + state);
                 onFailed?.Invoke(state);
                 return;
             });
        }


        public async void SaveGameDataAsync(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("���� ������ �����Ϸ� ������ ���� �ε尡 �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);
            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.GameData.Get(selectedProbabilityFileId, where), (bro) =>
           {
               var rows = bro.FlattenRows();
               if (rows != null && 0 < rows.Count)
               {
                   UpdateGameDataAsync(selectedProbabilityFileId, bro.GetInDate(), maxRepeatCount, param, onCompleted, onFailed);
                   return;
               }

               else
               {
                   InsertGameDataAsync(selectedProbabilityFileId, maxRepeatCount, param, onCompleted, onFailed);
                   return;
               }

           },
           (state) =>
           {
               Debug.LogError("���� ���� ���� ���� �߻�:" + selectedProbabilityFileId + "  State: " + state);
               onFailed?.Invoke(state);
               return;
           });
        }


        /// <summary> ��Ʈ ID�� �ݺ� Ƚ��, ������ ���� ��� ������ �Լ��� �޾� �ڳ� GameData���� ������ ���������� �߰��ϴ� �Լ� </summary>
        public void InsertGameData(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("���� ������ �����Ϸ� ������ ���� �ε尡 �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            ExecuteWithRetry(maxRepeatCount, () => Backend.GameData.Insert(selectedProbabilityFileId, param), (bro) =>
            {
                Debug.Log("���� ���� ���� ����:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
                OnInsertGameDataHandler?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("���� ���� ���� ���� �߻�:" + selectedProbabilityFileId);
                onFailed?.Invoke(state);
            });
        }


        /// <summary> ��Ʈ ID�� �ݺ� Ƚ��, ������ ���� ��� ������ �Լ��� �޾� �ڳ� GameData���� ������ �񵿱������� �߰��ϴ� �Լ� </summary>
        public void InsertGameDataAsync(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            if(maxRepeatCount <= 0)
            {
                Debug.LogError("��õ� Ƚ���� ���� ����߽��ϴ�: " + selectedProbabilityFileId);
                return;
            }

            Backend.GameData.Insert(selectedProbabilityFileId, param, bro =>
            {
                switch (HandleError(bro))
                {
                    case BackendState.Failure:
                        onFailed?.Invoke(BackendState.Failure);
                        break;

                    case BackendState.Maintainance:
                        onFailed?.Invoke(BackendState.Maintainance);
                        break;

                    case BackendState.Retry:
                        InsertGameDataAsync(selectedProbabilityFileId, maxRepeatCount - 1, param, onCompleted, onFailed);
                        break;

                    case BackendState.Success:
                        onCompleted?.Invoke(bro);
                        OnInsertGameDataHandler?.Invoke(bro);
                        break;
                }
            });      
        }


        /// <summary> ��Ʈ ID�� �ݺ� Ƚ��, ������ ���� ��� ������ �Լ��� �޾� �ڳ� GameData���� ������ ���������� �߰��ϴ� �Լ� </summary>
        public void UpdateGameData(string selectedProbabilityFileId, string inDate, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("���� ������ �����Ϸ� ������ ���� �ε尡 �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            ExecuteWithRetry(maxRepeatCount, () => Backend.GameData.UpdateV2(selectedProbabilityFileId, inDate, Backend.UserInDate, param), (bro) =>
            {
                Debug.Log("���� ���� ���� ����:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("���� ���� ���� ���� �߻�:" + selectedProbabilityFileId);
                onFailed?.Invoke(state);
            });
        }


        /// <summary> ��Ʈ ID�� �ݺ� Ƚ��, ������ ���� ��� ������ �Լ��� �޾� �ڳ� GameData���� ������ �񵿱������� �߰��ϴ� �Լ� </summary>
        public void UpdateGameDataAsync(string selectedProbabilityFileId, string inDate, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("�α����� �Ǿ����� �ʽ��ϴ�.");
                return;
            }

            if (maxRepeatCount <= 0)
            {
                Debug.LogError("��õ� Ƚ���� ���� ����߽��ϴ�: " + selectedProbabilityFileId);
                return;
            }

            Backend.GameData.UpdateV2(selectedProbabilityFileId, inDate, Backend.UserInDate, param, bro =>
            {
                switch (HandleError(bro))
                {
                    case BackendState.Failure:
                        onFailed?.Invoke(BackendState.Failure);
                        break;

                    case BackendState.Maintainance:
                        onFailed?.Invoke(BackendState.Failure);
                        break;

                    case BackendState.Retry:
                        UpdateGameDataAsync(selectedProbabilityFileId, inDate, maxRepeatCount - 1, param, onCompleted, onFailed);
                        break;

                    case BackendState.Success:
                        onCompleted?.Invoke(bro);
                        break;
                }
            });
        }


        private void ExecuteWithRetry(int maxRepeatCount, Func<BackendReturnObject> action, Action<BackendReturnObject> onSuccess, Action<BackendState> onFail)
        {
            BackendReturnObject bro = action.Invoke();
            BackendState state = HandleError(bro);
            if (maxRepeatCount <= 0)
            {
                onFail?.Invoke(state);
                return;
            }

            if (state == BackendState.Retry)
            {
                ExecuteWithRetry(maxRepeatCount - 1, action, onSuccess, onFail);
                return;
            }
            else if (state == BackendState.Success)
            {
                onSuccess?.Invoke(bro);
                return;
            }

            onFail?.Invoke(state);
        }

        private void ExecuteWithRetry(int maxRepeatCount, Func<BackendReturnObject> action, Action<BackendReturnObject> onSuccess, Action<BackendReturnObject> onFail)
        {
            BackendReturnObject bro = action.Invoke();
            BackendState state = HandleError(bro);
            if (maxRepeatCount <= 0)
            {
                onFail?.Invoke(bro);
                return;
            }

            if (state == BackendState.Retry)
            {
                ExecuteWithRetry(maxRepeatCount - 1, action, onSuccess, onFail);
                return;
            }
            else if (state == BackendState.Success)
            {
                onSuccess?.Invoke(bro);
                return;
            }

            onFail?.Invoke(bro);
        }


        private async Task ExecuteWithRetryAsync(int maxRepeatCount, Func<BackendReturnObject> action, Action<BackendReturnObject> onSuccess, Action<BackendState> onFail)
        {
            BackendReturnObject bro = action.Invoke();
            BackendState state = HandleError(bro);
            if (maxRepeatCount <= 0)
            {
                onFail?.Invoke(state);
                return;
            }

            if(state == BackendState.Retry)
            {
                await Task.Delay(100);
                await ExecuteWithRetryAsync(maxRepeatCount - 1, action, onSuccess, onFail);
                return;
            }
            else if(state == BackendState.Success)
            {
                onSuccess?.Invoke(bro);
                return;
            }

            onFail?.Invoke(state);
        }


        private async Task ExecuteWithRetryAsync(int maxRepeatCount, Func<BackendReturnObject> action, Action<BackendReturnObject> onSuccess, Action<BackendReturnObject> onFail)
        {
            BackendReturnObject bro = action.Invoke();
            BackendState state = HandleError(bro);
            if (maxRepeatCount <= 0)
            {
                onFail?.Invoke(bro);
                return;
            }

            if (state == BackendState.Retry)
            {
                await Task.Delay(100);
                await ExecuteWithRetryAsync(maxRepeatCount - 1, action, onSuccess, onFail);
                return;
            }
            else if (state == BackendState.Success)
            {
                onSuccess?.Invoke(bro);
                return;
            }

            onFail?.Invoke(bro);
        }




        /// <summary> ������ ���� ���¸� üũ�ϰ� BackendState���� ��ȯ�ϴ� �Լ� </summary>
        public BackendState HandleError(BackendReturnObject bro)
        {
            if (bro.IsSuccess())
            {
                return BackendState.Success;
            }
            else
            {
                ErrorLogUpload(bro);

                if (bro.IsClientRequestFailError()) // Ŭ���̾�Ʈ�� �Ͻ����� ��Ʈ��ũ ���� ��
                {
                    Debug.LogError("�Ͻ����� ��Ʈ��ũ ����");
                    return BackendState.Retry;
                }
                else if (bro.IsServerError()) // ������ �̻� �߻� ��
                {
                    Debug.LogError("���� �̻� �߻�");
                    return BackendState.Retry;
                }
                else if (bro.IsMaintenanceError()) // ���� ���°� '����'�� ��
                {
                    //���� �˾�â + �α��� ȭ������ ������
                    Debug.Log("���� ������");

                    string errorName = "���� ������";
                    string errorDescription = "���� ���� ���Դϴ�. \n������ �Ϸ�� �� ������ �ּ���.";
                    //_popup.Show(errorName, errorDescription, ExitApp);

                    return BackendState.Maintainance;
                }
                else if (bro.IsTooManyRequestError()) // �ܱⰣ�� ���� ��û�� ���� ��� �߻��ϴ� 403 Forbbiden �߻� ��
                {
                    //�ܱⰣ�� ���� ��û�� ������ �߻��մϴ�. 5�е��� �ڳ��� �Լ� ��û�� �����ؾ��մϴ�.  
                    DebugLog.LogError("�ܱⰣ�� ���� ��û�� ���½��ϴ�. 5�а� ��� �Ұ�");
                    string errorName = "���� ��û ����";
                    string errorDescription = "�ܱⰣ ���� ��û�� ���½��ϴ�. \n5�е� �ٽ� ������ �õ��ϼ���.";
                    //_popup.Show(errorName, errorDescription, ExitApp);

                    return BackendState.Failure;
                }
                else if (bro.IsBadAccessTokenError())
                {
                    return RefreshTheBackendToken(3) ? BackendState.Retry : BackendState.Failure;
                }

/*                //���� ��⿡�� �α��� ������ �����ִµ� ������ �����Ͱ� ������
                //��⿡ ����� �α��� ������ �����Ѵ�.
                else if (bro.GetMessage() == "bad customId, �߸��� customId �Դϴ�")
                {
                    Backend.BMember.DeleteGuestInfo();
                }
*/

                else
                {
                    DebugLog.LogError(bro.GetErrorCode() + ", " + bro.GetErrorMessage());
                    return BackendState.Failure;
                }
            }
        }



        /// <summary> �ڳ� ��ū ��߱� �Լ� </summary>
        /// maxRepeatCount : ���� ���� ���н� �� �õ��� Ƚ��
        public bool RefreshTheBackendToken(int maxRepeatCount)
        {
            if (maxRepeatCount <= 0)
            {
                Debug.Log("��ū �߱� ����");
                return false;
            }
            
            BackendReturnObject callback = Backend.BMember.RefreshTheBackendToken();

            if (callback.IsSuccess())
            {
                Debug.Log("��ū �߱� ����");
                return true;
            }
            else
            {
                if (callback.IsClientRequestFailError()) // Ŭ���̾�Ʈ�� �Ͻ����� ��Ʈ��ũ ���� ��
                {
                    return RefreshTheBackendToken(maxRepeatCount - 1);
                }
                else if (callback.IsServerError()) // ������ �̻� �߻� ��
                {
                    return RefreshTheBackendToken(maxRepeatCount - 1);
                }
                else if (callback.IsMaintenanceError()) // ���� ���°� '����'�� ��
                {
                    //���� �˾�â + �α��� ȭ������ ������
                    return false;
                }
                else if (callback.IsTooManyRequestError()) // �ܱⰣ�� ���� ��û�� ���� ��� �߻��ϴ� 403 Forbbiden �߻� ��
                {
                    //�ʹ� ���� ��û�� ������ ��
                    return false;
                }
                else
                {
                    //��õ��� �ص� �׼�����ū ��߱��� �Ұ����� ���
                    //Ŀ���� �α��� Ȥ�� �䵥���̼� �α����� ���� ���� �α����� �����ؾ��մϴ�.  
                    //�ߺ� �α����� ��� 401 bad refreshToken ������ �Բ� �߻��� �� �ֽ��ϴ�.  
                    return false;
                }
            }
        }


        /// <summary>�г����� �����ϴ� �Լ�, ������ true, �ߺ� �г��� Ȥ�� ���н� false ��ȯ</summary>
        public bool UpdateNickName(string nickName)
        {
            BackendReturnObject checkBro = Backend.BMember.CheckNicknameDuplication(nickName);

            if (!checkBro.IsSuccess())
                return false;

            BackendReturnObject bro = Backend.BMember.UpdateNickname(nickName);
            return bro.IsSuccess();
        }


        /// <summary>�г����� �����ϴ� �Լ�, ������ true, �ߺ� �г��� Ȥ�� ���н� false ��ȯ</summary>
        public bool CreateNickName(string nickName)
        {
            BackendReturnObject checkBro = Backend.BMember.CheckNicknameDuplication(nickName);

            if(!checkBro.IsSuccess())
                return false;

            BackendReturnObject bro = Backend.BMember.CreateNickname(nickName);
            return bro.IsSuccess();
        }


        public void ErrorLogUpload(BackendReturnObject bro)
        {
            if (!Backend.IsLogin || !_isLogin)
                return;

            Param logParam = new Param();
            logParam.Add(Backend.UserNickName + "ErrorLog", bro.ToString());

            Backend.GameLog.InsertLogV2("ErrorLogs", logParam);
        }


        public void LogUpload(string logName, string logDescription)
        {
            if (!Backend.IsLogin || !_isLogin)
                return;

            Param logParam = new Param();
            logParam.Add(logName, logDescription);
            Backend.GameLog.InsertLogV2("UserLogs", logParam, bro => { });
        }


        public void BugReportUpload(string email, string logDescription)
        {
            if (!Backend.IsLogin || !_isLogin)
                return;

            Param logParam = new Param();
            logParam.Add("Email", email);
            logParam.Add("Description", logDescription);
            Backend.GameLog.InsertLogV2("BugReport", logParam);
        }


        /// <summary>���� ���� �˾��� ����ִ� �Լ�</summary>
        public void ShowPopup(string title, string description, UnityAction onButtonClicked = null)
        {
            //_popup.Show(title, description, onButtonClicked);
        }


        private void ExitApp()
        {
            Application.Quit();
        }
    }
}

