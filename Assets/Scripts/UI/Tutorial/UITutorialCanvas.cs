using Muks.DataBind;
using Muks.MobileUI;
using UnityEngine;

public class UITutorialCanvas : MonoBehaviour
{
    [SerializeField] private UICustomerTutorial _customerTutorial;

    private MobileUINavigation _uiNav;


    void Awake()
    {
        _uiNav = GetComponent<MobileUINavigation>();
        DataBind.SetUnityActionValue("HideCustomerTutorialUI", OnHideCustomerTutorialUI);

        DataBind.SetUnityActionValue("ShowTutorialSkipUI", OnShowTutorialSkipUI);
        DataBind.SetUnityActionValue("HideTutorialSkipUI", OnHideTutorialSkipUI);
    }

    private void OnHideCustomerTutorialUI()
    {
        _customerTutorial.PopEnabled = true;
        _uiNav.Pop("UICustomerTutorial");
    }

    private void OnShowTutorialSkipUI()
    {
        _uiNav.Push("UITutorialSkip");
    }


    private void OnHideTutorialSkipUI()
    {
        _uiNav.Pop("UITutorialSkip");
    }

}
