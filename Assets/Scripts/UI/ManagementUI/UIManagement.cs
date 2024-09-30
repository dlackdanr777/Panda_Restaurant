using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManagement : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Camera _kitchenCamera;
    [SerializeField] private Camera _restaurantCamera;

    [Space]
    [Header("Set Effect Components")]
    [SerializeField] private UIManagementSetEffect _setEffectGroup;


    [Space]
    [Header("Management Components")]
    [SerializeField] private TextMeshProUGUI _tipPerMinuteValue;
    [SerializeField] private TextMeshProUGUI _totalCustomerValue;
    [SerializeField] private TextMeshProUGUI _totalMoneyValue;

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
        _setEffectGroup.Init();


        OnChangeTipPerMinuteEvent();
        OnAddCustomerEvent();
        OnChangeMoneyEvent();

        GameManager.Instance.OnChangeTipPerMinuteHandler += OnChangeTipPerMinuteEvent;
        UserInfo.OnAddCustomerCountHandler += OnAddCustomerEvent;
        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;

        _kitchenCamera.gameObject.SetActive(false);
        _restaurantCamera.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.gameObject.SetActive(true);
        _kitchenCamera.gameObject.SetActive(true);
        _restaurantCamera.gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        OnChangeTipPerMinuteEvent();
        OnChangeMoneyEvent();
        OnAddCustomerEvent();
        OnChangeMoneyEvent();

        _setEffectGroup.UpdateUI();

        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _canvasGroup.blocksRaycasts = false;
        _animeUI.SetActive(true);
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            _kitchenCamera.gameObject.SetActive(false);
            _restaurantCamera.gameObject.SetActive(false);
            gameObject.SetActive(false);
        });
    }

    private void OnChangeTipPerMinuteEvent()
    {
        if (!gameObject.activeSelf)
            return;

        _tipPerMinuteValue.text = Utility.StringAddHyphen(Utility.ConvertToMoney(GameManager.Instance.TipPerMinute), 9);
    }


    private void OnAddCustomerEvent()
    {
        if (!gameObject.activeSelf)
            return;

        _totalCustomerValue.text = Utility.StringAddHyphen(Utility.ConvertToMoney(UserInfo.TotalCumulativeCustomerCount), 9);
    }


    private void OnChangeMoneyEvent()
    {
        if (!gameObject.activeSelf)
            return;

        _totalMoneyValue.text = Utility.StringAddHyphen(Utility.ConvertToMoney(UserInfo.TotalAddMoney), 9);
    }


}
