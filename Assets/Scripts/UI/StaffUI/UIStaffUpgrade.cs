using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.MobileUI;
using Muks.Tween;

public class UIStaffUpgrade : MobileUIView
{
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Image _staffImage;
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _upgradeDescription;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _showDuration;
    [SerializeField] private TweenMode _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private TweenMode _hideTweenMode;


    private StaffData _currentStaffData;
    private Action<StaffData> _onUpgradeButtonClicked;

    public override void Init()
    {
        UserInfo.OnUpgradeStaffHandler += UpgradeStaffEvent;
        gameObject.SetActive(false);
    }


    public void SetAction(Action<StaffData> onUpgradeButtonClicked)
    {
        _onUpgradeButtonClicked = onUpgradeButtonClicked;
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        gameObject.SetActive(true);
        _dontTouchArea.SetActive(true);
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
            _dontTouchArea.SetActive(false);
            gameObject.SetActive(false);
        });
    }


    public void SetStaffData(StaffData data)
    {
        if (data == null || !UserInfo.IsGiveStaff(data))
            return;

        _currentStaffData = data;

        _staffImage.sprite = data.Sprite;
        _nameText.text = data.Name;
        _upgradeButton.RemoveAllListeners();

        int level = UserInfo.GetStaffLevel(data);
        _levelText.text = level + "단계";
        string description = "작업 간격: " + data.GetActionValue(level) + "초";
        _description.text = description;

        TimedDisplayManager.Instance.ShowText("테스트입니다.");
        if (data.UpgradeEnable(level))
        {
            _upgradeButton.Interactable(true);
            _upgradeButton.AddListener(() => _onUpgradeButtonClicked(_currentStaffData));
            _upgradeButton.SetText("강화");

            string upgradeDescription = "강화 평점: " + data.GetUpgradeMinScore(level)+ "점" + "\n강화 금액: " + data.GetUpgradePrice(level) + "골드";
            _upgradeDescription.text = upgradeDescription;
        }
        else
        {
            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("최대 강화");

            _upgradeDescription.text = string.Empty;
            return;
        }

        if (GameManager.Instance.Score < data.GetUpgradeMinScore(level))
        {
            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("평점 부족");
            return;
        }

        if(GameManager.Instance.Tip < data.GetUpgradePrice(level))
        {
            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("금액 부족");
            return;
        }
    }

    private void UpgradeStaffEvent()
    {
        SetStaffData(_currentStaffData);
    }
}
