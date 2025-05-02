using System;
using TMPro;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.UI;

public class UIRecipePreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIMiniGameController _uiMiniGame;
    [SerializeField] private UITimeSkip _uiTimeSkip;
    [SerializeField] private UIRecipeSelectSlot _selectGroup;
    [SerializeField] private UIImageAndText _levelGroup;
    [SerializeField] private UIImageAndText _priceGroup;
    [SerializeField] private UIImageAndText _cookSpeedGroup;
    [SerializeField] private UIImageAndText _totalSellGroup;
    [SerializeField] private UITextAndText _descriptionGroup;
    [SerializeField] private GameObject _priceTextGroup;
    [SerializeField] private GameObject _cookSpeedTextGroup;
    [SerializeField] private UIFoodType _uiFoodType;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;
    [SerializeField] private UIButtonImageText _minigameButton;
    [SerializeField] private Button _needItemButton;
    [SerializeField] private UIImageAndText _needItemImage;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Image _notEnoughImage;
    [SerializeField] private Image _buyImage;
    [SerializeField] private Sprite _notEnoughMoneySprite;
    [SerializeField] private Sprite _notEnoughDiaSprite;
    [SerializeField] private Sprite _buyMoneySprite;
    [SerializeField] private Sprite _buyDiaSprite;
    [SerializeField] private Sprite _questionMarkSprite;

    private Action<FoodData> _onBuyButtonClicked;
    private FoodData _currentData;

    public void Init(Action<FoodData> onBuyButonClicked, Action<FoodData> onUpgradeButtonClicked)
    {
        _selectGroup.Init();
        _onBuyButtonClicked = onBuyButonClicked;
        _minigameButton.AddListener(OnMiniGameButtonClicked);
        _buyButton.AddListener(OnBuyEvent);
        _notEnoughMoneyButton.AddListener(OnBuyEvent);
        _needItemButton.onClick.AddListener(OnNeedItemButtonClicked);
        _selectGroup.OnButtonClicked(onUpgradeButtonClicked);

        UserInfo.OnUpgradeRecipeHandler += UpdateUI;
        UserInfo.OnGiveRecipeHandler += UpdateUI;
        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        UserInfo.OnAddCookCountHandler += UpdateUI;
        GameManager.Instance.OnChangeScoreHandler += UpdateUI;
        TimeManager.Instance.OnUpdateTimeHandler += TimeManagerUpdateEvent;
        TimeManager.Instance.OnAddTimeHandler += TimeManagerAddRemoveEvent;
        TimeManager.Instance.OnRemoveTimeHandler += TimeManagerAddRemoveEvent;
    }


    public void SetData(FoodData data)
    {
        _currentData = data;
        _selectGroup.SetData(data);
        _buyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _minigameButton.gameObject.SetActive(false);
        _needItemImage.gameObject.SetActive(false);
        _levelGroup.gameObject.SetActive(false);
        _totalSellGroup.gameObject.SetActive(false);

        if (data == null)
        {
            _priceGroup.gameObject.SetActive(false);
            _cookSpeedGroup.gameObject.SetActive(false);
            _descriptionGroup.gameObject.SetActive(false);
            _priceTextGroup.gameObject.SetActive(false);
            _cookSpeedTextGroup.gameObject.SetActive(false);
            _uiFoodType.gameObject.SetActive(false);
            _descriptionGroup.SetText1(string.Empty);
            _descriptionGroup.SetText2(string.Empty);
            _selectGroup.ImageColor = new Color(1, 1, 1, 0);
            _selectGroup.SetText(string.Empty);
            return;
        }
        else
        {
            _priceGroup.gameObject.SetActive(true);
            _cookSpeedGroup.gameObject.SetActive(true);
            _descriptionGroup.gameObject.SetActive(true);
            _priceTextGroup.gameObject.SetActive(true);
            _cookSpeedTextGroup.gameObject.SetActive(true);
            _uiFoodType.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }
        _uiFoodType.SetFoodType(data.FoodType);
        int level = UserInfo.IsGiveRecipe(data) ? UserInfo.GetRecipeLevel(data) : 1;

        _selectGroup.SetSprite(data.ThumbnailSprite);
        _selectGroup.SetText(data.Name);
        _priceGroup.SetText(Utility.ConvertToMoney(data.GetSellPrice(level)));
        _cookSpeedGroup.SetText(data.GetCookingTime(level) + "s");
        _descriptionGroup.SetText1("����");
        _descriptionGroup.SetText2(data.Description);

        if (UserInfo.IsGiveRecipe(data))
        {
            _levelGroup.gameObject.SetActive(true);
            _totalSellGroup.gameObject.SetActive(true);
            _totalSellGroup.SetText(UserInfo.GetCookCount(data).ToString());
            _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
            _levelGroup.SetText(data.UpgradeEnable(level) ? "Lv." + level : "Lv.Max");
        }
        else
        {
            if (!UserInfo.IsScoreValid(data))
            {
                _selectGroup.ImageColor = Utility.GetColor(ColorType.None);
                _selectGroup.SetSprite(_questionMarkSprite);
                _scoreButton.gameObject.SetActive(true);
                _scoreButton.SetText(data.BuyScore.ToString());
                _priceGroup.SetText("???");
                _cookSpeedGroup.SetText("???");
                return;
            }

            _selectGroup.ImageColor = Utility.GetColor(ColorType.NoGive);
            if (!string.IsNullOrWhiteSpace(data.NeedItem))
            {
                GachaItemData gachaItemData = ItemManager.Instance.GetGachaItemData(data.NeedItem);
                if (gachaItemData == null)
                    throw new Exception("�ʿ� �������� �����ϳ� �����ͺ��̽� �� �������� �ʽ��ϴ�: " + data.NeedItem);

                if (!UserInfo.IsGiveGachaItem(gachaItemData))
                {
                    _needItemImage.gameObject.SetActive(true);
                    _needItemImage.SetSprite(gachaItemData.Sprite);
                    _needItemImage.SetText(gachaItemData.Name);
                    return;
                }
                int waitTime = TimeManager.Instance.GetTime(data.Id + "_MiniGame");
                _minigameButton.gameObject.SetActive(true);
                _minigameButton.SetImage(gachaItemData.Sprite);
                if (0 < waitTime)
                {
                    _minigameButton.SetText(Utility.SecondsToTimeString(waitTime));
                    return;
                }
                else
                {
                    _minigameButton.SetText("���� ����");
                }

                return;
            }

            else
            {
                _selectGroup.ImageColor = Utility.GetColor(ColorType.NoGive);
                MoneyType moneyType = data.MoneyType;
                int price = data.BuyPrice;

                if (moneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(price))
                {
                    _notEnoughMoneyButton.gameObject.SetActive(true);
                    _notEnoughImage.sprite = _notEnoughMoneySprite;
                    _notEnoughMoneyButton.SetText(data.BuyPrice <= 0 ? "����" : Utility.ConvertToMoney(data.BuyPrice));
                    return;
                }

                else if (moneyType == MoneyType.Dia && !UserInfo.IsDiaValid(price))
                {
                    _notEnoughMoneyButton.gameObject.SetActive(true);
                    _notEnoughImage.sprite = _notEnoughDiaSprite;
                    _notEnoughMoneyButton.SetText(data.BuyPrice <= 0 ? "����" : Utility.ConvertToMoney(data.BuyPrice));
                    return;
                }


                _buyButton.gameObject.SetActive(true);
                _buyButton.SetText(data.BuyPrice <= 0 ? "����" : Utility.ConvertToMoney(data.BuyPrice));
                _buyImage.sprite = moneyType == MoneyType.Gold ? _buyMoneySprite : _buyDiaSprite;
            }
        }
    }




    public void UpdateUI()
    {
        SetData(_currentData);
    }


    private void OnBuyEvent()
    {
        if (_currentData == null)
        {
            DebugLog.LogError("���� ���� �����Ͱ� �����ϴ�.");
            return;
        }

        _onBuyButtonClicked?.Invoke(_currentData);
    }


    private void OnMiniGameButtonClicked()
    {
        DebugLog.Log("�̴ϰ��� ��ư Ŭ��");
        if(_currentData == null)
        {
            DebugLog.LogError("���� ���� �����Ͱ� �����ϴ�.");
            return;
        }

        int waitTime = TimeManager.Instance.GetTime(_currentData.Id + "_MiniGame");
        if (0 < waitTime)
        {
            _uiTimeSkip.ShowTimeSkipUI(_currentData.Id + "_MiniGame");
        }
        else
        {
            _uiMiniGame.ShowMiniGame1(_currentData);
        }
    }


    private void OnNeedItemButtonClicked()
    {
        if(_currentData == null)
        {
            DebugLog.LogError("���� ���� �����Ͱ� �����ϴ�.");
            return;
        }

        GachaItemData itemData = ItemManager.Instance.GetGachaItemData(_currentData.NeedItem);
        if(itemData == null)
        {
            DebugLog.LogError("�� ������ �䱸 �������� �����ϴ�.");
            return;
        }

        PopupManager.Instance.ShowDisplayText("ĸ�� �̱⸦ ����\n" + Utility.SetStringColor(itemData.Name, ColorType.Positive) + " �������� �ʿ��մϴ�.");
    }

    private void TimeManagerUpdateEvent()
    {

        if (!_minigameButton.gameObject.activeInHierarchy)
        {
            DebugLog.LogError("�̴ϰ��� ��ư�� Ȱ��ȭ �Ǿ� ���� �ʽ��ϴ�.");
            return;
        }

        if (_currentData == null)
        {
            DebugLog.LogError("���� ���� �����Ͱ� �����ϴ�.");
            return;
        }

        if(string.IsNullOrWhiteSpace(_currentData.NeedItem))
        {
            DebugLog.LogError("�� ������ �䱸 �������� �����ϴ�.");
            return;
        }

        int waitTime = TimeManager.Instance.GetTime(_currentData.Id + "_MiniGame");
        if (0 < waitTime)
        {
            _minigameButton.SetText(Utility.SecondsToTimeString(waitTime));
        }
        else
        {
            _minigameButton.SetText("���� ����");
        }
    }

    private void TimeManagerAddRemoveEvent(string id)
    {
        if (!_minigameButton.gameObject.activeInHierarchy)
        {
            DebugLog.LogError("�̴ϰ��� ��ư�� Ȱ��ȭ �Ǿ� ���� �ʽ��ϴ�.");
            return;
        }

        if (_currentData == null)
        {
            DebugLog.LogError("���� ���� �����Ͱ� �����ϴ�.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentData.NeedItem))
        {
            DebugLog.LogError("�� ������ �䱸 �������� �����ϴ�.");
            return;
        }

        if(!id.Equals(_currentData.Id + "_MiniGame"))
        {
            DebugLog.LogError("�̴ϰ��� ���̵� �ٸ��ϴ�.");
            return;
        }

        int waitTime = TimeManager.Instance.GetTime(_currentData.Id + "_MiniGame");
        if (0 < waitTime)
        {
            _minigameButton.SetText(Utility.SecondsToTimeString(waitTime));
        }
        else
        {
            _minigameButton.SetText("���� ����");
        }
    }

    private void OnDestroy()
    {
        UserInfo.OnUpgradeRecipeHandler -= UpdateUI;
        UserInfo.OnGiveRecipeHandler -= UpdateUI;
        UserInfo.OnChangeMoneyHandler -= UpdateUI;
        UserInfo.OnChangeScoreHandler -= UpdateUI;
        UserInfo.OnAddCookCountHandler -= UpdateUI;
        GameManager.Instance.OnChangeScoreHandler -= UpdateUI;
        TimeManager.Instance.OnUpdateTimeHandler -= TimeManagerUpdateEvent;
        TimeManager.Instance.OnAddTimeHandler -= TimeManagerAddRemoveEvent;
        TimeManager.Instance.OnRemoveTimeHandler -= TimeManagerAddRemoveEvent;
    }
}
