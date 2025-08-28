using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStatusPreview : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private UIFoodType _uiFoodType;
    [SerializeField] private UIStatusItemPreview _itemPreview;

    [Space]
    [Header("Slots")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIStatusPreviewSlot _slotPrefab;

    private List<UIStatusPreviewSlot> _slots = new List<UIStatusPreviewSlot>();

    private UpgradeType _upgradeType;
    public void Init()
    {
        for (int i = 0; i < 20; ++i)
        {
            UIStatusPreviewSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(OnSlotButtonClicked);
            slot.gameObject.SetActive(false);
            _slots.Add(slot);
        }
        _itemPreview.Init();
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
        else if (_upgradeType == UpgradeType.UPGRADE29)
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
        else if (upgradeType == UpgradeType.UPGRADE29)
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
        UpdateFoodType(upgradeType);
        SlotUpdate();
    }


    private void SlotUpdate()
    {
        foreach (UIStatusPreviewSlot slot in _slots)
        {
            slot.gameObject.SetActive(false);
        }

        List<GachaItemData> gachaItems = UserInfo.GetGiveGachaItemDataList(_upgradeType);
        for (int i = 0, cnt = gachaItems.Count; i < cnt; ++i)
        {
            if (i < _slots.Count)
            {
                _slots[i].SetData(gachaItems[i]);
                _slots[i].gameObject.SetActive(true);
            }
            else
            {
                UIStatusPreviewSlot slot = Instantiate(_slotPrefab, _slotParent);
                slot.Init(OnSlotButtonClicked);
                slot.SetData(gachaItems[i]);
                slot.gameObject.SetActive(true);
                _slots.Add(slot);
            }
        }
    }


    private void UpdateFoodType(UpgradeType type)
    {
        FoodType foodType = FoodType.None;
        switch (type)
        {
            case UpgradeType.UPGRADE03:
                foodType = FoodType.Natural;
                break;

            case UpgradeType.UPGRADE04:
                foodType = FoodType.Modern;
                break;

            case UpgradeType.UPGRADE05:
                foodType = FoodType.Vintage;
                break;

            case UpgradeType.UPGRADE06:
                foodType = FoodType.Traditional;
                break;

            case UpgradeType.UPGRADE07:
                foodType = FoodType.Tropical;
                break;

            case UpgradeType.UPGRADE08:
                foodType = FoodType.Luxury;
                break;

            case UpgradeType.UPGRADE09:
                foodType = FoodType.Cozy;
                break;

            case UpgradeType.UPGRADE11:
                foodType = FoodType.Natural;
                break;

            case UpgradeType.UPGRADE12:
                foodType = FoodType.Modern;
                break;

            case UpgradeType.UPGRADE13:
                foodType = FoodType.Vintage;
                break;

            case UpgradeType.UPGRADE14:
                foodType = FoodType.Traditional;
                break;

            case UpgradeType.UPGRADE15:
                foodType = FoodType.Tropical;
                break;

            case UpgradeType.UPGRADE16:
                foodType = FoodType.Luxury;
                break;

            case UpgradeType.UPGRADE17:
                foodType = FoodType.Cozy;
                break;

            default:
                _uiFoodType.gameObject.SetActive(false);
                return;
        }

        _uiFoodType.gameObject.SetActive(true);
        _uiFoodType.SetFoodType(foodType);
    }

    private void OnSlotButtonClicked(GachaItemData data)
    {
        _itemPreview.Show(data);
    }


    private void OnDisable()
    {
        _itemPreview.Hide();
    }

    private void OnEnable()
    {
        _itemPreview.Hide();
    }

}
