using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메일 상세보기 팝업 - 미수령 편지 터치 시 표시</summary>
public class UIMailDetailPopup : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _overlayButton;         // 팝업 외부 클릭 → 닫기
    [SerializeField] private Image _senderProfileImage;     // 발신자 프로필 이미지
    [SerializeField] private TextMeshProUGUI _contentText;  // 편지 본문
    [SerializeField] private GameObject _rewardArea;        // 보상 아이콘 영역
    [SerializeField] private Image _rewardIcon;             // 보상 아이템 아이콘
    [SerializeField] private TextMeshProUGUI _rewardCountText; // 보상 수량 (x100)
    [SerializeField] private Button _receiveButton;         // 받기 버튼
    [SerializeField] private GameObject _receiveButtonObj;

    private MailData _currentMail;
    private Action<MailData> _onReceive;

    private void Awake()
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

        // 본문
        if (_contentText != null)
            _contentText.text = mail.Content;

        // 보상 표시 (첫 번째 아이템 기준)
        bool hasReward = mail.Items != null && mail.Items.Count > 0;
        if (_rewardArea != null)
            _rewardArea.SetActive(hasReward);

        if (hasReward && _rewardCountText != null)
        {
            var item = mail.Items[0];
            _rewardCountText.text = $"x{item.ItemCount}";
        }

        // 받기 버튼 표시 여부
        bool canReceive = !mail.IsReceived && !mail.IsExpired;
        if (_receiveButtonObj != null)
            _receiveButtonObj.SetActive(canReceive);
        else if (_receiveButton != null)
            _receiveButton.gameObject.SetActive(canReceive);

        if (_receiveButton != null)
            _receiveButton.interactable = canReceive;

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
        _receiveButton.interactable = false;
        _onReceive?.Invoke(_currentMail);
    }
}
