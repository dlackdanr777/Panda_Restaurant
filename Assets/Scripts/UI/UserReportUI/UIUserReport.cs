using BackEnd;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIUserReport : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _userIdText;
    // 인스펙터에서 이 필드의 레이블을 "카테고리"로 변경해주세요.
    // 카테고리 예시: bug_report / payment_issue / reward_missing / ad_issue / account_issue / etc
    [SerializeField] private TMP_InputField _emailField;
    [SerializeField] private TMP_InputField _descriptionField;
    [SerializeField] private Button _sendButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    public override void Init()
    {
        _sendButton.onClick.AddListener(OnSendButtonClicked);
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        transform.SetAsLastSibling();
        _userIdText.text = BackendManager.Instance.IsLogin
            ? (string.IsNullOrEmpty(UserInfo.GamerId) ? Backend.UserNickName : UserInfo.GamerId)
            : "로그인안됨";
        _emailField.text = string.Empty;
        _descriptionField.text = string.Empty;
        _sendButton.interactable = true;
        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });

    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;
        _animeUI.SetActive(true);
        transform.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    private void OnSendButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(_descriptionField.text))
        {
            PopupManager.Instance.ShowDisplayText("문의 내용을 입력해주세요.");
            return;
        }

        if (InquiryManager.Instance.IsOnCooldown)
        {
            int sec = Mathf.CeilToInt(InquiryManager.Instance.CooldownRemaining);
            PopupManager.Instance.ShowDisplayText($"{sec}초 후에 다시 전송해주세요.");
            return;
        }

        // _emailField.text 는 카테고리 입력으로 용도 변경 (인스펙터 레이블 "카테고리" 로 수정 필요)
        string category = string.IsNullOrWhiteSpace(_emailField.text)
            ? InquiryManager.CATEGORY_ETC
            : _emailField.text.Trim();

        _sendButton.interactable = false;

        InquiryManager.Instance.SubmitInquiryAsync(
            category:  category,
            message:   _descriptionField.text,
            onSuccess: () =>
            {
                PopupManager.Instance.ShowDisplayText(
                    "문의가 접수되었습니다.\n확인 후 필요한 경우 편지함으로 답변드리겠습니다.");
                _uiNav.Pop("UIUserReport");
            },
            onFail: () =>
            {
                _sendButton.interactable = true;
                PopupManager.Instance.ShowDisplayText("전송 중 오류가 발생했습니다.\n잠시 후 다시 시도해주세요.");
            }
        );
    }
}
