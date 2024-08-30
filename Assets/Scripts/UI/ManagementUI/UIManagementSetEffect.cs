using TMPro;
using UnityEngine;

public class UIManagementSetEffect : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _setName;
    [SerializeField] private TextMeshProUGUI _setDescription;
    [SerializeField] private TextMeshProUGUI _setValue;

    public void SetData(SetData data)
    {
        if (data == null)
        {
            _setName.gameObject.SetActive(false);
            _setDescription.gameObject.SetActive(false);
            _setValue.gameObject.SetActive(false);
            return;
        }

        _setName.gameObject.SetActive(true);
        _setDescription.gameObject.SetActive(true);
        _setValue.gameObject.SetActive(true);

        if (data is TipPerMinuteSetData)
        {
            TipPerMinuteSetData tipSetData = (TipPerMinuteSetData)data;
            _setDescription.text = tipSetData.Description;
            _setValue.text = Utility.StringAddHyphen(Utility.ConvertToNumber(tipSetData.TipPerMinuteValue), 9);
            return;
        }

        if (data is CookingSpeedUpSetData)
        {
            CookingSpeedUpSetData cookSetData = (CookingSpeedUpSetData)data;
            _setDescription.text = cookSetData.Description;
            _setValue.text = Utility.StringAddHyphen(cookSetData.CookingSpeedUpMul.ToString(), 8) + "%";
        }
    }
}
