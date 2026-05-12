using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메일함 목록의 개별 메일 슬롯 UI</summary>
public class UIMailSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _contentText;
    [SerializeField] private TextMeshProUGUI _rewardText;
    [SerializeField] private TextMeshProUGUI _expirationText;
    [SerializeField] private Button _receiveButton;
    [SerializeField] private Button _deleteButton;
    [SerializeField] private Button _slotButton;           // 슬롯 전체 클릭 → 상세 팝업
    [SerializeField] private GameObject _receivedBadge;   // 수령 완료 표시 오브젝트
    [SerializeField] private GameObject _expiredBadge;    // 만료 표시 오브젝트

    private MailData _data;
    private Action<MailData> _onReceive;
    private Action<MailData> _onSlotClick;
    private Action<MailData> _onDelete;

    public void Init(Action<MailData> onReceive, Action<MailData> onSlotClick = null, Action<MailData> onDelete = null)
    {
        _onReceive = onReceive;
        _onSlotClick = onSlotClick;
        _onDelete = onDelete;
        _receiveButton.onClick.AddListener(OnReceiveClicked);
        if (_slotButton != null)
            _slotButton.onClick.AddListener(OnSlotClicked);
        if (_deleteButton != null)
            _deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    public void SetData(MailData data)
    {
        _data = data;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (_data == null) return;

        _titleText.text = _data.Title;
        _contentText.text = _data.Content;

        // 보상 미리보기 (GetPostList에서 items 배열 제공)
        if (_data.Items.Count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var item in _data.Items)
                sb.Append($"{item.ItemName} x{item.ItemCount}  ");
            _rewardText.text = sb.ToString().TrimEnd();
        }
        else
        {
            _rewardText.text = "보상 없음";
        }

        // 만료일
        if (_expirationText != null)
        {
            _expirationText.text = _data.ExpirationDate != System.DateTime.MaxValue
                ? $"만료: {_data.ExpirationDate:MM/dd HH:mm}"
                : string.Empty;
        }

        bool isExpired  = _data.IsExpired;
        bool isReceived = _data.IsReceived;

        _receiveButton.gameObject.SetActive(!isReceived && !isExpired);

        if (_deleteButton != null)
            _deleteButton.gameObject.SetActive(isReceived); // 수령 완료된 편지만 삭제 가능

        if (_receivedBadge != null)
            _receivedBadge.SetActive(isReceived);

        if (_expiredBadge != null)
            _expiredBadge.SetActive(isExpired && !isReceived);
    }

    private void OnSlotClicked()
    {
        if (_data == null) return;
        _onSlotClick?.Invoke(_data);
    }

    private void OnDeleteClicked()
    {
        if (_data == null || !_data.IsReceived) return;
        _deleteButton.interactable = false;
        _onDelete?.Invoke(_data);
    }

    private void OnReceiveClicked()
    {
        if (_data == null || _data.IsReceived || _data.IsExpired) return;
        _receiveButton.interactable = false;
        _onReceive?.Invoke(_data);
    }
}
