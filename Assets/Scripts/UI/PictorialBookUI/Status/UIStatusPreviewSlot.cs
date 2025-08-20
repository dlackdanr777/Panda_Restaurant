using System.Text;
using TMPro;
using UnityEngine;

public class UIStatusPreviewSlot : MonoBehaviour
{
    [SerializeField] private UIPictorialBookGachaItemSlot _gachaItemSlot;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;


    public void Init()
    {

    }

    public void SetData(GachaItemData data)
    {
        if (data == null)
        {
            gameObject.SetActive(false);
            return;
        }

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
}
