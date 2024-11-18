using BackEnd;
using LitJson;
using Muks.BackEnd;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TmpNoticeManagement : IDisposable
{
    private int _maxRepeatCount;

    public TmpNoticeManagement() 
    {
        _maxRepeatCount = 10;
    }


    /// <summary>뒤끝 임시 공지 확인 함수</summary>
    public bool TmpNoticeCheck(UnityAction onButtonClicked = null)
    {
        if (_maxRepeatCount <= 0)
        {
            string errorName = "서버 접속 오류";
            string errorDescription = "서버에 접속하지 못했습니다. \n재 접속 해주세요.";
            BackendManager.Instance.ShowPopup(errorName, errorDescription, () => Application.Quit());
            return false;
        }

        string tmpNotice = Backend.Notice.GetTempNotice();

        //공지가 없으면
        if (string.IsNullOrEmpty(tmpNotice))
            return false;

        //있으면
        JsonData data = JsonMapper.ToObject(tmpNotice);
        bool isUse = bool.Parse(data["isUse"].ToString());

        if (isUse)
        {
            string title = "공지사항";
            string description = data["contents"].ToString();
            BackendManager.Instance.ShowPopup(title, description, onButtonClicked);
            return true;
        }

        return false;
    }


    public void Dispose()
    {
    }


}
