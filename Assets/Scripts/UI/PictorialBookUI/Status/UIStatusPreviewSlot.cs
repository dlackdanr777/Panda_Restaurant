using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStatusPreviewSlot : MonoBehaviour
{
    [SerializeField] private UIPictorialBookGachaItemSlot _gachaItemSlot;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Button _button;

    private Action<GachaItemData> _onClick;
    private GachaItemData _data;
    public void Init(Action<GachaItemData> onClick = null)
    {
        _onClick = onClick;
        _button.onClick.AddListener(OnButtonClicked);
    }

    public void SetData(GachaItemData data)
    {
        if (data == null)
        {
            _data = null;
            gameObject.SetActive(false);
            return;
        }
        _data = data;
        gameObject.SetActive(true);
        _gachaItemSlot.SetData(data);
        UpgradeType upgradeType = data.UpgradeType;
        _nameText.SetText(data.Name);

        string descriptionEndStr = upgradeType >= UpgradeType.UPGRADE02 && upgradeType <= UpgradeType.UPGRADE09
            ? "단축"
            : "증가";
        StringBuilder descriptionStr = new StringBuilder();
        if (upgradeType == UpgradeType.UPGRADE30)
        {
            descriptionStr.Append(Utility.SetStringColor(((int)Math.Abs(Utility.GetGachaItemEffectValue(_data))).ToString(), ColorType.Positive));
            descriptionStr.Append("명 ");
        }
        else if (upgradeType == UpgradeType.UPGRADE29)
        {
            descriptionStr.Append(Utility.SetStringColor(Mathf.Abs(Utility.GetGachaItemEffectValue(_data)).ToString("F3"), ColorType.Positive));
            descriptionStr.Append("초 ");
        }
        else
        {
            descriptionStr.Append(Utility.SetStringColor(Mathf.Abs(Utility.GetGachaItemEffectValue(_data)).ToString("F3"), ColorType.Positive));
            descriptionStr.Append("% ");
        }
        descriptionStr.Append(descriptionEndStr);
        _descriptionText.SetText(descriptionStr.ToString());
    }

    private void OnButtonClicked()
    {
        if (_data == null)
            return;

        _onClick?.Invoke(_data);    
    }
}
