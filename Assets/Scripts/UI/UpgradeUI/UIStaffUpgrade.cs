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
    [SerializeField] private UIUpgradeAreaGroup _currentLevelGroup;
    [SerializeField] private UIUpgradeAreaGroup _nextLevelGroup;
    [SerializeField] private UIUpgradeAreaGroup _maxLevelGroup;
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
        if (_currentData == null)
            throw new System.Exception("스탭 데이터가 NULL입니다.");

        if(!UserInfo.IsGiveStaff(UserInfo.CurrentStage, _currentData))
            throw new System.Exception("해당 스탭을 고용하지 않았습니다.");

        _upgradeButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _notEnoughDiaButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);

        int level = UserInfo.GetStaffLevel(UserInfo.CurrentStage, _currentData);
        _selectGroup.SetSprite(_currentData.ThumbnailSprite);
        _selectGroup.SetText(_currentData.Name);


        if(_currentData.UpgradeEnable(level))
        {
            _levelText.text = "Lv." + level;
            _lowerFrame.gameObject.SetActive(true);
            _maxLevelGroup.gameObject.SetActive(false);
            _currentLevelGroup.SetData(level, _currentData.GetAddScore(level).ToString(), _currentData.GetAddTipMul(level) + "%");
            _nextLevelGroup.SetData(level + 1, _currentData.GetAddScore(level + 1).ToString(), _currentData.GetAddTipMul(level + 1) + "%");
        }
        else
        {
            _levelText.text = "Lv.Max";
            _lowerFrame.gameObject.SetActive(false);
            _maxLevelGroup.gameObject.SetActive(true);
            _maxLevelGroup.SetData(level, _currentData.GetAddScore(level).ToString(), _currentData.GetAddTipMul(level) + "%");
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
        if (_currentData == null)
            throw new System.Exception("스태프 데이터가 NULL입니다.");

        if (!UserInfo.IsGiveStaff(UserInfo.CurrentStage, _currentData))
            throw new System.Exception("해당 스탭을 고용하지 않았습니다.");

        int level = UserInfo.GetStaffLevel(UserInfo.CurrentStage, _currentData);
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

            if(upgradeMoneyData.MoneyType == MoneyType.Gold)
                UserInfo.AddMoney(-upgradeMoneyData.Price);

            else if (upgradeMoneyData.MoneyType == MoneyType.Dia)
                UserInfo.AddDia(-upgradeMoneyData.Price);

            UserInfo.UpgradeStaff(UserInfo.CurrentStage, _currentData);
            PopupManager.Instance.ShowDisplayText("직원 업그레이드를 완료했어요!");
            SoundManager.Instance.PlayEffectAudio(_upgradeSound);
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
