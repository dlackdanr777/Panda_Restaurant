using TMPro;
using UnityEngine;

public class UIManagementSetCount : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _setNameText;
    [SerializeField] private TextMeshProUGUI _setCountValueText;
    [SerializeField] private TextMeshProUGUI _setDscriptionText;


    public void SetData(SetData data, int count, int totalCount)
    {
        _setNameText.text = data.Name;
        string color = totalCount <= count ? Utility.ColorToHex(Utility.GetColor(ColorType.Positive)) : Utility.ColorToHex(Utility.GetColor(ColorType.Negative));
        _setCountValueText.text = "<color=" + color +">"+ count + "</color>/" + totalCount;
        _setDscriptionText.text = "(�ϼ��� " + Utility.GetSetEffectDescription(data) + ")";
    }

}
