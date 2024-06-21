using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [SerializeField] private Image _staffImage;

    [SerializeField] private TextMeshProUGUI _staffNameText;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private Button _equipButton;
    [SerializeField] private TextMeshProUGUI _equipButtonText;

    [Space]
    [Header("Options")]
    [SerializeField] private Color _usingButtonColor;
    [SerializeField] private Color _equipButtotnColor;
    [SerializeField] private Color _buyButtonColor;

    private Action<StaffData> _onBuyButtonClicked;
    private Action<StaffData> _onEquipButtonClicked;
    private StaffData _currentStaffData;
    private Image _buttonImage;
    public void Init(Action<StaffData> onEquipButtonClicked, Action<StaffData> onBuyButtonClicked)
    {
        _buttonImage = _equipButton.GetComponent<Image>();
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;
    }


    public void SetStaff(StaffData data)
    {
        _currentStaffData = data;
        if (data == null)
        {
            _staffImage.gameObject.SetActive(false);
            _staffNameText.gameObject.SetActive(false);
            _effectDescription.gameObject.SetActive(false);
            _equipButton.gameObject.SetActive(false);
            return;
        }

        _staffImage.gameObject.SetActive(true);
        _staffNameText.gameObject.SetActive(true);
        _effectDescription.gameObject.SetActive(true);
        _staffImage.sprite = data.Sprite;
        _staffNameText.text = data.Name;
        _effectDescription.text = data.Description;

        if(UserInfo.IsEquipStaff(data))
        {
            _equipButton.gameObject.SetActive(true);
            _buttonImage.color = _usingButtonColor;
            _equipButton.interactable = false;
            _equipButtonText.text = "사용중";
            _staffImage.color = new Color(1, 1, 1);
        }
        else
        {
            if(UserInfo.IsGiveStaff(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.onClick.RemoveAllListeners();
                _equipButton.onClick.AddListener(() => { _onEquipButtonClicked(_currentStaffData); });

                _equipButton.interactable = true;
                _buttonImage.color = _equipButtotnColor;
                _equipButtonText.text = "사용";
                _staffImage.color = new Color(1, 1, 1);
            }
            else
            {
                _staffImage.color = new Color(0, 0, 0);


                if (data.BuyMinScore <= GameManager.Instance.Score)
                {
                    _equipButton.gameObject.SetActive(true);
                    _equipButton.interactable = true;
                    _equipButton.onClick.RemoveAllListeners();
                    _equipButton.onClick.AddListener(() => { _onBuyButtonClicked(_currentStaffData); });
                    _buttonImage.color = _equipButtotnColor;

                    int price = data.MoneyData.Price;
                    if (1000 <= price)
                    {
                        int div = price / 1000;
                        _equipButtonText.text = div + "K";
                    }
                    else
                    {
                        _equipButtonText.text = price.ToString();
                    }

                }
                else
                {
                    _equipButton.gameObject.SetActive(false);
                }

            }
        }
    }
}
