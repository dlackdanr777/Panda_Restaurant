using UnityEngine;
using Muks.MobileUI;
using TMPro;
using UnityEngine.UI;

public class UIRecipeUpgrade : MobileUIView
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

    private FoodData _currentData;


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

    
    public void SetData(FoodData data)
    {
        _currentData = data;
    }


    private void UpdateData()
    {
        if (_currentData == null)
            throw new System.Exception("레시피 데이터가 NULL입니다.");

        if(!UserInfo.IsGiveRecipe(_currentData))
            throw new System.Exception("해당 레시피를 구매하지 않았습니다.");

        _upgradeButton.gameObject.SetActive(false);
        _notEnoughMoneyButton.gameObject.SetActive(false);
        _scoreButton.gameObject.SetActive(false);

        int level = UserInfo.GetRecipeLevel(_currentData);
        _selectGroup.SetSprite(_currentData.ThumbnailSprite);
        _selectGroup.SetText(_currentData.Name);


        if(_currentData.UpgradeEnable(level))
        {
            _levelText.text = "Lv." + level;
            _lowerFrame.gameObject.SetActive(true);
            _maxLevelGroup.gameObject.SetActive(false);
            _currentLevelGroup.SetData(level, Utility.ConvertToMoney(_currentData.GetSellPrice(level)), _currentData.GetCookingTime(level) + "s");
            _nextLevelGroup.SetData(level + 1, Utility.ConvertToMoney(_currentData.GetSellPrice(level + 1)), _currentData.GetCookingTime(level + 1) + "s");
        }
        else
        {
            _levelText.text = "Lv.Max";
            _lowerFrame.gameObject.SetActive(false);
            _maxLevelGroup.gameObject.SetActive(true);
            _maxLevelGroup.SetData(level, Utility.ConvertToMoney(_currentData.GetSellPrice(level)), _currentData.GetCookingTime(level) + "s");
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
            throw new System.Exception("레시피 데이터가 NULL입니다.");

        if (!UserInfo.IsGiveRecipe(_currentData))
            throw new System.Exception("해당 레시피를 구매하지 않았습니다.");


        int level = UserInfo.GetRecipeLevel(_currentData);
        if (UserInfo.IsScoreValid(_currentData.GetUpgradeMinScore(level)))
        {
            if(UserInfo.IsMoneyValid(_currentData.GetUpgradePrice(level)))
            {
                UserInfo.UpgradeRecipe(_currentData);
                UserInfo.AddMoney(-_currentData.GetUpgradePrice(level));
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
