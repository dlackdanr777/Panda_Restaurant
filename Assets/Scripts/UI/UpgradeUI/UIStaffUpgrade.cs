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

    private StaffData _currentData;


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
        _currentData = data;
    }


    private void UpdateData()
    {
        if (_currentData == null)
            throw new System.Exception("���� �����Ͱ� NULL�Դϴ�.");

        if(!UserInfo.IsGiveStaff(_currentData))
            throw new System.Exception("�ش� ������ ������� �ʾҽ��ϴ�.");

        _upgradeButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);

        int level = UserInfo.GetStaffLevel(_currentData);
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
            if (UserInfo.IsMoneyValid(_currentData.GetUpgradePrice(level)))
            {
                _upgradeButton.gameObject.SetActive(true);
                _upgradeButton.SetText(Utility.ConvertToMoney(_currentData.GetUpgradePrice(level)));
                return;
            }
            else
            {
                _notEnoughMoneyButton.gameObject.SetActive(true);
                _notEnoughMoneyButton.SetText(Utility.ConvertToMoney(_currentData.GetUpgradePrice(level)));
                return;
            }
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
            throw new System.Exception("������ �����Ͱ� NULL�Դϴ�.");

        if (!UserInfo.IsGiveStaff(_currentData))
            throw new System.Exception("�ش� ������ ������� �ʾҽ��ϴ�.");

        int level = UserInfo.GetStaffLevel(_currentData);
        if (UserInfo.IsScoreValid(_currentData.GetUpgradeMinScore(level)))
        {
            if(UserInfo.IsMoneyValid(_currentData.GetUpgradePrice(level)))
            {
                UserInfo.UpgradeStaff(_currentData);
                UserInfo.AddMoney(-_currentData.GetUpgradePrice(level));
                TimedDisplayManager.Instance.ShowText("���� ���׷��̵带 �Ϸ��߾��!");
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
