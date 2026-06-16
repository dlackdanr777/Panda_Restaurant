using System;
using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFloorLock : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private GameObject _buttonGroup;
    [SerializeField] private Button _okButton;
    [SerializeField] private TextMeshProUGUI _moneyText;
    [SerializeField] private GameObject _moneyIcon;
    [SerializeField] private GameObject _diaIcon;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;

    private ERestaurantFloorType _floorType;
    private Action _onUnlock;   
    public override void Init()
    {
        _okButton.onClick.AddListener(OnOkButtonClicked);
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();

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
        _animeUI.SetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }

    public void SetData(int score, MoneyType moneyType, int moneyAmount, ERestaurantFloorType floorType, Action onUnlock = null)
    {
        _uiNav.Push("UIFloorLock");
        _floorType = floorType;
        _moneyIcon.SetActive(moneyType == MoneyType.Gold);
        _diaIcon.SetActive(moneyType == MoneyType.Dia);
        _onUnlock = onUnlock;
        
        if (!UserInfo.IsScoreValid(score))
        {
            _titleText.SetText("평점이 부족합니다,,,");
            _buttonGroup.gameObject.SetActive(false);
            return;
        }

        if (!UserInfo.IsDiaValid(moneyAmount) && moneyType == MoneyType.Dia)
        {
            _titleText.SetText("다이아가 부족합니다,,,");
            _buttonGroup.gameObject.SetActive(false);
            return;
        }

        if (!UserInfo.IsMoneyValid(moneyAmount) && moneyType == MoneyType.Gold)
        {
            _titleText.SetText("골드가 부족합니다,,,");
            _buttonGroup.gameObject.SetActive(false);
            return;
        }
        _buttonGroup.gameObject.SetActive(true);
        _titleText.SetText("잠금을 해제하시겠습니까?");
        _moneyText.SetText(Utility.ConvertToMoney(moneyAmount));
    }


    private void OnOkButtonClicked()
    {
        UserInfo.ChangeUnlockFloor(UserInfo.CurrentStage, _floorType);
        _uiNav.Pop();
        _onUnlock?.Invoke();
    }
}
