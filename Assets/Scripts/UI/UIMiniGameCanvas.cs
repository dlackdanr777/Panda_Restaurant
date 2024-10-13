using Muks.MobileUI;
using Muks.DataBind;
using UnityEngine;

[RequireComponent(typeof(MobileUINavigation))]
public class UIMiniGameCanvas : MonoBehaviour
{
    private MobileUINavigation _uiNav;

    private void Awake()
    {
        _uiNav = GetComponent<MobileUINavigation>();
    }

    void Start()
    {
        DataBind.SetUnityActionValue("ShowMiniGameUI", OnShowMiniGameUI);
        DataBind.SetUnityActionValue("HideMiniGameUI", OnHideMiniGameUI);
    }

    private void OnShowMiniGameUI()
    {
        _uiNav.Push("UIMiniGame");
    }

    private void OnHideMiniGameUI()
    {
        _uiNav.Pop("UIMiniGame");
    }
}
