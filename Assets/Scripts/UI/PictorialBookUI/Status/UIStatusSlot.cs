using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStatusSlot : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    private UpgradeType _upgradeType;
    private Action<UpgradeType> _onClickCallback;

    public void Init(Action<UpgradeType> onClickCallback)
    {
        _onClickCallback = onClickCallback;
        _button.onClick.AddListener(OnButtonClicked);
    }

    public void UpdateUI()
    {
        string descriptionEndStr = _upgradeType >= UpgradeType.UPGRADE02 && _upgradeType <= UpgradeType.UPGRADE09
            ? "단축"
            : "증가";
        StringBuilder descriptionStr = new StringBuilder();
        if (_upgradeType == UpgradeType.UPGRADE30)
        {
            descriptionStr.Append(Utility.SetStringColor(((int)GameManager.Instance.GetGachaItemUpgradeValue(_upgradeType)).ToString(), ColorType.Positive));
            descriptionStr.Append("명 ");
        }
        else if(_upgradeType == UpgradeType.UPGRADE29)
        {
            descriptionStr.Append(Utility.SetStringColor(GameManager.Instance.GetGachaItemUpgradeValue(_upgradeType).ToString("F3"), ColorType.Positive));
            descriptionStr.Append("초 ");
        }
        else
        {
            descriptionStr.Append(Utility.SetStringColor(GameManager.Instance.GetGachaItemUpgradeValue(_upgradeType).ToString("F3"), ColorType.Positive));
            descriptionStr.Append("% ");
        }
        descriptionStr.Append(descriptionEndStr);
        _descriptionText.SetText(descriptionStr.ToString());
    }

    public void SetData(UpgradeType upgradeType)
    {
        _upgradeType = upgradeType;

        _iconImage.sprite = ItemManager.Instance.GetUpgradeIcon(upgradeType);
        _nameText.SetText(Utility.GetGachaItemUpgradeTypeName(upgradeType));

        string descriptionEndStr = upgradeType >= UpgradeType.UPGRADE02 && upgradeType <= UpgradeType.UPGRADE09
            ? "단축"
            : "증가";
        StringBuilder descriptionStr = new StringBuilder();
        if (upgradeType == UpgradeType.UPGRADE30)
        {
            descriptionStr.Append(Utility.SetStringColor(((int)GameManager.Instance.GetGachaItemUpgradeValue(upgradeType)).ToString(), ColorType.Positive));
            descriptionStr.Append("명 ");
        }
        else if(upgradeType == UpgradeType.UPGRADE29)
        {
            descriptionStr.Append(Utility.SetStringColor(GameManager.Instance.GetGachaItemUpgradeValue(upgradeType).ToString("F3"), ColorType.Positive));
            descriptionStr.Append("초 ");
        }
        else
        {
            descriptionStr.Append(Utility.SetStringColor(GameManager.Instance.GetGachaItemUpgradeValue(upgradeType).ToString("F3"), ColorType.Positive));
            descriptionStr.Append("% ");
        }

        descriptionStr.Append(descriptionEndStr);
        _descriptionText.SetText(descriptionStr.ToString());
    }


    private void OnButtonClicked()
    {
        _onClickCallback?.Invoke(_upgradeType);
    }
}
