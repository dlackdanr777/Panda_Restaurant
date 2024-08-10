using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [SerializeField] private GameObject[] _hideObjs;

    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _staffNameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private TextMeshProUGUI _skillDescription;
    [SerializeField] private TextMeshProUGUI _skillCooltimeDescription;
    [SerializeField] private UIStaffLackDescription _lackDescription;
    [SerializeField] private UIStaffOwnDescription _ownDescription;

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
        UserInfo.OnChangeScoreHandler += UpdateStaff;
        UserInfo.OnChangeStaffHandler += UpdateStaff;
        UserInfo.OnGiveStaffHandler += UpdateStaff;
    }


    public void SetStaffData(StaffData data)
    {
        _currentStaffData = data;

        if (data == null)
        {
            for (int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
            {
                _hideObjs[i].SetActive(false);
            }
            return;
        }
        for (int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
        {
            _hideObjs[i].SetActive(true);
        }

        _staffImage.sprite = data.ThumbnailSprite;
        _staffNameText.text = data.Name;
        _levelText.text = UserInfo.IsGiveStaff(data) ? "lv." + UserInfo.GetStaffLevel(data) : "lv.1";
        _effectDescription.text = data.Description;
        _skillDescription.text = data.Skill != null ? data.Skill.Description + "(" + data.Skill.Duration + "초)" : "없음";
        _skillCooltimeDescription.text = data.Skill != null ? data.Skill.Cooldown + "초" : "없음";
        

        if (UserInfo.IsGiveStaff(data))
        {
            _ownDescription.gameObject.SetActive(true);
            _lackDescription.gameObject.SetActive(false);
            _ownDescription.SetStaffData(data, _onEquipButtonClicked, _onUpgradeButtonClicked);
            _staffImage.color = Color.white;
        }

        else
        {
            _lackDescription.gameObject.SetActive(true);
            _ownDescription.gameObject.SetActive(false);
            _lackDescription.SetStaffData(data, _onBuyButtonClicked);
            _staffImage.color = Color.black;
        } 
    }

    private void UpdateStaff()
    {
        SetStaffData(_currentStaffData);
    }
}
