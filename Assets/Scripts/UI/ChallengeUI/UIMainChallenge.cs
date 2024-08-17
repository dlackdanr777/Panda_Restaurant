using Muks.Tween;
using Muks.MobileUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.DataBind;

public class UIMainChallenge : MobileUIView
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _dontTouchArea;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _rewardValue;
    [SerializeField] private Image _moneyImage;
    [SerializeField] private Image _diaImage;
    [SerializeField] private Button _shortCutButton;
    [SerializeField] private Button _rewardButton;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private ChallengeData _currentData;

    public override void Init()
    {
        _rewardButton.onClick.AddListener(OnRewardButtonClicked);
        _shortCutButton.onClick.AddListener(OnShortcutButtonClicked);

        UpdateData();
        ChallengeManager.Instance.OnMainChallengeUpdateHandler += UpdateData;
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.gameObject.SetActive(false);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

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
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);

        TweenData tween = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
        });
    }


    private void UpdateData()
    {
        _currentData = ChallengeManager.Instance.GetCurrentMainChallengeData();

        if(_currentData == null)
        {
            _description.text = "현재 메인 과제가 없습니다.";
            DataBind.SetTextValue("MainChallengeDescription", "현재 메인 과제가 없습니다.");

            _rewardValue.gameObject.SetActive(false);
            _moneyImage.gameObject.SetActive(false); 
            _diaImage.gameObject.SetActive(false);
            _rewardButton.gameObject.SetActive(false);
            _shortCutButton.gameObject.SetActive(false);
            return;
        }

        if(_currentData.MoneyType == MoneyType.Gold)
        {
            _moneyImage.gameObject.SetActive(true);
            _diaImage.gameObject.SetActive(false);
        }
        else
        {
            _diaImage.gameObject.SetActive(true);
            _moneyImage.gameObject.SetActive(false);
        }

        if(UserInfo.GetIsDoneMainChallenge(_currentData.Id))
        {
            _rewardButton.gameObject.SetActive(true);
            _shortCutButton.gameObject.SetActive(false);
        }
        else
        {
            _shortCutButton.gameObject.SetActive(true);
            _rewardButton.gameObject.SetActive(false);
        }

        _rewardValue.gameObject.SetActive(true);
        _description.text = _currentData.Description;
        DataBind.SetTextValue("MainChallengeDescription", _currentData.Description);
        _rewardValue.text = Utility.ConvertToNumber(_currentData.RewardMoney);
    }


    private void OnRewardButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("메인 과제 데이터가 슬롯에 없습니다.");
            return;
        }

        if(!UserInfo.GetIsDoneMainChallenge(_currentData.Id))
        {
            DebugLog.Log("메인 과제가 완료되지 않았습니다: " + _currentData.Id);
            return;
        }

        UserInfo.ClearMainChallenge(_currentData.Id);
        UpdateData();
    }


    private void OnShortcutButtonClicked()
    {
        if (_currentData == null)
        {
            DebugLog.Log("메인 과제 데이터가 슬롯에 없습니다.");
            return;
        }

        if (_currentData.ShortcutAction.Item == null)
        {
            DebugLog.Log("바로가기 메서드 정보가 없습니다.");
            return;
        }

        _currentData.ShortcutAction.Item?.Invoke();
    }
}
