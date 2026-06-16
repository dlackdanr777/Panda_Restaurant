using System;
using BackEnd;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메일함 목록의 개별 메일 슬롯 UI</summary>
public class UIMailSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _npcImage;           // NPC 이미지 (발신자에 따라 다르게 표시)
    [SerializeField] private UIImageAndText _item;           // 아이템
    [SerializeField] private Image _itemBackground;       // 아이템 배경 (아이템이 있는 경우)
    [SerializeField] private TextMeshProUGUI _senderText;       // 발신자
    [SerializeField] private TextMeshProUGUI _contentText;      // 내용
    //[SerializeField] private TextMeshProUGUI _expirationText;   // 만료일
    [SerializeField] private Button _receiveButton;             // 받기 버튼 (아이템 있는 편지)
    [SerializeField] private Button _readButton;                // 읽기 버튼 (아이템 없는 편지)
    [SerializeField] private Button _slotButton;                // 슬롯 전체 클릭 → 상세 팝업
    [SerializeField] private GameObject _unreceivedImage;      // 미수령 상태 이미지
    [SerializeField] private GameObject _receivedImage;        // 수령 완료 이미지

    [SerializeField] private Image _receivedIcon;
    [SerializeField] private Image _readIcon;

    [Space]
    [SerializeField] private Sprite _adminSprite;       // 관리자 우편 이미지
    [SerializeField] private Sprite _defaultNpcSprite;  // 기본 NPC 이미지
    [SerializeField] private Sprite _diaSprite;
    [SerializeField] private Sprite _goldSprite;
    [SerializeField] private Sprite _unreceivedSlotSprite; // 기본 아이템 이미지 (아이콘 못 찾았을 때)
    [SerializeField] private Sprite _receivedSlotSprite;   // 수령 완료 아이템 이미지 (아이콘 못 찾았을 때)

    private MailData _data;
    private Action<MailData> _onReceive;
    private Action<MailData> _onSlotClick;


    public void Init(Action<MailData> onReceive, Action<MailData> onSlotClick = null)
    {
        _onReceive = onReceive;
        _onSlotClick = onSlotClick;
        _receiveButton.onClick.AddListener(OnReceiveClicked);
        if (_readButton != null)
            _readButton.onClick.AddListener(OnReadClicked);
        if (_slotButton != null)
            _slotButton.onClick.AddListener(OnSlotClicked);
    }

    public void SetData(MailData data)
    {
        _data = data;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (_data == null) return;

        if (_senderText != null)  _senderText.text  = GetAuthorDisplayName(_data.Author);
        if (_contentText != null) _contentText.text = GetContentPreview(_data.Content);

        // ── NPC 이미지: 쿠폰 우편이거나 발신자가 "운영팀"이면 어드민 스프라이트 ──
        if (_npcImage != null)
        {
            bool isAdmin = _data.PostType == PostType.Coupon || _data.Author == "운영자" || _data.Author == "운영팀";
            if (isAdmin)
            {
                _npcImage.sprite = _adminSprite;
            }
            else if (_data.Author.StartsWith("STAFF", System.StringComparison.OrdinalIgnoreCase))
            {
                Sprite staffSprite = null;
                try { staffSprite = StaffDataManager.Instance.GetStaffData(_data.Author)?.Sprite; } catch { }
                _npcImage.sprite = staffSprite != null ? staffSprite : _defaultNpcSprite;
            }
            else if (_data.Author.StartsWith("CUSTOMER", System.StringComparison.OrdinalIgnoreCase))
            {
                Sprite customerSprite = null;
                try { customerSprite = CustomerDataManager.Instance.GetCustomerData(_data.Author)?.ThumbnailSprite; } catch { }
                _npcImage.sprite = customerSprite != null ? customerSprite : _defaultNpcSprite;
            }
            else
            {
                _npcImage.sprite = _defaultNpcSprite;
            }
        }

        // ── 아이템 아이콘 & 텍스트 ───────────────────────────────────────────
        if (_data.Items.Count > 0)
        {
            System.Text.StringBuilder names  = new System.Text.StringBuilder();
            System.Text.StringBuilder counts = new System.Text.StringBuilder();
            foreach (var item in _data.Items)
            {
                names.AppendLine(item.ItemName);
                counts.AppendLine($"x{Utility.ConvertToMoney(item.ItemCount)}");
            }
            if (_item != null)
            {
                _item.SetText(counts.ToString().TrimEnd());
            }

            // 첫 번째 아이템 기준으로 아이콘 결정
            if (_item != null)
            {
                string firstId = _data.Items[0].ItemName;
                Sprite icon = GetItemSprite(firstId);
                if (icon != null)
                {
                    _item.SetSprite(icon);
                    _item.gameObject.SetActive(true);
                }
                else
                {
                    _item.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            if (_item != null)
            {
                _item.SetText(string.Empty);
            }
            if (_item != null) _item.gameObject.SetActive(false);
        }

        // if (_expirationText != null)
        // {
        //     _expirationText.text = _data.ExpirationDate != System.DateTime.MaxValue
        //         ? $"만료: {_data.ExpirationDate:MM/dd HH:mm}"
        //         : string.Empty;
        // }

        bool isReceived = _data.IsReceived;
        bool isExpired  = _data.IsExpired;
        bool hasItem    = _data.Items != null && _data.Items.Count > 0;

        // 아이템 있는 미수령: 받기 버튼 / 아이템 없는 미수령: 읽기 버튼
        bool canReceive = !isReceived && !isExpired && hasItem;
        bool canRead    = !isReceived && !isExpired && !hasItem;
        _receiveButton.gameObject.SetActive(canReceive);
        if (_readButton != null) _readButton.gameObject.SetActive(canRead);

        if (_receivedIcon != null) _receivedIcon.gameObject.SetActive(hasItem && isReceived);
        if (_readIcon != null)     _readIcon.gameObject.SetActive(!hasItem && isReceived);
        
        if (_itemBackground != null)
        {
            _itemBackground.gameObject.SetActive(hasItem);
            if (hasItem) _itemBackground.sprite = isReceived ? _receivedSlotSprite : _unreceivedSlotSprite;
        }
    }

    /// <summary>내용 미리보기: 20자 초과 시 잘라서 … 표시, 줄바꿈이 먼저 나오면 그 앞까지만 표시</summary>
    private string GetContentPreview(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        int nlIdx = content.IndexOfAny(new[] { '\n', '\r' });
        if (nlIdx >= 0 && nlIdx < 20)
            return content.Substring(0, nlIdx);
        if (content.Length > 20)
            return content.Substring(0, 20) + "...";
        return content;
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

    private void OnSlotClicked()
    {
        if (_data == null) return;
        _onSlotClick?.Invoke(_data);
    }

    private void OnReadClicked()
    {
        DebugLog.Log($"[UIMailSlot] OnReadClicked - mail={_data?.Title}, isReceived={_data?.IsReceived}, onSlotClick null={_onSlotClick == null}");
        if (_data == null || _data.IsReceived || _data.IsExpired) return;
        _readButton.interactable = false;
        _onSlotClick?.Invoke(_data);
    }

    private void OnReceiveClicked()
    {
        if (_data == null || _data.IsReceived || _data.IsExpired) return;
        _receiveButton.interactable = false;
        _onReceive?.Invoke(_data);
    }
}
