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

        PopupManager.Instance.ShowDisplayText("원스토어 리워드를 참가하시려면\n가구 '평범한 세트' 를 완성해 주세요!");
    }
}
