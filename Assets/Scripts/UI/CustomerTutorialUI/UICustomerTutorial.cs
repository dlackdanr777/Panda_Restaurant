using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;

public class UICustomerTutorial : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UICustomerTutorialTab _gateCrasher1Tab;
    [SerializeField] private UICustomerTutorialTab _gateCrasher2Tab;
    [SerializeField] private UICustomerTutorialTab _specialCustomer1Tab;
    [SerializeField] private UICustomerTutorialTab _specialCustomer2Tab;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    public override void Init()
    {
        _gateCrasher1Tab.Init();
        _gateCrasher2Tab.Init();
        _specialCustomer1Tab.Init();
        _specialCustomer2Tab.Init();
        gameObject.SetActive(false);
    }



    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        Tween.Wait(2,() =>
        {
            gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = false;
            _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
            tween.OnComplete(() =>
            {
                VisibleState = VisibleState.Appeared;
                _canvasGroup.blocksRaycasts = true;
            });
        });
    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        _gateCrasher1Tab.StopTab();
        _gateCrasher2Tab.StopTab();
        _specialCustomer1Tab.StopTab();
        _specialCustomer2Tab.StopTab();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            PopEnabled = false;
            gameObject.SetActive(false);
        });
    }

    public void ShowTutorial(CustomerData data)
    {
        if(data is GatecrasherCustomerData)
        {
            if(!UserInfo.IsGatecrasher1TutorialClear && data is GatecrasherCustomer1Data)
            {
                UserInfo.IsGatecrasher1TutorialClear = true;
                ShowGatecrasher1();
                return;
            }

            if(!UserInfo.IsGatecrasher2TutorialClear && data is GatecrasherCustomer2Data)
            {
                UserInfo.IsGatecrasher2TutorialClear = true;
                ShowGatecrasher2();
                return;
            }

            return;
        }

        else if (data is SpecialCustomerData)
        {
            if(!UserInfo.IsSpecialCustomer1TutorialClear && data.Id == "CUSTOMER78")
            {
                UserInfo.IsSpecialCustomer1TutorialClear = true;
                ShowSpecialCustomer1();
                return;
            }

            if (!UserInfo.IsSpecialCustomer2TutorialClear && data.Id == "CUSTOMER79")
            {
                UserInfo.IsSpecialCustomer2TutorialClear = true;
                ShowSpecialCustomer2();
                return;
            }

            return;
        }

        DebugLog.Log("일반 고객입니다: " + data.Id);
    }


    private void ShowGatecrasher1()
    {
        _uiNav.Push("UICustomerTutorial");
        _gateCrasher1Tab.gameObject.SetActive(true);
        _gateCrasher2Tab.gameObject.SetActive(false);
        _specialCustomer1Tab.gameObject.SetActive(false);
        _specialCustomer2Tab.gameObject.SetActive(false);
        _gateCrasher1Tab.StartTab();
    }

    private void ShowGatecrasher2()
    {
        _uiNav.Push("UICustomerTutorial");
        _gateCrasher1Tab.gameObject.SetActive(false);
        _gateCrasher2Tab.gameObject.SetActive(true);
        _specialCustomer1Tab.gameObject.SetActive(false);
        _specialCustomer2Tab.gameObject.SetActive(false);
        _gateCrasher2Tab.StartTab();
    }

    private void ShowSpecialCustomer1()
    {
        _uiNav.Push("UICustomerTutorial");
        _gateCrasher1Tab.gameObject.SetActive(false);
        _gateCrasher2Tab.gameObject.SetActive(false);
        _specialCustomer1Tab.gameObject.SetActive(true);
        _specialCustomer2Tab.gameObject.SetActive(false);
        _specialCustomer1Tab.StartTab();
    }

    private void ShowSpecialCustomer2()
    {
        _uiNav.Push("UICustomerTutorial");
        _gateCrasher1Tab.gameObject.SetActive(false);
        _gateCrasher2Tab.gameObject.SetActive(false);
        _specialCustomer1Tab.gameObject.SetActive(false);
        _specialCustomer2Tab.gameObject.SetActive(true);
        _specialCustomer2Tab.StartTab();
    }
}
