using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _staffNameText;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _lowScoreButton;
    [SerializeField] private UIButtonAndText _upgradeButton;

    private Action<StaffData> _onBuyButtonClicked;
    private Action<StaffData> _onEquipButtonClicked;
    private Action<StaffData> _onUpgradeButtonClicked;
    private StaffData _currentStaffData;

    public void Init(Action<StaffData> onEquipButtonClicked, Action<StaffData> onBuyButtonClicked, Action<StaffData> onUpgradeButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;
        _onUpgradeButtonClicked = onUpgradeButtonClicked;

        UserInfo.OnUpgradeStaffHandler += UpgradeStaffEvent;
    }


    public void SetStaffData(StaffData data)
    {
        _currentStaffData = data;
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _lowScoreButton.gameObject.SetActive(false);
        _upgradeButton.gameObject.SetActive(false);

        if (data == null)
        {
            _staffImage.gameObject.SetActive(false);
            _staffNameText.gameObject.SetActive(false);
            _effectDescription.gameObject.SetActive(false);
            return;
        }

        _staffImage.gameObject.SetActive(true);
        _staffNameText.gameObject.SetActive(true);
        _effectDescription.gameObject.SetActive(true);
        _staffImage.sprite = data.Sprite;
        _staffNameText.text = data.Name;
        _effectDescription.text = data.Description;

        if(UserInfo.IsGiveStaff(data))
        {
            _upgradeButton.gameObject.SetActive(true);
            _upgradeButton.RemoveAllListeners();

            int level = UserInfo.GetStaffLevel(data);
            if(data.UpgradeEnable(level))
            {

                _upgradeButton.Interactable(true);
                _upgradeButton.SetText((level + 1)+ "단계 강화");
                _upgradeButton.AddListener(() => { _onUpgradeButtonClicked(data); });
            }
            else
            {
                _upgradeButton.Interactable(false);
                _upgradeButton.SetText("최대 강화");
            }
        }


        if(UserInfo.IsEquipStaff(data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("사용중");
            _staffImage.color = new Color(1, 1, 1);
        }
        else
        {
            if(UserInfo.IsGiveStaff(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.SetText("사용");
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentStaffData); });
                _staffImage.color = new Color(1, 1, 1);
            }
            else
            {
                _staffImage.color = new Color(0, 0, 0);

                if (data.BuyMinScore <= GameManager.Instance.Score)
                {
                    _buyButton.gameObject.SetActive(true);
                    _buyButton.RemoveAllListeners();
                    _buyButton.AddListener(() => { _onBuyButtonClicked(_currentStaffData); }); 
                    _buyButton.SetText(Utility.ConvertToNumber(data.MoneyData.Price));
                }
                else
                {
                    _lowScoreButton.gameObject.SetActive(true);
                    _lowScoreButton.SetText(Utility.ConvertToNumber(data.BuyMinScore));
                }

            }
        }
    }

    private void UpgradeStaffEvent()
    {
        SetStaffData(_currentStaffData);
    }
}
