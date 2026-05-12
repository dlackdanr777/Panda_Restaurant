using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>메일함 UI 패널 - 관리자·쿠폰 우편을 한 목록에 표시합니다</summary>
public class UIMailbox : MobileUIView
{
    [Header("Components")]
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _receiveAllButton;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private Transform _slotParent;
    [SerializeField] private UIMailSlot _slotPrefab;
    [SerializeField] private GameObject _emptyNotice;
    [SerializeField] private GameObject _loadingIndicator;

    [Space]
    [Header("Sort & Detail")]
    [SerializeField] private Button _sortButton;
    [SerializeField] private TextMeshProUGUI _sortButtonText;
    [SerializeField] private UIMailDetailPopup _detailPopup;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration = 0.25f;
    [SerializeField] private Ease _showTweenMode = Ease.OutBack;
    [SerializeField] private float _hideDuration = 0.2f;
    [SerializeField] private Ease _hideTweenMode = Ease.InBack;

    private List<UIMailSlot> _slotPool = new List<UIMailSlot>();
    private bool _sortNewest = true;

    public override void Init()
    {
        _closeButton.onClick.AddListener(Hide);
        _receiveAllButton.onClick.AddListener(OnReceiveAllClicked);
        if (_refreshButton != null) _refreshButton.onClick.AddListener(OnRefreshClicked);
        if (_sortButton != null) _sortButton.onClick.AddListener(OnSortClicked);

        MailManager.Instance.OnMailListRefreshed += RefreshUI;

        gameObject.SetActive(false);
    }

    public override void Show()
    {
        _animeUI.TweenStop();
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();
        if(_dontTouchArea != null) _dontTouchArea.SetActive(true);
        TweenData tween = _animeUI.TweenScale(new Vector3(1f, 1f, 1f), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            if(_dontTouchArea != null) _dontTouchArea.SetActive(false);
        });

        LoadMailList();
    }

    public override void Hide()
    {
        _animeUI.TweenStop();
        VisibleState = VisibleState.Disappearing;
        if (_dontTouchArea != null) _dontTouchArea.SetActive(true);
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }

    // ─────────────────────────────────────────────────────
    #region 메일 로드 및 UI 갱신

    private void LoadMailList()
    {
        SetLoading(true);
        MailManager.Instance.LoadMailListAsync(
            onSuccess: () => SetLoading(false),
            onFail: () => SetLoading(false)
        );
    }

    private void RefreshUI()
    {
        ClearSlots();

        var mailList = MailManager.Instance.MailList;

        // 정렬: 최신순 = InDate 내림차순, 오래된순 = 오름차순
        IEnumerable<MailData> sorted = _sortNewest
            ? mailList.OrderByDescending(m => m.InDate)
            : mailList.OrderBy(m => m.InDate);

        int shown = 0;
        foreach (var mail in sorted)
        {
            UIMailSlot slot = GetOrCreateSlot();
            slot.SetData(mail);
            slot.gameObject.SetActive(true);
            shown++;
        }

        if (_emptyNotice != null)
            _emptyNotice.SetActive(shown == 0);

        _receiveAllButton.interactable = MailManager.Instance.UnreceivedCount > 0;
    }

    private void SetLoading(bool isLoading)
    {
        if (_loadingIndicator != null)
            _loadingIndicator.SetActive(isLoading);

        _receiveAllButton.interactable = !isLoading;
        if (_refreshButton != null) _refreshButton.interactable = !isLoading;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 버튼 이벤트

    private void OnReceiveAllClicked()
    {
        _receiveAllButton.interactable = false;
        SetLoading(true);

        int remaining = 0;

        System.Action onDone = null;
        onDone = () =>
        {
            remaining--;
            if (remaining <= 0)
            {
                SetLoading(false);
                PopupManager.Instance?.ShowDisplayText("모든 보상을 수령했습니다!");
            }
        };
        System.Action onFail = () =>
        {
            remaining--;
            if (remaining <= 0) SetLoading(false);
        };

        // Admin + Coupon 동시 수령 시도
        remaining = 2;
        MailManager.Instance.ReceiveAllMailAsync(onDone, onFail);
        MailManager.Instance.ReceiveAllCouponMailAsync(onDone, onFail);
    }

    private void OnRefreshClicked()
    {
        LoadMailList();
    }

    private void OnSortClicked()
    {
        _sortNewest = !_sortNewest;
        if (_sortButtonText != null)
            _sortButtonText.text = _sortNewest ? "최신순" : "오래된순";
        RefreshUI();
    }

    private void OnSlotClicked(MailData mail)
    {
        if (_detailPopup == null) return;
        _detailPopup.ShowDetail(mail, OnDetailReceive);
    }

    private void OnDetailReceive(MailData mail)
    {
        _detailPopup?.Hide();
        MailManager.Instance.ReceiveMailAsync(
            mail,
            onSuccess: received =>
            {
                RefreshUI();
                PopupManager.Instance?.ShowDisplayText(BuildRewardSummary(received));
            },
            onFail: () => RefreshUI()
        );
    }

    private void OnSlotDelete(MailData mail)
    {
        MailManager.Instance.DeleteMailAsync(
            mail,
            onSuccess: () => { /* OnMailListRefreshed 이벤트로 RefreshUI 자동 호출 */ },
            onFail: () =>
            {
                RefreshUI(); // 슬롯 인터랙티브 복원
                PopupManager.Instance?.ShowDisplayText("삭제에 실패했습니다.");
            }
        );
    }

    private void OnSlotReceive(MailData mail)
    {
        MailManager.Instance.ReceiveMailAsync(
            mail,
            onSuccess: received =>
            {
                RefreshUI();
                PopupManager.Instance?.ShowDisplayText(BuildRewardSummary(received));
            },
            onFail: () => RefreshUI()
        );
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 슬롯 풀링

    private UIMailSlot GetOrCreateSlot()
    {
        foreach (var s in _slotPool)
            if (!s.gameObject.activeSelf) return s;

        UIMailSlot newSlot = Instantiate(_slotPrefab, _slotParent);
        newSlot.Init(OnSlotReceive, OnSlotClicked, OnSlotDelete);
        _slotPool.Add(newSlot);
        return newSlot;
    }

    private void ClearSlots()
    {
        foreach (var slot in _slotPool)
            slot.gameObject.SetActive(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 유틸

    private string BuildRewardSummary(MailData mail)
    {
        if (mail.Items.Count == 0) return "보상 수령 완료";

        System.Text.StringBuilder sb = new System.Text.StringBuilder("보상: ");
        foreach (var item in mail.Items)
            sb.Append($"{item.ItemName} x{item.ItemCount}  ");
        return sb.ToString().TrimEnd();
    }

    #endregion

    private void OnDestroy()
    {
        if (MailManager.Instance != null)
            MailManager.Instance.OnMailListRefreshed -= RefreshUI;
    }
}
