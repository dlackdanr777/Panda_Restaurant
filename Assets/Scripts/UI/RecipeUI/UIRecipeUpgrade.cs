using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.MobileUI;
using Muks.Tween;

public class UIRecipeUpgrade : MobileUIView
{
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Image _foodImage;
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _upgradeDescription;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private FoodData _currentFoodData;
    private Action<FoodData> _onUpgradeButtonClicked;

    public override void Init()
    {
        UserInfo.OnUpgradeRecipeHandler += UpgradeRecipeEvent;
        gameObject.SetActive(false);
    }


    public void SetAction(Action<FoodData> onUpgradeButtonClicked)
    {
        _onUpgradeButtonClicked = onUpgradeButtonClicked;
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
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
        _animeUI.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            _dontTouchArea.SetActive(false);
            gameObject.SetActive(false);
        });
    }


    public void SetFoodData(FoodData foodData)
    {
        if (foodData == null || !UserInfo.IsGiveRecipe(foodData))
            return;

        _currentFoodData = foodData;

        _foodImage.sprite = foodData.Sprite;
        _nameText.text = foodData.Name;
        _upgradeButton.RemoveAllListeners();

        int level = UserInfo.GetRecipeLevel(foodData);
        _levelText.text = level + "�ܰ�";
        string description = "���� �ð�: " + foodData.GetCookingTime(level) + "��" + "\n���� �ݾ�: " + foodData.GetSellPrice(level) + "���";
        _description.text = description;

        if (foodData.UpgradeEnable(level))
        {
            _upgradeButton.Interactable(true);
            _upgradeButton.AddListener(() => _onUpgradeButtonClicked(_currentFoodData));
            _upgradeButton.SetText("��ȭ");

            string upgradeDescription = "��ȭ ����: " + foodData.GetUpgradeMinScore(level)+ "��" + "\n��ȭ �ݾ�: " + foodData.GetUpgradePrice(level) + "���";
            _upgradeDescription.text = upgradeDescription;
        }
        else
        {
            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("�ִ� ��ȭ");

            _upgradeDescription.text = string.Empty;
            return;
        }

        if (UserInfo.Score < foodData.GetUpgradeMinScore(level))
        {
            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("���� ����");
            return;
        }
        
        if(UserInfo.Money < foodData.GetUpgradePrice(level))
        {
            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("�ݾ� ����");
            return;
        }
    }

    private void UpgradeRecipeEvent()
    {
        SetFoodData(_currentFoodData);
    }
}
