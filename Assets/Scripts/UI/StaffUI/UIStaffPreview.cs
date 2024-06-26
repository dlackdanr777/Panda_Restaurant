using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _staffNameText;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private GameObject _lockImage;
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;
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

        UserInfo.OnUpgradeStaffHandler += UpdateStaff;
        UserInfo.OnChangeMoneyHandler += UpdateStaff;
        UserInfo.OnChangeScoreHanlder += UpdateStaff;
        UserInfo.OnChangeStaffHandler += UpdateStaff;
        UserInfo.OnGiveStaffHandler += UpdateStaff;
    }


    public void SetStaffData(StaffData data)
    {
        _currentStaffData = data;
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _upgradeButton.gameObject.SetActive(false);
        _lockImage.SetActive(false);

        if (data == null)
        {
            _staffImage.gameObject.SetActive(false);
            _staffNameText.gameObject.SetActive(false);
            _description.gameObject.SetActive(false);
            _effectDescription.gameObject.SetActive(false);
            return;
        }

        _staffImage.gameObject.SetActive(true);
        _staffNameText.gameObject.SetActive(true);
        _description.gameObject.SetActive(true);
        _effectDescription.gameObject.SetActive(true);
        _staffImage.sprite = data.Sprite;
        _staffNameText.text = data.Name;
        _description.text = data.Description;
        _effectDescription.text = data.Skill.Description;

        if (UserInfo.IsGiveStaff(data))
        {
            int level = UserInfo.GetStaffLevel(data);
            _upgradeButton.gameObject.SetActive(true);
            _upgradeButton.RemoveAllListeners();
            _upgradeButton.AddListener(() => { _onUpgradeButtonClicked(data); });
            _upgradeButton.SetText(Utility.ConvertToNumber(data.GetUpgradePrice(level)));
        }

        if(UserInfo.IsEquipStaff(data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("사용중");
            _usingButton.Interactable(false);
            _staffImage.color = new Color(1, 1, 1);
        }
        else
        {
            if(UserInfo.IsGiveStaff(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.SetText("사용하기");
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentStaffData); });
                _staffImage.color = new Color(1, 1, 1);
            }
            else
            {
                _staffImage.color = new Color(0, 0, 0);

                _buyButton.gameObject.SetActive(true);
                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentStaffData); });
                _buyButton.SetText(Utility.ConvertToNumber(data.MoneyData.Price));
                _lockImage.SetActive(true);
            }
        }
    }

    private void UpdateStaff()
    {
        SetStaffData(_currentStaffData);
    }
}
