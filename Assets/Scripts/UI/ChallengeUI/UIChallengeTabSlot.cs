using UnityEngine;
using Muks.RecyclableScrollView;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System;

public class UIChallengeTabSlot : RecyclableScrollSlot<ChallengeData>
{
    [Header("Components")]
    [SerializeField] private Button _shortCutButton;
    [SerializeField] private Button _doneButton;
    [SerializeField] private Button _clearButton;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Image _layoutImage;
    [SerializeField] private Image _percentBar;
    [SerializeField] private Image _moneyImage;
    [SerializeField] private Image _diaImage;
    [SerializeField] private Image _scoreImage;
    [SerializeField] private TextMeshProUGUI _rewardText;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private GameObject _andImage;
    [SerializeField] private UITextAndText _scoreTextGroup;

    [Space]
    [Header("Option")]
    [SerializeField] private Color _progressColor;
    [SerializeField] private Color _doneColor;

    private ChallengeData _data;
    private ChallengeData _fixedData;
    private string _fixedDescription;
    private long _fixedRewardMoney;
    private long _fixedRewardScore;
    private MoneyType _fixedMoneyType;
    public ChallengeData Data => _data;


    private UIChallenge _uiChallenge;

    public override void Init()
    {
        _shortCutButton.onClick.AddListener(OnShortcutButtonClicked);
        _doneButton.onClick.AddListener(OnDoneButtonClicked);

    }

    public void SetUIChellenge(UIChallenge uiChallenge)
    {
        _uiChallenge = uiChallenge;
    }

    public override void UpdateSlot(ChallengeData data)
    {
        if (data == null)
        {
            _data = null;
            gameObject.SetActive(false);
            return;
        }

        _data = data;
        gameObject.SetActive(true);
        if (!IsFixedDisplayCurrent(data))
            BindFixedDisplay(data);

        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(data);
        _countText.SetText(ChallengeManager.Instance.GetChallengeCountStr(data));
        if (UserInfo.GetIsClearChallenge(_data.Id))
            {
                _clearButton.gameObject.SetActive(true);
                _doneButton.gameObject.SetActive(false);
                _shortCutButton.gameObject.SetActive(false);
                _layoutImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
                _percentBar.fillAmount = 1;
                _percentBar.color = _doneColor;
                return;
            }
            else if (UserInfo.GetIsDoneChallenge(_data.Id))
            {
                _doneButton.gameObject.SetActive(true);
                _shortCutButton.gameObject.SetActive(false);
                _clearButton.gameObject.SetActive(false);
                _layoutImage.color = Color.white;
                _percentBar.fillAmount = 1;
                _percentBar.color = _doneColor;
                return;
            }
            else
            {
                _shortCutButton.gameObject.SetActive(true);
                _doneButton.gameObject.SetActive(false);
                _clearButton.gameObject.SetActive(false);
                _layoutImage.color = Color.white;
                _percentBar.color = _progressColor;
                return;
            }
    }


    // Id, description, reward and score never change for a data object, so they are rebuilt only when the slot
    // is bound to a different challenge (or its fixed values differ); progress and state are refreshed every time.
    private bool IsFixedDisplayCurrent(ChallengeData data)
    {
        return ReferenceEquals(_fixedData, data)
            && _fixedMoneyType == data.MoneyType
            && _fixedRewardMoney == data.RewardMoney
            && _fixedRewardScore == data.RewardScore
            && string.Equals(_fixedDescription, data.Description, StringComparison.Ordinal);
    }

    private void BindFixedDisplay(ChallengeData data)
    {
        _fixedData = data;
        _fixedMoneyType = data.MoneyType;
        _fixedRewardMoney = data.RewardMoney;
        _fixedRewardScore = data.RewardScore;
        _fixedDescription = data.Description;

        if (data.MoneyType == MoneyType.Gold)
        {
            _moneyImage.gameObject.SetActive(true);
            _diaImage.gameObject.SetActive(false);
        }
        else if (data.MoneyType == MoneyType.Dia)
        {
            _diaImage.gameObject.SetActive(true);
            _moneyImage.gameObject.SetActive(false);
        }

        _descriptionText.text = data.Description;
        _rewardText.text = Utility.ConvertToMoney(data.RewardMoney);

        if (data.RewardScore <= 0)
        {
            _scoreTextGroup.gameObject.SetActive(false);
            _andImage.SetActive(false);
        }
        else
        {
            _scoreTextGroup.gameObject.SetActive(true);
            _andImage.SetActive(true);
            _scoreTextGroup.SetText1(Utility.ConvertToMoney(data.RewardScore));
        }
    }

    public void UpdatePercent(ChallengeType type)
    {
        if (_data == null)
            return;

        if (type != _data.Type)
            return;

        if (_clearButton.gameObject.activeSelf || _doneButton.gameObject.activeSelf)
            return;

        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(_data);
        _countText.SetText(ChallengeManager.Instance.GetChallengeCountStr(_data));
    }


    private void OnDoneButtonClicked()
    {
        if (_data == null)
        {
            DebugLog.Log("도전과제 데이터가 슬롯에 없습니다.");
            return;
        }

        if (_uiChallenge == null)
        {
            DebugLog.LogError("UIChallenge 참조가 없습니다. SetUIChellenge()를 확인해주세요.");
            return;
        }

        if(_data.MoneyType == MoneyType.Gold)
        {
            UserInfo.AddMoney(_data.RewardMoney);
            SoundManager.Instance.PlayEffectAudio(EffectType.None, SoundEffectType.GoldSound);
            _uiChallenge.StartCoinAnime(_data.RewardMoney, _moneyImage.transform.position);
        }

        else if(_data.MoneyType == MoneyType.Dia)
        {
            UserInfo.AddDia((int)_data.RewardMoney);
            SoundManager.Instance.PlayEffectAudio(EffectType.None, SoundEffectType.DiaSound);
            _uiChallenge.StartDiaAnime(_data.RewardMoney, _diaImage.transform.position);
        }

        if (0 < _data.RewardScore)
        {
            UserInfo.AddScore((int)_data.RewardScore);
            _uiChallenge.StartScoreAnime(_scoreImage.transform.position);
        }
        UserInfo.ClearChallenge(_data);
    }


    private void OnShortcutButtonClicked()
    {
        if (_data == null)
        {
            DebugLog.Log("도전과제 데이터가 슬롯에 없습니다.");
            return;
        }

        if (_data.ShortcutAction.Item == null)
        {
            DebugLog.Log("바로가기 메서드 정보가 없습니다.");
            return;
        }

        _data.ShortcutAction.Item?.Invoke();
    }
}
