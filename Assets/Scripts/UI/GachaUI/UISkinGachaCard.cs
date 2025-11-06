using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISkinGachaCard : MonoBehaviour
{
    [SerializeField] private Image _star1Frame;
    [SerializeField] private Image _star3Frame;
    [SerializeField] private Image _star4Frame;
    [SerializeField] private Image _star5Frame;
    [SerializeField] private Image _skinImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private TextMeshProUGUI _typeText;

    [SerializeField] private UIItemStar _itemStar;



    public void SetData(SkinData data)
    {
        if (data == null)
        {
            DebugLog.LogError("스킨 데이터가 없습니다.");
            return;
        }
        
        SetImage(data);
        UpdateFrame(data);
        SetName(data);
        SetDescription(data);
        SetEffect(data);
        SetType(data);
        _itemStar.SetStar(data.Rank);
    }
    
    private void SetImage(SkinData data)
    {
        if (data == null)
        {
            _skinImage.sprite = null;
            return;
        }

        _skinImage.sprite = data.ThumbnailSprite;
    }


    private void SetDescription(SkinData data)
    {
        _descriptionText.SetText(data.Description);
    }


    private void SetName(SkinData data)
    {
        _nameText.SetText(data.Name);
    }

    private void SetEffect(SkinData data)
    {
        
    }


    private void SetType(SkinData data)
    {
        if (data is StaffSkinData)
        {
            StaffData staffData = StaffDataManager.Instance.GetStaffData(data.EquipId);

            if (staffData == null)
            {
                DebugLog.LogError("해당 스킨의 스탭 데이터를 찾을 수 없습니다. 스킨ID: " + data.Id);
                return;
            }

            string typeStr = Utility.StaffTypeStringConverter(StaffDataManager.Instance.GetStaffGroupType(staffData));
            _typeText.SetText(typeStr);
        }

        else if (data is CustomerSkinData)
        {
            CustomerData customerData = CustomerDataManager.Instance.GetCustomerData(data.EquipId);

            if (customerData == null)
            {
                DebugLog.LogError("해당 스킨의 고객 데이터를 찾을 수 없습니다. 스킨ID: " + data.Id);
                return;
            }

            string typeStr = "손님";
            _typeText.SetText(typeStr);
        }
    }


     private void UpdateFrame(SkinData skinData)
    {
        _star1Frame.gameObject.SetActive(false);
        _star3Frame.gameObject.SetActive(false);
        _star4Frame.gameObject.SetActive(false);
        _star5Frame.gameObject.SetActive(false);

        if (skinData == null)
        {
            _star1Frame.gameObject.SetActive(true);
        }
        else
        {
            switch (skinData.Rank)
            {
                case Rank.Normal1:
                case Rank.Normal2:
                    _star1Frame.gameObject.SetActive(true);
                    break;

                case Rank.Rare:
                    _star3Frame.gameObject.SetActive(true);
                    break;
                case Rank.Unique:
                    _star4Frame.gameObject.SetActive(true);
                    break;
                case Rank.Special:
                    _star5Frame.gameObject.SetActive(true);
                    break;
            }
        }
    }
}
