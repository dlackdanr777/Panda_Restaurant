using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIChallengeTabSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _clearButton;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Image _percentBar;
    [SerializeField] private Image _moneyImage;
    [SerializeField] private Image _diaImage;
    [SerializeField] private TextMeshProUGUI _rewardText;


    public void SetData(ChallengeData data)
    {
        _percentBar.fillAmount = ChallengeManager.Instance.GetChallengePercent(data);
        _moneyImage.gameObject.SetActive(true);
        _diaImage.gameObject.SetActive(false);
        _descriptionText.text = data.Description;
        _rewardText.text = Utility.ConvertToNumber(data.RewardMoney);
    }
}
