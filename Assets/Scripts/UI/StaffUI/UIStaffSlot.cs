using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffSlot : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private GameObject _useImage;
    [SerializeField] private GameObject _operateImage;
    [SerializeField] private GameObject _enoughMoneyImage;
    [SerializeField] private GameObject _notEnoughMoneyImage;
    [SerializeField] private GameObject _lowReputationImage;


    public void SetUse(StaffData data)
    {
        _useImage.SetActive(true);
        _operateImage.SetActive(false);
        _notEnoughMoneyImage.SetActive(false);
        _lowReputationImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = "사용 중";
    }

    public void SetOperate(StaffData data)
    {
        _operateImage.SetActive(true);
        _notEnoughMoneyImage.SetActive(false);
        _lowReputationImage.SetActive(false);
        _useImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = "보유 중";
    }

    public void SetNotEnoughMoney(StaffData data)
    {
        _notEnoughMoneyImage.SetActive(true);
        _lowReputationImage.SetActive(false);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(1, 1, 1, 1);
        _text.text = data.Price.ToString();
    }

    public void SetEnoughMoney(StaffData data)
    {
        _enoughMoneyImage.SetActive(true);
        _lowReputationImage.SetActive(false);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);
        _notEnoughMoneyImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);
        _text.text = data.Price.ToString();
    }

    public void SetLowReputation(StaffData data)
    {
        _lowReputationImage.SetActive(true);
        _useImage.SetActive(false);
        _operateImage.SetActive(false);
        _enoughMoneyImage.SetActive(false);
        _notEnoughMoneyImage.SetActive(false);

        _image.sprite = data.Sprite;
        _image.color = new Color(0, 0, 0, 1);
        _text.text = data.ScoreIncrement.ToString() + "\n필요";
    }
}
