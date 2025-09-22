using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManagementLayoutStaffSlot : MonoBehaviour
{
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _staffTypeText;
    [SerializeField] private Button _button;

    private StaffData _staffData;

    private Action<StaffData> _onClick;

    public void Init(Action<StaffData> onClick)
    {
        _onClick = onClick;
        _button.onClick.AddListener(OnButtonClicked);
    }

    public void SetData(StaffData staffData, EquipStaffType type)
    {
        if (staffData == null)
        {
            _staffData = null;
            _staffImage.sprite = null;
            _staffTypeText.text = string.Empty;
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _staffData = staffData;
        _staffImage.sprite = staffData.Sprite;
        _staffTypeText.text = Utility.StaffTypeStringConverter(type);
    }

    private void OnButtonClicked()
    {
        if (_staffData == null)
        {
            DebugLog.LogError("Staff data is null");
            return;
        }
        _onClick?.Invoke(_staffData);
    }
}
