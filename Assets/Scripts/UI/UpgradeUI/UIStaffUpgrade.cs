using UnityEngine;
using Muks.MobileUI;
using TMPro;
using UnityEngine.UI;

public class UIStaffUpgrade : MobileUIView
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private UIImageAndText _selectGroup;
    [SerializeField] private GameObject _lowerFrame;
    [SerializeField] private UIUpgradeStaffLevelArea _currentLevelGroup;
    [SerializeField] private UIUpgradeStaffLevelArea _nextLevelGroup;
    [SerializeField] private UIUpgradeStaffLevelArea _maxLevelGroup;
    [SerializeField] private ParticleSystem _flashEffect;

    [Header("Buttons")]
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _notEnoughDiaButton;
    [SerializeField] private UIButtonAndText _scoreButton;

    [Header("Sprites")]
    [SerializeField] private Image _upgradeImage;
    [SerializeField] private Sprite _upgradeMoneySprite;
    [SerializeField] private Sprite _upgradeDiaSprite;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _upgradeSound;

    private StaffData _currentData;


    public override void Init()
    {
        _upgradeButton.AddListener(OnUpgradeButtonClicked);
        _notEnoughMoneyButton.AddListener(OnUpgradeButtonClicked);
        _notEnoughDiaButton.AddListener(OnUpgradeButtonClicked);
        gameObject.SetActive(false);
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        _flashEffect.Stop();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appeared;
        UpdateData();
        gameObject.SetActive(true);
    }

    
    public void SetData(StaffData data)
    {
        _currentData = data;
    }


    private void UpdateData()
    {
        _upgradeButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _notEnoughDiaButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);

        if (_currentData == null || !UserInfo.IsGiveStaff(UserInfo.CurrentStage, _currentData))
        {
            _levelText.text = "확인 필요";
            _lowerFrame.gameObject.SetActive(false);
            _maxLevelGroup.gameObject.SetActive(false);
            return;
        }

        int level = UserInfo.GetStaffLevel(UserInfo.CurrentStage, _currentData);
        _selectGroup.SetSprite(_currentData.ThumbnailSprite);
        _selectGroup.SetText(_currentData.Name);


        if(!_currentData.IsMaxLevel(level) && _currentData.UpgradeEnable(level))
        {
            _levelText.text = "Lv." + level;
            _lowerFrame.gameObject.SetActive(true);
            _maxLevelGroup.gameObject.SetActive(false);
 
            _currentLevelGroup.SetLevelText(level);
            _currentLevelGroup.SetEffectText(Utility.GetStaffEffectDescription(_currentData, level));
            _nextLevelGroup.SetLevelText(level + 1);
            _nextLevelGroup.SetEffectText(Utility.GetStaffEffectDescription(_currentData, level + 1));
        }
        else
        {
            _levelText.text = "Lv.Max";
            _lowerFrame.gameObject.SetActive(false);
            _maxLevelGroup.gameObject.SetActive(true);
            _maxLevelGroup.SetLevelText(level);
            _maxLevelGroup.SetEffectText(Utility.GetStaffEffectDescription(_currentData, level));
            return;
        }


        if (UserInfo.IsScoreValid(_currentData.GetUpgradeMinScore(level)))
        {
            UpgradeMoneyData upgradeMoneyData = _currentData.GetUpgradeMoneyData(level);

            if (upgradeMoneyData.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(upgradeMoneyData.Price))
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughMoneyButton.SetText(Utility.ConvertToMoney(upgradeMoneyData.Price));
                return;
            }

            if (upgradeMoneyData.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(upgradeMoneyData.Price))
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughMoneyButton.SetText(Utility.ConvertToMoney(upgradeMoneyData.Price));
                return;
            }

            _upgradeButton.gameObject.SetActive(true);
            _upgradeImage.sprite = upgradeMoneyData.MoneyType == MoneyType.Gold ? _upgradeMoneySprite : _upgradeDiaSprite;
            _upgradeButton.SetText(Utility.ConvertToMoney(upgradeMoneyData.Price));
            return;

        }
        else
        {
            _scoreButton.gameObject.SetActive(true);
            _scoreButton.SetText(_currentData.GetUpgradeMinScore(level).ToString());
            return;
        }
    }


    private void OnUpgradeButtonClicked()
    {
        if (_currentData == null || !UserInfo.IsGiveStaff(UserInfo.CurrentStage, _currentData))
        {
            PopupManager.Instance.ShowDisplayText("현재 직원 정보를 확인할 수 없습니다.");
            UpdateData();
            return;
        }

        int level = UserInfo.GetStaffLevel(UserInfo.CurrentStage, _currentData);
        if (_currentData.IsMaxLevel(level)
            || !UserInfo.CanUpgradeStaff(UserInfo.CurrentStage, _currentData))
        {
            return;
        }

        if (UserInfo.IsScoreValid(_currentData.GetUpgradeMinScore(level)))
        {
            UpgradeMoneyData upgradeMoneyData = _currentData.GetUpgradeMoneyData(level);

            if (upgradeMoneyData.MoneyType == MoneyType.Gold && !UserInfo.IsMoneyValid(upgradeMoneyData.Price))
            {
                PopupManager.Instance.ShowTextLackMoney();
                return;
            }

            if (upgradeMoneyData.MoneyType == MoneyType.Dia && !UserInfo.IsDiaValid(upgradeMoneyData.Price))
            {
                PopupManager.Instance.ShowTextLackDia();
                return;
            }

            // The runtime rechecks the account, saved level and cost, commits both values, then publishes events.
            if (!UserInfo.UpgradeStaff(UserInfo.CurrentStage, _currentData))
            {
                PopupManager.Instance.ShowDisplayText("현재 직원 상태에서는 업그레이드할 수 없습니다.");
                UpdateData();
                return;
            }
            PopupManager.Instance.ShowDisplayText("직원 업그레이드를 완료했어요!");
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _upgradeSound);
            _flashEffect.Emit(1);
            UpdateData();
            return;

        }

        else
        {
            PopupManager.Instance.ShowTextLackScore();
            return;
        }
    }

}
