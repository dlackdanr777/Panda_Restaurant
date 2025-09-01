using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManagementLayoutStaffSlot : MonoBehaviour
{
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _staffTypeText;

    private StaffData _staffData;

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
}
