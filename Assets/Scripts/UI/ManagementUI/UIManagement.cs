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
    [SerializeField] private Image _setImage;
    [SerializeField] private TextMeshProUGUI _setEffectName;
    [SerializeField] private TextMeshProUGUI _setEffectDescription;
    [SerializeField] private TextMeshProUGUI _setEffectValue;

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
        OnChangeSetEffectEvent();
        OnChangeTipPerMinuteEvent();
        OnAddCustomerEvent();
        OnChangeMoneyEvent();


        UserInfo.OnChangeFurnitureHandler += (type) => OnChangeSetEffectEvent();
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

    private void OnChangeSetEffectEvent()
    {
        SetData equipFurnitureSetData = UserInfo.GetEquipFurnitureSetData();

        if(equipFurnitureSetData == null)
        {
            _setImage.gameObject.SetActive(false);
            _setEffectName.gameObject.SetActive(false);
            _setEffectDescription.gameObject.SetActive(false);
            _setEffectValue.gameObject.SetActive(false);
            return;
        }

        _setEffectName.gameObject.SetActive(true);
        _setEffectDescription.gameObject.SetActive(true);
        _setEffectValue.gameObject.SetActive(true);

        _setEffectName.text = equipFurnitureSetData.Name;
        
        if(equipFurnitureSetData is TipPerMinuteSetData)
        {
            TipPerMinuteSetData tipSetData = (TipPerMinuteSetData)equipFurnitureSetData;
            _setEffectDescription.text = tipSetData.Description;
            _setEffectValue.text = Utility.StringAddHyphen(Utility.ConvertToNumber(tipSetData.TipPerMinuteValue), 9);
            return;
        }

        if(equipFurnitureSetData is CookingSpeedUpSetData)
        {
            CookingSpeedUpSetData cookSetData = (CookingSpeedUpSetData)equipFurnitureSetData;
            _setEffectDescription.text = cookSetData.Description;
            _setEffectValue.text = Utility.StringAddHyphen(cookSetData.CookingSpeedUpMul.ToString(), 8) + "%";
        }
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
