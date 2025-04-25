using BackEnd;
using Muks.BackEnd;
using System;
using UnityEngine;

namespace Muks.BackEnd
{
    public class VersionManagement : IDisposable
    {
        private const string _playStoreLink = "market://details?id=패키지 네임";
        private const string _oneStoreLink = "onestore://common/product/0000774867";
        private const string _appStoreLink = "itms-apps://itunes.apple.com/app/앱ID";


        private int _maxRepeatCount;


        public VersionManagement()
        {
            _maxRepeatCount = 10;
        }


        /// <summary>서버 버전과 클라이언트 버전이 맞는지 확인 하는 함수 </summary>
        public bool UpdateCheck()
        {
            #if UNITY_EDITOR

            return true;

            #endif

            Version client = new Version(Application.version);
            Debug.Log("client Version: " + client);
            BackendReturnObject bro = BackendManager.Instance.ProcessBackendAPISync("버전 조회", Backend.Utils.GetLatestVersion);
            if(bro == null || !bro.IsSuccess())
            {
                string errorName = "버전 조회 오류";
                string errorDescription = "서버에 접속하지 못했습니다. \n다시 접속 해주세요.";
                BackendManager.Instance.ShowPopup(errorName, errorDescription);
                BackendManager.Instance.ShowPopupExitButton();
                return false;
            }

            string version = bro.GetReturnValuetoJSON()["version"].ToString();
            if (version == Application.version)
            {
                return true;
            }

            string forceUpdate = bro.GetReturnValuetoJSON()["type"].ToString();
            //마이너 업데이트면 업데이트 필요 없음
            if(forceUpdate == "1")
            {
                return true;
            }
            //메이저 업데이트면 업데이트 필수
            else if(forceUpdate == "2")
            {
                OpenStoreLink();
                return false;
            }

            Debug.LogError("버전 조회가 아무 조건에도 해당하지 않습니다.");
            return false;

            // //2번안
            // Version server = new Version(version);
            // int result = server.CompareTo(client);
            
            // if (result == 0)
            // {
            //     return true;
            // }

            // else if (result < 0)
            // {
            //     return true;
            // }

            // else if (client == null)
            // {
            //     string errorName = "버전 조회 오류";
            //     string errorDescription = "클라이언트 버전 정보가 없습니다. \n다시 접속 해주세요.";
            //     BackendManager.Instance.ShowPopup(errorName, errorDescription, () => Application.Quit());
            //     return false;
            // }

            // OpenStoreLink();
            // return false;
        }



        private void OpenStoreLink()
        {
            string errorName = "업데이트 필요";
            string errorDescription = "최신 버전이 존재합니다. \n업데이트를 진행해 주세요.";
            BackendManager.Instance.ShowPopup(errorName, errorDescription);
            BackendManager.Instance.SetPopupButton1("업데이트", () =>
            {
                Application.OpenURL(_oneStoreLink);
                Application.Quit();
            });
        }

        public void Dispose()
        {
        }
    }

}