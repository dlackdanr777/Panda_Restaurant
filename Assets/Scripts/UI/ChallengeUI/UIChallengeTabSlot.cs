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
    [SerializeField] private TextMeshProUGUI _rewardText;


    private ChallengeData _data;
    public ChallengeData Data => _data;

    public override void Init()
    {
        _shortCutButton.onClick.AddListener(OnShortcutButtonClicked);
        _doneButton.onClick.AddListener(OnDoneButtonClicked);

        ChallengeManager.Instance.OnChallengePercentUpdateHandler += UpdatePercent;
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
        if (data.MoneyType == MoneyType.Gold)
        {
            _moneyImage.gameObject.SetActive(true);
            _diaImage.gameObject.SetActive(false);
        }
        else if(data.MoneyType == MoneyType.Dia)
        {
            _diaImage.gameObject.SetActive(true);
            _moneyImage.gameObject.SetActive(false);
        }

        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(data);
        _descriptionText.text = data.Description;
        _rewardText.text = Utility.ConvertToMoney(data.RewardMoney);

        if (UserInfo.GetIsClearChallenge(_data.Id))
        {
            _clearButton.gameObject.SetActive(true);
            _doneButton.gameObject.SetActive(false);
            _shortCutButton.gameObject.SetActive(false);
            _layoutImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            _percentBar.fillAmount = 1;
            return;
        }
        else if (UserInfo.GetIsDoneChallenge(_data.Id))
        {
            _doneButton.gameObject.SetActive(true);
            _shortCutButton.gameObject.SetActive(false);
            _clearButton.gameObject.SetActive(false);
            _layoutImage.color = Color.white;
            _percentBar.fillAmount = 1;
            return;
        }
        else
        {
            _shortCutButton.gameObject.SetActive(true);
            _doneButton.gameObject.SetActive(false);
            _clearButton.gameObject.SetActive(false);
            _layoutImage.color = Color.white;
            return;
        }
    }


    private void UpdatePercent(ChallengeType type)
    {
        if (_data == null)
            return;

        if (type != _data.Type)
            return;

        if (_clearButton.gameObject.activeSelf || _doneButton.gameObject.activeSelf)
            return;

        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(_data);
    }


    private void OnDoneButtonClicked()
    {
        if (_data == null)
        {
            DebugLog.Log("도전과제 데이터가 슬롯에 없습니다.");
            return;
        }
        
        if(_data.MoneyType == MoneyType.Gold)
        {
            UserInfo.AddMoney(_data.RewardMoney);
            SoundManager.Instance.PlayEffectAudio(SoundEffectType.GoldSound);
        }

        else if(_data.MoneyType == MoneyType.Dia)
        {
            UserInfo.AddDia(_data.RewardMoney);
            SoundManager.Instance.PlayEffectAudio(SoundEffectType.DiaSound);
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
