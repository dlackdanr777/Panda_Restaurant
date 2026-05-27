using System;
using BackEnd;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메일 상세보기 팝업 - 미수령 편지 터치 시 표시</summary>
public class UIMailDetailPopup : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _overlayButton;             // 팝업 외부 클릭 → 닫기
    [SerializeField] private Image _npcImage;                   // NPC 이미지 (발신자에 따라 다르게 표시)
    [SerializeField] private TextMeshProUGUI _senderText;       // 발신자
    [SerializeField] private TextMeshProUGUI _titleText;        // 제목
    [SerializeField] private TextMeshProUGUI _descriptionText;  // 내용
    [SerializeField] private Image _itemIcon;                   // 아이템 아이콘
    [SerializeField] private Image _itemBackground;             // 아이템 배경 (아이템이 있는 경우)
    [SerializeField] private TextMeshProUGUI _itemCountText;    // 아이템 수량
    [SerializeField] private Button _receiveButton;             // 받기 버튼 (아이템 있는 편지)
    //[SerializeField] private TextMeshProUGUI _expirationText;   // 만료일
    [SerializeField] private Image _receivedIcon;              // 아이템 수령 완료 아이콘
    [SerializeField] private Image _readIcon;                  // 편지 읽음 완료 아이콘

    [Space]
    [SerializeField] private Sprite _adminSprite;               // 관리자 우편 이미지
    [SerializeField] private Sprite _defaultNpcSprite;          // 기본 NPC 이미지
    [SerializeField] private Sprite _diaSprite;
    [SerializeField] private Sprite _goldSprite;
    [SerializeField] private Sprite _unreceivedSlotSprite;     // 미수령 아이템 배경 스프라이트
    [SerializeField] private Sprite _receivedSlotSprite;       // 수령 완료 아이템 배경 스프라이트

    private MailData _currentMail;
    private Action<MailData> _onReceive;

    public void Init()
    {
        _overlayButton?.onClick.AddListener(Hide);
        _receiveButton?.onClick.AddListener(OnReceiveClicked);
        gameObject.SetActive(false);
    }

    /// <summary>메일 상세 팝업을 표시합니다</summary>
    public void ShowDetail(MailData mail, Action<MailData> onReceive)
    {
        _currentMail = mail;
        _onReceive = onReceive;
        DebugLog.Log($"메일 상세 팝업 Show: {mail.Title} (받기 가능: {!mail.IsReceived && !mail.IsExpired && mail.Items != null && mail.Items.Count > 0})");
        // 발신자
        if (_senderText != null)
            _senderText.text = GetAuthorDisplayName(mail.Author);

        // 제목
        if (_titleText != null)
            _titleText.text = mail.Title;

        // 내용
        if (_descriptionText != null)
            _descriptionText.text = mail.Content;

        // NPC 이미지
        if (_npcImage != null)
        {
            bool isAdmin = mail.PostType == PostType.Coupon || mail.Author == "운영팀" || mail.Author == "운영자";
            if (isAdmin)
            {
                _npcImage.sprite = _adminSprite;
            }
            else if (mail.Author.StartsWith("STAFF", System.StringComparison.OrdinalIgnoreCase))
            {
                Sprite staffSprite = null;
                try { staffSprite = StaffDataManager.Instance.GetStaffData(mail.Author)?.Sprite; } catch { }
                _npcImage.sprite = staffSprite != null ? staffSprite : _defaultNpcSprite;
            }
            else if (mail.Author.StartsWith("CUSTOMER", System.StringComparison.OrdinalIgnoreCase))
            {
                Sprite customerSprite = null;
                try { customerSprite = CustomerDataManager.Instance.GetCustomerData(mail.Author)?.ThumbnailSprite; } catch { }
                _npcImage.sprite = customerSprite != null ? customerSprite : _defaultNpcSprite;
            }
            else
            {
                _npcImage.sprite = _defaultNpcSprite;
            }
        }

        // 아이템 아이콘 & 수량
        bool hasItem = mail.Items != null && mail.Items.Count > 0;
        if (hasItem)
        {
            var first = mail.Items[0];

            if (_itemCountText != null)
            {
                _itemCountText.text = $"x{Utility.ConvertToMoney(first.ItemCount)}";
                _itemCountText.gameObject.SetActive(true);
            }

            if (_itemIcon != null)
            {
                Sprite icon = GetItemSprite(first.ItemName);
                if (icon != null)
                {
                    _itemIcon.sprite = icon;
                    _itemIcon.gameObject.SetActive(true);
                }
                else
                {
                    _itemIcon.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            if (_itemCountText != null)
            {
                _itemCountText.text = string.Empty;
                _itemCountText.gameObject.SetActive(false);
            }
            if (_itemIcon != null)
                _itemIcon.gameObject.SetActive(false);
        }

        // // 만료일
        // if (_expirationText != null)
        // {
        //     _expirationText.text = mail.ExpirationDate != System.DateTime.MaxValue
        //         ? $"만료: {mail.ExpirationDate:MM/dd HH:mm}"
        //         : string.Empty;
        // }

        // 받기 버튼: 아이템 있는 미수령 편지만 표시
        bool canReceive = !mail.IsReceived && !mail.IsExpired && hasItem;
        if (_receiveButton != null)
        {
            _receiveButton.gameObject.SetActive(canReceive);
            _receiveButton.interactable = canReceive;
        }

        bool isReceived = mail.IsReceived;

        // 읽음 아이콘: 아이템 없는 편지는 팝업을 여는 순간 읽은 것으로 표시
        // (실제 SetReceived/백엔드 저장은 UIMailbox.OnSlotClicked에서 처리)
        bool showAsRead = isReceived || (!hasItem && !mail.IsExpired);

        if (_receivedIcon != null) _receivedIcon.gameObject.SetActive(hasItem && isReceived);
        if (_readIcon     != null) _readIcon.gameObject.SetActive(!hasItem && showAsRead);

        // 아이템 배경: 아이템 있을 때만 활성화, 수령 여부에 따라 스프라이트 변경
        if (_itemBackground != null)
        {
            _itemBackground.gameObject.SetActive(hasItem);
            if (hasItem) _itemBackground.sprite = isReceived ? _receivedSlotSprite : _unreceivedSlotSprite;
        }

        gameObject.SetActive(true);
    }

    /// <summary>팝업을 닫습니다</summary>
    public void Hide()
    {
        _currentMail = null;
        _onReceive = null;
        gameObject.SetActive(false);
    }

    private void OnReceiveClicked()
    {
        if (_currentMail == null || _currentMail.IsReceived || _currentMail.IsExpired) return;
        _receiveButton.gameObject.SetActive(false);
        _onReceive?.Invoke(_currentMail);
    }

    /// <summary>발신자 ID를 표시 이름으로 변환합니다.</summary>
    private string GetAuthorDisplayName(string author)
    {
        if (string.IsNullOrEmpty(author)) return author;
        if (author.StartsWith("STAFF", System.StringComparison.OrdinalIgnoreCase))
        {
            try { string n = StaffDataManager.Instance.GetStaffData(author)?.Name; if (!string.IsNullOrEmpty(n)) return n; } catch { }
        }
        else if (author.StartsWith("CUSTOMER", System.StringComparison.OrdinalIgnoreCase))
        {
            try { string n = CustomerDataManager.Instance.GetCustomerData(author)?.Name; if (!string.IsNullOrEmpty(n)) return n; } catch { }
        }
        return author;
    }

    /// <summary>아이템 ID로 스프라이트를 반환합니다. 해당 없으면 null.</summary>
    private Sprite GetItemSprite(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (itemId == "Dia" || itemId == "다이아") return _diaSprite;
        if (itemId == "Gold" || itemId == "코인")  return _goldSprite;

        try { return ItemManager.Instance.GetGachaItemData(itemId)?.Sprite; } catch { }
        try { return FoodDataManager.Instance.GetFoodData(itemId)?.ThumbnailSprite; } catch { }
        try { return FurnitureDataManager.Instance.GetFurnitureData(itemId)?.ThumbnailSprite; } catch { }
        try { return KitchenUtensilDataManager.Instance.GetKitchenUtensilData(itemId)?.ThumbnailSprite; } catch { }
        try { return StaffDataManager.Instance.GetStaffData(itemId)?.Sprite; } catch { }
        try { return SkinDataManager.Instance.GetCustomerSkinData(itemId)?.Sprite; } catch { }
        try { return SkinDataManager.Instance.GetStaffSkinData(itemId)?.Sprite; } catch { }

        return null;
    }
}
