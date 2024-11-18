using BackEnd;
using Muks.BackEnd;
using System;
using UnityEngine;

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

        if (_maxRepeatCount <= 0)
        {
            string errorName = "서버 접속 오류";
            string errorDescription = "서버에 접속하지 못했습니다. \n재 접속 해주세요.";
            BackendManager.Instance.ShowPopup(errorName, errorDescription, () => Application.Quit());
            return false;
        }

        Version client = new Version(Application.version);
        Debug.Log("client Version: " + client);

        BackendReturnObject bro = Backend.Utils.GetLatestVersion();

        if (!bro.IsSuccess())
        {
            Debug.LogError("버전 조회 실패");
            return false;
        }

        switch (BackendManager.Instance.HandleError(bro))
        {
            case BackendState.Failure:
                return false;

            case BackendState.Maintainance:
                return false;

            case BackendState.Retry:
                _maxRepeatCount--;
                return UpdateCheck();

            case BackendState.Success:
                string version = bro.GetReturnValuetoJSON()["version"].ToString();
                Version server = new Version(version);

                int result = server.CompareTo(client);

               
                if (result == 0)
                {
                    return true;
                }

                else if (result < 0)
                {
                    return true;
                }

                else if (client == null)
                {
                    Debug.Log("클라이언트 버전 정보 null");
                    return false;
                }

                OpenStoreLink();
                return false;
        }
        return false;
    }



    private void OpenStoreLink()
    {
        string errorName = "업데이트";
        string errorDescription = "최신 버전이 존재합니다. \n업데이트를 진행해 주세요.";
        BackendManager.Instance.ShowPopup(errorName, errorDescription, () =>
        {
            Application.OpenURL(_oneStoreLink);
            Application.Quit();
        });

    }

    public void Dispose()
    {
    }
}
