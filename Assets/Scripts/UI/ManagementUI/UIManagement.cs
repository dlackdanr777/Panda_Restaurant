using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManagement : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Space]
    [Header("Set Effect Components")]
    [SerializeField] private UIManagementSetEffect _furnitureSetEffect;


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
        _furnitureSetEffect.SetData(UserInfo.GetEquipFurnitureSetData());
        OnChangeTipPerMinuteEvent();
        OnAddCustomerEvent();
        OnChangeMoneyEvent();

        UserInfo.OnChangeFurnitureHandler += (type) => _furnitureSetEffect.SetData(UserInfo.GetEquipFurnitureSetData());
        GameManager.Instance.OnChangeTipPerMinuteHandler += OnChangeTipPerMinuteEvent;
        UserInfo.OnAddCustomerCountHandler += OnAddCustomerEvent;
        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;

        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

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
            gameObject.SetActive(false);
        });
    }


    private void OnChangeTipPerMinuteEvent()
    {
        _tipPerMinuteValue.text = Utility.StringAddHyphen(Utility.ConvertToNumber(GameManager.Instance.TipPerMinute), 9);
    }


    private void OnAddCustomerEvent()
    {
        _totalCustomerValue.text = Utility.StringAddHyphen(Utility.ConvertToNumber(UserInfo.TotalCumulativeCustomerCount), 9);
    }


    private void OnChangeMoneyEvent()
    {
        _totalMoneyValue.text = Utility.StringAddHyphen(Utility.ConvertToNumber(UserInfo.TotalAddMoney), 9);
    }


}
