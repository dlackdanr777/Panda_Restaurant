using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIChallengeTabSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _rectTransform;
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

    public void Init(ChallengeData data, UnityAction onShortCutButtonClicked, UnityAction onDoneButtonClicked)
    {
        _shortCutButton.onClick.AddListener(onShortCutButtonClicked);
        _doneButton.onClick.AddListener(OnDoneButtonClicked);
        _doneButton.onClick.AddListener(onDoneButtonClicked);

        SetData(data);
    }


    public void SetData(ChallengeData data)
    {
        _data = data;
        if (data == null)
            gameObject.SetActive(false);

        gameObject.SetActive(true);

        _moneyImage.gameObject.SetActive(false);
        _diaImage.gameObject.SetActive(false);

        if (data.MoneyType == MoneyType.Gold)
            _moneyImage.gameObject.SetActive(true);
        else 
            _diaImage.gameObject.SetActive(true);

        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(data);
        _descriptionText.text = data.Description;
        _rewardText.text = Utility.ConvertToNumber(data.RewardMoney);
    }

    public void SetNone()
    {
        _shortCutButton.gameObject.SetActive(true);
        _doneButton.gameObject.SetActive(false);
        _clearButton.gameObject.SetActive(false);
        _layoutImage.color = Color.white;
        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(_data);
        _rectTransform.SetAsLastSibling();
    }

    public void SetDone()
    {
        _doneButton.gameObject.SetActive(true);
        _shortCutButton.gameObject.SetActive(false);
        _clearButton.gameObject.SetActive(false);
        _layoutImage.color = Color.white;
        _percentBar.fillAmount = 1;
        _rectTransform.SetAsFirstSibling();
    }

    public void SetClear()
    {
        _clearButton.gameObject.SetActive(true);
        _doneButton.gameObject.SetActive(false);
        _shortCutButton.gameObject.SetActive(false);
        _layoutImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
        _percentBar.fillAmount = 1;
        _rectTransform.SetAsLastSibling();
    }


    private void OnDoneButtonClicked()
    {
        ChallengeManager.Instance.ChallengeClear(_data.Id);
    }
}
