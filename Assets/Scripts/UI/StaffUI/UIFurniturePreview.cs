using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIFurniturePreview : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private GameObject _descriptionObj;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _effectDescription;
    [SerializeField] private TextMeshProUGUI _equipScoreText;
    [SerializeField] private TextMeshProUGUI _equipTipText;
    [SerializeField] private TextMeshProUGUI _skillDescription;
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;
    [SerializeField] private UIButtonAndText _upgradeButton;

    private Action<FurnitureData> _onBuyButtonClicked;
    private Action<FurnitureData> _onEquipButtonClicked;
    private Action<FurnitureData> _onUpgradeButtonClicked;
    private FurnitureData _currentData;

    public void Init(Action<FurnitureData> onEquipButtonClicked, Action<FurnitureData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        UserInfo.OnChangeMoneyHandler += UpdateFurniture;
        UserInfo.OnChangeScoreHanlder += UpdateFurniture;
        UserInfo.OnChangeFurnitureHandler += (type) => UpdateFurniture();
        UserInfo.OnGiveFurnitureHandler += UpdateFurniture;
    }


    public void SetFurnitureData(FurnitureData data)
    {
        _currentData = data;
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _upgradeButton.gameObject.SetActive(false);

        if (data == null)
        {
            _image.gameObject.SetActive(false);
            _nameText.gameObject.SetActive(false);
            _descriptionObj.gameObject.SetActive(false);
            _description.gameObject.SetActive(false);
            return;
        }

        _image.gameObject.SetActive(true);
        _nameText.gameObject.SetActive(true);
        _description.gameObject.SetActive(true);
        _descriptionObj.gameObject.SetActive(true);
        _image.sprite = data.Sprite;
        _nameText.text = data.Name;
        _description.text = string.Empty;


        if(UserInfo.IsEquipFurniture(data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("사용중");
            _usingButton.Interactable(false);
            _image.color = new Color(1, 1, 1);
        }
        else
        {
            if(UserInfo.IsGiveFurniture(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.SetText("사용하기");
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentData); });
                _image.color = new Color(1, 1, 1);
            }
            else
            {
                _image.color = new Color(0, 0, 0);

                _buyButton.gameObject.SetActive(true);
                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _buyButton.SetText(Utility.ConvertToNumber(data.BuyMinPrice));
            }
        }
    }

    private void UpdateFurniture()
    {
        SetFurnitureData(_currentData);
    }
}
