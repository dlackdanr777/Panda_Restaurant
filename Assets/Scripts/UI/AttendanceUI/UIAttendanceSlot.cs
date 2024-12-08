using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIAttendanceSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _normalImages;
    [SerializeField] private GameObject _specialImages;
    [SerializeField] private TextMeshProUGUI _dayText;
    [SerializeField] private TextMeshProUGUI _giftCountText;
    [SerializeField] private Image _giftImage;
    [SerializeField] private GameObject _checkImage;
    [SerializeField] private GameObject _notCheckImage;
    [SerializeField] private GameObject _todayArrow;


    [Space]
    [Header("Sprites")]
    [SerializeField] private Sprite _coinSprite;
    [SerializeField] private Sprite _diaSprite;

    private AttendanceData _data;

    public void SetData(int day, AttendanceData data)
    {
        if (data == null)
        {
            DebugLog.LogError("현재 데이터는 Null입니다.");
            return;
        }

        if (_data == data)
            return;

        _dayText.text = day + "일";
        _normalImages.gameObject.SetActive(true);
        _specialImages.gameObject.SetActive(false);
        _giftImage.sprite = data.MoneyType == MoneyType.Gold ? _coinSprite : _diaSprite;
        _giftCountText.text = Utility.ConvertToMoney(data.RewardValue);

        _checkImage.gameObject.SetActive(false);
        _notCheckImage.gameObject.SetActive(false);
        _todayArrow.gameObject.SetActive(false);
    }


    public void SetDataToSpecial(AttendanceData data)
    {
        if (data == null)
        {
            DebugLog.LogError("현재 데이터는 Null입니다.");
            return;
        }

        if (_data == data)
            return;


        _dayText.text = "개근상";
        _normalImages.gameObject.SetActive(false);
        _specialImages.gameObject.SetActive(true);
        _giftImage.sprite = data.MoneyType == MoneyType.Gold ? _coinSprite : _diaSprite;
        _giftCountText.text = Utility.ConvertToMoney(data.RewardValue);

        _checkImage.gameObject.SetActive(false);
        _notCheckImage.gameObject.SetActive(false);
        _todayArrow.gameObject.SetActive(false);
    }

    public void IsAttendanceCheckSlot(bool value)
    {
        _checkImage.gameObject.SetActive(value);
        _notCheckImage.gameObject.SetActive(!value);
    }


    public void SetTotaySlotChecked()
    {
        _canvasGroup.alpha = 1f;
        _todayArrow.gameObject.SetActive(true);
        _checkImage.gameObject.SetActive(true);
        _notCheckImage.gameObject.SetActive(false);
    }

    public void SetTotaySlotUnChecked()
    {
        _canvasGroup.alpha = 1f;
        _todayArrow.gameObject.SetActive(true);
        _checkImage.gameObject.SetActive(false);
        _notCheckImage.gameObject.SetActive(false);
    }

    public void SetChecked()
    {
        _canvasGroup.alpha = 0.7f;
        _checkImage.gameObject.SetActive(true);
        _notCheckImage.gameObject.SetActive(false);
        _todayArrow.gameObject.SetActive(false);
    }



    public void SetUnchecked()
    {
        _canvasGroup.alpha = 0.7f;
        _checkImage.gameObject.SetActive(false);
        _notCheckImage.gameObject.SetActive(true);
        _todayArrow.gameObject.SetActive(false);
    }
}
