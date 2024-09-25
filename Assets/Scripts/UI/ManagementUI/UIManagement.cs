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
    [SerializeField] private RectTransform _setEffectGroup;
    [SerializeField] private UIManagementSetEffect _furnitureSetEffect;
    [SerializeField] private UIManagementSetEffect _kitchenUntensilsSetEffect;
    [SerializeField] private ButtonPressEffect _leftArrowButton;
    [SerializeField] private ButtonPressEffect _rightArrowButton;


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

    private SetEffectType _currentSetEffectType;

    public override void Init()
    {
        _furnitureSetEffect.Init(SetEffectType.Furniture);
        _kitchenUntensilsSetEffect.Init(SetEffectType.KitchenUntensils);
        _furnitureSetEffect.SetData(UserInfo.GetEquipFurnitureSetData());
        _kitchenUntensilsSetEffect.SetData(UserInfo.GetEquipKitchenUntensilSetData());

        _leftArrowButton.AddListener(() => OnSetEffectArrowButtonClicked(SetEffectType.Furniture));
        _rightArrowButton.AddListener(() => OnSetEffectArrowButtonClicked(SetEffectType.KitchenUntensils));

        OnChangeFurnitureEvent();
        OnChangeKitchenUntensilsEvent();
        OnChangeTipPerMinuteEvent();
        OnAddCustomerEvent();
        OnChangeMoneyEvent();
        OnSetEffectArrowButtonClicked(_currentSetEffectType);

        UserInfo.OnChangeFurnitureHandler += (type) => OnChangeFurnitureEvent();
        UserInfo.OnChangeKitchenUtensilHandler += (type) => OnChangeKitchenUntensilsEvent();
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
        OnSetEffectArrowButtonClicked(_currentSetEffectType);

        OnChangeFurnitureEvent();
        OnChangeKitchenUntensilsEvent();
        OnChangeTipPerMinuteEvent();
        OnChangeMoneyEvent();
        OnAddCustomerEvent();
        OnChangeMoneyEvent();

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

    private void OnSetEffectArrowButtonClicked(SetEffectType type)
    {
        if(type == SetEffectType.Furniture)
        {
            _rightArrowButton.gameObject.SetActive(true);
            _leftArrowButton.gameObject.SetActive(false);
        }
        else
        {
            _leftArrowButton.gameObject.SetActive(true);
            _rightArrowButton.gameObject.SetActive(false);
        }

        if (_currentSetEffectType == type)
            return;

        _currentSetEffectType = type;
        _setEffectGroup.TweenStop();
        if (type == SetEffectType.Furniture)
        {
            _setEffectGroup.TweenAnchoredPosition(new Vector2(0, -5.6f), 0.5f, Ease.Smoothstep);
            return;
        }

        if (type == SetEffectType.KitchenUntensils)
        {
            _setEffectGroup.TweenAnchoredPosition(new Vector2(-610, -5.6f), 0.5f, Ease.Smoothstep);
            return;
        }
    }


    private void OnChangeFurnitureEvent()
    {
        if (!gameObject.activeSelf)
            return;

        _furnitureSetEffect.SetData(UserInfo.GetEquipFurnitureSetData());
    }


    private void OnChangeKitchenUntensilsEvent()
    {
        if (!gameObject.activeSelf)
            return;

        _kitchenUntensilsSetEffect.SetData(UserInfo.GetEquipKitchenUntensilSetData());
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
