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
        _userIdText.text = UserInfo.UserId;
        _emailField.text = string.Empty;
        _descriptionField.text = string.Empty;
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
        if(string.IsNullOrWhiteSpace(_descriptionField.text))
        {
            PopupManager.Instance.ShowDisplayText("신고 내용을 적어주세요.");
            return;
        }

        BackendManager.Instance.BugReportUpload(UserInfo.UserId, _emailField.text, _descriptionField.text);
        PopupManager.Instance.ShowDisplayText("신고가 접수되었습니다.");
        _uiNav.Pop("UIUserReport");
    }
}
