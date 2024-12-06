using BackEnd;
using LitJson;
using System;
using System.Data;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Muks.BackEnd
{
    /// <summary>서버와의 연결 상태을 확인 하는 열거형</summary>
    public enum BackendState
    {
        Failure,
        Maintainance,
        Retry,
        Success,
    }


    /// <summary> 뒤끝과 연동할 수 있게 해주는 싱글톤 클래스 </summary>
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



        /// <summary>이 값이 참일 때만 서버에 정보를 보냅니다.(로그인 실패인데 정보를 보내면 서버 정보가 초기화)</summary> 
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


        /// <summary>뒤끝 초기 설정</summary>

        private async Task BackendInit(int maxRepeatCount = 10)
        {
            await ExecuteWithRetryAsync(maxRepeatCount, Backend.Initialize, (bro) =>
            {
                Debug.Log("초기화 성공");
            }, 
            (BackendState state) =>
            {
                Debug.LogError("뒤끝을 초기화하지 못했습니다. 다시 실행:" + state);
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


        /// <summary> id, pw, 서버 연결 실패시 반복횟수, 완료 시 실행할 함수를 받아 로그인을 진행하는 함수 </summary>
        public async Task CustomLogin(string id, string pw, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.BMember.CustomLogin(id, pw), (bro) =>
            {
                Debug.Log("커스텀 로그인 성공");
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("커스텀 로그인 에러 발생:" + state);
                onFailed?.Invoke(state);
            });
        }


        /// <summary>게스트 로그인을 진행하는 함수 </summary>
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
                        Debug.Log("게스트 로그인 실패: 서버에 정보가 없음");
                        Backend.BMember.DeleteGuestInfo();
                        await GuestLogin(maxRepeatCount - 1, onCompleted, onFailed);
                    }
                    else
                    {
                        Debug.Log("게스트 로그인 실패: " + bro.GetMessage());
                    }

                    return;
                }



                Debug.Log("게스트 로그인 성공");
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
                    Debug.Log("게스트 로그인 실패: 서버에 정보가 없음");
                    Backend.BMember.DeleteGuestInfo();
                    await GuestLogin(maxRepeatCount - 1, onCompleted, onFailed);
                }
                else
                {
                    Debug.Log("게스트 로그인 실패: " + bro.GetMessage());
                }
            });
        }

        public void LogOut()
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            _isLogin = false;
            _isLoaded = false;
        }


        /// <summary> id, pw, 서버 연결 실패시 반복횟수, 완료 시 실행할 함수를 받아 회원가입을 진행하는 함수 </summary>
        public async Task CustomSignup(string id, string pw, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.BMember.CustomSignUp(id, pw), (bro) =>
            {
                Debug.Log("계정 생성 성공");
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("계정 생성 에러 발생: " + state);
                onFailed?.Invoke(state);
            });
        }


        /// <summary> 내 데이터 ID를 받아 서버 연결 확인 후 받은 함수를 처리해주는 함수 </summary>
        public void GetMyData(string selectedProbabilityFileId, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            Where where = new Where();
            where.Equal("owner_inDate", Backend.UserInDate);

            ExecuteWithRetry(maxRepeatCount, () => Backend.GameData.Get(selectedProbabilityFileId, where), (bro) =>
            {
                Debug.Log("내 정보 불러오기 성공:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
                _isLoaded = true;
            },
            (state) =>
            {
                Debug.LogError("내 정보 불러오기 에러 발생:" + selectedProbabilityFileId + "  State: " + state);
                onFailed?.Invoke(state);
                _isLoaded = false;
            });
        }


        /// <summary> 차트 ID와 반복 횟수, 연결이 됬을 경우 실행할 함수를 받아 뒤끝에서 ChartData를 받아오는 함수 </summary>
        public async Task GetChartData(string selectedProbabilityFileId, int maxRepeatCount = 10, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            await ExecuteWithRetryAsync(maxRepeatCount, () => Backend.Chart.GetOneChartAndSave(selectedProbabilityFileId), (bro) =>
            {
                Debug.LogError("차트 불러오기 성공:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("차트 불러오기 에러 발생:" + selectedProbabilityFileId);
                onFailed?.Invoke(state);
            });
        }


        public void SaveGameData(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("게임 정보를 삽입하려 했으나 현재 로드가 되어있지 않습니다.");
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
                 Debug.LogError("게임 정보 저장 에러 발생:" + selectedProbabilityFileId + "  State: " + state);
                 onFailed?.Invoke(state);
                 return;
             });
        }


        public async void SaveGameDataAsync(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("게임 정보를 삽입하려 했으나 현재 로드가 되어있지 않습니다.");
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
               Debug.LogError("게임 정보 저장 에러 발생:" + selectedProbabilityFileId + "  State: " + state);
               onFailed?.Invoke(state);
               return;
           });
        }


        /// <summary> 차트 ID와 반복 횟수, 연결이 됬을 경우 실행할 함수를 받아 뒤끝 GameData란에 정보를 동기적으로 추가하는 함수 </summary>
        public void InsertGameData(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("게임 정보를 삽입하려 했으나 현재 로드가 되어있지 않습니다.");
                return;
            }

            ExecuteWithRetry(maxRepeatCount, () => Backend.GameData.Insert(selectedProbabilityFileId, param), (bro) =>
            {
                Debug.Log("게임 정보 삽입 성공:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
                OnInsertGameDataHandler?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("게임 정보 삽입 에러 발생:" + selectedProbabilityFileId);
                onFailed?.Invoke(state);
            });
        }


        /// <summary> 차트 ID와 반복 횟수, 연결이 됬을 경우 실행할 함수를 받아 뒤끝 GameData란에 정보를 비동기적으로 추가하는 함수 </summary>
        public void InsertGameDataAsync(string selectedProbabilityFileId, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            if(maxRepeatCount <= 0)
            {
                Debug.LogError("재시도 횟수를 전부 사용했습니다: " + selectedProbabilityFileId);
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


        /// <summary> 차트 ID와 반복 횟수, 연결이 됬을 경우 실행할 함수를 받아 뒤끝 GameData란에 정보를 동기적으로 추가하는 함수 </summary>
        public void UpdateGameData(string selectedProbabilityFileId, string inDate, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            if (!IsLoaded)
            {
                DebugLog.LogError("게임 정보를 갱신하려 했으나 현재 로드가 되어있지 않습니다.");
                return;
            }

            ExecuteWithRetry(maxRepeatCount, () => Backend.GameData.UpdateV2(selectedProbabilityFileId, inDate, Backend.UserInDate, param), (bro) =>
            {
                Debug.Log("게임 정보 갱신 성공:" + selectedProbabilityFileId);
                onCompleted?.Invoke(bro);
            },
            (state) =>
            {
                Debug.LogError("게임 정보 갱신 에러 발생:" + selectedProbabilityFileId);
                onFailed?.Invoke(state);
            });
        }


        /// <summary> 차트 ID와 반복 횟수, 연결이 됬을 경우 실행할 함수를 받아 뒤끝 GameData란에 정보를 비동기적으로 추가하는 함수 </summary>
        public void UpdateGameDataAsync(string selectedProbabilityFileId, string inDate, int maxRepeatCount, Param param, Action<BackendReturnObject> onCompleted = null, Action<BackendState> onFailed = null)
        {
            if (!Backend.IsLogin && !_isLogin)
            {
                Debug.LogError("로그인이 되어있지 않습니다.");
                return;
            }

            if (maxRepeatCount <= 0)
            {
                Debug.LogError("재시도 횟수를 전부 사용했습니다: " + selectedProbabilityFileId);
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




        /// <summary> 서버와 연결 상태를 체크하고 BackendState값을 반환하는 함수 </summary>
        public BackendState HandleError(BackendReturnObject bro)
        {
            if (bro.IsSuccess())
            {
                return BackendState.Success;
            }
            else
            {
                ErrorLogUpload(bro);

                if (bro.IsClientRequestFailError()) // 클라이언트의 일시적인 네트워크 끊김 시
                {
                    Debug.LogError("일시적인 네트워크 끊김");
                    return BackendState.Retry;
                }
                else if (bro.IsServerError()) // 서버의 이상 발생 시
                {
                    Debug.LogError("서버 이상 발생");
                    return BackendState.Retry;
                }
                else if (bro.IsMaintenanceError()) // 서버 상태가 '점검'일 시
                {
                    //점검 팝업창 + 로그인 화면으로 보내기
                    Debug.Log("게임 점검중");

                    string errorName = "서버 점검중";
                    string errorDescription = "서버 점검 중입니다. \n점검이 완료된 후 접속해 주세요.";
                    //_popup.Show(errorName, errorDescription, ExitApp);

                    return BackendState.Maintainance;
                }
                else if (bro.IsTooManyRequestError()) // 단기간에 많은 요청을 보낼 경우 발생하는 403 Forbbiden 발생 시
                {
                    //단기간에 많은 요청을 보내면 발생합니다. 5분동안 뒤끝의 함수 요청을 중지해야합니다.  
                    DebugLog.LogError("단기간에 많은 요청을 보냈습니다. 5분간 사용 불가");
                    string errorName = "서버 요청 오류";
                    string errorDescription = "단기간 많은 요청을 보냈습니다. \n5분뒤 다시 접속을 시도하세요.";
                    //_popup.Show(errorName, errorDescription, ExitApp);

                    return BackendState.Failure;
                }
                else if (bro.IsBadAccessTokenError())
                {
                    return RefreshTheBackendToken(3) ? BackendState.Retry : BackendState.Failure;
                }

/*                //만약 기기에는 로그인 정보가 남아있는데 서버에 데이터가 없으면
                //기기에 저장된 로그인 정보를 삭제한다.
                else if (bro.GetMessage() == "bad customId, 잘못된 customId 입니다")
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



        /// <summary> 뒤끝 토큰 재발급 함수 </summary>
        /// maxRepeatCount : 서버 연결 실패시 재 시도할 횟수
        public bool RefreshTheBackendToken(int maxRepeatCount)
        {
            if (maxRepeatCount <= 0)
            {
                Debug.Log("토큰 발급 실패");
                return false;
            }
            
            BackendReturnObject callback = Backend.BMember.RefreshTheBackendToken();

            if (callback.IsSuccess())
            {
                Debug.Log("토큰 발급 성공");
                return true;
            }
            else
            {
                if (callback.IsClientRequestFailError()) // 클라이언트의 일시적인 네트워크 끊김 시
                {
                    return RefreshTheBackendToken(maxRepeatCount - 1);
                }
                else if (callback.IsServerError()) // 서버의 이상 발생 시
                {
                    return RefreshTheBackendToken(maxRepeatCount - 1);
                }
                else if (callback.IsMaintenanceError()) // 서버 상태가 '점검'일 시
                {
                    //점검 팝업창 + 로그인 화면으로 보내기
                    return false;
                }
                else if (callback.IsTooManyRequestError()) // 단기간에 많은 요청을 보낼 경우 발생하는 403 Forbbiden 발생 시
                {
                    //너무 많은 요청을 보내는 중
                    return false;
                }
                else
                {
                    //재시도를 해도 액세스토큰 재발급이 불가능한 경우
                    //커스텀 로그인 혹은 페데레이션 로그인을 통해 수동 로그인을 진행해야합니다.  
                    //중복 로그인일 경우 401 bad refreshToken 에러와 함께 발생할 수 있습니다.  
                    return false;
                }
            }
        }


        /// <summary>닉네임을 변경하는 함수, 성공시 true, 중복 닉네임 혹은 실패시 false 반환</summary>
        public bool UpdateNickName(string nickName)
        {
            BackendReturnObject checkBro = Backend.BMember.CheckNicknameDuplication(nickName);

            if (!checkBro.IsSuccess())
                return false;

            BackendReturnObject bro = Backend.BMember.UpdateNickname(nickName);
            return bro.IsSuccess();
        }


        /// <summary>닉네임을 생성하는 함수, 성공시 true, 중복 닉네임 혹은 실패시 false 반환</summary>
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


        /// <summary>서버 오류 팝업을 띄워주는 함수</summary>
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

