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

    public void Init(ChallengeData data, UnityAction onDoneButtonClicked)
    {
        _shortCutButton.onClick.AddListener(OnShortcutButtonClicked);
        _doneButton.onClick.AddListener(OnDoneButtonClicked);
        _doneButton.onClick.AddListener(onDoneButtonClicked);
        SetData(data);
    }


    public void SetData(ChallengeData data)
    {
        if (_data != null)
            ChallengeManager.Instance.OnChallengePercentUpdateHandler -= UpdatePercent;

        _data = data;
        if (data == null)
            gameObject.SetActive(false);
        else
            ChallengeManager.Instance.OnChallengePercentUpdateHandler += UpdatePercent;

        gameObject.SetActive(true);

        _moneyImage.gameObject.SetActive(false);
        _diaImage.gameObject.SetActive(false);

        if (data.MoneyType == MoneyType.Gold)
            _moneyImage.gameObject.SetActive(true);
        else 
            _diaImage.gameObject.SetActive(true);

        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(data);
        _descriptionText.text = data.Description;
        _rewardText.text = Utility.ConvertToMoney(data.RewardMoney);
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

    public void UpdatePercent(ChallengeType type)
    {
        if (type != _data.Type)
            return;

        if (_clearButton.gameObject.activeSelf || _doneButton.gameObject.activeSelf)
            return;

        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(_data);
    }


    private void OnDoneButtonClicked()
    {
        ChallengeManager.Instance.ChallengeClear(_data);
        UserInfo.AppendMoney(_data.RewardMoney);
    }

    private void OnShortcutButtonClicked()
    {
        if (_data == null)
        {
            DebugLog.Log("도전과제 데이터가 슬롯에 없습니다.");
            return;
        }

        if(_data.ShortcutAction.Item == null)
        {
            DebugLog.Log("바로가기 메서드 정보가 없습니다.");
            return;
        }

        _data.ShortcutAction.Item?.Invoke();
    }
}
