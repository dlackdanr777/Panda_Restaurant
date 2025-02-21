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
        OnGiveFurnitureEvent();
        UserInfo.OnGiveFurnitureHandler += OnGiveFurnitureEvent;
    }


    private void OnEnable()
    {
        OnGiveFurnitureEvent();
    }


    private void OnGiveFurnitureEvent()
    {
        bool isActive = UserInfo.IsActivatedFurnitureEffectSet(EStage.Stage1, "SET01");
        _alramImage.gameObject.SetActive(isActive);
    }


    private void OnButtonClicked()
    {
        DebugLog.Log(UserInfo.GetEffectSetFurnitureCount(EStage.Stage1, "SET01"));
        if(UserInfo.IsActivatedFurnitureEffectSet(EStage.Stage1, "SET01"))
        {
            Application.OpenURL("https://naver.me/5XJ7mKyj");
            return;
        }

        PopupManager.Instance.ShowDisplayText("������� �����带 �����Ͻ÷���\n���� '����� ��Ʈ' �� �ϼ��� �ּ���!");
    }
}
