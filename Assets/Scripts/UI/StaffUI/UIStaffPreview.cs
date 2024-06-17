using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _staffNameText;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private Button _equipButton;
    [SerializeField] private TextMeshProUGUI _equipButtonText;

    private StaffData _currentStaffData;


    public void SetStaff(StaffData data, Action<StaffData> onEquipButtonClicked)
    {
        _currentStaffData = data;
        if (data == null)
        {
            _staffImage.gameObject.SetActive(false);
            _staffNameText.gameObject.SetActive(false);
            _effectDescription.gameObject.SetActive(false);
            _equipButton.gameObject.SetActive(false);
            return;
        }

        _staffImage.gameObject.SetActive(true);
        _staffNameText.gameObject.SetActive(true);
        _effectDescription.gameObject.SetActive(true);
        _staffImage.sprite = data.Sprite;
        _staffNameText.text = data.Name;
        _effectDescription.text = data.Description;

        if(UserInfo.IsEquipStaff(data))
        {
            _equipButton.gameObject.SetActive(true);
            _equipButton.interactable = false;
            _equipButtonText.text = "사용중";
        }
        else
        {
            if(UserInfo.IsGiveStaff(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.onClick.RemoveAllListeners();
                _equipButton.onClick.AddListener(() => onEquipButtonClicked(_currentStaffData));

                _equipButton.interactable = true;
                _equipButtonText.text = "배치하기";
            }
            else
            {
                _equipButton.gameObject.SetActive(false);
            }
        }
    }
}
