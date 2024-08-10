using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIKitchenPreview : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private GameObject[] _hideObjs;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private GameObject _setEffectObj;
    [SerializeField] private TextMeshProUGUI _setEffectDescription;
    [SerializeField] private GameObject _buyMinScoreObj;
    [SerializeField] private GameObject _addTipObj;
    [SerializeField] private GameObject _cookingSpeedObj;
    [SerializeField] private TextMeshProUGUI _buyMinScoreDescription;
    [SerializeField] private TextMeshProUGUI _addScoreDescription;
    [SerializeField] private TextMeshProUGUI _addTipDescription;
    [SerializeField] private TextMeshProUGUI _cookingSpeedDescription;
    [SerializeField] private UIButtonAndText _usingButton;
    [SerializeField] private UIButtonAndText _equipButton;
    [SerializeField] private UIButtonAndText _buyButton;

    private Action<KitchenUtensilData> _onBuyButtonClicked;
    private Action<KitchenUtensilData> _onEquipButtonClicked;
    private KitchenUtensilData _currentData;

    public void Init(Action<KitchenUtensilData> onEquipButtonClicked, Action<KitchenUtensilData> onBuyButtonClicked)
    {
        _onEquipButtonClicked = onEquipButtonClicked;
        _onBuyButtonClicked = onBuyButtonClicked;

        UserInfo.OnChangeMoneyHandler += UpdateUI;
        UserInfo.OnChangeScoreHandler += UpdateUI;
        UserInfo.OnChangeKitchenUtensilHandler += (type) => UpdateUI();
        UserInfo.OnGiveKitchenUtensilHandler += UpdateUI;
    }


    public void SetData(KitchenUtensilData data)
    {
        _currentData = data;
        _usingButton.gameObject.SetActive(false);
        _equipButton.gameObject.SetActive(false);
        _buyButton.gameObject.SetActive(false);
        _buyMinScoreObj.SetActive(false);
        _setEffectObj.SetActive(false);

        if (data == null)
        {
            for(int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
            {
                _hideObjs[i].SetActive(false);
            }
            return;
        }

        for (int i = 0, cnt = _hideObjs.Length; i < cnt; ++i)
        {
            _hideObjs[i].SetActive(true);
        }

        _image.sprite = data.ThumbnailSprite;
        _nameText.text = data.Name;
        _addScoreDescription.text = data.AddScore.ToString();

        SetData setData = SetDataManager.Instance.GetSetData(data.SetId);
        _setEffectDescription.text = setData != null ? setData.Description : string.Empty;

        if (data is CookingSpeedUpKitchenUtensilData)
        {
            _cookingSpeedObj.gameObject.SetActive(true);
            _addTipObj.gameObject.SetActive(false);
            _cookingSpeedDescription.text = data.EffectValue.ToString() + "%";
        }
            
        else if (data is TipPerMinuteKitchenUtensilData)
        {
            _addTipObj.gameObject.SetActive(true);
            _cookingSpeedObj.gameObject.SetActive(false);
            _addTipDescription.text = Utility.ConvertToNumber(data.EffectValue);
        }

        else
        {
            _addTipObj.gameObject.SetActive(false);
            _cookingSpeedObj.gameObject.SetActive(false);
        }
        

        if (UserInfo.IsEquipKitchenUtensil(data))
        {
            _usingButton.gameObject.SetActive(true);
            _usingButton.SetText("사용 중");
            _usingButton.Interactable(false);
            _image.color = new Color(1, 1, 1);
            _setEffectObj.SetActive(true);

        }
        else
        {
            if(UserInfo.IsGiveKitchenUtensil(data))
            {
                _equipButton.gameObject.SetActive(true);
                _equipButton.SetText("사용 하기");
                _equipButton.RemoveAllListeners();
                _equipButton.AddListener(() => { _onEquipButtonClicked(_currentData); });
                _image.color = new Color(1, 1, 1);
                _setEffectObj.SetActive(true);
            }
            else
            {
                _image.color = new Color(0, 0, 0);

                _buyButton.gameObject.SetActive(true);
                _buyButton.RemoveAllListeners();
                _buyButton.AddListener(() => { _onBuyButtonClicked(_currentData); });
                _buyButton.SetText(Utility.ConvertToNumber(data.BuyPrice));
                _buyMinScoreObj.SetActive(true);
                _buyMinScoreDescription.text = Utility.ConvertToNumber(data.BuyScore);
            }
        }
    }

    private void UpdateUI()
    {
        SetData(_currentData);
    }
}
