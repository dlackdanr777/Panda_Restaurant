using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class UIAttendanceSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _dayText;
    [SerializeField] private TextMeshProUGUI _giftCountText;
    [SerializeField] private Image _giftImage;
    [SerializeField] private GameObject _checkImage;
    [SerializeField] private GameObject _notCheckImage;


    public void SetData(int day, int giftCount, Sprite giftSprite)
    {
        _dayText.text = day.ToString();
        _giftCountText.text = giftCount.ToString();
        _giftImage.sprite = giftSprite;

        _checkImage.gameObject.SetActive(false);
        _notCheckImage.gameObject.SetActive(false);
    }

    public void IsAttendanceCheckSlot(bool value)
    {
        _checkImage.gameObject.SetActive(value);
        _notCheckImage.gameObject.SetActive(!value);
    }
}
