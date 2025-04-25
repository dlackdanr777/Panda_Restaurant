using BackEnd;
using Muks.BackEnd;
using System;
using UnityEngine;

namespace Muks.BackEnd
{
    public class VersionManagement : IDisposable
    {
        private const string _playStoreLink = "market://details?id=��Ű�� ����";
        private const string _oneStoreLink = "onestore://common/product/0000774867";
        private const string _appStoreLink = "itms-apps://itunes.apple.com/app/��ID";


        private int _maxRepeatCount;


        public VersionManagement()
        {
            _maxRepeatCount = 10;
        }


        /// <summary>���� ������ Ŭ���̾�Ʈ ������ �´��� Ȯ�� �ϴ� �Լ� </summary>
        public bool UpdateCheck()
        {
            #if UNITY_EDITOR

            return true;

            #endif

            Version client = new Version(Application.version);
            Debug.Log("client Version: " + client);
            BackendReturnObject bro = BackendManager.Instance.ProcessBackendAPISync("���� ��ȸ", Backend.Utils.GetLatestVersion);
            if(bro == null || !bro.IsSuccess())
            {
                string errorName = "���� ��ȸ ����";
                string errorDescription = "������ �������� ���߽��ϴ�. \n�ٽ� ���� ���ּ���.";
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
            //���̳� ������Ʈ�� ������Ʈ �ʿ� ����
            if(forceUpdate == "1")
            {
                return true;
            }
            //������ ������Ʈ�� ������Ʈ �ʼ�
            else if(forceUpdate == "2")
            {
                OpenStoreLink();
                return false;
            }

            Debug.LogError("���� ��ȸ�� �ƹ� ���ǿ��� �ش����� �ʽ��ϴ�.");
            return false;

            // //2����
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
            //     string errorName = "���� ��ȸ ����";
            //     string errorDescription = "Ŭ���̾�Ʈ ���� ������ �����ϴ�. \n�ٽ� ���� ���ּ���.";
            //     BackendManager.Instance.ShowPopup(errorName, errorDescription, () => Application.Quit());
            //     return false;
            // }

            // OpenStoreLink();
            // return false;
        }



        private void OpenStoreLink()
        {
            string errorName = "������Ʈ �ʿ�";
            string errorDescription = "�ֽ� ������ �����մϴ�. \n������Ʈ�� ������ �ּ���.";
            BackendManager.Instance.ShowPopup(errorName, errorDescription);
            BackendManager.Instance.SetPopupButton1("������Ʈ", () =>
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