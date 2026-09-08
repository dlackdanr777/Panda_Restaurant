using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGachaCard : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private Image _star1Frame;
    [SerializeField] private Image _star3Frame;
    [SerializeField] private Image _star4Frame;
    [SerializeField] private Image _star5Frame;
    [SerializeField] private Image _skinImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private TextMeshProUGUI _typeText;
    [SerializeField] private Button _closeButton;

    [SerializeField] private UIItemStar _itemStar;

    private Vector3 _tmpScale;


    public void Init()
    {
        _tmpScale = _rectTransform.localScale;
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
    }

    public void SetScale(float scale)
    {
        _rectTransform.localScale = Vector3.one * scale;
    }

    public void ResetScale()
    {
        _rectTransform.localScale = _tmpScale;
    }

    public void SetPosition(Vector3 position)
    {
        _rectTransform.anchoredPosition = position;
    }


    public void SetData(GachaData data)
    {
        SetData(data, true);
    }

    private void SetData(GachaData data, bool showOwnedStaffEffect)
    {
        if (data == null)
        {
            DebugLog.LogError("가챠 카드에 표시할 데이터가 없습니다.");
            ClearData();
            return;
        }

        SetImage(data);
        UpdateFrame(data);
        SetName(data);
        SetDescription(data);
        SetEffect(data, showOwnedStaffEffect);
        SetType(data);
        _itemStar.SetStar(data.Rank);
    }

    /// <summary>
    /// 계산이 완료된 직원 한 항목을 표시한다. 추첨, 지급, 재화 변경 및 저장은 수행하지 않는다.
    /// 기존 SetData도 설명 전체를 다시 쓰므로 카드 재사용 시 획득 문구가 남지 않는다.
    /// </summary>
    public bool TrySetStaffAcquisitionResult(
        GachaStaffData data, StaffGachaAcquisitionItem item, bool isTestPreview = false)
    {
        if (data == null || data.StaffData == null || item == null ||
            string.IsNullOrWhiteSpace(item.StaffId) ||
            !string.Equals(data.Id, item.StaffId, System.StringComparison.Ordinal) ||
            !string.Equals(data.StaffData.Id, item.StaffId, System.StringComparison.Ordinal) ||
            data.Rank != item.Rank || data.StaffData.Rank != item.Rank)
        {
            ClearData();
            return false;
        }

        // 획득 결과는 기존 상세 화면의 기본(Lv.1) 능력 문구를 사용한다.
        // 실제 계정의 보유 레벨을 조회하거나 미보유 정보를 가리지 않는다.
        SetData(data, false);
        string previewLabel = isTestPreview ? "[테스트 미리보기]\n" : string.Empty;
        _descriptionText.SetText(item.IsNew
            ? previewLabel + "신규 획득"
            : previewLabel + "중복 획득\n판다토큰 +" + item.PandaTokenReward);
        return true;
    }
    
    private void SetImage(GachaData data)
    {
        _skinImage.sprite = data.ThumbnailSprite == null ? data.Sprite : data.ThumbnailSprite;
    }


    private void SetDescription(GachaData data)
    {
        _descriptionText.SetText(data.Description ?? string.Empty);
    }


    private void SetName(GachaData data)
    {
        _nameText.SetText(string.IsNullOrWhiteSpace(data.Name) ? data.Id : data.Name);
    }

    private void SetEffect(GachaData data, bool showOwnedStaffEffect)
    {
        if (data is SkinData)
        {
            if (data is StaffSkinData staffSkinData)
            {
                _effectText.SetText(Utility.GetStaffSkinEffectDescription(staffSkinData));
            }

            else if (data is CustomerSkinData customerSkinData)
            {
                _effectText.SetText(Utility.GetCustomerSkinEffectDescription(customerSkinData));
            }

        }
        else if (data is GachaItemData itemData)
        {
            _effectText.SetText(Utility.GetGachaItemEffectDescription(itemData));
        }
        else if (data is GachaStaffData staffGachaData && staffGachaData.StaffData != null)
        {
            _effectText.SetText(showOwnedStaffEffect
                ? Utility.GetStaffEffectDescription(staffGachaData.StaffData)
                : Utility.GetStaffEffectDescription(staffGachaData.StaffData, 1));
        }
        else
        {
            _effectText.SetText(string.Empty);
        }

    }


    private void SetType(GachaData data)
    {
        if (data is SkinData)
        {
            SkinData skinData = (SkinData)data;
            if (data is StaffSkinData)
            {

                StaffData staffData = StaffDataManager.Instance.GetStaffData(skinData.EquipId);

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
                CustomerData customerData = CustomerDataManager.Instance.GetCustomerData(skinData.EquipId);

                if (customerData == null)
                {
                    DebugLog.LogError("해당 스킨의 고객 데이터를 찾을 수 없습니다. 스킨ID: " + data.Id);
                    return;
                }

                string typeStr = "손님";
                _typeText.SetText(typeStr);
            }
        }

        else if (data is GachaStaffData staffGachaData)
        {
            StaffData staffData = staffGachaData.StaffData;
            if (staffData == null)
            {
                _typeText.SetText("직원");
                return;
            }

            _typeText.SetText(Utility.StaffTypeStringConverter(StaffDataManager.Instance.GetStaffGroupType(staffData)));
        }
        else
        {
            _typeText.SetText("아이템");
        }

    }


    private void UpdateFrame(GachaData data)
    {
        _star1Frame.gameObject.SetActive(false);
        _star3Frame.gameObject.SetActive(false);
        _star4Frame.gameObject.SetActive(false);
        _star5Frame.gameObject.SetActive(false);

        if (data == null)
        {
            _star1Frame.gameObject.SetActive(true);
        }
        else
        {
            switch (data.Rank)
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
    
    private void OnCloseButtonClicked()
    {
        gameObject.SetActive(false);
    }

    private void ClearData()
    {
        _skinImage.sprite = null;
        _nameText.SetText(string.Empty);
        _descriptionText.SetText(string.Empty);
        _effectText.SetText(string.Empty);
        _typeText.SetText(string.Empty);
        UpdateFrame(null);
        _itemStar.SetStar(Rank.Normal1);
    }
}
