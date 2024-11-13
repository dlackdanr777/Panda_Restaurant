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
    }

    private void OnHideCustomerTutorialUI()
    {
        _customerTutorial.PopEnabled = true;
        _uiNav.Pop("UICustomerTutorial");
    }

}
