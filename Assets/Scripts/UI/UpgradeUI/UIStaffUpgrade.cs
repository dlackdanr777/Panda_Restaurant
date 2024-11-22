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

    [Header("Buttons")]
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private UIButtonAndText _notEnoughMoneyButton;
    [SerializeField] private UIButtonAndText _scoreButton;

    private StaffData _currentStaffData;


    public override void Init()
    {
        _upgradeButton.AddListener(OnUpgradeButtonClicked);
        _notEnoughMoneyButton.AddListener(OnUpgradeButtonClicked);
        gameObject.SetActive(false);
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
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
        _currentStaffData = data;
    }


    private void UpdateData()
    {
        if (_currentStaffData == null)
            throw new System.Exception("스탭 데이터가 NULL입니다.");

        if(!UserInfo.IsGiveStaff(_currentStaffData))
            throw new System.Exception("해당 스탭을 고용하지 않았습니다.");

        _upgradeButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);

        int level = UserInfo.GetStaffLevel(_currentStaffData);
        _selectGroup.SetSprite(_currentStaffData.ThumbnailSprite);
        _selectGroup.SetText(_currentStaffData.Name);


        if(_currentStaffData.UpgradeEnable(level))
        {
            _levelText.text = "Lv." + level;
            _lowerFrame.gameObject.SetActive(true);
            _maxLevelGroup.gameObject.SetActive(false);
            _currentLevelGroup.SetData(level, _currentStaffData.GetAddScore(level).ToString(), _currentStaffData.GetAddTipMul(level) + "%");
            _nextLevelGroup.SetData(level + 1, _currentStaffData.GetAddScore(level + 1).ToString(), _currentStaffData.GetAddTipMul(level + 1) + "%");
        }
        else
        {
            _levelText.text = "Lv.Max";
            _lowerFrame.gameObject.SetActive(false);
            _maxLevelGroup.gameObject.SetActive(true);
            _maxLevelGroup.SetData(level, _currentStaffData.GetAddScore(level).ToString(), _currentStaffData.GetAddTipMul(level) + "%");
        }

        if (UserInfo.IsScoreValid(_currentStaffData.GetUpgradeMinScore(level)))
        {
            if (UserInfo.IsMoneyValid(_currentStaffData.GetUpgradePrice(level)))
            {
                _upgradeButton.gameObject.SetActive(true);
                _upgradeButton.SetText(Utility.ConvertToMoney(_currentStaffData.GetUpgradePrice(level)));
                return;
            }
            else
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughMoneyButton.SetText(Utility.ConvertToMoney(_currentStaffData.GetUpgradePrice(level)));
                return;
            }
        }
        else
        {
            _scoreButton.gameObject.SetActive(true);
            _scoreButton.SetText(_currentStaffData.GetUpgradeMinScore(level).ToString());
            return;
        }
    }


    private void OnUpgradeButtonClicked()
    {
        if (_currentStaffData == null)
            throw new System.Exception("스태프 데이터가 NULL입니다.");

        if (!UserInfo.IsGiveStaff(_currentStaffData))
            throw new System.Exception("해당 스탭을 고용하지 않았습니다.");

        int level = UserInfo.GetStaffLevel(_currentStaffData);
        if (UserInfo.IsScoreValid(_currentStaffData.GetUpgradeMinScore(level)))
        {
            if(UserInfo.IsMoneyValid(_currentStaffData.GetUpgradePrice(level)))
            {
                UserInfo.UpgradeStaff(_currentStaffData);
                UserInfo.AddMoney(-_currentStaffData.GetUpgradePrice(level));
                TimedDisplayManager.Instance.ShowText("직원 업그레이드를 완료했어요!");
                UpdateData();
                return;
            }
            else
            {
                TimedDisplayManager.Instance.ShowTextLackMoney();
                return;
            }

        }

        else
        {
            TimedDisplayManager.Instance.ShowTextLackScore();
            return;
        }
    }

}
