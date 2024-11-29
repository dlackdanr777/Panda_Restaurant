using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UIStaffSelectSlot _selectGroup;
    [SerializeField] private UIImageAndText _levelGroup;
    [SerializeField] private UIImageAndText _scoreGroup;
    [SerializeField] private UIImageAndText _addTipPercentGroup;
    [SerializeField] private UIImageAndImage _scoreSignGroup;
    [SerializeField] private UIImageAndImage _effectSignGroup;
    [SerializeField] private UIStaffSkillEffect _skillEffectGroup;
    [SerializeField] private GameObject _effetGroup;

    [Space]
    [Header("Buttons")]
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Sprite _questionMarkSprite;

    private Action<StaffData> _onBuyButtonClicked;
    private Action<StaffData> _onEquipButtonClicked;
    private StaffData _currentData;

    public void Init(Action<StaffData> onEquipButtonClicked, Action<StaffData> onBuyButtonClicked, Action<StaffData> onUpgradeButtonClicked)
    {
        _selectGroup.Init();
        _skillEffectGroup.Init();
        _selectGroup.OnButtonClicked(onUpgradeButtonClicked);
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        _buyButton.AddListener(OnBuyEvent);
        _notEnoughMoneyButton.AddListener(OnBuyEvent);
        _equipButton.AddListener(OnEquipEvent);
    }


    public void SetData(StaffData data)
    {
        _currentData = data;
        _selectGroup.SetData(data);
        _levelGroup.gameObject.SetActive(false);
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);

        if (data == null)
        {
            _scoreGroup.gameObject.SetActive(false);
            _skillEffectGroup.gameObject.SetActive(false);
            _effetGroup.gameObject.SetActive(false);
            _addTipPercentGroup.gameObject.SetActive(false);
            _selectGroup.ImageColor = new Color(1, 1, 1, 0);
            _selectGroup.SetText(string.Empty);
            return;
        }
        else
        {
            _scoreGroup.gameObject.SetActive(true);
            _skillEffectGroup.gameObject.SetActive(true);
            _effetGroup.gameObject.SetActive(true);
            _addTipPercentGroup.gameObject.SetActive(true);
            _selectGroup.ImageColor = Color.white;
        }
        int level = UserInfo.IsGiveStaff(data) ? UserInfo.GetStaffLevel(data) : 1;

        _selectGroup.SetSprite(data.ThumbnailSprite);
        _selectGroup.SetText(data.Name);
        _scoreGroup.SetText("<color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.GetAddScore(level).ToString() + "</color> 점 증가");
        _addTipPercentGroup.SetText("메뉴별 팁 <color=" + Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) + ">" + data.GetAddTipMul(level) + "%</color> 증가");
        _skillEffectGroup.SetData(data);

        StaffData equipData = UserInfo.GetEquipStaff(StaffDataManager.Instance.GetStaffType(data));
        int equipDataLevel = equipData == null ? 1 : UserInfo.IsGiveStaff(equipData) ? UserInfo.GetStaffLevel(equipData) : 1;
        if (equipData == null)
        {
            _scoreSignGroup.Image1SetActive(false);
            _scoreSignGroup.Image2SetActive(false);
            _effectSignGroup.Image1SetActive(false);
            _effectSignGroup.Image2SetActive(false);
        }
        else
        {
            if (equipData.GetAddScore(equipDataLevel) < data.GetAddScore(level))
            {
                _scoreSignGroup.Image1SetActive(false);
                _scoreSignGroup.Image2SetActive(true);
            }
            else if (data.GetAddScore(level) < equipData.GetAddScore(equipDataLevel))
            {
                _scoreSignGroup.Image1SetActive(true);
                _scoreSignGroup.Image2SetActive(false);
            }
            else
            {
                _scoreSignGroup.Image1SetActive(false);
                _scoreSignGroup.Image2SetActive(false);
            }

            if (equipData.GetAddTipMul(equipDataLevel) < data.GetAddTipMul(level))
            {
                _effectSignGroup.Image1SetActive(false);
                _effectSignGroup.Image2SetActive(true);
            }
            else if (data.GetAddTipMul(level) < equipData.GetAddTipMul(equipDataLevel))
            {
                _effectSignGroup.Image1SetActive(true);
                _effectSignGroup.Image2SetActive(false);
            }
            else
            {
                _effectSignGroup.Image1SetActive(false);
                _effectSignGroup.Image2SetActive(false);
            }
        }


        if (UserInfo.IsEquipStaff(data))
        {
            _levelGroup.gameObject.SetActive(true);
            _usingButton.gameObject.SetActive(true);
            _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
            _levelGroup.SetText(data.UpgradeEnable(level) ? "Lv." + level : "Lv.Max");
        }
        else
        {
            if (UserInfo.IsGiveStaff(data))
            {
                _levelGroup.gameObject.SetActive(true);
                _equipButton.gameObject.SetActive(true);
                _selectGroup.ImageColor = Utility.GetColor(ColorType.Give);
                _levelGroup.SetText(data.UpgradeEnable(level) ? "Lv." + level : "Lv.Max");
            }
            else
            {
                if (!UserInfo.IsScoreValid(data))
                {
                    _selectGroup.ImageColor = Utility.GetColor(ColorType.None);
                    _scoreButton.gameObject.SetActive(true);
                    _scoreButton.SetText(data.BuyScore.ToString());
                    _selectGroup.SetSprite(_questionMarkSprite);
                    _scoreGroup.SetText("???");
                    _addTipPercentGroup.SetText("???");
                    _skillEffectGroup.SetData(null);
                    _effectSignGroup.Image1SetActive(false);
                    _effectSignGroup.Image2SetActive(false);
                    _scoreSignGroup.Image1SetActive(false);
                    _scoreSignGroup.Image2SetActive(false);
                    return;
                }

                _selectGroup.ImageColor = Utility.GetColor(ColorType.NoGive);
                if (!UserInfo.IsMoneyValid(data))
                {
                    _notEnoughMoneyButton.gameObject.SetActive(true);
                    _notEnoughMoneyButton.SetText(data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
                    return;
                }

                _buyButton.gameObject.SetActive(true);
                _buyButton.SetText(data.BuyPrice <= 0 ? "무료" : Utility.ConvertToMoney(data.BuyPrice));
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
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        _onBuyButtonClicked?.Invoke(_currentData);
    }

    private void OnEquipEvent()
    {
        if (_currentData == null)
        {
            DebugLog.Log("현재 데이터가 존재하지 않습니다.");
            return;
        }

        _onEquipButtonClicked?.Invoke(_currentData);
    }
}
