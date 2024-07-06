using System;
using TMPro;
using UnityEngine;

public class UIStaffOwnDescription : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _upgradeMinScoreDescription;
    [SerializeField] private TextMeshProUGUI _addScoreDescription;
    [SerializeField] private TextMeshProUGUI _upgradeAddScoreDescription;
    [SerializeField] private TextMeshProUGUI _addTipDescription;
    [SerializeField] private TextMeshProUGUI _upgradeAddTipDescription;
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _useButton;


    public void SetStaffData(StaffData data, Action<StaffData> onEquipButtonCliekd, Action<StaffData> onUpgradeButtonClicked)
    {        
        int level = UserInfo.GetStaffLevel(data);
        _addScoreDescription.text = data.GetAddScore(level).ToString();
        _addTipDescription.text = data.GetAddTipMul(level) + "%";

        if (data.UpgradeEnable(level))
        {
            _upgradeAddScoreDescription.gameObject.SetActive(true);
            _upgradeAddTipDescription.gameObject.SetActive(true);
            _upgradeButton.RemoveAllListeners();
            _upgradeButton.AddListener(() => onUpgradeButtonClicked?.Invoke(data));
            _upgradeButton.Interactable(true);
            _upgradeButton.SetText(Utility.ConvertToNumber(data.GetUpgradePrice(level)));

            _titleText.text = "강화 lv." + level;
            _upgradeMinScoreDescription.text = Utility.ConvertToNumber(data.GetUpgradeMinScore(level));
            _upgradeAddScoreDescription.text = data.GetAddScore(level + 1).ToString();
            _upgradeAddTipDescription.text = data.GetAddTipMul(level + 1) + "%";
        }

        else
        {
            _upgradeAddScoreDescription.gameObject.SetActive(false);
            _upgradeAddTipDescription.gameObject.SetActive(false);
            _upgradeButton.Interactable(false);

            _titleText.text = "lv." + level;
            _upgradeButton.SetText("최대 강화");
            _upgradeMinScoreDescription.text = "최대 강화";
        }


        if(UserInfo.IsEquipStaff(data))
        {
            _useButton.gameObject.SetActive(true);
            _equipButton.gameObject.SetActive(false);
            _useButton.Interactable(false);
            _equipButton.SetText("사용중");
        }
        else
        {
            _equipButton.gameObject.SetActive(true);
            _useButton.gameObject.SetActive(false);

            _equipButton.RemoveAllListeners();
            _equipButton.AddListener(() => onEquipButtonCliekd(data));
            _equipButton.SetText("사용");
        }
    }
}
