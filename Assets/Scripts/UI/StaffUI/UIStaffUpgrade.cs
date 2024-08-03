using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.MobileUI;
using Muks.Tween;

public class UIStaffUpgrade : MobileUIView
{
    [Header("Components")]
    [SerializeField] private Image _staffImage;
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _upgradeScoreText;
    [SerializeField] private TextMeshProUGUI _upgradePriceText;
    [SerializeField] private TextMeshProUGUI _nowLevelText;
    [SerializeField] private TextMeshProUGUI _nowLevelDescription;
    [SerializeField] private TextMeshProUGUI _upgradeLevelText;
    [SerializeField] private TextMeshProUGUI _upgradeLevelDescription;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


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


    public void SetStaffData(StaffData data)
    {
        if (data == null || !UserInfo.IsGiveStaff(data))
            return;

        _currentStaffData = data;

        _staffImage.sprite = data.Sprite;
        _nameText.text = data.Name;
        _upgradeButton.RemoveAllListeners();

        int level = UserInfo.GetStaffLevel(data);
        _nowLevelText.text = "* lv." + level;
        _nowLevelDescription.text = "팁" + data.GetAddTipMul(level) + "%, 평점 " + data.GetAddScore(level) + "상승";

        if (data.UpgradeEnable(level))
        {
            _upgradeLevelText.gameObject.SetActive(true);
            _nowLevelDescription.gameObject.SetActive(true);

            _upgradeLevelText.text = "* lv. " + (level + 1);
            _nowLevelDescription.text = "팁" + data.GetAddTipMul(level + 1) + "%, 평점 " + data.GetAddScore(level + 1) + "상승";

            _upgradePriceText.text = Utility.ConvertToNumber(data.GetUpgradePrice(level));
            _upgradeScoreText.text = Utility.ConvertToNumber(data.GetUpgradeMinScore(level));

            _upgradeButton.Interactable(true);
            _upgradeButton.AddListener(() => _onUpgradeButtonClicked(_currentStaffData));
            _upgradeButton.SetText("강화");

        }
        else
        {
            _upgradeLevelText.gameObject.SetActive(false);
            _upgradeLevelDescription.gameObject.SetActive(false);

            _upgradeScoreText.text = "최대 강화";
            _upgradePriceText.text = "최대 강화";


            _upgradeButton.Interactable(false);
            _upgradeButton.SetText("최대 강화");

            return;
        }
    }

    private void UpgradeStaffEvent()
    {
        SetStaffData(_currentStaffData);
    }
}
