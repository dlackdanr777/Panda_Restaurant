using UnityEngine;
using UnityEngine.UI;

public class UIBetaTestSurvey : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _surveyButton;
    [SerializeField] private Image _alramImage;

    private void Awake()
    {
        _surveyButton.onClick.AddListener(OnButtonClicked);
        OnVisitiedCustomerEvent();
        UserInfo.OnAddCustomerCountHandler += OnVisitiedCustomerEvent;
    }


    private void OnEnable()
    {
        OnVisitiedCustomerEvent();
    }


   private void OnVisitiedCustomerEvent()
    {
        bool isActive = UserInfo.TotalCumulativeCustomerCount >= 200;
        _alramImage.gameObject.SetActive(isActive);
    }


    private void OnButtonClicked()
    {
        DebugLog.Log(UserInfo.TotalCumulativeCustomerCount);
        if(UserInfo.TotalCumulativeCustomerCount >= 200)
        {
            Application.OpenURL("https://form.naver.com/response/oXNuUpre4vaCQgEzQ5J0LQ");
            return;
        }

        PopupManager.Instance.ShowDisplayText("원스토어 리워드를 참가하시려면\n방문 손님 200명을 달성하세요.");
    }
}
