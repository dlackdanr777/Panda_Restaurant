using System;
using TMPro;
using UnityEngine;

public class UIStaffLackDescription : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _needItemDescription;
    [SerializeField] private TextMeshProUGUI _addScoreDescription;
    [SerializeField] private TextMeshProUGUI _addTipDescription;
    [SerializeField] private UIButtonAndText _buyButton;


    public void SetStaffData(StaffData data, Action<StaffData> onBuyButtonClicked)
    {
        _needItemDescription.text = string.Empty;
        _addScoreDescription.text = Utility.ConvertToNumber(data.GetAddScore(1));
        _addTipDescription.text = data.GetAddTipMul(1) + "%";

        _buyButton.RemoveAllListeners();
        _buyButton.AddListener(() => onBuyButtonClicked(data));
        _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
    }
}
