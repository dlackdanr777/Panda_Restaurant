using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _staffNameText;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private GameObject _descriptionObj;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private TextMeshProUGUI _equipScoreText;
    [SerializeField] private TextMeshProUGUI _equipTipText;
    [SerializeField] private TextMeshProUGUI _skillDescription;
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

        if (data == null)
        {
            _staffImage.gameObject.SetActive(false);
            _staffNameText.gameObject.SetActive(false);
            _descriptionObj.gameObject.SetActive(false);
            _description.gameObject.SetActive(false);
            return;
        }

        _staffImage.gameObject.SetActive(true);
        _staffNameText.gameObject.SetActive(true);
        _description.gameObject.SetActive(true);
        _descriptionObj.gameObject.SetActive(true);
        _staffImage.sprite = data.Sprite;
        _staffNameText.text = data.Name;
        _description.text = data.Description;

        if(data.Skill != null)
        {
            string skillDecription = data.Skill.Description;
            skillDecription = skillDecription.Replace("Duration", data.Skill.Duration == 0 ? "지속" : data.Skill.Duration.ToString("n1"));
            skillDecription = skillDecription.Replace("CoolTime", data.Skill.Cooldown == 0 ? "패시브" : data.Skill.Cooldown.ToString("n1"));

            string firstValue = data.Skill.FirstValue % 1 != 0 ? data.Skill.FirstValue.ToString("n1") : ((int)data.Skill.FirstValue).ToString();
            skillDecription = skillDecription.Replace("FirstValue", firstValue);
            string secondValue = data.Skill.SecondValue % 1 != 0 ? data.Skill.SecondValue.ToString("n1") : ((int)data.Skill.SecondValue).ToString();
            skillDecription = skillDecription.Replace("SecondValue", secondValue);

            _skillDescription.text = skillDecription;
        }

        else
        {
            _skillDescription.text = "없음";
        }


        if (UserInfo.IsGiveStaff(data))
        {
            int level = UserInfo.GetStaffLevel(data);

            _levelText.text = "LV." + level;
            string effectDescription = data.EffectDescription;
            effectDescription = effectDescription.Replace("ActionValue", data.GetActionValue(level).ToString("n1"));
            effectDescription = effectDescription.Replace("SecondValue", data.SecondValue.ToString("n1"));
            _effectDescription.text = effectDescription;
            _equipScoreText.text = data.GetAddScore(level).ToString();
            _equipTipText.text = data.GetAddTipMul(level).ToString() + '%';

            _upgradeButton.gameObject.SetActive(true);
            _upgradeButton.RemoveAllListeners();
            _upgradeButton.AddListener(() => { _onUpgradeButtonClicked(data); });

            if(data.UpgradeEnable(level))
            {
                _upgradeButton.SetText("강화");
                _upgradeButton.Interactable(true);
            }
            else
            {
                _upgradeButton.SetText("최대 강화");
                _upgradeButton.Interactable(false);
            }     
        }

        else
        {
            int level = 1;
            _levelText.text = "LV." + level;
            _equipScoreText.text = data.GetAddScore(level).ToString();
            _equipTipText.text = data.GetAddTipMul(level).ToString() + '%';

            if(string.IsNullOrWhiteSpace(data.EffectDescription))
            {
                _effectDescription.text = string.Empty;
            }
            else
            {
                string effectDescription = data.EffectDescription;
                effectDescription = effectDescription.Replace("ActionValue", data.GetActionValue(level).ToString("n1"));
                effectDescription = effectDescription.Replace("SecondValue", data.SecondValue.ToString("n1"));
                _effectDescription.text = effectDescription;
            }
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
            }
        }
    }

    private void UpdateStaff()
    {
        SetStaffData(_currentStaffData);
    }
}
